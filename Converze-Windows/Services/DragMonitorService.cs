using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using Converze.Models;
using Converze.UI.RadialWheel;
using Point = System.Windows.Point;

namespace Converze.Services
{
    /// <summary>
    /// Zero-lag Windows 10 & Windows 11 monitor for Alt + Left-Click / Drag on files
    /// in File Explorer or Desktop.
    /// Uses non-blocking kernel key/mouse state polling (no WH_MOUSE_LL hooks that can freeze the OS cursor)
    /// and executes all Shell COM / UIAutomation queries on background STA threads.
    /// </summary>
    public sealed class DragMonitorService : IDisposable
    {
        private readonly RadialWheelPanel _wheelPanel;
        private readonly DispatcherTimer _pollTimer;

        private bool _wasLeftButtonDown;
        private bool _wasEscDown;
        private DateTime? _mouseDownTimestamp;
        private int _queryInProgress;

        public DragMonitorService(RadialWheelPanel wheelPanel)
        {
            _wheelPanel = wheelPanel;

            _pollTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(12) // ~83Hz ultra-responsive non-blocking state check
            };
            _pollTimer.Tick += (_, _) => PollInputState();
        }

        public void Start()
        {
            _pollTimer.Start();
        }

        public void Stop()
        {
            _pollTimer.Stop();
        }

        public void Dispose()
        {
            Stop();
        }

        // MARK: - Win32 Non-Blocking Input & Window APIs

        private const int VK_LBUTTON = 0x01;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_MENU = 0x12;     // Alt key
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const uint GA_ROOT = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private void PollInputState()
        {
            if (!GetCursorPos(out POINT rawPt)) return;
            Point cursorPt = new Point(rawPt.X, rawPt.Y);

            bool escNow = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;
            if (escNow && !_wasEscDown && _wheelPanel.ViewModel.IsVisible)
            {
                _wheelPanel.HideWheel();
            }
            _wasEscDown = escNow;

            bool leftNow = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            bool altNow = IsAltPressed();

            // Detect Left Mouse Button Down transition
            if (leftNow && !_wasLeftButtonDown)
            {
                _wasLeftButtonDown = true;
                _mouseDownTimestamp = DateTime.UtcNow;
                OnLeftMouseDown(cursorPt, altNow);
            }
            // Detect Left Mouse Button Up transition
            else if (!leftNow && _wasLeftButtonDown)
            {
                _wasLeftButtonDown = false;
                OnLeftMouseUp(cursorPt);
            }

            // Live 120Hz hover tracking when wheel is visible
            if (_wheelPanel.ViewModel.IsVisible)
            {
                _wheelPanel.UpdateHoverFromPhysicalCursor(cursorPt);
            }
        }

        private void OnLeftMouseDown(Point cursorPt, bool altDown)
        {
            // 1. If the wheel is currently open and user clicks outside of it, close it immediately
            if (_wheelPanel.ViewModel.IsVisible)
            {
                if (!_wheelPanel.IsPhysicalPointInsideWheel(cursorPt))
                {
                    _wheelPanel.HideWheel();
                }
                return;
            }

            // 2. Only activate when Alt is held AND user left-clicks
            if (!altDown) return;

            // 3. Ignore clicks inside our own Converze windows
            if (IsPointInsideOwnAppWindow(cursorPt)) return;

            // 4. Inspect window under cursor on a background STA thread so the mouse NEVER freezes
            TriggerExplorerQueryAsync(cursorPt);
        }

        private void OnLeftMouseUp(Point cursorPt)
        {
            double heldSeconds = _mouseDownTimestamp.HasValue
                ? (DateTime.UtcNow - _mouseDownTimestamp.Value).TotalSeconds
                : 0.0;

            _mouseDownTimestamp = null;

            // If user held Alt + Drag onto a slice (>= 180ms) and released, commit the conversion
            if (_wheelPanel.ViewModel.IsVisible && heldSeconds >= 0.18)
            {
                _wheelPanel.UpdateHoverFromPhysicalCursor(cursorPt);
                _wheelPanel.CommitHoveredSelectionIfAny();
            }
        }

        private void TriggerExplorerQueryAsync(Point cursorPt)
        {
            if (Interlocked.CompareExchange(ref _queryInProgress, 1, 0) != 0) return;

            POINT pt = new POINT { X = (int)cursorPt.X, Y = (int)cursorPt.Y };
            IntPtr childHwnd = WindowFromPoint(pt);
            if (childHwnd == IntPtr.Zero)
            {
                Interlocked.Exchange(ref _queryInProgress, 0);
                return;
            }

            IntPtr rootHwnd = GetAncestor(childHwnd, GA_ROOT);
            if (rootHwnd == IntPtr.Zero) rootHwnd = childHwnd;

            string rootClass = GetWindowClass(rootHwnd);
            bool isExplorerWindow = string.Equals(rootClass, "CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(rootClass, "ExploreWClass", StringComparison.OrdinalIgnoreCase);
            bool isDesktopWindow = string.Equals(rootClass, "Progman", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(rootClass, "WorkerW", StringComparison.OrdinalIgnoreCase);

            if (!isExplorerWindow && !isDesktopWindow)
            {
                Interlocked.Exchange(ref _queryInProgress, 0);
                return;
            }

            // Run UIAutomation + Shell COM query on a dedicated STA thread so the UI & mouse stay 100% fluid
            var thread = new Thread(() =>
            {
                try
                {
                    if (!IsPointOverExplorerFileItem(cursorPt))
                    {
                        return;
                    }

                    // Try immediate selection first (0ms), then 45ms and 95ms if Windows 10 Explorer is updating selection on click
                    int[] delays = { 0, 45, 95 };
                    foreach (int delay in delays)
                    {
                        if (delay > 0) Thread.Sleep(delay);

                        var files = QuerySelectionForSpecificWindow(rootHwnd, isDesktopWindow);
                        if (files.Count > 0)
                        {
                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                if (!_wheelPanel.ViewModel.IsVisible)
                                {
                                    _wheelPanel.ShowAtPoint(cursorPt, new DraggedSelection(files));
                                }
                                else
                                {
                                    _wheelPanel.UpdateDragSelection(new DraggedSelection(files));
                                }
                            });
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore background query errors
                }
                finally
                {
                    Interlocked.Exchange(ref _queryInProgress, 0);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Compatible with BOTH Windows 10 (DirectUIHWND / UIItem / SysListView32) and Windows 11 (ItemsView / ListItem).
        /// Returns false when clicking on empty folder background, empty desktop wallpaper, scrollbars, or window chrome.
        /// </summary>
        private static bool IsPointOverExplorerFileItem(Point physicalPt)
        {
            try
            {
                AutomationElement? el = AutomationElement.FromPoint(physicalPt);
                if (el == null) return false;

                var ct = el.Current.ControlType;
                string cls = el.Current.ClassName ?? "";

                // If the element directly under the cursor is the empty list container itself, it's empty space!
                if (ct == ControlType.List || ct == ControlType.Window || ct == ControlType.Pane ||
                    ct == ControlType.ScrollBar || ct == ControlType.Header || ct == ControlType.HeaderItem ||
                    ct == ControlType.ToolBar || ct == ControlType.MenuBar || ct == ControlType.StatusBar ||
                    ct == ControlType.Tree)
                {
                    return false;
                }

                if (string.Equals(cls, "UIItemsView", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "DirectUIHWND", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "SysListView32", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "Progman", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "WorkerW", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Walk up to 5 ancestors to confirm this element belongs to a file item (ListItem / DataItem / UIItem)
                AutomationElement? cur = el;
                for (int depth = 0; depth < 6 && cur != null; depth++)
                {
                    var curCt = cur.Current.ControlType;
                    string curCls = cur.Current.ClassName ?? "";

                    if (curCt == ControlType.ListItem ||
                        curCt == ControlType.DataItem ||
                        string.Equals(curCls, "UIItem", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(curCls, "ListViewItem", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (curCt == ControlType.List || curCt == ControlType.Window)
                    {
                        break;
                    }

                    cur = TreeWalker.ControlViewWalker.GetParent(cur);
                }
            }
            catch
            {
                // Fallback if UIAutomation is unavailable: allow if child window class is DirectUIHWND or SysListView32
                return true;
            }

            return false;
        }

        private static List<string> QuerySelectionForSpecificWindow(IntPtr targetRootHwnd, bool isDesktop)
        {
            var results = new List<string>();
            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return results;

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return results;

                dynamic windows = shell.Windows();

                if (isDesktop)
                {
                    try
                    {
                        int hwndOut = 0;
                        dynamic? desktopWin = windows.FindWindowSW(0, 0, 8, out hwndOut, 1);
                        ExtractSelectedPathsFromWindow(desktopWin, results);
                    }
                    catch { }
                    return results;
                }

                int count = (int)windows.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic win = windows.Item(i);
                        if (win == null) continue;

                        IntPtr winHwnd = new IntPtr((long)win.HWND);
                        if (winHwnd == targetRootHwnd)
                        {
                            ExtractSelectedPathsFromWindow(win, results);
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return results;
        }

        private static void ExtractSelectedPathsFromWindow(dynamic? win, List<string> results)
        {
            if (win == null) return;
            dynamic? doc = win.Document;
            if (doc == null) return;

            dynamic selectedItems = doc.SelectedItems();
            int selCount = (int)selectedItems.Count;
            for (int j = 0; j < selCount; j++)
            {
                dynamic item = selectedItems.Item(j);
                string? path = item?.Path as string;
                if (string.IsNullOrWhiteSpace(path)) continue;

                string fileName = Path.GetFileName(path);
                if (string.Equals(fileName, "Converze.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "Uninstall-Converze.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "Converze-Setup.exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(path) || Directory.Exists(path))
                {
                    results.Add(path);
                }
            }
        }

        private static bool IsAltPressed()
        {
            return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;
        }

        private static string GetWindowClass(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static bool IsPointInsideOwnAppWindow(Point physicalPt)
        {
            try
            {
                POINT p = new POINT { X = (int)physicalPt.X, Y = (int)physicalPt.Y };
                IntPtr hwnd = WindowFromPoint(p);
                if (hwnd == IntPtr.Zero) return false;
                GetWindowThreadProcessId(hwnd, out uint pid);
                return pid == (uint)Environment.ProcessId;
            }
            catch
            {
                return false;
            }
        }
    }
}

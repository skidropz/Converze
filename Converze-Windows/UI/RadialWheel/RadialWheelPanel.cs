using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Converze.Models;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

namespace Converze.UI.RadialWheel
{
    public sealed class RadialWheelPanel : Window
    {
        public RadialWheelViewModel ViewModel { get; } = new RadialWheelViewModel();
        public Action<List<string>, WheelTarget>? OnConversionRequested { get; set; }

        private readonly RadialWheelControl _wheelControl;
        private Point _anchorScreenDip;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public RadialWheelPanel()
        {
            Title = "Converze Radial Wheel";
            Width = RadialWheelControl.TotalControlWidth;
            Height = RadialWheelControl.TotalControlHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            AllowDrop = true;

            _wheelControl = new RadialWheelControl(
                ViewModel,
                onTargetSelected: target => CommitTarget(target),
                onOutsideClicked: () => HideWheel());

            Content = _wheelControl;

            PreviewKeyDown += OnPreviewKeyDown;
            DragEnter += OnOleDragEnterOrOver;
            DragOver += OnOleDragEnterOrOver;
            DragLeave += (_, _) => ViewModel.UpdateHover(new Point(0, 0));
            Drop += OnOleDrop;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWheel();
                e.Handled = true;
            }
        }

        public void ShowAtPoint(Point physicalScreenCursor, DraggedSelection? selection)
        {
            if (selection != null)
            {
                ViewModel.Selection = selection;
            }

            ViewModel.HoveredIndex = null;
            ViewModel.PointerOffset = new Point(0, 0);
            ViewModel.IsVisible = true;

            // Ensure HWND & PresentationSource exist on Windows 10 BEFORE computing DPI coordinates
            if (!IsVisible)
            {
                Show();
            }

            Point dipPt = PhysicalToDip(physicalScreenCursor);

            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point((int)physicalScreenCursor.X, (int)physicalScreenCursor.Y));
            Rect workAreaDip = PhysicalRectToDip(screen.WorkingArea);

            double marginX = 165.0;
            double marginY = 185.0;
            double clampedX = Math.Clamp(dipPt.X, workAreaDip.Left + marginX, workAreaDip.Right - marginX);
            double clampedY = Math.Clamp(dipPt.Y, workAreaDip.Top + marginY, workAreaDip.Bottom - marginY);

            _anchorScreenDip = new Point(clampedX, clampedY);
            Left = _anchorScreenDip.X - RadialWheelControl.WheelCenterX;
            Top = _anchorScreenDip.Y - RadialWheelControl.WheelCenterY;

            ViewModel.NotifyChanged();
            _wheelControl.ResetOpenAnimation();

            // Force topmost Z-order on Windows 10 without stealing focus
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
            }
            catch { }
        }

        public void UpdateDragSelection(DraggedSelection selection)
        {
            if (!Equals(ViewModel.Selection, selection))
            {
                ViewModel.Selection = selection;
                ViewModel.NotifyChanged();
            }
        }

        public void UpdateHoverFromPhysicalCursor(Point physicalScreenCursor)
        {
            if (!ViewModel.IsVisible) return;
            Point dipPt = PhysicalToDip(physicalScreenCursor);
            double dx = dipPt.X - _anchorScreenDip.X;
            double dy = dipPt.Y - _anchorScreenDip.Y;
            ViewModel.UpdateHover(new Point(dx, dy));
        }

        public bool IsPhysicalPointInsideWheel(Point physicalScreenCursor)
        {
            if (!ViewModel.IsVisible) return false;
            Point dipPt = PhysicalToDip(physicalScreenCursor);
            double dx = dipPt.X - _anchorScreenDip.X;
            double dy = dipPt.Y - _anchorScreenDip.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            return dist <= ConverzeWheelMetrics.BackdropRadius + 24.0;
        }

        public bool CommitHoveredSelectionIfAny()
        {
            if (!ViewModel.IsVisible) return false;
            var targets = ViewModel.Targets;
            if (ViewModel.HoveredIndex.HasValue &&
                ViewModel.HoveredIndex.Value >= 0 &&
                ViewModel.HoveredIndex.Value < targets.Count &&
                ViewModel.Selection != null &&
                ViewModel.Selection.FilePaths.Count > 0)
            {
                int idx = ViewModel.HoveredIndex.Value;
                var chosen = targets[idx];
                var paths = ViewModel.Selection.FilePaths.ToList();

                ViewModel.TriggerDropSuccess(idx, () =>
                {
                    HideWheel();
                    OnConversionRequested?.Invoke(paths, chosen);
                });
                return true;
            }
            return false;
        }

        private void CommitTarget(WheelTarget target)
        {
            var paths = ViewModel.Selection?.FilePaths?.ToList() ?? new List<string>();
            HideWheel();
            if (paths.Count > 0)
            {
                OnConversionRequested?.Invoke(paths, target);
            }
        }

        public void HideWheel()
        {
            if (!ViewModel.IsVisible && !_wheelControl.IsClosing) return;
            ViewModel.IsVisible = false;
            ViewModel.HoveredIndex = null;
            ViewModel.PointerOffset = new Point(0, 0);
            ViewModel.NotifyChanged();

            _wheelControl.StartCloseAnimation(() =>
            {
                if (!ViewModel.IsVisible)
                {
                    Hide();
                }
            });
        }

        // MARK: - Native Windows Explorer OLE Drag & Drop Support

        private void OnOleDragEnterOrOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    if (ViewModel.Selection == null || !ViewModel.Selection.FilePaths.SequenceEqual(files))
                    {
                        ViewModel.Selection = new DraggedSelection(files);
                    }
                }

                Point pos = e.GetPosition(_wheelControl);
                double dx = pos.X - RadialWheelControl.WheelCenterX;
                double dy = pos.Y - RadialWheelControl.WheelCenterY;
                ViewModel.UpdateHover(new Point(dx, dy));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnOleDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                var validFiles = files.Where(f => File.Exists(f) || Directory.Exists(f)).ToList();
                if (validFiles.Count > 0)
                {
                    ViewModel.Selection = new DraggedSelection(validFiles);
                }

                Point pos = e.GetPosition(_wheelControl);
                double dx = pos.X - RadialWheelControl.WheelCenterX;
                double dy = pos.Y - RadialWheelControl.WheelCenterY;
                ViewModel.UpdateHover(new Point(dx, dy));

                CommitHoveredSelectionIfAny();
            }
            e.Handled = true;
        }

        private Point PhysicalToDip(Point physicalPt)
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    Matrix m = source.CompositionTarget.TransformFromDevice;
                    return m.Transform(physicalPt);
                }
            }
            catch { }

            double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (dpiScale <= 0) dpiScale = 1.0;
            return new Point(physicalPt.X / dpiScale, physicalPt.Y / dpiScale);
        }

        private Rect PhysicalRectToDip(System.Drawing.Rectangle r)
        {
            Point tl = PhysicalToDip(new Point(r.Left, r.Top));
            Point br = PhysicalToDip(new Point(r.Right, r.Bottom));
            return new Rect(tl, br);
        }
    }
}

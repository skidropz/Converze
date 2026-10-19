using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Converze.Models;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;

namespace Converze.UI.Uninstaller
{
    public sealed class UninstallWindow : Window
    {
        private readonly TextBlock _statusText;
        private readonly Border _progressFill;
        private readonly Border _uninstallBtn;
        private readonly Border _cancelBtn;
        private bool _isUninstalling;

        public UninstallWindow()
        {
            var prefs = ConverzePreferences.Shared;
            bool isDark = prefs.IsDarkMode;
            var theme = prefs.ColorTheme;

            Width = 480;
            Height = 290;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Title = prefs.Tr("Dezinstalare Converze", "Uninstall Converze");

            Color bgTop = isDark ? Color.FromRgb(18, 20, 28) : Color.FromRgb(247, 249, 253);
            Color bgBot = isDark ? Color.FromRgb(13, 14, 20) : Color.FromRgb(236, 241, 249);

            var root = new Border
            {
                Background = new LinearGradientBrush(bgTop, bgBot, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)),
                Padding = new Thickness(26, 22, 26, 22)
            };

            var stack = new StackPanel();

            // Header
            var headerRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            headerRow.Children.Add(ConverzeAppIconLoader.CreateAppIconElement(42));

            var titleCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            titleCol.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Dezinstalare Converze", "Uninstall Converze"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });
            titleCol.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Elimină complet Converze din Program Files și din Windows", "Completely remove Converze from Program Files and Windows"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(165, 255, 255, 255) : Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            headerRow.Children.Add(titleCol);
            stack.Children.Add(headerRow);

            _statusText = new TextBlock
            {
                Text = prefs.Tr(
                    "Ești sigur că vrei să dezinstalezi Converze de pe acest calculator? Toate scurtăturile și fișierele aplicației din Program Files vor fi șterse.",
                    "Are you sure you want to uninstall Converze from this computer? All shortcuts and application files in Program Files will be removed."),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(215, 255, 255, 255) : Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 4, 0, 18)
            };
            stack.Children.Add(_statusText);

            // Progress Bar
            _progressFill = new Border
            {
                Height = 8,
                Width = 0,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(239, 68, 68))
            };
            var progressTrack = new Border
            {
                Width = 420,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 24),
                Child = _progressFill
            };
            stack.Children.Add(progressTrack);

            // Buttons
            var btnRow = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            _cancelBtn = new Border
            {
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(isDark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(18, 15, 23, 42)),
                Child = new TextBlock
                {
                    Text = prefs.Tr("Anulează", "Cancel"),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(30, 41, 59))
                }
            };
            _cancelBtn.MouseLeftButtonUp += (_, _) =>
            {
                if (!_isUninstalling) System.Windows.Application.Current.Shutdown();
            };

            _uninstallBtn = new Border
            {
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(20, 8, 20, 8),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Child = new TextBlock
                {
                    Text = prefs.Tr("Dezinstalează Converze", "Uninstall Converze"),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White
                }
            };
            _uninstallBtn.MouseLeftButtonUp += async (_, _) => await RunUninstallAsync();

            btnRow.Children.Add(_cancelBtn);
            btnRow.Children.Add(_uninstallBtn);
            stack.Children.Add(btnRow);

            root.Child = stack;
            Content = root;
        }

        public static bool IsAdministrator()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private async Task RunUninstallAsync()
        {
            if (_isUninstalling) return;

            // Request UAC elevation if not running as Admin so we can remove from C:\Program Files\Converze
            if (!IsAdministrator())
            {
                try
                {
                    string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Uninstall-Converze.exe");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--uninstall-elevated",
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    System.Windows.Application.Current.Shutdown();
                    return;
                }
                catch
                {
                    // Continue best-effort if user declined UAC
                }
            }

            _isUninstalling = true;
            _cancelBtn.Visibility = Visibility.Collapsed;
            _uninstallBtn.IsEnabled = false;
            _uninstallBtn.Opacity = 0.5;

            var prefs = ConverzePreferences.Shared;
            string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Converze");
            string currentDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            if (currentDir.EndsWith("Converze", StringComparison.OrdinalIgnoreCase))
            {
                installDir = currentDir;
            }

            // Step 1: Stop running Converze instances
            _statusText.Text = prefs.Tr("Se opresc procesele Converze active...", "Stopping active Converze processes...");
            _progressFill.Width = 120;
            await Task.Delay(250);

            int myPid = Environment.ProcessId;
            foreach (var proc in Process.GetProcessesByName("Converze"))
            {
                try
                {
                    if (proc.Id != myPid)
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                }
                catch { }
            }

            // Step 2: Remove shortcuts & registry keys
            _statusText.Text = prefs.Tr("Se șterg scurtăturile și înregistrările din Windows...", "Removing shortcuts and Windows registry entries...");
            _progressFill.Width = 280;
            await Task.Delay(250);

            try
            {
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Converze.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

                string commonDesktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Converze.lnk");
                if (File.Exists(commonDesktopLnk)) File.Delete(commonDesktopLnk);

                string commonStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Converze");
                if (Directory.Exists(commonStartMenu)) Directory.Delete(commonStartMenu, true);

                string userStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Converze");
                if (Directory.Exists(userStartMenu)) Directory.Delete(userStartMenu, true);
            }
            catch { }

            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.DeleteValue("Converze", false);
            }
            catch { }

            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Converze", false);
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Converze", false);
            }
            catch { }

            try
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Converze");
                if (Directory.Exists(appDataDir)) Directory.Delete(appDataDir, true);
            }
            catch { }

            // Step 3: Schedule deletion of C:\Program Files\Converze after uninstaller process exits
            _statusText.Text = prefs.Tr("Converze a fost dezinstalat complet!", "Converze has been completely uninstalled!");
            _progressFill.Width = 420;
            await Task.Delay(500);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{installDir}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetTempPath()
                });
            }
            catch { }

            System.Windows.Application.Current.Shutdown();
        }
    }
}

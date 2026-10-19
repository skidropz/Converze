using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Converze.Models;
using Converze.Services;
using Converze.UI.MenuBar;
using Converze.UI.Progress;
using Converze.UI.RadialWheel;
using Converze.UI.Settings;
using Converze.UI.Uninstaller;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace Converze.App
{
    public static class Program
    {
        private static Mutex? _singleInstanceMutex;

        [STAThread]
        public static void Main(string[] args)
        {
            string exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
            bool isUninstallMode = args.Any(a => a.StartsWith("--uninstall", StringComparison.OrdinalIgnoreCase)) ||
                                   exeName.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase);

            if (isUninstallMode)
            {
                var uninstallerApp = new System.Windows.Application
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose
                };
                uninstallerApp.Run(new UninstallWindow());
                return;
            }

            _singleInstanceMutex = new Mutex(true, "Converze_Windows_SingleInstance_v2_2", out bool isNewInstance);
            if (!isNewInstance)
            {
                return;
            }

            var app = new ConverzeApplication();
            app.Run();
        }
    }

    public sealed class ConverzeApplication : System.Windows.Application
    {
        private RadialWheelPanel? _wheelPanel;
        private DragMonitorService? _dragMonitor;
        private Forms.NotifyIcon? _trayIcon;

        private ConverzeOnboardingWindow? _onboardingWindow;
        private SettingsWindow? _settingsWindow;

        public ConverzeApplication()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize Radial Wheel Overlay Window
            _wheelPanel = new RadialWheelPanel
            {
                OnConversionRequested = ExecuteWheelConversion
            };

            // 2. Start Zero-Lag System-Wide Alt + Left-Click / Drag Monitor
            _dragMonitor = new DragMonitorService(_wheelPanel);
            _dragMonitor.Start();

            // 3. Initialize System Tray Icon & Bilingual Menu
            SetupSystemTrayIcon();
            ConverzePreferences.Shared.OnPreferencesChanged += RebuildTrayMenu;

            // 4. First-Launch Welcome Screen Check (MANDATORY on the first run after installation, and ONLY on the first run)
            if (!ConverzePreferences.Shared.HasCompletedOnboarding)
            {
                OpenOnboardingWindow();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _dragMonitor?.Dispose();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnExit(e);
        }

        private void SetupSystemTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = ConverzeAppIconLoader.LoadTrayIcon(),
                Text = "Converze",
                Visible = true
            };

            _trayIcon.MouseUp += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    OpenSettingsWindow();
                }
            };
            RebuildTrayMenu();
        }

        private void RebuildTrayMenu()
        {
            if (_trayIcon == null) return;
            var prefs = ConverzePreferences.Shared;

            var menu = new Forms.ContextMenuStrip();

            var statusItem = new Forms.ToolStripMenuItem(
                prefs.Tr("● Converze este activ", "● Converze is active"))
            {
                Enabled = false
            };
            menu.Items.Add(statusItem);
            menu.Items.Add(new Forms.ToolStripSeparator());

            var convertFileItem = new Forms.ToolStripMenuItem(
                prefs.Tr("Convertește un fișier...", "Convert a File..."));
            convertFileItem.Click += (_, _) => PromptFileAndOpenWheel();
            menu.Items.Add(convertFileItem);

            menu.Items.Add(new Forms.ToolStripSeparator());

            var welcomeItem = new Forms.ToolStripMenuItem(
                prefs.Tr("Ghid de bun venit...", "Welcome Guide..."));
            welcomeItem.Click += (_, _) => OpenOnboardingWindow();
            menu.Items.Add(welcomeItem);

            var settingsItem = new Forms.ToolStripMenuItem(
                prefs.Tr("Setări", "Settings"));
            settingsItem.Click += (_, _) => OpenSettingsWindow();
            menu.Items.Add(settingsItem);

            menu.Items.Add(new Forms.ToolStripSeparator());

            var quitItem = new Forms.ToolStripMenuItem(
                prefs.Tr("Închide Converze", "Quit Converze"));
            quitItem.Click += (_, _) => Shutdown();
            menu.Items.Add(quitItem);

            _trayIcon.ContextMenuStrip = menu;
        }

        public void OpenOnboardingWindow()
        {
            if (_onboardingWindow == null || !_onboardingWindow.IsLoaded)
            {
                _onboardingWindow = new ConverzeOnboardingWindow(
                    onFinishedOpenMain: OpenSettingsWindow,
                    onFilesDropped: OpenWheelForFiles);
            }
            _onboardingWindow.Show();
            _onboardingWindow.Activate();
        }

        public void OpenSettingsWindow()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(
                    onOpenOnboarding: OpenOnboardingWindow,
                    onFilesDropped: OpenWheelForFiles,
                    onPickFileClicked: PromptFileAndOpenWheel);
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void OpenWheelForFiles(string[] files)
        {
            var valid = files.Where(f => File.Exists(f) || Directory.Exists(f)).ToList();
            if (valid.Count == 0 || _wheelPanel == null) return;

            var cursor = Forms.Cursor.Position;
            _wheelPanel.ShowAtPoint(new Point(cursor.X, cursor.Y), new DraggedSelection(valid));
        }

        private void PromptFileAndOpenWheel()
        {
            var prefs = ConverzePreferences.Shared;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = prefs.Tr("Selectează fișierul pe care dorești să-l convertești", "Select the file you want to convert"),
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0 && _wheelPanel != null)
            {
                var screen = Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
                Point centerScreen = new Point(screen.Width * 0.5, screen.Height * 0.5);
                _wheelPanel.ShowAtPoint(centerScreen, new DraggedSelection(dlg.FileNames));
            }
        }

        private async void ExecuteWheelConversion(List<string> inputPaths, WheelTarget target)
        {
            if (inputPaths.Count == 0) return;
            string firstFile = Path.GetFileName(inputPaths[0]);
            ConversionHUDManager.Shared.ShowConverting(firstFile, target.WheelLabel);

            try
            {
                var results = await ConversionEngine.Shared.ExecuteAsync(
                    inputPaths,
                    target,
                    progressCallback: (frac, msg) => ConversionHUDManager.Shared.UpdateProgress(frac, msg));

                if (results.Count > 0)
                {
                    ConversionHUDManager.Shared.ShowCompleted(results[0]);
                }
            }
            catch (Exception ex)
            {
                ConversionHUDManager.Shared.ShowError(ex.Message);
            }
        }
    }
}

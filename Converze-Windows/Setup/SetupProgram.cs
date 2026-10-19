using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ConverzeSetup
{
    public static class SetupProgram
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose
            };
            app.Run(new SetupWizardWindow());
        }
    }

    public sealed class SetupWizardWindow : Window
    {
        private readonly string _installDir;
        private bool _isRomanian = true;

        private readonly TextBlock _headerTitle;
        private readonly TextBlock _headerSubtitle;
        private readonly Border _langRoBtn;
        private readonly Border _langEnBtn;
        private readonly TextBlock _folderHeaderLabel;
        private readonly CheckBox _cbDesktopShortcut;
        private readonly TextBlock _cbDesktopText;
        private readonly CheckBox _cbStartMenuShortcut;
        private readonly TextBlock _cbStartMenuText;
        private readonly CheckBox _cbStartupRun;
        private readonly TextBlock _cbStartupText;
        private readonly TextBlock _statusLabel;
        private readonly TextBlock _percentLabel;
        private readonly Border _progressFill;
        private readonly Border _actionBtn;
        private readonly TextBlock _actionBtnText;
        private readonly Border _cancelBtn;
        private readonly TextBlock _cancelBtnText;

        private bool _isInstalling;
        private bool _isFinished;
        private const double TrackWidth = 484.0;

        private static readonly Color PrimaryBlue = Color.FromRgb(37, 99, 235);
        private static readonly Color LightBlue = Color.FromRgb(96, 165, 250);
        private static readonly Color DeepBlue = Color.FromRgb(29, 78, 216);

        public SetupWizardWindow()
        {
            _installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Converze");

            Width = 560;
            Height = 455;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var root = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(18, 20, 28),
                    Color.FromRgb(12, 14, 20),
                    new Point(0, 0),
                    new Point(1, 1)),
                Padding = new Thickness(28, 22, 28, 22)
            };

            var mainStack = new StackPanel();

            // 1. Header with App Icon + Language Selector (Română / English)
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconImg = LoadEmbeddedIcon(46);
            if (iconImg != null)
            {
                Grid.SetColumn(iconImg, 0);
                headerGrid.Children.Add(iconImg);
            }

            var titleCol = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 10, 0)
            };
            _headerTitle = new TextBlock
            {
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            _headerSubtitle = new TextBlock
            {
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromArgb(175, 255, 255, 255)),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            titleCol.Children.Add(_headerTitle);
            titleCol.Children.Add(_headerSubtitle);
            Grid.SetColumn(titleCol, 1);
            headerGrid.Children.Add(titleCol);

            // Language pill toggle (🇷🇴 RO | 🇬🇧 EN)
            var langBar = new Border
            {
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(3),
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            var langRow = new StackPanel { Orientation = Orientation.Horizontal };

            _langRoBtn = CreateLangPillButton("Română", () => SetLanguage(true));
            _langEnBtn = CreateLangPillButton("English", () => SetLanguage(false));

            langRow.Children.Add(_langRoBtn);
            langRow.Children.Add(_langEnBtn);
            langBar.Child = langRow;
            Grid.SetColumn(langBar, 2);
            headerGrid.Children.Add(langBar);

            mainStack.Children.Add(headerGrid);

            // 2. Destination Folder Card
            var folderCard = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 14),
                Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            var folderStack = new StackPanel();
            _folderHeaderLabel = new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(LightBlue)
            };
            folderStack.Children.Add(_folderHeaderLabel);
            folderStack.Children.Add(new TextBlock
            {
                Text = _installDir,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 0)
            });
            folderCard.Child = folderStack;
            mainStack.Children.Add(folderCard);

            // 3. Options Card
            var optionsCard = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 18),
                Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            var optStack = new StackPanel();

            (_cbDesktopShortcut, _cbDesktopText) = CreateOptionCheckBox(true);
            (_cbStartMenuShortcut, _cbStartMenuText) = CreateOptionCheckBox(true);
            (_cbStartupRun, _cbStartupText) = CreateOptionCheckBox(true);

            optStack.Children.Add(_cbDesktopShortcut);
            optStack.Children.Add(_cbStartMenuShortcut);
            optStack.Children.Add(_cbStartupRun);
            optionsCard.Child = optStack;
            mainStack.Children.Add(optionsCard);

            // 4. Status & Progress Bar
            var statusRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            _statusLabel = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255))
            };
            _percentLabel = new TextBlock
            {
                Text = "0%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(LightBlue),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            statusRow.Children.Add(_statusLabel);
            statusRow.Children.Add(_percentLabel);
            mainStack.Children.Add(statusRow);

            _progressFill = new Border
            {
                Height = 8,
                Width = 0,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(LightBlue, PrimaryBlue, new Point(0, 0), new Point(1, 0))
            };
            var progressTrack = new Border
            {
                Width = TrackWidth,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 22),
                Child = _progressFill
            };
            mainStack.Children.Add(progressTrack);

            // 5. Footer Buttons
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _cancelBtnText = new TextBlock
            {
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            _cancelBtn = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 9, 18, 9),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                Child = _cancelBtnText
            };
            _cancelBtn.MouseLeftButtonUp += (_, _) =>
            {
                if (!_isInstalling) Close();
            };

            _actionBtnText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            _actionBtn = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(22, 9, 22, 9),
                Cursor = Cursors.Hand,
                Background = new LinearGradientBrush(LightBlue, DeepBlue, new Point(0, 0), new Point(1, 1)),
                Child = _actionBtnText
            };
            _actionBtn.MouseLeftButtonUp += async (_, _) => await OnActionClickedAsync();

            btnRow.Children.Add(_cancelBtn);
            btnRow.Children.Add(_actionBtn);
            mainStack.Children.Add(btnRow);

            root.Child = mainStack;
            Content = root;

            ApplyLanguageStrings();
        }

        private string Tr(string ro, string en) => _isRomanian ? ro : en;

        private void SetLanguage(bool romanian)
        {
            if (_isRomanian == romanian) return;
            _isRomanian = romanian;
            ApplyLanguageStrings();
        }

        private void ApplyLanguageStrings()
        {
            Title = Tr("Instalare Converze 1.0", "Converze 1.0 Installation");
            _headerTitle.Text = Tr("Instalare Converze 1.0", "Install Converze 1.0");
            _headerSubtitle.Text = Tr(
                "Convertor offline de fișiere",
                "Instant offline file converter");

            _langRoBtn.Background = new SolidColorBrush(_isRomanian ? PrimaryBlue : Colors.Transparent);
            _langEnBtn.Background = new SolidColorBrush(!_isRomanian ? PrimaryBlue : Colors.Transparent);

            _folderHeaderLabel.Text = Tr(
                "DIRECTOR DE INSTALARE (PROGRAM FILES):",
                "INSTALLATION DIRECTORY (PROGRAM FILES):");

            _cbDesktopText.Text = Tr(
                "Creează scurtătură pe Desktop (Converze)",
                "Create Desktop shortcut (Converze)");
            _cbStartMenuText.Text = Tr(
                "Adaugă în Start Menu și înregistrează Uninstaller-ul în Windows",
                "Add to Start Menu and register Windows Uninstaller");
            _cbStartupText.Text = Tr(
                "Pornește automat Converze la deschiderea Windows-ului",
                "Launch Converze automatically at Windows startup");

            _cancelBtnText.Text = Tr("Anulează", "Cancel");

            if (_isFinished)
            {
                _statusLabel.Text = Tr(
                    "✓ Converze a fost instalat cu succes!",
                    "✓ Converze was successfully installed!");
                _actionBtnText.Text = Tr("Pornește Converze acum ✓", "Launch Converze Now ✓");
            }
            else if (!_isInstalling)
            {
                _statusLabel.Text = Tr(
                    "Pregătit pentru instalare.",
                    "Ready to install");
                _actionBtnText.Text = Tr("Instalează în Program Files →", "Install to Program Files →");
            }
        }

        private static Border CreateLangPillButton(string label, Action onClick)
        {
            var btn = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };
            btn.MouseLeftButtonUp += (_, _) => onClick();
            return btn;
        }

        private static (CheckBox Box, TextBlock Label) CreateOptionCheckBox(bool isChecked)
        {
            var tb = new TextBlock
            {
                FontSize = 12,
                Foreground = Brushes.White
            };
            var cb = new CheckBox
            {
                IsChecked = isChecked,
                Margin = new Thickness(0, 4, 0, 4),
                Cursor = Cursors.Hand,
                Content = tb
            };
            return (cb, tb);
        }

        private void AnimateProgress(double fraction, string status)
        {
            _statusLabel.Text = status;
            int pct = (int)Math.Round(fraction * 100.0);
            _percentLabel.Text = $"{pct}%";

            var anim = new DoubleAnimation
            {
                To = TrackWidth * Math.Clamp(fraction, 0.0, 1.0),
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _progressFill.BeginAnimation(WidthProperty, anim);
        }

        private async Task OnActionClickedAsync()
        {
            if (_isInstalling) return;

            if (_isFinished)
            {
                LaunchInstalledAppNonElevated();
                Close();
                return;
            }

            _isInstalling = true;
            _cancelBtn.Visibility = Visibility.Collapsed;
            _cbDesktopShortcut.IsEnabled = false;
            _cbStartMenuShortcut.IsEnabled = false;
            _cbStartupRun.IsEnabled = false;
            _actionBtn.Opacity = 0.6;

            try
            {
                // Step 1: Stop any running Converze instances
                AnimateProgress(0.15, Tr("Se opresc instanțele anterioare Converze...", "Stopping previous Converze instances..."));
                await Task.Delay(200);

                foreach (var proc in Process.GetProcessesByName("Converze"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                    catch { }
                }

                // Step 2: Extract embedded payload.zip into C:\Program Files\Converze
                AnimateProgress(0.45, Tr($"Se extrag fișierele în {_installDir}...", $"Extracting files to {_installDir}..."));
                await Task.Delay(250);

                Directory.CreateDirectory(_installDir);
                using (Stream? payloadStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                {
                    if (payloadStream == null)
                    {
                        throw new InvalidOperationException(Tr(
                            "Pachetul payload.zip nu a fost găsit în installer.",
                            "Embedded payload.zip was not found in the installer."));
                    }
                    using var archive = new ZipArchive(payloadStream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(_installDir, overwriteFiles: true);
                }

                string mainExePath = Path.Combine(_installDir, "Converze.exe");
                string uninstallExePath = Path.Combine(_installDir, "Uninstall-Converze.exe");
                string iconPath = Path.Combine(_installDir, "Resources", "AppIcon.ico");

                if (File.Exists(mainExePath) && !File.Exists(uninstallExePath))
                {
                    File.Copy(mainExePath, uninstallExePath, overwrite: true);
                }

                // Step 3: Write fresh install signature AND pre-configure chosen language with HasCompletedOnboarding = false
                AnimateProgress(0.68, Tr("Se configurează limba și fereastra de bun venit", "Configuring language and first-launch Welcome Screen..."));
                await Task.Delay(180);

                string newInstallSig = $"converze-installed-{Guid.NewGuid():N}";
                File.WriteAllText(Path.Combine(_installDir, "install_signature.txt"), newInstallSig);

                try
                {
                    string appDataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Converze");
                    Directory.CreateDirectory(appDataDir);
                    string appDataPrefs = Path.Combine(appDataDir, "preferences.json");
                    string langCode = _isRomanian ? "ro" : "en";
                    string initialJson = $"{{\n  \"Language\": \"{langCode}\",\n  \"AppearanceMode\": \"light\",\n  \"ColorTheme\": \"sapphire\",\n  \"HasCompletedOnboarding\": false,\n  \"OnboardedInstallSignature\": \"\",\n  \"HapticsEnabled\": true,\n  \"AutoRevealInFinder\": false,\n  \"ImageCompressionQuality\": 0.9\n}}";
                    File.WriteAllText(appDataPrefs, initialJson);
                }
                catch { }

                // Step 4: Create Shortcuts & Register Uninstaller in Windows Installed Apps (Control Panel)
                AnimateProgress(0.88, Tr("Se creează scurtăturile și se înregistrează Uninstaller-ul...", "Creating shortcuts and registering Uninstaller..."));
                await Task.Delay(220);

                if (_cbDesktopShortcut.IsChecked == true)
                {
                    string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Converze.lnk");
                    CreateWindowsShortcut(desktopLnk, mainExePath, _installDir, iconPath, "Converze — Radial File Converter");
                }

                if (_cbStartMenuShortcut.IsChecked == true)
                {
                    string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Converze");
                    Directory.CreateDirectory(startMenuDir);
                    CreateWindowsShortcut(
                        Path.Combine(startMenuDir, "Converze.lnk"),
                        mainExePath,
                        _installDir,
                        iconPath,
                        "Converze");
                    CreateWindowsShortcut(
                        Path.Combine(startMenuDir, "Uninstall Converze.lnk"),
                        uninstallExePath,
                        _installDir,
                        iconPath,
                        Tr("Dezinstalare Converze", "Uninstall Converze"),
                        "--uninstall");
                }

                if (_cbStartupRun.IsChecked == true)
                {
                    using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    runKey?.SetValue("Converze", $"\"{mainExePath}\"");
                }

                RegisterUninstallerInRegistry(mainExePath, uninstallExePath, iconPath);

                // Step 5: Completed!
                AnimateProgress(1.0, Tr("✓ Converze a fost instalat cu succes!", "✓ Converze was successfully installed!"));
                _isInstalling = false;
                _isFinished = true;
                _actionBtn.Opacity = 1.0;
                _actionBtnText.Text = Tr("Pornește Converze", "Launch Converze");
            }
            catch (Exception ex)
            {
                _isInstalling = false;
                _actionBtn.Opacity = 1.0;
                _statusLabel.Text = Tr($"Eroare la instalare: {ex.Message}", $"Installation error: {ex.Message}");
                MessageBox.Show(ex.Message, Tr("Eroare la instalare", "Installation Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterUninstallerInRegistry(string mainExePath, string uninstallExePath, string iconPath)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Converze");
                if (key != null)
                {
                    key.SetValue("DisplayName", "Converze");
                    key.SetValue("DisplayVersion", "1.0");
                    key.SetValue("Publisher", "Converze");
                    key.SetValue("DisplayIcon", iconPath);
                    key.SetValue("InstallLocation", _installDir);
                    key.SetValue("UninstallString", $"\"{uninstallExePath}\" --uninstall");
                    key.SetValue("QuietUninstallString", $"\"{uninstallExePath}\" --uninstall");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("EstimatedSize", 68500, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static void CreateWindowsShortcut(
            string shortcutPath,
            string targetPath,
            string workingDir,
            string iconPath,
            string description,
            string arguments = "")
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return;

                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDir;
                if (!string.IsNullOrEmpty(arguments))
                {
                    shortcut.Arguments = arguments;
                }
                if (File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath;
                }
                shortcut.Description = description;
                shortcut.Save();
            }
            catch { }
        }

        private void LaunchInstalledAppNonElevated()
        {
            try
            {
                string mainExePath = Path.Combine(_installDir, "Converze.exe");
                if (!File.Exists(mainExePath)) return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{mainExePath}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static FrameworkElement? LoadEmbeddedIcon(double size)
        {
            try
            {
                using Stream? s = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.png");
                if (s == null) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = s;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                var img = new Image
                {
                    Source = bmp,
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Opacity = 0.35,
                        BlurRadius = 12,
                        ShadowDepth = 2
                    }
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                return img;
            }
            catch
            {
                return null;
            }
        }
    }
}

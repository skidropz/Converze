using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;

namespace Converze.Models
{
    // MARK: - Language Selection (Română & English)

    public enum AppLanguage
    {
        Ro,
        En
    }

    public static class AppLanguageExtensions
    {
        public static string DisplayName(this AppLanguage lang) =>
            lang == AppLanguage.Ro ? "Română" : "English";

        public static string FlagEmoji(this AppLanguage lang) =>
            lang == AppLanguage.Ro ? "🇷🇴" : "🇬🇧";

        public static string Subtitle(this AppLanguage lang) =>
            lang == AppLanguage.Ro ? "Interfață completă în limba română" : "Full UI in English";
    }

    // MARK: - UI Appearance Mode (White Mode / Dark Mode / System)

    public enum AppAppearanceMode
    {
        Light,
        Dark,
        System
    }

    public static class AppAppearanceModeExtensions
    {
        public static string Title(this AppAppearanceMode mode, AppLanguage lang) => (mode, lang) switch
        {
            (AppAppearanceMode.Light, AppLanguage.Ro) => "Mod light (alb)",
            (AppAppearanceMode.Light, AppLanguage.En) => "Light Mode (White)",
            (AppAppearanceMode.Dark, AppLanguage.Ro) => "Mod întunecat (dark)",
            (AppAppearanceMode.Dark, AppLanguage.En) => "Dark Mode",
            (AppAppearanceMode.System, AppLanguage.Ro) => "Automat",
            _ => "System Auto"
        };

        public static string Subtitle(this AppAppearanceMode mode, AppLanguage lang) => (mode, lang) switch
        {
            (AppAppearanceMode.Light, AppLanguage.Ro) => "Efect de sticlă și contrast clar",
            (AppAppearanceMode.Light, AppLanguage.En) => "Crisp frosted white glass interface",
            (AppAppearanceMode.Dark, AppLanguage.Ro) => "Sticlă mată cu reflexii de lumină.",
            (AppAppearanceMode.Dark, AppLanguage.En) => "Sleek dark obsidian glass with luminous glow",
            (AppAppearanceMode.System, AppLanguage.Ro) => "Aplică automat tema din Windows",
            _ => "Follows your Windows appearance setting"
        };

        public static string GlyphIcon(this AppAppearanceMode mode) => mode switch
        {
            AppAppearanceMode.Light => "☀️",
            AppAppearanceMode.Dark => "🌙",
            _ => "◐"
        };
    }

    // MARK: - User-Selectable Color Palettes (Non-Orange Curated Themes)

    public enum ConverzeColorTheme
    {
        Sapphire,
        Indigo,
        Emerald,
        Cyan,
        Amethyst,
        Rose
    }

    public static class ConverzeColorThemeExtensions
    {
        public static readonly ConverzeColorTheme[] AllCases = new[]
        {
            ConverzeColorTheme.Sapphire,
            ConverzeColorTheme.Indigo,
            ConverzeColorTheme.Emerald,
            ConverzeColorTheme.Cyan,
            ConverzeColorTheme.Amethyst,
            ConverzeColorTheme.Rose
        };

        public static string Title(this ConverzeColorTheme theme, AppLanguage lang) => (theme, lang) switch
        {
            (ConverzeColorTheme.Sapphire, AppLanguage.Ro) => "Safir",
            (ConverzeColorTheme.Sapphire, AppLanguage.En) => "Sapphire",
            (ConverzeColorTheme.Indigo, AppLanguage.Ro) => "Indigo",
            (ConverzeColorTheme.Indigo, AppLanguage.En) => "Indigo",
            (ConverzeColorTheme.Emerald, AppLanguage.Ro) => "Smarald",
            (ConverzeColorTheme.Emerald, AppLanguage.En) => "Emerald",
            (ConverzeColorTheme.Cyan, AppLanguage.Ro) => "Azuriu",
            (ConverzeColorTheme.Cyan, AppLanguage.En) => "Cyan",
            (ConverzeColorTheme.Amethyst, AppLanguage.Ro) => "Ametist",
            (ConverzeColorTheme.Amethyst, AppLanguage.En) => "Amethyst",
            (ConverzeColorTheme.Rose, AppLanguage.Ro) => "Rubin",
            _ => "Rose"
        };

        public static Color PrimaryColor(this ConverzeColorTheme theme) => theme switch
        {
            ConverzeColorTheme.Sapphire => Color.FromRgb(37, 99, 235),   // #2563EB
            ConverzeColorTheme.Indigo   => Color.FromRgb(99, 102, 241),  // #6366F1
            ConverzeColorTheme.Emerald  => Color.FromRgb(16, 185, 129),  // #10B981
            ConverzeColorTheme.Cyan     => Color.FromRgb(6, 182, 212),   // #06B6D4
            ConverzeColorTheme.Amethyst => Color.FromRgb(147, 51, 234),  // #9333EA
            _                           => Color.FromRgb(244, 63, 94)    // #F43F5E
        };

        public static Color LightStop(this ConverzeColorTheme theme) => theme switch
        {
            ConverzeColorTheme.Sapphire => Color.FromRgb(96, 165, 250),  // #60A5FA
            ConverzeColorTheme.Indigo   => Color.FromRgb(129, 140, 248), // #818CF8
            ConverzeColorTheme.Emerald  => Color.FromRgb(52, 211, 153),  // #34D399
            ConverzeColorTheme.Cyan     => Color.FromRgb(34, 211, 238),  // #22D3EE
            ConverzeColorTheme.Amethyst => Color.FromRgb(192, 132, 252), // #C084FC
            _                           => Color.FromRgb(251, 113, 133)  // #FB7185
        };

        public static Color DeepStop(this ConverzeColorTheme theme) => theme switch
        {
            ConverzeColorTheme.Sapphire => Color.FromRgb(29, 78, 216),   // #1D4ED8
            ConverzeColorTheme.Indigo   => Color.FromRgb(67, 56, 202),   // #4338CA
            ConverzeColorTheme.Emerald  => Color.FromRgb(5, 122, 85),    // #057A55
            ConverzeColorTheme.Cyan     => Color.FromRgb(14, 116, 144),  // #0E7490
            ConverzeColorTheme.Amethyst => Color.FromRgb(107, 33, 168),  // #6B21A8
            _                           => Color.FromRgb(190, 18, 60)    // #BE123C
        };

        public static Color SparkSecondary(this ConverzeColorTheme theme) => theme switch
        {
            ConverzeColorTheme.Sapphire => Color.FromRgb(148, 209, 255),
            ConverzeColorTheme.Indigo   => Color.FromRgb(199, 209, 255),
            ConverzeColorTheme.Emerald  => Color.FromRgb(166, 245, 209),
            ConverzeColorTheme.Cyan     => Color.FromRgb(166, 242, 255),
            ConverzeColorTheme.Amethyst => Color.FromRgb(235, 199, 255),
            _                           => Color.FromRgb(255, 191, 209)
        };
    }

    // MARK: - Global Converze Preferences & Localization Store

    public sealed class ConverzePreferences : INotifyPropertyChanged
    {
        public static readonly ConverzePreferences Shared = new ConverzePreferences();

        private class StoredState
        {
            public string Language { get; set; } = "ro";
            public string AppearanceMode { get; set; } = "light";
            public string ColorTheme { get; set; } = "sapphire";
            public bool HasCompletedOnboarding { get; set; } = false;
            public string OnboardedInstallSignature { get; set; } = "";
            public bool HapticsEnabled { get; set; } = true;
            public bool AutoRevealInFinder { get; set; } = false;
            public double ImageCompressionQuality { get; set; } = 0.90;
        }

        private readonly string _prefsFilePath;
        private AppLanguage _language = AppLanguage.Ro;
        private AppAppearanceMode _appearanceMode = AppAppearanceMode.Light;
        private ConverzeColorTheme _colorTheme = ConverzeColorTheme.Sapphire;
        private bool _hasCompletedOnboarding = false;
        private bool _hapticsEnabled = true;
        private bool _autoRevealInFinder = false;
        private double _imageCompressionQuality = 0.90;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnPreferencesChanged;

        private ConverzePreferences()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Converze");
            Directory.CreateDirectory(dir);
            _prefsFilePath = Path.Combine(dir, "preferences.json");
            Load();
        }

        public AppLanguage Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    Save();
                    NotifyVisualThemeChanged();
                }
            }
        }

        public AppAppearanceMode AppearanceMode
        {
            get => _appearanceMode;
            set
            {
                if (_appearanceMode != value)
                {
                    _appearanceMode = value;
                    Save();
                    NotifyVisualThemeChanged();
                }
            }
        }

        public ConverzeColorTheme ColorTheme
        {
            get => _colorTheme;
            set
            {
                if (_colorTheme != value)
                {
                    _colorTheme = value;
                    Save();
                    NotifyVisualThemeChanged();
                }
            }
        }

        public bool HasCompletedOnboarding
        {
            get => _hasCompletedOnboarding;
            set
            {
                if (_hasCompletedOnboarding != value)
                {
                    _hasCompletedOnboarding = value;
                    Save();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCompletedOnboarding)));
                }
            }
        }

        public bool HapticsEnabled
        {
            get => _hapticsEnabled;
            set
            {
                if (_hapticsEnabled != value)
                {
                    _hapticsEnabled = value;
                    Save();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HapticsEnabled)));
                }
            }
        }

        public bool AutoRevealInFinder
        {
            get => _autoRevealInFinder;
            set
            {
                if (_autoRevealInFinder != value)
                {
                    _autoRevealInFinder = value;
                    Save();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoRevealInFinder)));
                }
            }
        }

        public double ImageCompressionQuality
        {
            get => _imageCompressionQuality;
            set
            {
                double clamped = Math.Clamp(value, 0.50, 1.00);
                if (Math.Abs(_imageCompressionQuality - clamped) > 0.0005)
                {
                    _imageCompressionQuality = clamped;
                    Save();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageCompressionQuality)));
                }
            }
        }

        public bool IsDarkMode
        {
            get
            {
                return _appearanceMode switch
                {
                    AppAppearanceMode.Light => false,
                    AppAppearanceMode.Dark => true,
                    _ => IsWindowsSystemDarkMode()
                };
            }
        }

        public string Tr(string ro, string en) => _language == AppLanguage.Ro ? ro : en;

        public static bool IsWindowsSystemDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val)
                {
                    return val == 0;
                }
            }
            catch
            {
                // Ignore registry read errors
            }
            return false;
        }

        public bool IsLaunchAtStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("Converze") is string val && !string.IsNullOrWhiteSpace(val);
            }
            catch
            {
                return false;
            }
        }

        public void SetLaunchAtStartup(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                if (enabled)
                {
                    string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Converze.exe");
                    key.SetValue("Converze", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("Converze", false);
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLaunchAtStartupEnabled)));
            }
            catch
            {
                // Ignore registry write errors
            }
        }

        public static string CurrentInstallSignature()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string sigPath = Path.Combine(baseDir, "install_signature.txt");
                if (File.Exists(sigPath))
                {
                    string sig = File.ReadAllText(sigPath).Trim();
                    if (!string.IsNullOrEmpty(sig)) return sig;
                }

                string exePath = Environment.ProcessPath ?? Path.Combine(baseDir, "Converze.exe");
                if (File.Exists(exePath))
                {
                    long ticks = File.GetLastWriteTimeUtc(exePath).Ticks;
                    return $"{exePath}#{ticks}";
                }
                return $"{baseDir}#2.2.0";
            }
            catch
            {
                return "converze-win-2.2.0";
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_prefsFilePath)) return;
                string json = File.ReadAllText(_prefsFilePath);
                var state = JsonSerializer.Deserialize<StoredState>(json);
                if (state == null) return;

                _language = state.Language == "en" ? AppLanguage.En : AppLanguage.Ro;
                _appearanceMode = state.AppearanceMode switch
                {
                    "dark" => AppAppearanceMode.Dark,
                    "system" => AppAppearanceMode.System,
                    _ => AppAppearanceMode.Light
                };
                _colorTheme = state.ColorTheme switch
                {
                    "indigo" => ConverzeColorTheme.Indigo,
                    "emerald" => ConverzeColorTheme.Emerald,
                    "cyan" => ConverzeColorTheme.Cyan,
                    "amethyst" => ConverzeColorTheme.Amethyst,
                    "rose" => ConverzeColorTheme.Rose,
                    _ => ConverzeColorTheme.Sapphire
                };

                string currentSig = CurrentInstallSignature();
                _hasCompletedOnboarding = state.HasCompletedOnboarding &&
                                          string.Equals(state.OnboardedInstallSignature, currentSig, StringComparison.Ordinal);
                _hapticsEnabled = state.HapticsEnabled;
                _autoRevealInFinder = state.AutoRevealInFinder;
                _imageCompressionQuality = state.ImageCompressionQuality > 0.1 ? state.ImageCompressionQuality : 0.90;
            }
            catch
            {
                // Keep defaults on first run
            }
        }

        private void Save()
        {
            try
            {
                var state = new StoredState
                {
                    Language = _language == AppLanguage.En ? "en" : "ro",
                    AppearanceMode = _appearanceMode switch
                    {
                        AppAppearanceMode.Dark => "dark",
                        AppAppearanceMode.System => "system",
                        _ => "light"
                    },
                    ColorTheme = _colorTheme switch
                    {
                        ConverzeColorTheme.Indigo => "indigo",
                        ConverzeColorTheme.Emerald => "emerald",
                        ConverzeColorTheme.Cyan => "cyan",
                        ConverzeColorTheme.Amethyst => "amethyst",
                        ConverzeColorTheme.Rose => "rose",
                        _ => "sapphire"
                    },
                    HasCompletedOnboarding = _hasCompletedOnboarding,
                    OnboardedInstallSignature = _hasCompletedOnboarding ? CurrentInstallSignature() : "",
                    HapticsEnabled = _hapticsEnabled,
                    AutoRevealInFinder = _autoRevealInFinder,
                    ImageCompressionQuality = _imageCompressionQuality
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_prefsFilePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        private void NotifyVisualThemeChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            OnPreferencesChanged?.Invoke();
        }
    }

    // MARK: - Radial Wheel Target (Pure Format Conversion)

    public sealed class WheelTarget : IEquatable<WheelTarget>
    {
        public string TargetFormat { get; }
        public string Id => $"fmt_{TargetFormat}";
        public string WheelLabel => FormatMatrix.DisplayLabel(TargetFormat);

        public WheelTarget(string format)
        {
            TargetFormat = format.ToLowerInvariant();
        }

        public string ActionDescription
        {
            get
            {
                string label = WheelLabel;
                return ConverzePreferences.Shared.Tr(
                    $"convertești în {label}",
                    $"convert to {label}"
                );
            }
        }

        public bool Equals(WheelTarget? other) =>
            other != null && string.Equals(TargetFormat, other.TargetFormat, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => Equals(obj as WheelTarget);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(TargetFormat);
    }

    // MARK: - Converze Application Icon Loader & View Helper

    public static class ConverzeAppIconLoader
    {
        private static BitmapImage? _cachedBitmap;

        public static BitmapImage? LoadAppIconBitmap()
        {
            if (_cachedBitmap != null) return _cachedBitmap;

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string pngPath = Path.Combine(baseDir, "Resources", "AppIcon.png");
                if (File.Exists(pngPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(pngPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _cachedBitmap = bmp;
                    return bmp;
                }

                var packUri = new Uri("pack://application:,,,/Resources/AppIcon.png", UriKind.Absolute);
                var resourceBmp = new BitmapImage(packUri);
                resourceBmp.Freeze();
                _cachedBitmap = resourceBmp;
                return resourceBmp;
            }
            catch
            {
                return null;
            }
        }

        public static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = Path.Combine(baseDir, "Resources", "AppIcon.ico");
                if (File.Exists(icoPath))
                {
                    return new System.Drawing.Icon(icoPath);
                }
            }
            catch
            {
                // Fallback below
            }
            return System.Drawing.SystemIcons.Application;
        }

        public static FrameworkElement CreateAppIconElement(double size = 38)
        {
            var bmp = LoadAppIconBitmap();
            if (bmp != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = bmp,
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Opacity = 0.24,
                        BlurRadius = size * 0.25,
                        ShadowDepth = 2,
                        Direction = 270
                    }
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                return img;
            }

            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * 0.24),
                Background = new SolidColorBrush(ConverzePreferences.Shared.ColorTheme.PrimaryColor())
            };
        }
    }
}

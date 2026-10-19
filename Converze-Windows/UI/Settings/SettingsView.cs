using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Converze.Models;
using Converze.UI.MenuBar;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Orientation = System.Windows.Controls.Orientation;

namespace Converze.UI.Settings
{
    public enum SettingsSection
    {
        Appearance,
        LanguageAndSystem,
        Formats
    }

    public sealed class SettingsWindow : Window
    {
        private SettingsSection _selectedSection = SettingsSection.Appearance;
        private readonly Action _onOpenOnboarding;
        private readonly Action<string[]>? _onFilesDropped;
        private readonly Action? _onPickFileClicked;
        private readonly Border _rootContainer;
        private ScrollViewer? _rightScroll;

        public SettingsWindow(
            Action onOpenOnboarding,
            Action<string[]>? onFilesDropped = null,
            Action? onPickFileClicked = null)
        {
            _onOpenOnboarding = onOpenOnboarding;
            _onFilesDropped = onFilesDropped;
            _onPickFileClicked = onPickFileClicked;

            Width = 780;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Title = "Converze — Setări / Settings";
            AllowDrop = true;

            _rootContainer = new Border();
            Content = _rootContainer;

            DragEnter += OnWindowDragOver;
            DragOver += OnWindowDragOver;
            Drop += OnWindowDrop;

            ConverzePreferences.Shared.OnPreferencesChanged += RebuildUI;
            Closed += (_, _) => ConverzePreferences.Shared.OnPreferencesChanged -= RebuildUI;

            RebuildUI();
        }

        private void OnWindowDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnWindowDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                _onFilesDropped?.Invoke(files);
            }
            e.Handled = true;
        }

        private void RebuildUI()
        {
            var prefs = ConverzePreferences.Shared;
            bool isDark = prefs.IsDarkMode;
            var theme = prefs.ColorTheme;

            Title = prefs.Tr("Panou principal & setări", "Dashboard & Settings");

            Color bgTop = isDark ? Color.FromRgb(18, 20, 28) : Color.FromRgb(246, 248, 252);
            Color bgBot = isDark ? Color.FromRgb(13, 14, 20) : Color.FromRgb(236, 241, 249);
            _rootContainer.Background = new LinearGradientBrush(bgTop, bgBot, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sidebar = BuildSidebar(prefs, isDark, theme);
            Grid.SetColumn(sidebar, 0);
            grid.Children.Add(sidebar);

            var divider = new Border
            {
                Background = new SolidColorBrush(isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42))
            };
            Grid.SetColumn(divider, 1);
            grid.Children.Add(divider);

            var sectionHost = new Border
            {
                Child = BuildSectionContent(prefs, isDark, theme)
            };

            _rightScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 18, 24, 18),
                Content = sectionHost
            };
            Grid.SetColumn(_rightScroll, 2);
            grid.Children.Add(_rightScroll);

            _rootContainer.Child = grid;
            AnimateSectionTransition(sectionHost);
        }

        private static void AnimateSectionTransition(FrameworkElement elem)
        {
            var trans = new TranslateTransform(0, 8);
            elem.RenderTransform = trans;
            elem.Opacity = 0.0;

            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(170))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slide = new DoubleAnimation(8.0, 0.0, TimeSpan.FromMilliseconds(190))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            elem.BeginAnimation(OpacityProperty, fade);
            trans.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        private UIElement BuildSidebar(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var grid = new Grid
            {
                Background = new SolidColorBrush(isDark ? Color.FromArgb(14, 255, 255, 255) : Color.FromArgb(145, 255, 255, 255))
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Brand header
            var brandRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(18, 20, 16, 16)
            };
            brandRow.Children.Add(ConverzeAppIconLoader.CreateAppIconElement(34));

            var brandText = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            brandText.Children.Add(new TextBlock
            {
                Text = "Converze",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });
            brandText.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Convertor rapid de fișiere", "Pure Radial File Converter"),
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(150, 255, 255, 255) : Color.FromRgb(100, 116, 139))
            });
            brandRow.Children.Add(brandText);
            Grid.SetRow(brandRow, 0);
            grid.Children.Add(brandRow);

            // Navigation items
            var navStack = new StackPanel { Margin = new Thickness(12, 0, 12, 10) };
            var sections = new (SettingsSection Sec, string Icon, string Label)[]
            {
                (SettingsSection.Appearance, "", prefs.Tr("Aspect și culori", "Appearance & Colors")),
                (SettingsSection.LanguageAndSystem, "", prefs.Tr("Limbă și sistem", "Language & System")),
                (SettingsSection.Formats, "", prefs.Tr("Formate (275)", "Formats (275)"))
            };

            foreach (var item in sections)
            {
                bool active = (_selectedSection == item.Sec);
                var navBtn = new Border
                {
                    CornerRadius = new CornerRadius(11),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(active
                        ? WithAlpha(theme.PrimaryColor(), isDark ? 0.28 : 0.14)
                        : Colors.Transparent),
                    BorderBrush = new SolidColorBrush(active
                        ? WithAlpha(theme.PrimaryColor(), 0.55)
                        : Colors.Transparent),
                    BorderThickness = new Thickness(1)
                };

                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock
                {
                    Text = item.Icon,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                row.Children.Add(new TextBlock
                {
                    Text = item.Label,
                    FontSize = 12.5,
                    FontWeight = active ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = new SolidColorBrush(active
                        ? (isDark ? Colors.White : theme.PrimaryColor())
                        : (isDark ? Color.FromArgb(190, 255, 255, 255) : Color.FromRgb(51, 65, 85)))
                });
                navBtn.Child = row;

                var captured = item.Sec;
                navBtn.MouseLeftButtonUp += (_, _) =>
                {
                    if (_selectedSection != captured)
                    {
                        _selectedSection = captured;
                        RebuildUI();
                    }
                };
                navStack.Children.Add(navBtn);
            }

            Grid.SetRow(navStack, 1);
            grid.Children.Add(navStack);

            // Live Mini Wheel Preview at bottom of sidebar (also accepts file drop & click!)
            var previewStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            previewStack.Children.Add(new TextBlock
            {
                Text = prefs.Tr("TRAGE FIȘIERUL SAU CLICK", "DROP FILE ON WHEEL OR CLICK"),
                FontSize = 9.0,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(145, 255, 255, 255) : Color.FromRgb(100, 116, 139)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });
            previewStack.Children.Add(new MiniWheelPreviewControl(
                148,
                onFilesDropped: files => _onFilesDropped?.Invoke(files),
                onClicked: () => _onPickFileClicked?.Invoke()));

            Grid.SetRow(previewStack, 2);
            grid.Children.Add(previewStack);

            return grid;
        }

        private UIElement BuildQuickDropZoneBanner(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var dropZone = new Border
            {
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 18),
                Cursor = Cursors.Hand,
                AllowDrop = true,
                Background = new SolidColorBrush(WithAlpha(theme.PrimaryColor(), isDark ? 0.18 : 0.09)),
                BorderBrush = new SolidColorBrush(WithAlpha(theme.PrimaryColor(), isDark ? 0.65 : 0.45)),
                BorderThickness = new Thickness(1.5)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBadge = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(theme.PrimaryColor()),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "📂",
                    FontSize = 17,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(iconBadge, 0);
            grid.Children.Add(iconBadge);

            var textCol = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0)
            };
            textCol.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Trage orice fișier aici pentru conversie rapidă", "Drag any file here for instant conversion"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });
            textCol.Children.Add(new TextBlock
            {
                Text = prefs.Tr(
                    "Sau ține apăsat Alt + Click Stânga pe orice fișier în File Explorer / Desktop.",
                    "Or hold Alt + Left-Click on any file in File Explorer or Desktop."),
                FontSize = 11,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(185, 255, 255, 255) : Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(textCol, 1);
            grid.Children.Add(textCol);

            var pickBtn = new Border
            {
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(13, 7, 13, 7),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(theme.PrimaryColor()),
                Child = new TextBlock
                {
                    Text = prefs.Tr("Alege fișier...", "Choose file..."),
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White
                }
            };
            Grid.SetColumn(pickBtn, 2);
            grid.Children.Add(pickBtn);

            dropZone.Child = grid;
            dropZone.MouseLeftButtonUp += (_, _) => _onPickFileClicked?.Invoke();
            return dropZone;
        }

        private UIElement BuildSectionContent(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            return _selectedSection switch
            {
                SettingsSection.Appearance => BuildAppearanceSection(prefs, isDark, theme),
                SettingsSection.LanguageAndSystem => BuildLanguageAndSystemSection(prefs, isDark, theme),
                _ => BuildFormatsSection(prefs, isDark, theme)
            };
        }

        private UIElement BuildAppearanceSection(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var stack = new StackPanel();

            // Prominent Top Drag & Drop / Choose File Banner
            stack.Children.Add(BuildQuickDropZoneBanner(prefs, isDark, theme));

            // Section 1: Display Mode
            stack.Children.Add(CreateSectionHeader(
                prefs.Tr("Mod de afișare", "Appearance Mode"),
                prefs.Tr("Alege între interfața luminoasă, întunecată sau automată.", "Choose between light mode, dark mode or automatic Windows sync."),
                isDark));

            var modeGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 18) };
            foreach (var mode in new[] { AppAppearanceMode.Light, AppAppearanceMode.Dark, AppAppearanceMode.System })
            {
                bool selected = (prefs.AppearanceMode == mode);
                var card = new Border
                {
                    Margin = new Thickness(4),
                    Padding = new Thickness(10, 11, 10, 11),
                    CornerRadius = new CornerRadius(13),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(selected
                        ? WithAlpha(theme.PrimaryColor(), isDark ? 0.25 : 0.12)
                        : (isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White)),
                    BorderBrush = new SolidColorBrush(selected
                        ? theme.PrimaryColor()
                        : (isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42))),
                    BorderThickness = new Thickness(selected ? 1.8 : 1.0)
                };

                var col = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                col.Children.Add(new TextBlock
                {
                    Text = mode.GlyphIcon(),
                    FontSize = 17,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                col.Children.Add(new TextBlock
                {
                    Text = mode.Title(prefs.Language),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });
                card.Child = col;
                var captured = mode;
                card.MouseLeftButtonUp += (_, _) => prefs.AppearanceMode = captured;
                modeGrid.Children.Add(card);
            }
            stack.Children.Add(modeGrid);

            // Section 2: Color Palette
            stack.Children.Add(CreateSectionHeader(
                prefs.Tr("Paleta de culori a roții", "Wheel Color Palette"),
                prefs.Tr("Culoarea activă folosită pentru felia selectată, strălucirea roții și particule.", "Active accent color used for hovered format slices, wheel glow, and spark particles."),
                isDark));

            var themeGrid = new UniformGrid { Columns = 3, Rows = 2, Margin = new Thickness(0, 0, 0, 18) };
            foreach (var palette in ConverzeColorThemeExtensions.AllCases)
            {
                bool selected = (prefs.ColorTheme == palette);
                var card = new Border
                {
                    Margin = new Thickness(4),
                    Padding = new Thickness(10, 10, 10, 10),
                    CornerRadius = new CornerRadius(13),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(selected
                        ? WithAlpha(palette.PrimaryColor(), isDark ? 0.25 : 0.12)
                        : (isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White)),
                    BorderBrush = new SolidColorBrush(selected
                        ? palette.PrimaryColor()
                        : (isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42))),
                    BorderThickness = new Thickness(selected ? 1.8 : 1.0)
                };

                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                row.Children.Add(new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new LinearGradientBrush(palette.LightStop(), palette.DeepStop(), new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)),
                    Margin = new Thickness(0, 0, 8, 0)
                });
                row.Children.Add(new TextBlock
                {
                    Text = palette.Title(prefs.Language),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });
                card.Child = row;
                var captured = palette;
                card.MouseLeftButtonUp += (_, _) => prefs.ColorTheme = captured;
                themeGrid.Children.Add(card);
            }
            stack.Children.Add(themeGrid);

            // Section 3: Smooth Image Compression Quality Slider (Does NOT rebuild UI on drag!)
            var qualityCard = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 12, 16, 12),
                Background = new SolidColorBrush(isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White),
                BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(24, 15, 23, 42)),
                BorderThickness = new Thickness(1)
            };

            var qStack = new StackPanel();
            var qTop = new Grid();
            qTop.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Calitate compresie imagini", "Image Compression Quality"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });

            int initialPct = (int)Math.Round(prefs.ImageCompressionQuality * 100.0);
            var pctLabel = new TextBlock
            {
                Text = $"{initialPct}%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.PrimaryColor()),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            qTop.Children.Add(pctLabel);
            qStack.Children.Add(qTop);

            var slider = new Slider
            {
                Minimum = 50,
                Maximum = 100,
                Value = initialPct,
                SmallChange = 1,
                LargeChange = 5,
                IsSnapToTickEnabled = false,
                IsMoveToPointEnabled = true,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 10, 0, 2)
            };
            slider.ValueChanged += (_, e) =>
            {
                int rounded = (int)Math.Round(e.NewValue);
                pctLabel.Text = $"{rounded}%";
                prefs.ImageCompressionQuality = rounded / 100.0;
            };
            qStack.Children.Add(slider);
            qualityCard.Child = qStack;
            stack.Children.Add(qualityCard);

            return stack;
        }

        private UIElement BuildLanguageAndSystemSection(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var stack = new StackPanel();

            // 1. Language
            stack.Children.Add(CreateSectionHeader(
                prefs.Tr("Limba aplicației", "Application Language"),
                prefs.Tr("Schimbă instantaneu toate meniurile, explicațiile și notificările între Română și Engleză.", "Instantly switch all menus, explanations, and notifications between Romanian and English."),
                isDark));

            var langGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 20) };
            foreach (var lang in new[] { AppLanguage.Ro, AppLanguage.En })
            {
                bool selected = (prefs.Language == lang);
                var card = new Border
                {
                    Margin = new Thickness(4),
                    Padding = new Thickness(14, 12, 14, 12),
                    CornerRadius = new CornerRadius(13),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(selected
                        ? WithAlpha(theme.PrimaryColor(), isDark ? 0.25 : 0.12)
                        : (isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White)),
                    BorderBrush = new SolidColorBrush(selected
                        ? theme.PrimaryColor()
                        : (isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42))),
                    BorderThickness = new Thickness(selected ? 1.8 : 1.0)
                };

                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock
                {
                    Text = lang.FlagEmoji(),
                    FontSize = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                var col = new StackPanel();
                col.Children.Add(new TextBlock
                {
                    Text = lang.DisplayName(),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });
                col.Children.Add(new TextBlock
                {
                    Text = lang.Subtitle(),
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(isDark ? Color.FromArgb(150, 255, 255, 255) : Color.FromRgb(100, 116, 139))
                });
                row.Children.Add(col);
                card.Child = row;

                var captured = lang;
                card.MouseLeftButtonUp += (_, _) => prefs.Language = captured;
                langGrid.Children.Add(card);
            }
            stack.Children.Add(langGrid);

            // 2. System Behavior Toggles
            stack.Children.Add(CreateSectionHeader(
                prefs.Tr("Comportament în sistem", "System Behavior"),
                prefs.Tr("Tasta de activare: Ține apăsată tasta Alt și dă click stânga sau trage orice fișier selectat în File Explorer sau pe Desktop.", "Activation shortcut: Hold Alt and left-click or drag any selected file in File Explorer or on the Desktop."),
                isDark));

            var togglesCard = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 18),
                Background = new SolidColorBrush(isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White),
                BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(24, 15, 23, 42)),
                BorderThickness = new Thickness(1)
            };

            var togglesStack = new StackPanel();
            togglesStack.Children.Add(CreateToggleRow(
                prefs.Tr("Pornește automat la autentificare (Windows Startup)", "Launch automatically at Windows login"),
                prefs.IsLaunchAtStartupEnabled(),
                isDark,
                val => prefs.SetLaunchAtStartup(val)));

            togglesStack.Children.Add(CreateToggleRow(
                prefs.Tr("Feedback sonor la finalizarea conversiei", "Sound feedback when conversion finishes"),
                prefs.HapticsEnabled,
                isDark,
                val => prefs.HapticsEnabled = val));

            togglesStack.Children.Add(CreateToggleRow(
                prefs.Tr("Arată automat fișierul în File Explorer după conversie", "Auto-reveal converted file in File Explorer"),
                prefs.AutoRevealInFinder,
                isDark,
                val => prefs.AutoRevealInFinder = val));

            togglesCard.Child = togglesStack;
            stack.Children.Add(togglesCard);

            // 3. Re-open Welcome Guide Card
            var guideCard = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 12, 16, 12),
                Background = new SolidColorBrush(isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White),
                BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(24, 15, 23, 42)),
                BorderThickness = new Thickness(1)
            };

            var guideGrid = new Grid();
            guideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            guideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var guideText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            guideText.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Ghid de bun venit", "Welcome Guide"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });
            guideText.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Redeschide ecranul de configurare inițială", "Re-open the 3-step initial setup wizard"),
                FontSize = 11,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(150, 255, 255, 255) : Color.FromRgb(100, 116, 139))
            });
            Grid.SetColumn(guideText, 0);
            guideGrid.Children.Add(guideText);

            var openGuideBtn = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(theme.PrimaryColor()),
                Child = new TextBlock
                {
                    Text = prefs.Tr("Deschide", "Open"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White
                }
            };
            openGuideBtn.MouseLeftButtonUp += (_, _) => _onOpenOnboarding();
            Grid.SetColumn(openGuideBtn, 1);
            guideGrid.Children.Add(openGuideBtn);

            guideCard.Child = guideGrid;
            stack.Children.Add(guideCard);

            return stack;
        }

        private UIElement BuildFormatsSection(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeader(
                prefs.Tr("275 Căi de Conversie Locale", "275 Local Format Conversion Paths"),
                prefs.Tr("Toate conversiile se execută 100% local pe PC-ul tău, fără cloud și fără limite de dimensiune.", "All conversions run 100% locally on your PC with zero cloud uploads and no file size limits."),
                isDark));

            var categories = new (string Title, string[] Inputs, string[] Outputs)[]
            {
                (prefs.Tr("Imagini", "Images"), FormatMatrix.ImageInputs, FormatMatrix.ImageOutputs),
                (prefs.Tr("Video", "Video"), FormatMatrix.VideoInputs, FormatMatrix.VideoOutputs),
                (prefs.Tr("Audio", "Audio"), FormatMatrix.AudioInputs, FormatMatrix.AudioOutputs),
                (prefs.Tr("Documente și PDF", "Documents & PDF"), FormatMatrix.DocumentInputs, FormatMatrix.DocumentOutputs),
                (prefs.Tr("Foi de calcul și date", "Spreadsheets & Data"), FormatMatrix.SpreadsheetInputs, FormatMatrix.SpreadsheetOutputs),
                (prefs.Tr("Prezentări", "Presentations"), FormatMatrix.PresentationInputs, FormatMatrix.PresentationOutputs),
                (prefs.Tr("Subtitrări", "Subtitles"), FormatMatrix.SubtitleInputs, FormatMatrix.SubtitleOutputs),
                (prefs.Tr("Arhive", "Archives"), FormatMatrix.ArchiveInputs, FormatMatrix.ArchiveOutputs)
            };

            foreach (var cat in categories)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(13),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 10),
                    Background = new SolidColorBrush(isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White),
                    BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(24, 15, 23, 42)),
                    BorderThickness = new Thickness(1)
                };

                var cStack = new StackPanel();
                cStack.Children.Add(new TextBlock
                {
                    Text = cat.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(theme.PrimaryColor()),
                    Margin = new Thickness(0, 0, 0, 4)
                });
                cStack.Children.Add(new TextBlock
                {
                    Text = $"IN: {string.Join(", ", cat.Inputs).ToUpperInvariant()}",
                    FontSize = 10.5,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(isDark ? Color.FromArgb(170, 255, 255, 255) : Color.FromRgb(100, 116, 139))
                });
                cStack.Children.Add(new TextBlock
                {
                    Text = $"OUT → {string.Join(" · ", cat.Outputs).ToUpperInvariant()}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0),
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });

                card.Child = cStack;
                stack.Children.Add(card);
            }

            return stack;
        }

        private static UIElement CreateSectionHeader(string title, string subtitle, bool isDark)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(160, 255, 255, 255) : Color.FromRgb(100, 116, 139))
            });
            return stack;
        }

        private static UIElement CreateToggleRow(string label, bool isChecked, bool isDark, Action<bool> onToggled)
        {
            var cb = new CheckBox
            {
                IsChecked = isChecked,
                Margin = new Thickness(0, 5, 0, 5),
                Cursor = Cursors.Hand,
                Content = new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(30, 41, 59))
                }
            };
            cb.Checked += (_, _) => onToggled(true);
            cb.Unchecked += (_, _) => onToggled(false);
            return cb;
        }

        private static Color WithAlpha(Color c, double alpha)
        {
            byte a = (byte)Math.Clamp(alpha * 255.0, 0.0, 255.0);
            return Color.FromArgb(a, c.R, c.G, c.B);
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Converze.Models;
using Converze.UI.RadialWheel;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace Converze.UI.MenuBar
{
    // MARK: - 120Hz Interactive Mini Radial Wheel Preview Control (Also Accepts File Drop & Click)

    public sealed class MiniWheelPreviewControl : FrameworkElement
    {
        private static readonly string[] SampleFormats = { "WEBP", "PNG", "JPG", "AVIF", "HEIC", "PDF" };

        private readonly double _size;
        private readonly Action<string[]>? _onFilesDropped;
        private readonly Action? _onClicked;

        private int _activeIndex = 0;
        private readonly DispatcherTimer _cycleTimer;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _lastFrameTime;
        private readonly double[] _sliceHoverPos = new double[6];
        private readonly double[] _sliceHoverVel = new double[6];

        public MiniWheelPreviewControl(
            double size = 188.0,
            Action<string[]>? onFilesDropped = null,
            Action? onClicked = null)
        {
            _size = size;
            _onFilesDropped = onFilesDropped;
            _onClicked = onClicked;

            Width = size;
            Height = size;
            Cursor = Cursors.Hand;
            AllowDrop = true;

            _sliceHoverPos[0] = 1.0;
            _lastFrameTime = _clock.Elapsed.TotalSeconds;

            _cycleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.6)
            };
            _cycleTimer.Tick += (_, _) =>
            {
                if (!IsMouseOver)
                {
                    _activeIndex = (_activeIndex + 1) % SampleFormats.Length;
                }
            };

            Loaded += (_, _) =>
            {
                _lastFrameTime = _clock.Elapsed.TotalSeconds;
                _cycleTimer.Start();
                CompositionTarget.Rendering -= OnFrameRendering;
                CompositionTarget.Rendering += OnFrameRendering;
            };

            Unloaded += (_, _) =>
            {
                _cycleTimer.Stop();
                CompositionTarget.Rendering -= OnFrameRendering;
            };

            DragEnter += OnDragEnterOrOver;
            DragOver += OnDragEnterOrOver;
            Drop += OnDropFiles;

            ConverzePreferences.Shared.OnPreferencesChanged += InvalidateVisual;
        }

        private void OnFrameRendering(object? sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Clamp(now - _lastFrameTime, 0.001, 0.033);
            _lastFrameTime = now;

            const double stiffness = 300.0;
            const double damping = 22.0;

            for (int i = 0; i < SampleFormats.Length; i++)
            {
                double target = (i == _activeIndex) ? 1.0 : 0.0;
                double force = stiffness * (target - _sliceHoverPos[i]) - damping * _sliceHoverVel[i];
                _sliceHoverVel[i] += force * dt;
                _sliceHoverPos[i] = Math.Clamp(_sliceHoverPos[i] + _sliceHoverVel[i] * dt, 0.0, 1.12);
            }

            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);
            double dx = pt.X - (_size * 0.5);
            double dy = pt.Y - (_size * 0.5);
            int? hit = ConverzeWheelMetrics.HitTestSlice(dx, dy, SampleFormats.Length);
            if (hit.HasValue && hit.Value != _activeIndex)
            {
                _activeIndex = hit.Value;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _onClicked?.Invoke();
        }

        private void OnDragEnterOrOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                Point pt = e.GetPosition(this);
                int? hit = ConverzeWheelMetrics.HitTestSlice(pt.X - _size * 0.5, pt.Y - _size * 0.5, SampleFormats.Length);
                if (hit.HasValue) _activeIndex = hit.Value;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDropFiles(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                _onFilesDropped?.Invoke(files);
            }
            e.Handled = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var prefs = ConverzePreferences.Shared;
            bool isDark = prefs.IsDarkMode;
            var theme = prefs.ColorTheme;
            double now = _clock.Elapsed.TotalSeconds;

            double cx = _size * 0.5;
            double cy = _size * 0.5;
            Point center = new Point(cx, cy);

            double innerR = _size * 0.21;
            double outerR = _size * 0.455;
            double holeR = _size * 0.175;
            double ringSize = _size - 16.0;

            dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, _size, _size));

            // 1. Ambient Glow
            var glow = new RadialGradientBrush(
                Color.FromArgb((byte)(isDark ? 76 : 48), theme.PrimaryColor().R, theme.PrimaryColor().G, theme.PrimaryColor().B),
                Colors.Transparent);
            dc.DrawEllipse(glow, null, center, _size * 0.48, _size * 0.48);

            // 2. Donut Backdrop Ring with Rotating Iridescent Rim
            dc.PushTransform(new TranslateTransform(8.0, 8.0));
            Geometry donut = DonutRingGeometry.Build(ringSize, ringSize, holeR);
            Color ringBg = isDark ? Color.FromArgb(238, 22, 24, 34) : Color.FromArgb(246, 248, 250, 255);

            double rotRad = ((now * 34.0) % 360.0) * Math.PI / 180.0;
            var rimStroke = new LinearGradientBrush
            {
                StartPoint = new Point(0.5 + 0.5 * Math.Cos(rotRad), 0.5 + 0.5 * Math.Sin(rotRad)),
                EndPoint = new Point(0.5 - 0.5 * Math.Cos(rotRad), 0.5 - 0.5 * Math.Sin(rotRad))
            };
            rimStroke.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(isDark ? 95 : 180), 255, 255, 255), 0.0));
            rimStroke.GradientStops.Add(new GradientStop(theme.LightStop(), 0.5));
            rimStroke.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(isDark ? 45 : 95), 255, 255, 255), 1.0));

            dc.DrawGeometry(
                new SolidColorBrush(ringBg),
                new System.Windows.Media.Pen(rimStroke, 1.15),
                donut);
            dc.Pop();

            // 3. Petals + Centered Format Labels with 120Hz Spring Pop
            int count = SampleFormats.Length;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            for (int idx = 0; idx < count; idx++)
            {
                double hoverT = Math.Clamp(_sliceHoverPos[idx], 0.0, 1.0);
                double angle = ConverzeWheelMetrics.CenterAngle(idx, count);
                double pop = hoverT * 3.4;
                double popDx = pop * Math.Cos(angle);
                double popDy = -pop * Math.Sin(angle);
                double scale = 1.0 + 0.032 * hoverT;

                dc.PushTransform(new TranslateTransform(popDx, popDy));
                dc.PushTransform(new ScaleTransform(scale, scale, cx, cy));

                StreamGeometry petal = ConverzePetalGeometry.Build(
                    idx, count,
                    _size, _size,
                    innerRadius: innerR,
                    outerRadius: outerR,
                    gap: 3.2,
                    cornerRadius: 12.0);

                Color idleCol = isDark ? Color.FromArgb(220, 42, 45, 58) : Color.FromArgb(242, 255, 255, 255);
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                grad.GradientStops.Add(new GradientStop(LerpColor(idleCol, theme.LightStop(), hoverT), 0.0));
                grad.GradientStops.Add(new GradientStop(LerpColor(idleCol, theme.PrimaryColor(), hoverT), 0.55));
                grad.GradientStops.Add(new GradientStop(LerpColor(idleCol, theme.DeepStop(), hoverT), 1.0));

                Color idleStroke = isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(30, 15, 23, 42);
                Color activeStroke = Color.FromArgb(195, 255, 255, 255);
                var pen = new System.Windows.Media.Pen(new SolidColorBrush(LerpColor(idleStroke, activeStroke, hoverT)), 0.8 + 0.4 * hoverT);

                dc.DrawGeometry(grad, pen, petal);

                Point labelOffset = ConverzeWheelMetrics.LabelOffset(
                    idx, count,
                    innerRadius: innerR,
                    outerRadius: outerR);

                Point labelPt = new Point(cx + labelOffset.X, cy + labelOffset.Y);
                Color idleLabelCol = isDark ? Color.FromArgb(225, 248, 250, 252) : Color.FromArgb(225, 30, 41, 59);
                Color labelCol = LerpColor(idleLabelCol, Colors.White, hoverT);

                var ft = new FormattedText(
                    SampleFormats[idx],
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    10.0,
                    new SolidColorBrush(labelCol),
                    dpi);

                dc.DrawText(ft, new Point(labelPt.X - ft.Width * 0.5, labelPt.Y - ft.Height * 0.5));

                dc.Pop();
                dc.Pop();
            }

            // 4. Center Pill Badge
            string badgeStr = $"→ {SampleFormats[_activeIndex]}";
            var badgeFt = new FormattedText(
                badgeStr,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                9.5,
                System.Windows.Media.Brushes.White,
                dpi);

            double pillW = Math.Max(48.0, badgeFt.Width + 14.0);
            double pillH = 20.0;
            dc.DrawRoundedRectangle(
                new SolidColorBrush(theme.PrimaryColor()),
                null,
                new Rect(cx - pillW * 0.5, cy - pillH * 0.5, pillW, pillH),
                10.0, 10.0);

            dc.DrawText(badgeFt, new Point(cx - badgeFt.Width * 0.5, cy - badgeFt.Height * 0.5));
        }

        private static Color LerpColor(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }
    }

    // MARK: - First-Launch Onboarding / Welcome Screen Window (760x520)

    public sealed class ConverzeOnboardingWindow : Window
    {
        private int _step = 0;
        private readonly Action? _onFinishedOpenMain;
        private readonly Action<string[]>? _onFilesDropped;
        private readonly Border _rootContainer;
        private Border? _rightCardHost;

        public ConverzeOnboardingWindow(Action? onFinishedOpenMain = null, Action<string[]>? onFilesDropped = null)
        {
            _onFinishedOpenMain = onFinishedOpenMain;
            _onFilesDropped = onFilesDropped;

            Width = 760;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Title = "Converze";
            AllowDrop = true;

            _rootContainer = new Border();
            Content = _rootContainer;

            ConverzePreferences.Shared.HasCompletedOnboarding = true;

            DragEnter += OnWindowDragOver;
            DragOver += OnWindowDragOver;
            Drop += OnWindowDrop;

            ConverzePreferences.Shared.OnPreferencesChanged += RebuildUI;
            Closed += (_, _) =>
            {
                ConverzePreferences.Shared.HasCompletedOnboarding = true;
                ConverzePreferences.Shared.OnPreferencesChanged -= RebuildUI;
            };

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

            Title = prefs.Tr("Converze", "Converze");

            Color bgTop = isDark ? Color.FromRgb(18, 20, 28) : Color.FromRgb(247, 249, 253);
            Color bgBot = isDark ? Color.FromRgb(13, 14, 20) : Color.FromRgb(237, 242, 250);
            _rootContainer.Background = new LinearGradientBrush(bgTop, bgBot, new Point(0, 0), new Point(1, 1));

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = BuildHeader(prefs, isDark, theme);
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            var bodyGrid = new Grid { Margin = new Thickness(26, 18, 26, 18) };
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(246) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftCard = BuildLeftPreviewCard(prefs, isDark, theme);
            Grid.SetColumn(leftCard, 0);
            bodyGrid.Children.Add(leftCard);

            _rightCardHost = BuildRightStepCard(prefs, isDark, theme);
            Grid.SetColumn(_rightCardHost, 2);
            bodyGrid.Children.Add(_rightCardHost);

            Grid.SetRow(bodyGrid, 1);
            mainGrid.Children.Add(bodyGrid);

            var footer = BuildFooter(prefs, isDark, theme);
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            _rootContainer.Child = mainGrid;
            AnimateRightCard();
        }

        private void AnimateRightCard()
        {
            if (_rightCardHost == null) return;
            var trans = new TranslateTransform(14, 0);
            _rightCardHost.RenderTransform = trans;
            _rightCardHost.Opacity = 0.0;

            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slide = new DoubleAnimation(14.0, 0.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _rightCardHost.BeginAnimation(OpacityProperty, fade);
            trans.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        private UIElement BuildHeader(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var grid = new Grid { Margin = new Thickness(26, 20, 26, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconElem = ConverzeAppIconLoader.CreateAppIconElement(40);
            Grid.SetColumn(iconElem, 0);
            grid.Children.Add(iconElem);

            var titleStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            titleStack.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Bun venit în Converze", "Welcome to Converze"),
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = prefs.Tr(
                    $"Configurare inițială rapidă · pasul {_step + 1} din 3",
                    $"Quick first-time setup · step {_step + 1} of 3"),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(165, 255, 255, 255) : Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(titleStack, 1);
            grid.Children.Add(titleStack);

            var pillsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            for (int i = 0; i < 3; i++)
            {
                pillsPanel.Children.Add(new Border
                {
                    Width = (i == _step) ? 24 : 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(3, 0, 3, 0),
                    Background = new SolidColorBrush(
                        (i == _step)
                            ? theme.PrimaryColor()
                            : (isDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(40, 15, 23, 42)))
                });
            }
            Grid.SetColumn(pillsPanel, 2);
            grid.Children.Add(pillsPanel);

            var wrapper = new StackPanel();
            wrapper.Children.Add(grid);
            wrapper.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42))
            });
            return wrapper;
        }

        private UIElement BuildLeftPreviewCard(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var stack = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text = prefs.Tr("PREVIZUALIZARE LIVE (TRAGE FIȘIER AICI)", "LIVE PREVIEW (DROP FILE HERE)"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(155, 255, 255, 255) : Color.FromRgb(100, 116, 139)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            stack.Children.Add(new MiniWheelPreviewControl(
                184,
                onFilesDropped: files => _onFilesDropped?.Invoke(files)));

            var infoPill = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Background = new SolidColorBrush(isDark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(18, 15, 23, 42)),
                Child = new TextBlock
                {
                    Text = $"{theme.Title(prefs.Language)} · {prefs.AppearanceMode.Title(prefs.Language)}",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(30, 41, 59)),
                    TextAlignment = TextAlignment.Center
                }
            };
            stack.Children.Add(infoPill);

            return new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(isDark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(220, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Child = stack
            };
        }

        private Border BuildRightStepCard(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            UIElement stepContent = _step switch
            {
                0 => BuildStepLanguageAndAppearance(prefs, isDark, theme),
                1 => BuildStepColorPalette(prefs, isDark, theme),
                _ => BuildStepHowItWorks(prefs, isDark, theme)
            };

            return new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(isDark ? Color.FromArgb(14, 255, 255, 255) : Color.FromArgb(215, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(22, 15, 23, 42)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 18, 20, 18),
                Child = stepContent
            };
        }

        private UIElement BuildStepLanguageAndAppearance(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = prefs.Tr("1. Alege limba aplicației", "1. Choose application language"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var langGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 20) };
            foreach (var lang in new[] { AppLanguage.Ro, AppLanguage.En })
            {
                bool selected = (prefs.Language == lang);
                var card = new Border
                {
                    Margin = new Thickness(0, 0, lang == AppLanguage.Ro ? 6 : 0, 0),
                    Padding = new Thickness(12, 10, 12, 10),
                    CornerRadius = new CornerRadius(13),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(selected
                        ? WithAlpha(theme.PrimaryColor(), isDark ? 0.25 : 0.13)
                        : (isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White)),
                    BorderBrush = new SolidColorBrush(selected
                        ? theme.PrimaryColor()
                        : (isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(28, 15, 23, 42))),
                    BorderThickness = new Thickness(selected ? 1.8 : 1.0)
                };

                var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                row.Children.Add(new TextBlock
                {
                    Text = lang.FlagEmoji(),
                    FontSize = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });

                var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textCol.Children.Add(new TextBlock
                {
                    Text = lang.DisplayName(),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });
                textCol.Children.Add(new TextBlock
                {
                    Text = lang.Subtitle(),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(isDark ? Color.FromArgb(150, 255, 255, 255) : Color.FromRgb(100, 116, 139))
                });
                row.Children.Add(textCol);

                card.Child = row;
                var captured = lang;
                card.MouseLeftButtonUp += (_, _) => prefs.Language = captured;
                langGrid.Children.Add(card);
            }
            stack.Children.Add(langGrid);

            stack.Children.Add(new TextBlock
            {
                Text = prefs.Tr("2. Mod de afișare", "2. Display mode"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var modeGrid = new UniformGrid { Columns = 3 };
            foreach (var mode in new[] { AppAppearanceMode.Light, AppAppearanceMode.Dark, AppAppearanceMode.System })
            {
                bool selected = (prefs.AppearanceMode == mode);
                var card = new Border
                {
                    Margin = new Thickness(3, 0, 3, 0),
                    Padding = new Thickness(10, 12, 10, 12),
                    CornerRadius = new CornerRadius(13),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(selected
                        ? WithAlpha(theme.PrimaryColor(), isDark ? 0.25 : 0.13)
                        : (isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White)),
                    BorderBrush = new SolidColorBrush(selected
                        ? theme.PrimaryColor()
                        : (isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(28, 15, 23, 42))),
                    BorderThickness = new Thickness(selected ? 1.8 : 1.0)
                };

                var col = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                col.Children.Add(new TextBlock
                {
                    Text = mode.GlyphIcon(),
                    FontSize = 18,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                col.Children.Add(new TextBlock
                {
                    Text = mode.Title(prefs.Language),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });
                col.Children.Add(new TextBlock
                {
                    Text = mode.Subtitle(prefs.Language),
                    FontSize = 9.5,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0),
                    Foreground = new SolidColorBrush(isDark ? Color.FromArgb(145, 255, 255, 255) : Color.FromRgb(100, 116, 139))
                });

                card.Child = col;
                var captured = mode;
                card.MouseLeftButtonUp += (_, _) => prefs.AppearanceMode = captured;
                modeGrid.Children.Add(card);
            }
            stack.Children.Add(modeGrid);

            return stack;
        }

        private UIElement BuildStepColorPalette(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Paleta de culori a roții", "Wheel Color Palette"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
            });

            stack.Children.Add(new TextBlock
            {
                Text = prefs.Tr(
                    "Alege nuanța preferată.",
                    "Pick the desired accent color."),
                FontSize = 12,
                Foreground = new SolidColorBrush(isDark ? Color.FromArgb(165, 255, 255, 255) : Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 4, 0, 14)
            });

            var grid = new UniformGrid { Columns = 3, Rows = 2 };
            foreach (var palette in ConverzeColorThemeExtensions.AllCases)
            {
                bool selected = (prefs.ColorTheme == palette);
                var card = new Border
                {
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 14, 12, 14),
                    CornerRadius = new CornerRadius(14),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(selected
                        ? WithAlpha(palette.PrimaryColor(), isDark ? 0.25 : 0.13)
                        : (isDark ? Color.FromArgb(18, 255, 255, 255) : Colors.White)),
                    BorderBrush = new SolidColorBrush(selected
                        ? palette.PrimaryColor()
                        : (isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(28, 15, 23, 42))),
                    BorderThickness = new Thickness(selected ? 2.0 : 1.0)
                };

                var col = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                var swatchGrad = new LinearGradientBrush(palette.LightStop(), palette.DeepStop(), new Point(0, 0), new Point(1, 1));
                col.Children.Add(new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(18),
                    Background = swatchGrad,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                col.Children.Add(new TextBlock
                {
                    Text = palette.Title(prefs.Language),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });

                card.Child = col;
                var captured = palette;
                card.MouseLeftButtonUp += (_, _) => prefs.ColorTheme = captured;
                grid.Children.Add(card);
            }

            stack.Children.Add(grid);
            return stack;
        }

        private UIElement BuildStepHowItWorks(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = prefs.Tr("Cum funcționează Converze", "How Converze works"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var rows = new (string Icon, string Title, string Desc)[]
            {
                ("Alt",
                 prefs.Tr("1. Ține apăsată tasta Alt.", "1. Hold the Alt key."),
                 prefs.Tr("Selectează orice fișier și apasă tasta Alt și click-stânga.", "Select any file and hold Alt + left-click.")),
                ("📂",
                 prefs.Tr("2. Poți să tragi fișierul și în fereastra de la Converze.", "2. You can also drag the file into Converze window."),
                 prefs.Tr("Poți trage oricând o poză sau un fișier direct pe zona de Drag & Drop din fereastra aplicației.", "You can also drag any photo or file directly onto the Drag & Drop zone inside the app window.")),
                ("⚡",
                 prefs.Tr("3. Alege formatul dorit.", "3. Pick your target format."),
                 prefs.Tr("Alege extensia dorită (ex: WEBP, PDF, MP4, MP3) iar fișierul convertit apare instant alături de cel original.", "Click to select the desired extension (e.g. WEBP, PDF, MP4, MP3) and the converted file is saved right next to the original.")),
                ("❖",
                 prefs.Tr("4. Poate fi accesat rapid din System Tray", "4. Quick access from System Tray"),
                 prefs.Tr("Converze rămâne activ în System Tray (dreapta-jos, lângă ceas).", "Converze stays active in your Windows System Tray."))
            };

            foreach (var r in rows)
            {
                var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var badge = new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(WithAlpha(theme.PrimaryColor(), isDark ? 0.24 : 0.14)),
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = new TextBlock
                    {
                        Text = r.Icon,
                        FontSize = r.Icon == "Alt" ? 11.5 : 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(theme.PrimaryColor()),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Grid.SetColumn(badge, 0);
                rowGrid.Children.Add(badge);

                var textStack = new StackPanel();
                textStack.Children.Add(new TextBlock
                {
                    Text = r.Title,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = r.Desc,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(isDark ? Color.FromArgb(165, 255, 255, 255) : Color.FromRgb(100, 116, 139)),
                    Margin = new Thickness(0, 1, 0, 0)
                });
                Grid.SetColumn(textStack, 2);
                rowGrid.Children.Add(textStack);

                stack.Children.Add(rowGrid);
            }

            return stack;
        }

        private UIElement BuildFooter(ConverzePreferences prefs, bool isDark, ConverzeColorTheme theme)
        {
            var grid = new Grid { Margin = new Thickness(26, 12, 26, 16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (_step > 0)
            {
                var backBtn = CreatePillButton(
                    prefs.Tr("← Înapoi", "← Back"),
                    isPrimary: false,
                    isDark: isDark,
                    theme: theme,
                    onClick: () =>
                    {
                        _step--;
                        RebuildUI();
                    });
                Grid.SetColumn(backBtn, 0);
                grid.Children.Add(backBtn);
            }

            var rightActions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            if (_step < 2)
            {
                var nextBtn = CreatePillButton(
                    prefs.Tr("Continuă →", "Continue →"),
                    isPrimary: true,
                    isDark: isDark,
                    theme: theme,
                    onClick: () =>
                    {
                        _step++;
                        RebuildUI();
                    });
                rightActions.Children.Add(nextBtn);
            }
            else
            {
                var finishBtn = CreatePillButton(
                    prefs.Tr("Deschide Panoul Converze ✓", "Open Converze Dashboard ✓"),
                    isPrimary: true,
                    isDark: isDark,
                    theme: theme,
                    onClick: () =>
                    {
                        prefs.HasCompletedOnboarding = true;
                        Close();
                        _onFinishedOpenMain?.Invoke();
                    });
                rightActions.Children.Add(finishBtn);
            }

            Grid.SetColumn(rightActions, 2);
            grid.Children.Add(rightActions);

            var wrapper = new StackPanel();
            wrapper.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(25, 15, 23, 42))
            });
            wrapper.Children.Add(grid);
            return wrapper;
        }

        private static Border CreatePillButton(
            string text,
            bool isPrimary,
            bool isDark,
            ConverzeColorTheme theme,
            Action onClick)
        {
            var btn = new Border
            {
                CornerRadius = new CornerRadius(17),
                Padding = new Thickness(18, 8, 18, 8),
                Cursor = Cursors.Hand,
                Background = isPrimary
                    ? new LinearGradientBrush(theme.LightStop(), theme.DeepStop(), new Point(0, 0), new Point(1, 1))
                    : new SolidColorBrush(isDark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(18, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(isPrimary
                    ? Colors.Transparent
                    : (isDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(30, 15, 23, 42))),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(isPrimary
                        ? Colors.White
                        : (isDark ? Colors.White : Color.FromRgb(30, 41, 59)))
                }
            };
            btn.MouseLeftButtonUp += (_, _) => onClick();
            return btn;
        }

        private static Color WithAlpha(Color c, double alpha)
        {
            byte a = (byte)Math.Clamp(alpha * 255.0, 0.0, 255.0);
            return Color.FromArgb(a, c.R, c.G, c.B);
        }
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Converze.Models;
using Converze.Services;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;

namespace Converze.UI.Progress
{
    public enum ConversionHUDPhase
    {
        Converting,
        Success,
        Failed
    }

    public sealed class ConversionHUDManager
    {
        public static readonly ConversionHUDManager Shared = new ConversionHUDManager();

        private ConversionHUDToastWindow? _window;
        private CancellationTokenSource? _dismissCts;
        private DispatcherTimer? _simulatedProgressTimer;
        private double _currentProgress;

        private ConversionHUDManager() { }

        public void ShowConverting(string fileName, string targetLabel)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _dismissCts?.Cancel();
                StopSimulatedTimer();
                EnsureWindow();

                var prefs = ConverzePreferences.Shared;
                _currentProgress = 0.12;
                _window!.UpdateContent(
                    ConversionHUDPhase.Converting,
                    prefs.Tr("Se convertește...", "Converting..."),
                    $"{fileName} → {targetLabel}",
                    progress: _currentProgress,
                    outputPath: null);
                _window.ShowCentered();

                _simulatedProgressTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(35)
                };
                _simulatedProgressTimer.Tick += (_, _) =>
                {
                    if (_currentProgress < 0.88)
                    {
                        _currentProgress += (0.90 - _currentProgress) * 0.14;
                        _window.SetProgress(_currentProgress);
                    }
                };
                _simulatedProgressTimer.Start();
            });
        }

        public void UpdateProgress(double progressFraction, string subtitle)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_window == null) return;
                _currentProgress = Math.Max(_currentProgress, Math.Clamp(progressFraction, 0.08, 0.95));
                _window.SetProgress(_currentProgress);
            });
        }

        public void ShowCompleted(ConversionJobResult result)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _dismissCts?.Cancel();
                StopSimulatedTimer();
                EnsureWindow();

                var prefs = ConverzePreferences.Shared;
                string outName = Path.GetFileName(result.OutputPath);
                string ms = $"{result.Duration.TotalSeconds:0.00}s";
                _currentProgress = 1.0;

                _window!.UpdateContent(
                    ConversionHUDPhase.Success,
                    prefs.Tr("Convertit cu succes!", "Converted Successfully!"),
                    $"{outName} · {ms}",
                    progress: 1.0,
                    outputPath: result.OutputPath);
                _window.ShowCentered();
                ScheduleAutoDismiss(4200);
            });
        }

        public void ShowError(string message)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _dismissCts?.Cancel();
                StopSimulatedTimer();
                EnsureWindow();

                var prefs = ConverzePreferences.Shared;
                _window!.UpdateContent(
                    ConversionHUDPhase.Failed,
                    prefs.Tr("Eroare la conversie", "Conversion Failed"),
                    message,
                    progress: 1.0,
                    outputPath: null);
                _window.ShowCentered();
                ScheduleAutoDismiss(4500);
            });
        }

        private void StopSimulatedTimer()
        {
            if (_simulatedProgressTimer != null)
            {
                _simulatedProgressTimer.Stop();
                _simulatedProgressTimer = null;
            }
        }

        private void EnsureWindow()
        {
            if (_window == null)
            {
                _window = new ConversionHUDToastWindow();
            }
        }

        private void ScheduleAutoDismiss(int delayMs)
        {
            _dismissCts?.Cancel();
            var cts = new CancellationTokenSource();
            _dismissCts = cts;

            Task.Delay(delayMs, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _window?.AnimateHide();
                    });
                }
            });
        }
    }

    public sealed class ConversionHUDToastWindow : Window
    {
        private readonly Border _cardBorder;
        private readonly Border _badgeCircle;
        private readonly TextBlock _badgeGlyph;
        private readonly TextBlock _titleBlock;
        private readonly TextBlock _subtitleBlock;
        private readonly TextBlock _percentBlock;
        private readonly Border _progressTrack;
        private readonly Border _progressFill;
        private readonly Border _revealBtn;
        private readonly TextBlock _revealText;
        private readonly Border _closeBtn;
        private string? _currentOutputPath;
        private const double TrackWidth = 372.0;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public ConversionHUDToastWindow()
        {
            Width = 430;
            Height = 128;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;

            _badgeGlyph = new TextBlock
            {
                Text = "✓",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _badgeCircle = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Child = _badgeGlyph,
                VerticalAlignment = VerticalAlignment.Center
            };

            _titleBlock = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _subtitleBlock = new TextBlock
            {
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 10, 0)
            };
            textStack.Children.Add(_titleBlock);
            textStack.Children.Add(_subtitleBlock);

            _percentBlock = new TextBlock
            {
                Text = "0%",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            _revealText = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _revealBtn = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0),
                Child = _revealText
            };
            _revealBtn.MouseLeftButtonUp += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_currentOutputPath))
                {
                    ConversionEngine.RevealInExplorer(_currentOutputPath!);
                }
                AnimateHide();
            };

            _closeBtn = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "✕",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _closeBtn.MouseLeftButtonUp += (_, _) => AnimateHide();

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(_badgeCircle, 0);
            Grid.SetColumn(textStack, 1);
            Grid.SetColumn(_percentBlock, 2);
            Grid.SetColumn(_revealBtn, 3);
            Grid.SetColumn(_closeBtn, 4);

            topRow.Children.Add(_badgeCircle);
            topRow.Children.Add(textStack);
            topRow.Children.Add(_percentBlock);
            topRow.Children.Add(_revealBtn);
            topRow.Children.Add(_closeBtn);

            // Progress Bar Track & Animated Fill
            _progressFill = new Border
            {
                Height = 8,
                Width = 0,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            _progressTrack = new Border
            {
                Width = TrackWidth,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Child = _progressFill
            };

            var mainStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            mainStack.Children.Add(topRow);
            mainStack.Children.Add(_progressTrack);

            _cardBorder = new Border
            {
                Margin = new Thickness(14),
                Padding = new Thickness(16, 14, 16, 14),
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1.2),
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1.0, 1.0),
                Child = mainStack,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.34,
                    BlurRadius = 22,
                    ShadowDepth = 6,
                    Direction = 270
                }
            };

            Content = _cardBorder;
        }

        public void UpdateContent(ConversionHUDPhase phase, string title, string subtitle, double progress, string? outputPath)
        {
            var prefs = ConverzePreferences.Shared;
            bool isDark = prefs.IsDarkMode;
            var theme = prefs.ColorTheme;

            _currentOutputPath = outputPath;
            _titleBlock.Text = title;
            _subtitleBlock.Text = subtitle;

            _cardBorder.Background = new SolidColorBrush(
                isDark ? Color.FromArgb(246, 20, 22, 32) : Color.FromArgb(250, 255, 255, 255));
            _cardBorder.BorderBrush = new SolidColorBrush(
                isDark ? Color.FromArgb(75, 255, 255, 255) : Color.FromArgb(40, 15, 23, 42));

            _titleBlock.Foreground = new SolidColorBrush(
                isDark ? Colors.White : Color.FromRgb(15, 23, 42));
            _subtitleBlock.Foreground = new SolidColorBrush(
                isDark ? Color.FromArgb(190, 255, 255, 255) : Color.FromArgb(180, 51, 65, 85));

            _percentBlock.Foreground = new SolidColorBrush(theme.PrimaryColor());
            _progressTrack.Background = new SolidColorBrush(
                isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(28, 15, 23, 42));

            var fillGrad = new LinearGradientBrush(theme.LightStop(), theme.PrimaryColor(), new System.Windows.Point(0, 0), new System.Windows.Point(1, 0));
            _progressFill.Background = fillGrad;

            _closeBtn.Background = new SolidColorBrush(
                isDark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(18, 15, 23, 42));
            if (_closeBtn.Child is TextBlock closeTb)
            {
                closeTb.Foreground = new SolidColorBrush(
                    isDark ? Color.FromArgb(195, 255, 255, 255) : Color.FromRgb(71, 85, 105));
            }

            switch (phase)
            {
                case ConversionHUDPhase.Converting:
                    _badgeGlyph.Text = "↻";
                    _badgeCircle.Background = new SolidColorBrush(theme.PrimaryColor());
                    _revealBtn.Visibility = Visibility.Collapsed;
                    break;

                case ConversionHUDPhase.Success:
                    _badgeGlyph.Text = "✓";
                    _badgeCircle.Background = new SolidColorBrush(theme.PrimaryColor());
                    _revealText.Text = prefs.Tr("Arată", "Reveal");
                    _revealBtn.Background = new SolidColorBrush(theme.PrimaryColor());
                    _revealBtn.Visibility = string.IsNullOrEmpty(outputPath) ? Visibility.Collapsed : Visibility.Visible;
                    break;

                case ConversionHUDPhase.Failed:
                    _badgeGlyph.Text = "!";
                    _badgeCircle.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    _progressFill.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    _revealBtn.Visibility = Visibility.Collapsed;
                    break;
            }

            SetProgress(progress);
        }

        public void SetProgress(double fraction)
        {
            double clamped = Math.Clamp(fraction, 0.0, 1.0);
            int pct = (int)Math.Round(clamped * 100.0);
            _percentBlock.Text = $"{pct}%";

            double targetWidth = TrackWidth * clamped;
            var widthAnim = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _progressFill.BeginAnimation(WidthProperty, widthAnim);
        }

        public void ShowCentered()
        {
            if (!IsVisible)
            {
                Show();
            }

            // Position prominently in the center of the active screen work area
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) * 0.5;
            Top = workArea.Top + (workArea.Height - Height) * 0.5 - 30.0;

            var fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));
            BeginAnimation(OpacityProperty, fadeAnim);

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

        public void AnimateHide()
        {
            if (!IsVisible) return;
            var fadeOut = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(180));
            fadeOut.Completed += (_, _) => Hide();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}

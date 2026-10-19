using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Converze.Models;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace Converze.UI.RadialWheel
{
    // MARK: - ViewModel for the Radial Wheel State

    public sealed class RadialWheelViewModel
    {
        public int? HoveredIndex { get; set; }
        public Point PointerOffset { get; set; } = new Point(0, 0);
        public bool IsVisible { get; set; }
        public bool DropCompleted { get; set; }
        public int? CommittedIndex { get; set; }
        public DraggedSelection? Selection { get; set; }

        public event Action? StateChanged;

        public List<WheelTarget> Targets
        {
            get
            {
                if (Selection != null)
                {
                    return Selection.AvailableFormatTargets();
                }
                return new List<WheelTarget>
                {
                    new("webp"), new("png"), new("jpg"),
                    new("avif"), new("heic"), new("pdf"), new("ico")
                };
            }
        }

        public WheelTarget? HoveredTarget
        {
            get
            {
                var list = Targets;
                if (HoveredIndex.HasValue && HoveredIndex.Value >= 0 && HoveredIndex.Value < list.Count)
                {
                    return list[HoveredIndex.Value];
                }
                return null;
            }
        }

        public void NotifyChanged() => StateChanged?.Invoke();

        public void UpdateHover(Point offset)
        {
            PointerOffset = offset;
            var currentTargets = Targets;
            int? newIndex = ConverzeWheelMetrics.HitTestSlice(offset.X, offset.Y, currentTargets.Count);

            if (newIndex != HoveredIndex)
            {
                HoveredIndex = newIndex;
                StateChanged?.Invoke();
            }
        }

        public void TriggerDropSuccess(int index, Action onComplete)
        {
            CommittedIndex = index;
            DropCompleted = true;
            if (ConverzePreferences.Shared.HapticsEnabled)
            {
                try { SystemSounds.Asterisk.Play(); } catch { }
            }
            StateChanged?.Invoke();

            Task.Delay(340).ContinueWith(_ =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    DropCompleted = false;
                    CommittedIndex = null;
                    onComplete();
                });
            });
        }
    }

    // MARK: - Custom 120Hz Continuous-Spring Rendered Radial Wheel Control

    public sealed class RadialWheelControl : FrameworkElement
    {
        public const double TotalControlWidth = 380.0;
        public const double TotalControlHeight = 410.0;
        public const double WheelCenterX = TotalControlWidth * 0.5;
        public const double WheelCenterY = 205.0;
        private const double CloseAnimationDuration = 0.20; // 200ms silky fade-out

        private readonly RadialWheelViewModel _vm;
        private readonly Action<WheelTarget>? _onTargetSelected;
        private readonly Action? _onOutsideClicked;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private double _openStartTime;
        private double _lastFrameTime;
        private double _dropStartTime;
        private bool _wasDropCompleted;

        // Closing fade-out animation state
        private bool _isClosing;
        private double _closeStartTime;
        private Action? _onCloseCompleted;

        // Per-slice continuous 120Hz spring states
        private readonly double[] _sliceHoverPos = new double[12];
        private readonly double[] _sliceHoverVel = new double[12];
        private double _smoothPtrX;
        private double _smoothPtrY;

        public bool IsClosing => _isClosing;

        public RadialWheelControl(
            RadialWheelViewModel viewModel,
            Action<WheelTarget>? onTargetSelected = null,
            Action? onOutsideClicked = null)
        {
            _vm = viewModel;
            _onTargetSelected = onTargetSelected;
            _onOutsideClicked = onOutsideClicked;

            Width = TotalControlWidth;
            Height = TotalControlHeight;
            Focusable = true;
            Cursor = Cursors.Hand;

            _openStartTime = _clock.Elapsed.TotalSeconds;
            _lastFrameTime = _openStartTime;
            _vm.StateChanged += OnViewModelStateChanged;

            Loaded += (_, _) =>
            {
                _openStartTime = _clock.Elapsed.TotalSeconds;
                _lastFrameTime = _openStartTime;
                CompositionTarget.Rendering -= OnFrameRendering;
                CompositionTarget.Rendering += OnFrameRendering;
            };

            Unloaded += (_, _) =>
            {
                CompositionTarget.Rendering -= OnFrameRendering;
            };
        }

        public void ResetOpenAnimation()
        {
            _isClosing = false;
            _onCloseCompleted = null;
            _openStartTime = _clock.Elapsed.TotalSeconds;
            _lastFrameTime = _openStartTime;
            Array.Clear(_sliceHoverPos, 0, _sliceHoverPos.Length);
            Array.Clear(_sliceHoverVel, 0, _sliceHoverVel.Length);
            _smoothPtrX = 0;
            _smoothPtrY = 0;
            InvalidateVisual();
        }

        public void StartCloseAnimation(Action onCompleted)
        {
            if (_isClosing)
            {
                _onCloseCompleted = onCompleted;
                return;
            }

            _isClosing = true;
            _closeStartTime = _clock.Elapsed.TotalSeconds;
            _lastFrameTime = _closeStartTime;
            _onCloseCompleted = onCompleted;
            InvalidateVisual();
        }

        private void OnViewModelStateChanged()
        {
            double now = _clock.Elapsed.TotalSeconds;
            if (_vm.DropCompleted && !_wasDropCompleted)
            {
                _dropStartTime = now;
            }
            _wasDropCompleted = _vm.DropCompleted;
            InvalidateVisual();
        }

        private void OnFrameRendering(object? sender, EventArgs e)
        {
            if (!_vm.IsVisible && !_isClosing) return;

            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Clamp(now - _lastFrameTime, 0.001, 0.033);
            _lastFrameTime = now;

            if (_isClosing)
            {
                double closeElapsed = now - _closeStartTime;
                if (closeElapsed >= CloseAnimationDuration)
                {
                    _isClosing = false;
                    var callback = _onCloseCompleted;
                    _onCloseCompleted = null;
                    InvalidateVisual();
                    callback?.Invoke();
                    return;
                }
            }

            int count = Math.Min(_sliceHoverPos.Length, _vm.Targets.Count);
            const double stiffness = 340.0;
            const double damping = 24.0;

            for (int i = 0; i < count; i++)
            {
                double target = (!_isClosing && (_vm.HoveredIndex == i || (_vm.CommittedIndex == i && _vm.DropCompleted))) ? 1.0 : 0.0;
                double force = stiffness * (target - _sliceHoverPos[i]) - damping * _sliceHoverVel[i];
                _sliceHoverVel[i] += force * dt;
                _sliceHoverPos[i] = Math.Clamp(_sliceHoverPos[i] + _sliceHoverVel[i] * dt, 0.0, 1.15);
            }

            double ptrLerp = Math.Clamp(dt * 26.0, 0.0, 1.0);
            _smoothPtrX += (_vm.PointerOffset.X - _smoothPtrX) * ptrLerp;
            _smoothPtrY += (_vm.PointerOffset.Y - _smoothPtrY) * ptrLerp;

            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isClosing) return;
            Point pt = e.GetPosition(this);
            double dx = pt.X - WheelCenterX;
            double dy = pt.Y - WheelCenterY;
            _vm.UpdateHover(new Point(dx, dy));
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isClosing) return;
            _vm.UpdateHover(new Point(0, 0));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_isClosing) return;

            Point pt = e.GetPosition(this);
            double dx = pt.X - WheelCenterX;
            double dy = pt.Y - WheelCenterY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist > ConverzeWheelMetrics.BackdropRadius + 16.0)
            {
                _onOutsideClicked?.Invoke();
                e.Handled = true;
                return;
            }

            var targets = _vm.Targets;
            int? sliceIdx = ConverzeWheelMetrics.HitTestSlice(dx, dy, targets.Count);
            if (sliceIdx.HasValue && sliceIdx.Value >= 0 && sliceIdx.Value < targets.Count)
            {
                var chosen = targets[sliceIdx.Value];
                _vm.HoveredIndex = sliceIdx.Value;
                _vm.TriggerDropSuccess(sliceIdx.Value, () =>
                {
                    _onTargetSelected?.Invoke(chosen);
                });
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var prefs = ConverzePreferences.Shared;
            bool isDark = prefs.IsDarkMode;
            var theme = prefs.ColorTheme;
            var targets = _vm.Targets;
            int count = targets.Count;

            double now = _clock.Elapsed.TotalSeconds;
            double openElapsed = Math.Max(0, now - _openStartTime);
            double openSpring = DampedSpring(openElapsed, damping: 0.72, frequency: 4.9);
            double openOpacity = Math.Clamp(openElapsed / 0.14, 0.0, 1.0);

            // Apply smooth fade-out and scale-down when closing
            double closeFactor = 1.0;
            double closeScaleMultiplier = 1.0;
            if (_isClosing)
            {
                double closeT = Math.Clamp((now - _closeStartTime) / CloseAnimationDuration, 0.0, 1.0);
                double closeEase = EaseOutCubic(closeT);
                closeFactor = Math.Clamp(1.0 - closeEase, 0.0, 1.0);
                closeScaleMultiplier = 1.0 - 0.18 * closeEase;
            }

            double finalOpacity = openOpacity * closeFactor;

            dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, Width, Height));

            if (!_vm.IsVisible && !_isClosing) return;
            if (finalOpacity <= 0.005) return;

            Point wheelCenter = new Point(WheelCenterX, WheelCenterY);
            double backdropSize = ConverzeWheelMetrics.ContainerSize - ConverzeWheelMetrics.RingInset * 2.0; // 251.84
            double backdropRadius = backdropSize * 0.5; // 125.92

            // 1. Shockwave Halo on Wheel Open
            double haloProgress = Math.Clamp(openElapsed / 0.52, 0.0, 1.0);
            if (haloProgress < 1.0 && !_isClosing)
            {
                double haloRadius = backdropRadius * (0.72 + 0.52 * EaseOutCubic(haloProgress));
                double haloAlpha = (1.0 - haloProgress) * (isDark ? 0.52 : 0.38) * closeFactor;
                var haloPen = new System.Windows.Media.Pen(
                    new SolidColorBrush(WithAlpha(theme.LightStop(), haloAlpha)),
                    1.8);
                dc.DrawEllipse(null, haloPen, wheelCenter, haloRadius, haloRadius);
            }

            // 2. Ambient Glow Under Wheel
            var glowBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            Color glowCol = _vm.HoveredIndex.HasValue ? theme.PrimaryColor() : (isDark ? Colors.Black : Color.FromRgb(30, 41, 59));
            double glowMaxAlpha = _vm.HoveredIndex.HasValue ? (isDark ? 0.38 : 0.24) : (isDark ? 0.45 : 0.18);
            glowBrush.GradientStops.Add(new GradientStop(WithAlpha(glowCol, glowMaxAlpha * finalOpacity), 0.25));
            glowBrush.GradientStops.Add(new GradientStop(WithAlpha(glowCol, 0.0), 1.0));
            dc.DrawEllipse(glowBrush, null, wheelCenter, backdropRadius + 28, backdropRadius + 28);

            // 3. Main Wheel Container Transform (Spring Scale + Close Scale around WheelCenter)
            double wheelScale = (0.35 + 0.65 * openSpring) * closeScaleMultiplier;
            dc.PushOpacity(finalOpacity);
            dc.PushTransform(new ScaleTransform(wheelScale, wheelScale, WheelCenterX, WheelCenterY));

            // 4. Donut Frosted Glass Ring
            dc.PushTransform(new TranslateTransform(WheelCenterX - backdropRadius, WheelCenterY - backdropRadius));
            Geometry donutGeom = DonutRingGeometry.Build(backdropSize, backdropSize, ConverzeWheelMetrics.CenterHoleRadius);

            Color ringFillTop = isDark ? Color.FromArgb(240, 22, 24, 32) : Color.FromArgb(245, 250, 251, 255);
            Color ringFillBot = isDark ? Color.FromArgb(230, 14, 15, 22) : Color.FromArgb(238, 235, 240, 250);
            var ringFillBrush = new LinearGradientBrush(ringFillTop, ringFillBot, new Point(0, 0), new Point(1, 1));

            double rotDeg = (now * 36.0) % 360.0;
            double rotRad = rotDeg * Math.PI / 180.0;
            var rimStrokeBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0.5 + 0.5 * Math.Cos(rotRad), 0.5 + 0.5 * Math.Sin(rotRad)),
                EndPoint = new Point(0.5 - 0.5 * Math.Cos(rotRad), 0.5 - 0.5 * Math.Sin(rotRad))
            };
            rimStrokeBrush.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, isDark ? 0.36 : 0.85), 0.0));
            rimStrokeBrush.GradientStops.Add(new GradientStop(WithAlpha(theme.LightStop(), isDark ? 0.48 : 0.55), 0.5));
            rimStrokeBrush.GradientStops.Add(new GradientStop(WithAlpha(Colors.White, isDark ? 0.14 : 0.45), 1.0));

            dc.DrawGeometry(ringFillBrush, new System.Windows.Media.Pen(rimStrokeBrush, 1.3), donutGeom);
            dc.Pop();

            // 5. Render Radial Format Petals + Centered Labels with 120Hz Continuous Hover Springs
            double containerHalf = ConverzeWheelMetrics.ContainerSize * 0.5;
            dc.PushTransform(new TranslateTransform(WheelCenterX - containerHalf, WheelCenterY - containerHalf));

            for (int idx = 0; idx < count; idx++)
            {
                var target = targets[idx];
                bool isHovered = (_vm.HoveredIndex == idx);
                bool isCommitted = (_vm.CommittedIndex == idx && _vm.DropCompleted);
                double hoverT = idx < _sliceHoverPos.Length ? Math.Clamp(_sliceHoverPos[idx], 0.0, 1.0) : (isHovered ? 1.0 : 0.0);

                double sliceDelay = idx * 0.014;
                double sliceElapsed = Math.Max(0.0, openElapsed - sliceDelay);
                double sliceSpring = DampedSpring(sliceElapsed, damping: 0.72, frequency: 5.2);

                double angle = ConverzeWheelMetrics.CenterAngle(idx, count);
                double popDistance = hoverT * 5.2;
                double popDx = popDistance * Math.Cos(angle);
                double popDy = -popDistance * Math.Sin(angle);

                double petalScale = (0.38 + 0.62 * sliceSpring) * (isCommitted ? 1.09 : (1.0 + 0.038 * hoverT));

                dc.PushTransform(new TranslateTransform(popDx, popDy));
                dc.PushTransform(new ScaleTransform(petalScale, petalScale, containerHalf, containerHalf));

                StreamGeometry petalGeom = ConverzePetalGeometry.Build(
                    idx, count,
                    ConverzeWheelMetrics.ContainerSize,
                    ConverzeWheelMetrics.ContainerSize);

                Color idleTop = isDark ? Color.FromArgb(230, 48, 52, 66) : Color.FromArgb(245, 255, 255, 255);
                Color idleBot = isDark ? Color.FromArgb(215, 32, 35, 46) : Color.FromArgb(235, 230, 236, 246);

                var petalGrad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                petalGrad.GradientStops.Add(new GradientStop(LerpColor(idleTop, theme.LightStop(), hoverT), 0.0));
                petalGrad.GradientStops.Add(new GradientStop(LerpColor(idleTop, theme.PrimaryColor(), hoverT), 0.52));
                petalGrad.GradientStops.Add(new GradientStop(LerpColor(idleBot, theme.DeepStop(), hoverT), 1.0));

                Color idleStroke = isDark ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(35, 15, 23, 42);
                Color activeStroke = WithAlpha(Colors.White, 0.78);
                var petalBorder = new System.Windows.Media.Pen(
                    new SolidColorBrush(LerpColor(idleStroke, activeStroke, hoverT)),
                    0.9 + 0.55 * hoverT);

                dc.DrawGeometry(petalGrad, petalBorder, petalGeom);

                Point labelOffset = ConverzeWheelMetrics.LabelOffset(
                    idx, count,
                    innerRadius: ConverzeWheelMetrics.LabelInnerRadius,
                    outerRadius: ConverzeWheelMetrics.OuterRadius);
                Point labelCenter = new Point(containerHalf + labelOffset.X, containerHalf + labelOffset.Y);

                Color idleTextCol = isDark ? Color.FromArgb(235, 248, 250, 252) : Color.FromArgb(230, 30, 41, 59);
                Color textColor = LerpColor(idleTextCol, Colors.White, hoverT);

                var formattedLabel = CreateFormattedText(
                    target.WheelLabel,
                    fontSize: 12.5 + 1.0 * hoverT,
                    fontWeight: FontWeights.Bold,
                    color: textColor);

                dc.DrawText(
                    formattedLabel,
                    new Point(labelCenter.X - formattedLabel.Width * 0.5, labelCenter.Y - formattedLabel.Height * 0.5));

                if (isCommitted)
                {
                    double sparkProgress = Math.Clamp((now - _dropStartTime) / 0.34, 0.0, 1.0);
                    DrawSparkBurst(dc, labelCenter, sparkProgress, theme);
                }

                dc.Pop();
                dc.Pop();
            }

            dc.Pop();

            // 6. Smooth Orbiting Pointer Dot on Inner Ring
            double ptrDist = Math.Sqrt(_smoothPtrX * _smoothPtrX + _smoothPtrY * _smoothPtrY);
            if (ptrDist > 8.0)
            {
                double ptrAngle = Math.Atan2(-_smoothPtrY, _smoothPtrX);
                double dotR = 45.5;
                Point dotPt = new Point(
                    WheelCenterX + dotR * Math.Cos(ptrAngle),
                    WheelCenterY - dotR * Math.Sin(ptrAngle));

                Color dotCol = _vm.HoveredIndex.HasValue ? theme.LightStop() : (isDark ? Colors.White : theme.PrimaryColor());
                dc.DrawEllipse(new SolidColorBrush(WithAlpha(dotCol, 0.35)), null, dotPt, 6.5, 6.5);
                dc.DrawEllipse(new SolidColorBrush(dotCol), null, dotPt, 3.2, 3.2);
            }

            // 7. Center Cutout Pill Badge
            DrawCenterPill(dc, wheelCenter, isDark, theme);

            dc.Pop();

            // 8. Top Floating File Info Pill & Bottom Action Hint Pill
            DrawTopHeaderPill(dc, isDark, theme);
            DrawBottomHintPill(dc, isDark, theme);

            dc.Pop();
        }

        private void DrawCenterPill(DrawingContext dc, Point center, bool isDark, ConverzeColorTheme theme)
        {
            var hovered = _vm.HoveredTarget;
            string badgeText;
            Color pillBg;
            Color pillBorder;
            Color textCol;

            if (hovered != null)
            {
                badgeText = $"→ {hovered.WheelLabel}";
                pillBg = theme.PrimaryColor();
                pillBorder = WithAlpha(Colors.White, 0.55);
                textCol = Colors.White;
            }
            else
            {
                badgeText = _vm.Selection?.FormattedSize ?? "2.2 MB";
                pillBg = isDark ? Color.FromArgb(228, 30, 32, 42) : Color.FromArgb(242, 255, 255, 255);
                pillBorder = isDark ? Color.FromArgb(65, 255, 255, 255) : Color.FromArgb(35, 15, 23, 42);
                textCol = isDark ? Colors.White : Color.FromRgb(30, 41, 59);
            }

            var ft = CreateFormattedText(badgeText, 11.5, FontWeights.Bold, textCol);
            double pillW = Math.Max(62.0, ft.Width + 20.0);
            double pillH = 25.0;
            Rect pillRect = new Rect(center.X - pillW * 0.5, center.Y - pillH * 0.5, pillW, pillH);

            dc.DrawRoundedRectangle(
                new SolidColorBrush(pillBg),
                new System.Windows.Media.Pen(new SolidColorBrush(pillBorder), 1.0),
                pillRect,
                pillH * 0.5,
                pillH * 0.5);

            dc.DrawText(ft, new Point(center.X - ft.Width * 0.5, center.Y - ft.Height * 0.5));
        }

        private void DrawTopHeaderPill(DrawingContext dc, bool isDark, ConverzeColorTheme theme)
        {
            if (_vm.Selection == null) return;

            string fileName = _vm.Selection.DisplayName;
            if (fileName.Length > 28) fileName = fileName.Substring(0, 25) + "...";
            string subText = $"{_vm.Selection.PrimaryExtension.ToUpperInvariant()} · {_vm.Selection.FormattedSize}";

            var titleFt = CreateFormattedText(fileName, 12.0, FontWeights.SemiBold, isDark ? Colors.White : Color.FromRgb(15, 23, 42));
            var subFt = CreateFormattedText(subText, 10.0, FontWeights.Medium, isDark ? Color.FromArgb(175, 255, 255, 255) : Color.FromArgb(165, 30, 41, 59));

            double textW = Math.Max(titleFt.Width, subFt.Width);
            double pillW = Math.Min(340.0, textW + 58.0);
            double pillH = 38.0;
            double pillX = WheelCenterX - pillW * 0.5;
            double pillY = 18.0;

            Color bg = isDark ? Color.FromArgb(235, 24, 26, 34) : Color.FromArgb(245, 255, 255, 255);
            Color border = isDark ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(30, 15, 23, 42);

            dc.DrawRoundedRectangle(
                new SolidColorBrush(bg),
                new System.Windows.Media.Pen(new SolidColorBrush(border), 1.0),
                new Rect(pillX, pillY, pillW, pillH),
                19.0, 19.0);

            var iconBmp = ConverzeAppIconLoader.LoadAppIconBitmap();
            if (iconBmp != null)
            {
                dc.DrawImage(iconBmp, new Rect(pillX + 10.0, pillY + 6.0, 26.0, 26.0));
            }
            else
            {
                dc.DrawEllipse(new SolidColorBrush(theme.PrimaryColor()), null, new Point(pillX + 23.0, pillY + 19.0), 11.0, 11.0);
            }

            dc.DrawText(titleFt, new Point(pillX + 44.0, pillY + 4.5));
            dc.DrawText(subFt, new Point(pillX + 44.0, pillY + 20.5));
        }

        private void DrawBottomHintPill(DrawingContext dc, bool isDark, ConverzeColorTheme theme)
        {
            var prefs = ConverzePreferences.Shared;
            var hovered = _vm.HoveredTarget;

            string text = hovered != null
                ? prefs.Tr(
                    $"Click sau eliberează pentru a {hovered.ActionDescription} · ESC închide",
                    $"Click or release to {hovered.ActionDescription} · ESC closes")
                : prefs.Tr(
                    "Selectează un format · ESC sau click în afară pentru a închide",
                    "Hover a format · ESC or click outside to close");

            Color textCol = hovered != null
                ? (isDark ? Colors.White : Color.FromRgb(15, 23, 42))
                : (isDark ? Color.FromArgb(190, 255, 255, 255) : Color.FromArgb(185, 30, 41, 59));

            var ft = CreateFormattedText(text, 11.0, FontWeights.SemiBold, textCol);
            double pillW = ft.Width + 28.0;
            double pillH = 28.0;
            double pillX = WheelCenterX - pillW * 0.5;
            double pillY = TotalControlHeight - 44.0;

            Color bg = isDark ? Color.FromArgb(232, 22, 24, 32) : Color.FromArgb(245, 255, 255, 255);
            Color border = hovered != null
                ? WithAlpha(theme.PrimaryColor(), 0.65)
                : (isDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(30, 15, 23, 42));

            dc.DrawRoundedRectangle(
                new SolidColorBrush(bg),
                new System.Windows.Media.Pen(new SolidColorBrush(border), 1.0),
                new Rect(pillX, pillY, pillW, pillH),
                14.0, 14.0);

            dc.DrawText(ft, new Point(WheelCenterX - ft.Width * 0.5, pillY + (pillH - ft.Height) * 0.5));
        }

        private static void DrawSparkBurst(DrawingContext dc, Point origin, double progress, ConverzeColorTheme theme)
        {
            const int sparkCount = 16;
            double ease = EaseOutCubic(progress);
            double alpha = 1.0 - progress;

            for (int i = 0; i < sparkCount; i++)
            {
                double angle = (2.0 * Math.PI * i) / sparkCount;
                double dist = 12.0 + 44.0 * ease * ((i % 2 == 0) ? 1.0 : 0.72);
                Point pt = new Point(
                    origin.X + dist * Math.Cos(angle),
                    origin.Y - dist * Math.Sin(angle));

                double r = (i % 2 == 0 ? 3.4 : 2.3) * (1.0 - progress * 0.65);
                Color col = (i % 3 == 0) ? Colors.White : ((i % 2 == 0) ? theme.LightStop() : theme.SparkSecondary());
                dc.DrawEllipse(new SolidColorBrush(WithAlpha(col, alpha)), null, pt, r, r);
            }
        }

        private FormattedText CreateFormattedText(string text, double fontSize, FontWeight fontWeight, Color color)
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            return new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, fontWeight, FontStretches.Normal),
                fontSize,
                new SolidColorBrush(color),
                dpi);
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

        private static double DampedSpring(double t, double damping, double frequency)
        {
            if (t <= 0) return 0.0;
            if (t > 1.2) return 1.0;
            double omega = frequency * 2.0 * Math.PI;
            double decay = Math.Exp(-damping * omega * t);
            double osc = Math.Cos(omega * Math.Sqrt(Math.Max(0.01, 1.0 - damping * damping)) * t);
            return Math.Clamp(1.0 - decay * osc, 0.0, 1.12);
        }

        private static double EaseOutCubic(double x)
        {
            double inv = 1.0 - Math.Clamp(x, 0.0, 1.0);
            return 1.0 - inv * inv * inv;
        }

        private static Color WithAlpha(Color c, double alpha)
        {
            byte a = (byte)Math.Clamp(alpha * 255.0, 0.0, 255.0);
            return Color.FromArgb(a, c.R, c.G, c.B);
        }
    }
}

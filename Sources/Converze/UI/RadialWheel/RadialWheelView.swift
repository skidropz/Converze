import SwiftUI
import AppKit

// MARK: - Translucent Glass Background

public struct VisualEffectGlassView: View {
    public init() {}

    public var body: some View {
        Rectangle()
            .fill(.ultraThinMaterial)
    }
}

// MARK: - 120Hz Physics-Driven Observable State for the Converze Radial Wheel

@MainActor
public final class RadialWheelViewModel: NSObject, ObservableObject {
    public enum AnimationPhase: Equatable {
        case hidden
        case opening
        case active
        case dropping
        case closing
    }

    @Published public var isVisible: Bool = false
    @Published public var phase: AnimationPhase = .hidden
    @Published public var targets: [WheelTarget] = []
    @Published public var selectedIndex: Int? = nil
    @Published public var selection: DraggedSelection? = nil
    @Published public var isKeyboardMode: Bool = false
    @Published public var droppingSliceIndex: Int? = nil

    // Explicit 120fps physics state (immune to NSHostingView frame-0 coalescing)
    @Published public var openElapsed: Double = 0.0
    @Published public var hoverProgress: [CGFloat] = []
    @Published public var dropProgress: CGFloat = 0.0
    @Published public var closeProgress: CGFloat = 0.0
    @Published public var shimmerAngle: Double = 0.0
    @Published public var pointerAngle: Double = .pi / 2.0

    private var hoverVelocities: [CGFloat] = []
    private var physicsTimer: Timer?
    private var lastTickTimestamp: TimeInterval = 0
    private var pendingCompletionAfterDrop: (() -> Void)?
    private var pendingHideAfterClose: (() -> Void)?

    public let metrics = ConverzeWheelMetrics(scale: 1.12)

    public var onSelectTarget: ((WheelTarget, DraggedSelection) -> Void)?
    public var onCancel: (() -> Void)?

    public override init() {
        super.init()
    }

    /// Prepare conversion targets and reset animation state to frame 0
    public func configure(
        selection: DraggedSelection,
        keyboardMode: Bool = false,
        startAnimationImmediately: Bool = true
    ) {
        self.selection = selection
        self.isKeyboardMode = keyboardMode
        self.droppingSliceIndex = nil
        self.targets = Self.resolveTargets(for: selection)
        self.selectedIndex = keyboardMode && !targets.isEmpty ? 0 : nil

        let count = self.targets.count
        self.hoverProgress = Array(repeating: 0.0, count: count)
        self.hoverVelocities = Array(repeating: 0.0, count: count)
        if let sel = self.selectedIndex, sel < count {
            self.pointerAngle = Double(ConverzeWheelMetrics.centerAngle(index: sel, count: count))
        }

        self.openElapsed = 0.0
        self.dropProgress = 0.0
        self.closeProgress = 0.0
        self.phase = .opening

        if startAnimationImmediately {
            startPhysicsLoop()
        }
    }

    public func startPhysicsLoop() {
        lastTickTimestamp = CACurrentMediaTime()
        if physicsTimer == nil {
            let timer = Timer(
                timeInterval: 1.0 / 120.0,
                target: self,
                selector: #selector(handlePhysicsTimer(_:)),
                userInfo: nil,
                repeats: true
            )
            RunLoop.main.add(timer, forMode: .common)
            physicsTimer = timer
        }
    }

    public func stopPhysicsLoop() {
        physicsTimer?.invalidate()
        physicsTimer = nil
    }

    @objc private func handlePhysicsTimer(_ timer: Timer) {
        tickPhysics()
    }

    private func tickPhysics() {
        let now = CACurrentMediaTime()
        let dt = min(0.033, max(0.001, now - lastTickTimestamp))
        lastTickTimestamp = now

        // 1. Continuous iridescent rim rotation
        shimmerAngle = (shimmerAngle + dt * 52.0).truncatingRemainder(dividingBy: 360.0)

        // 2. Opening staggered bloom progression
        if phase == .opening {
            openElapsed += dt
            let totalDuration = 0.55 + Double(max(0, targets.count)) * 0.030
            if openElapsed >= totalDuration {
                phase = .active
            }
        } else if phase == .active {
            openElapsed += dt
        }

        // 3. Per-slice underdamped spring hover interpolation
        let count = targets.count
        if hoverProgress.count == count && hoverVelocities.count == count {
            let stiffness: CGFloat = 340.0
            let damping: CGFloat = 22.0
            let stepDt = CGFloat(dt)

            for i in 0..<count {
                let targetVal: CGFloat = (selectedIndex == i) ? 1.0 : 0.0
                let displacement = hoverProgress[i] - targetVal
                let accel = -stiffness * displacement - damping * hoverVelocities[i]
                hoverVelocities[i] += accel * stepDt
                hoverProgress[i] += hoverVelocities[i] * stepDt
            }
        }

        // 4. Smoothly interpolate orbital pointer dot angle toward selected slice
        if let sel = selectedIndex, count > 0 {
            let targetTheta = Double(ConverzeWheelMetrics.centerAngle(index: sel, count: count))
            let diff = atan2(sin(targetTheta - pointerAngle), cos(targetTheta - pointerAngle))
            pointerAngle += diff * min(1.0, dt * 24.0)
        }

        // 5. Dropping celebratory burst animation
        if phase == .dropping {
            dropProgress = min(1.0, dropProgress + CGFloat(dt / 0.28))
            if dropProgress >= 1.0 {
                let completion = pendingCompletionAfterDrop
                pendingCompletionAfterDrop = nil
                stopPhysicsLoop()
                completion?()
            }
        }

        // 6. Closing swirl animation
        if phase == .closing {
            closeProgress = min(1.0, closeProgress + CGFloat(dt / 0.18))
            if closeProgress >= 1.0 {
                let completion = pendingHideAfterClose
                pendingHideAfterClose = nil
                phase = .hidden
                stopPhysicsLoop()
                completion?()
            }
        }
    }

    // MARK: - Analytical Underdamped Spring Curves

    public static func evaluateUnderdampedSpring(t: Double, omega0: Double = 18.5, zeta: Double = 0.54) -> CGFloat {
        guard t > 0 else { return 0.0 }
        if t > 0.75 { return 1.0 }
        let omegaD = omega0 * sqrt(max(0.001, 1.0 - zeta * zeta))
        let decay = exp(-zeta * omega0 * t)
        let osc = cos(omegaD * t) + (zeta / sqrt(max(0.001, 1.0 - zeta * zeta))) * sin(omegaD * t)
        return CGFloat(1.0 - decay * osc)
    }

    public var ringSpringScale: CGFloat {
        let openS = Self.evaluateUnderdampedSpring(t: openElapsed, omega0: 17.5, zeta: 0.58)
        let base = 0.18 + 0.82 * openS
        if phase == .dropping {
            return base * (1.0 - 0.18 * dropProgress)
        }
        if phase == .closing {
            return base * max(0.1, 1.0 - 0.82 * closeProgress)
        }
        return base
    }

    public var ringOpacity: Double {
        let openAlpha = min(1.0, max(0.0, openElapsed / 0.14))
        if phase == .dropping {
            return openAlpha * Double(max(0.0, 1.0 - dropProgress * 1.15))
        }
        if phase == .closing {
            return openAlpha * Double(max(0.0, 1.0 - closeProgress))
        }
        return openAlpha
    }

    public func petalSpring(for index: Int) -> CGFloat {
        let delay = Double(index) * 0.028
        let localT = openElapsed - delay
        let openVal = Self.evaluateUnderdampedSpring(t: localT, omega0: 19.0, zeta: 0.52)
        if phase == .closing {
            return openVal * max(0.0, 1.0 - closeProgress * 1.1)
        }
        if phase == .dropping && droppingSliceIndex != index {
            return openVal * max(0.0, 1.0 - dropProgress * 1.25)
        }
        return openVal
    }

    public func petalOpacity(for index: Int) -> Double {
        let delay = Double(index) * 0.028
        let localT = openElapsed - delay
        let openAlpha = min(1.0, max(0.0, localT / 0.11))
        if phase == .closing {
            return openAlpha * Double(max(0.0, 1.0 - closeProgress))
        }
        if phase == .dropping {
            if droppingSliceIndex == index {
                return openAlpha * Double(max(0.0, 1.0 - max(0.0, (dropProgress - 0.55) / 0.45)))
            } else {
                return openAlpha * Double(max(0.0, 1.0 - dropProgress * 1.35))
            }
        }
        return openAlpha
    }

    public static func resolveTargets(for selection: DraggedSelection) -> [WheelTarget] {
        let formats = FormatMatrix.conversionTargets(
            for: selection.primaryExtension,
            isDirectory: selection.isDirectory
        )
        return formats.map { .format($0) }
    }

    public func updateCursor(dx: CGFloat, dy: CGFloat) {
        guard phase != .dropping && phase != .closing else { return }
        let hit = metrics.hitTestSlice(dx: dx, dy: dy, count: targets.count)
        if hit != selectedIndex {
            selectedIndex = hit
            if hit != nil {
                FeedbackSoundPlayer.shared.playHighlightTick()
            }
        }
    }

    public func moveSelection(by delta: Int) {
        guard !targets.isEmpty, phase != .dropping, phase != .closing else { return }
        let current = selectedIndex ?? 0
        let next = (current + delta + targets.count) % targets.count
        if next != selectedIndex {
            selectedIndex = next
            FeedbackSoundPlayer.shared.playHighlightTick()
        }
    }

    public var currentlyHoveredTarget: WheelTarget? {
        guard let idx = selectedIndex, idx >= 0, idx < targets.count else { return nil }
        return targets[idx]
    }

    public func triggerSelected() {
        guard phase != .dropping,
              let idx = selectedIndex,
              idx >= 0, idx < targets.count,
              let sel = selection else { return }

        let target = targets[idx]
        droppingSliceIndex = idx
        dropProgress = 0.0
        phase = .dropping
        FeedbackSoundPlayer.shared.playCompletionPop()
        startPhysicsLoop()

        pendingCompletionAfterDrop = { [weak self] in
            self?.onSelectTarget?(target, sel)
        }
    }

    public func animateClose(completion: @escaping () -> Void) {
        guard phase != .closing && phase != .hidden else {
            completion()
            return
        }
        closeProgress = 0.0
        phase = .closing
        pendingHideAfterClose = completion
        startPhysicsLoop()
    }
}

// MARK: - Subtle Tactile Feedback

public final class FeedbackSoundPlayer {
    public static let shared = FeedbackSoundPlayer()
    private init() {}

    @MainActor
    public func playHighlightTick() {
        guard ConverzePreferences.shared.hapticsEnabled else { return }
        NSHapticFeedbackManager.defaultPerformer.perform(.alignment, performanceTime: .now)
    }

    @MainActor
    public func playCompletionPop() {
        guard ConverzePreferences.shared.hapticsEnabled else { return }
        NSHapticFeedbackManager.defaultPerformer.perform(.levelChange, performanceTime: .now)
    }
}

// MARK: - Celebratory 16-Spark Particle Burst Rendered Directly on the Selected Slice

public struct WheelSliceSparkBurstView: View {
    public let progress: CGFloat
    public let theme: ConverzeColorTheme

    public init(progress: CGFloat, theme: ConverzeColorTheme = .sapphire) {
        self.progress = progress
        self.theme = theme
    }

    public var body: some View {
        let ringStrokeWidth: CGFloat = max(1.0, 5.5 * (1.0 - progress))
        let ringScale: CGFloat = 0.30 + progress * 1.65
        let ringAlpha: Double = Double(max(0.0, 1.0 - progress))

        ZStack {
            Circle()
                .stroke(
                    LinearGradient(
                        colors: [theme.primaryColor, theme.lightStop],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    ),
                    lineWidth: ringStrokeWidth
                )
                .frame(width: 68, height: 68)
                .scaleEffect(ringScale)
                .opacity(ringAlpha)

            ForEach(0..<16, id: \.self) { i in
                sparkDot(index: i)
            }
        }
        .allowsHitTesting(false)
    }

    private func sparkDot(index: Int) -> some View {
        let angle = Double(index) * (.pi * 2.0 / 16.0)
        let dist = Double(10.0 + progress * 74.0)
        let dx = CGFloat(cos(angle) * dist)
        let dy = CGFloat(sin(angle) * dist)
        let isEven = index.isMultiple(of: 2)
        let fillColor: Color = isEven ? theme.primaryColor : theme.sparkSecondary
        let dotSize: CGFloat = isEven ? 8.5 : 6.0
        let dotScale: CGFloat = max(0.1, 1.2 - progress * 0.95)
        let dotAlpha: Double = Double(max(0.0, 1.0 - progress * 0.92))

        return Circle()
            .fill(fillColor)
            .frame(width: dotSize, height: dotSize)
            .scaleEffect(dotScale)
            .offset(x: dx, y: dy)
            .opacity(dotAlpha)
    }
}

// MARK: - SwiftUI Converze Radial Wheel View (White Mode / Dark Mode + User Color Palette)

public struct RadialWheelView: View {
    @ObservedObject public var viewModel: RadialWheelViewModel
    @ObservedObject public var prefs = ConverzePreferences.shared

    /// Dynamic theme color accessors
    public static var accentPrimary: Color { ConverzePreferences.shared.colorTheme.primaryColor }
    public static var accentLight: Color { ConverzePreferences.shared.colorTheme.lightStop }

    public init(viewModel: RadialWheelViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        let metrics = viewModel.metrics
        let size = metrics.diameter
        let rScale = max(0.01, viewModel.ringSpringScale)
        let rAlpha = viewModel.ringOpacity
        let isDark = prefs.isDarkMode
        let theme = prefs.colorTheme

        VStack(spacing: 10) {
            topHeaderPill(isDark: isDark, theme: theme)
                .frame(height: 36)
                .scaleEffect(rScale)
                .opacity(rAlpha)

            ZStack {
                if viewModel.targets.isEmpty {
                    emptyWheelCard(isDark: isDark, theme: theme)
                        .frame(width: 262, height: size)
                        .scaleEffect(rScale)
                        .opacity(rAlpha)
                } else {
                    wheelCanvas(
                        metrics: metrics,
                        size: size,
                        rScale: rScale,
                        rAlpha: rAlpha,
                        isDark: isDark,
                        theme: theme
                    )
                    .frame(width: size, height: size)
                }
            }

            statusCaptionPill(isDark: isDark, theme: theme)
                .frame(height: 36)
                .scaleEffect(rScale)
                .opacity(rAlpha)
        }
        .padding(14)
    }

    // MARK: - Top Header Pill (`Converze · filename.ext`)

    private func topHeaderPill(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let fileName: String = viewModel.selection?.displayName ?? "Converze"
        let sourceExt: String = viewModel.selection?.primaryExtension.uppercased() ?? ""
        let titleColor: Color = isDark ? .white : Color(red: 0.10, green: 0.12, blue: 0.16)
        let badgeBg: Color = theme.primaryColor.opacity(isDark ? 0.24 : 0.14)
        let pillBg: Color = isDark
            ? Color(red: 0.11, green: 0.13, blue: 0.18).opacity(0.95)
            : Color.white.opacity(0.96)
        let pillStroke: Color = isDark ? Color.white.opacity(0.18) : Color.black.opacity(0.08)
        let shadowColor: Color = Color.black.opacity(isDark ? 0.42 : 0.15)

        return HStack(spacing: 8) {
            ConverzeAppIconView(size: 20)

            Text(fileName)
                .font(.system(size: 12, weight: .bold, design: .rounded))
                .foregroundColor(titleColor)
                .lineLimit(1)

            if !sourceExt.isEmpty {
                Text(sourceExt)
                    .font(.system(size: 10, weight: .heavy, design: .rounded))
                    .foregroundColor(theme.primaryColor)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Capsule().fill(badgeBg))
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 7)
        .background(Capsule().fill(pillBg))
        .overlay(Capsule().stroke(pillStroke, lineWidth: 1))
        .shadow(color: shadowColor, radius: 10, x: 0, y: 4)
    }

    // MARK: - Radial Wheel Canvas

    @ViewBuilder
    private func wheelCanvas(
        metrics: ConverzeWheelMetrics,
        size: CGFloat,
        rScale: CGFloat,
        rAlpha: Double,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let count = viewModel.targets.count
        let center = CGPoint(x: size / 2.0, y: size / 2.0)
        let shockProgress = min(1.0, max(0.0, CGFloat(viewModel.openElapsed / 0.52)))

        ZStack {
            // 0. Expanding Opening Shockwave Halo
            if shockProgress < 1.0 {
                shockwaveHaloView(metrics: metrics, shockProgress: shockProgress, theme: theme)
            }

            // 1. Frosted Glass Donut Backdrop Ring + Rotating Iridescent Rim
            donutBackdropView(
                metrics: metrics,
                size: size,
                rScale: rScale,
                rAlpha: rAlpha,
                isDark: isDark,
                theme: theme
            )

            // 2. Choreographed Staggered Petals (120Hz Physics-Driven)
            ForEach(Array(viewModel.targets.enumerated()), id: \.element.id) { index, target in
                petalView(
                    index: index,
                    count: count,
                    target: target,
                    metrics: metrics,
                    size: size,
                    isDark: isDark,
                    theme: theme
                )
            }

            // 3. Celebratory Spark Burst when a Slice is Selected / Dropped!
            if viewModel.phase == .dropping,
               let dropIdx = viewModel.droppingSliceIndex,
               dropIdx < count {
                let sliceOffset = metrics.labelCenterOffset(index: dropIdx, count: count)
                WheelSliceSparkBurstView(progress: viewModel.dropProgress, theme: theme)
                    .position(x: center.x + sliceOffset.x, y: center.y + sliceOffset.y)
                    .zIndex(25)
            }

            // 4. Orbiting Pointer Dot on Inner Ring Aimed at Hovered Slice
            if viewModel.selectedIndex != nil && count > 0 {
                orbitingPointerDot(metrics: metrics, rAlpha: rAlpha, theme: theme)
            }

            // 5. Center Cutout Pill Badge (`2.2 MB` / `→ WEBP`)
            centerBadgePill(isDark: isDark, theme: theme)
                .scaleEffect(rScale)
                .opacity(rAlpha)
                .zIndex(16)
        }
    }

    private func shockwaveHaloView(
        metrics: ConverzeWheelMetrics,
        shockProgress: CGFloat,
        theme: ConverzeColorTheme
    ) -> some View {
        let strokeWidth: CGFloat = max(0.5, 4.0 * (1.0 - shockProgress))
        let diameter: CGFloat = metrics.backdropRadius * 2.0
        let haloScale: CGFloat = 0.42 + shockProgress * 0.92
        let haloAlpha: Double = Double(1.0 - shockProgress)

        return Circle()
            .stroke(
                LinearGradient(
                    colors: [theme.primaryColor.opacity(0.85), theme.lightStop.opacity(0.45)],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                ),
                lineWidth: strokeWidth
            )
            .frame(width: diameter, height: diameter)
            .scaleEffect(haloScale)
            .opacity(haloAlpha)
    }

    private func donutBackdropView(
        metrics: ConverzeWheelMetrics,
        size: CGFloat,
        rScale: CGFloat,
        rAlpha: Double,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let hasSelection: Bool = (viewModel.selectedIndex != nil)
        let tintColor: Color = isDark
            ? Color(red: 0.07, green: 0.09, blue: 0.14).opacity(0.76)
            : Color(red: 0.88, green: 0.91, blue: 0.95).opacity(0.62)
        let glassOpacity: Double = isDark ? 0.82 : 0.90
        let rimWhiteA: Color = isDark ? Color.white.opacity(0.35) : Color.white.opacity(0.85)
        let rimWhiteB: Color = isDark ? Color.white.opacity(0.45) : Color.white.opacity(0.90)
        let rimColors: [Color] = [
            rimWhiteA,
            theme.primaryColor.opacity(0.75),
            rimWhiteB,
            theme.lightStop.opacity(0.75),
            rimWhiteA
        ]
        let idleInnerStroke: Color = isDark ? Color.white.opacity(0.22) : Color.white.opacity(0.65)
        let innerStrokeColor: Color = hasSelection ? theme.primaryColor.opacity(0.80) : idleInnerStroke
        let innerStrokeWidth: CGFloat = hasSelection ? 2.2 : 1.1
        let activeShadow: Color = theme.primaryColor.opacity(isDark ? 0.42 : 0.30)
        let idleShadow: Color = Color.black.opacity(isDark ? 0.50 : 0.22)
        let ringShadowColor: Color = hasSelection ? activeShadow : idleShadow

        return ZStack {
            tintColor
            VisualEffectGlassView()
                .opacity(glassOpacity)
        }
        .frame(width: size, height: size)
        .clipShape(
            DonutRingShape(
                innerRadius: metrics.holeRadius,
                outerRadius: metrics.backdropRadius
            ),
            style: FillStyle(eoFill: true)
        )
        .overlay(
            Circle()
                .stroke(
                    AngularGradient(
                        gradient: Gradient(colors: rimColors),
                        center: .center,
                        angle: .degrees(viewModel.shimmerAngle)
                    ),
                    lineWidth: 2.2
                )
                .frame(width: metrics.backdropRadius * 2.0, height: metrics.backdropRadius * 2.0)
        )
        .overlay(
            Circle()
                .stroke(innerStrokeColor, lineWidth: innerStrokeWidth)
                .frame(width: metrics.holeRadius * 2.0, height: metrics.holeRadius * 2.0)
        )
        .shadow(color: ringShadowColor, radius: 20, x: 0, y: 8)
        .scaleEffect(rScale)
        .opacity(rAlpha)
    }

    private func orbitingPointerDot(
        metrics: ConverzeWheelMetrics,
        rAlpha: Double,
        theme: ConverzeColorTheme
    ) -> some View {
        let dotOffset = ConverzeWheelMetrics.polarOffset(
            radius: metrics.holeRadius,
            angle: CGFloat(viewModel.pointerAngle)
        )
        return Circle()
            .fill(theme.primaryColor)
            .frame(width: 10, height: 10)
            .overlay(Circle().stroke(Color.white, lineWidth: 1.8))
            .shadow(color: theme.primaryColor.opacity(0.85), radius: 6)
            .offset(x: dotOffset.x, y: dotOffset.y)
            .opacity(rAlpha)
            .zIndex(15)
    }

    // MARK: - Single Physics-Driven Petal Slice (Perfectly Centered Label in Slice)

    private func petalView(
        index: Int,
        count: Int,
        target: WheelTarget,
        metrics: ConverzeWheelMetrics,
        size: CGFloat,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let shape = ConverzePetalShape(index: index, count: count, metrics: metrics)
        let labelOffset = metrics.labelCenterOffset(index: index, count: count)
        let theta = ConverzeWheelMetrics.centerAngle(index: index, count: count)

        let pSpring = viewModel.petalSpring(for: index)
        let pAlpha = viewModel.petalOpacity(for: index)
        let hProg: CGFloat = (index < viewModel.hoverProgress.count) ? viewModel.hoverProgress[index] : 0.0
        let isHighlighted: Bool = (viewModel.selectedIndex == index)
        let isActiveAppearance: Bool = isHighlighted || (hProg > 0.35)
        let isDroppingThis: Bool = (viewModel.phase == .dropping && viewModel.droppingSliceIndex == index)

        var neighborShift: CGFloat = 0.0
        if let sel = viewModel.selectedIndex, count > 2, sel != index {
            let selHover: CGFloat = (sel < viewModel.hoverProgress.count) ? viewModel.hoverProgress[sel] : 0.0
            if (sel + 1) % count == index {
                neighborShift = 2.8 * selHover
            } else if (sel - 1 + count) % count == index {
                neighborShift = -2.8 * selHover
            }
        }

        let entryPull: CGFloat = -72.0 * (1.0 - pSpring)
        let dropExtraPush: CGFloat = isDroppingThis ? (20.0 * viewModel.dropProgress) : 0.0
        let hoverPush: CGFloat = 11.5 * hProg + dropExtraPush
        let radialVec = ConverzeWheelMetrics.polarOffset(radius: entryPull + hoverPush, angle: theta)
        let swirlDegrees = Double(-56.0 * (1.0 - pSpring) + neighborShift)
        let baseScale: CGFloat = max(0.01, 0.08 + 0.92 * pSpring)
        let dropScaleBoost: CGFloat = isDroppingThis ? (0.16 * sin(viewModel.dropProgress * .pi)) : 0.0
        let hoverScale: CGFloat = 1.0 + 0.09 * hProg + dropScaleBoost
        let combinedScale: CGFloat = baseScale * hoverScale

        let inactiveColors: [Color] = isDark
            ? [
                Color(red: 0.17, green: 0.20, blue: 0.28).opacity(0.95),
                Color(red: 0.12, green: 0.15, blue: 0.22).opacity(0.90),
                Color(red: 0.09, green: 0.11, blue: 0.17).opacity(0.94)
            ]
            : [
                Color.white.opacity(0.97),
                Color(red: 0.95, green: 0.97, blue: 0.99).opacity(0.88),
                Color(red: 0.91, green: 0.94, blue: 0.97).opacity(0.93)
            ]

        let sliceFill: AnyShapeStyle = isActiveAppearance
            ? AnyShapeStyle(
                LinearGradient(
                    colors: [theme.lightStop, theme.primaryColor, theme.deepStop],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            )
            : AnyShapeStyle(
                RadialGradient(
                    colors: inactiveColors,
                    center: .center,
                    startRadius: metrics.innerRadius * 0.7,
                    endRadius: metrics.outerRadius
                )
            )

        let idleBorder: Color = isDark
            ? Color.white.opacity(0.16)
            : Color(red: 0.36, green: 0.40, blue: 0.46).opacity(0.24)
        let borderColor: Color = isHighlighted ? Color.white.opacity(0.92) : idleBorder
        let borderWidth: CGFloat = isHighlighted ? 2.0 : 0.9

        let idleShadow: Color = Color.black.opacity(isDark ? 0.35 : 0.09)
        let shadowColor: Color = isHighlighted ? theme.primaryColor.opacity(0.62) : idleShadow
        let shadowRadius: CGFloat = isHighlighted ? 16.0 : 4.0
        let shadowY: CGFloat = isHighlighted ? 6.0 : 2.0

        let idleLabelColor: Color = isDark
            ? Color(red: 0.94, green: 0.96, blue: 0.99)
            : Color(red: 0.09, green: 0.11, blue: 0.15)
        let labelColor: Color = isActiveAppearance ? .white : idleLabelColor
        let labelShadow: Color = isHighlighted ? Color.black.opacity(0.22) : .clear
        let labelScale: CGFloat = 1.0 + 0.14 * hProg
        let sliceZIndex: Double = isHighlighted ? 12.0 : 1.0

        return ZStack(alignment: .center) {
            shape
                .fill(sliceFill)
                .overlay(shape.stroke(borderColor, lineWidth: borderWidth))
                .shadow(color: shadowColor, radius: shadowRadius, x: 0, y: shadowY)

            Text(target.wheelLabel)
                .font(.system(size: 14.5, weight: .heavy, design: .rounded))
                .tracking(0.3)
                .foregroundColor(labelColor)
                .multilineTextAlignment(.center)
                .fixedSize()
                .shadow(color: labelShadow, radius: 2, x: 0, y: 1)
                .scaleEffect(labelScale)
                .offset(x: labelOffset.x, y: labelOffset.y)
        }
        .frame(width: size, height: size)
        .scaleEffect(combinedScale)
        .rotationEffect(.degrees(swirlDegrees))
        .offset(x: radialVec.x, y: radialVec.y)
        .opacity(pAlpha)
        .zIndex(sliceZIndex)
        .contentShape(shape)
        .onTapGesture {
            viewModel.selectedIndex = index
            viewModel.triggerSelected()
        }
    }

    // MARK: - Center Cutout Pill Badge (`2.2 MB` / `→ WEBP`)

    private func centerBadgePill(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let isHighlighted: Bool = (viewModel.selectedIndex != nil)
        let centerText: String
        if let idx = viewModel.selectedIndex, idx >= 0, idx < viewModel.targets.count {
            centerText = "→ \(viewModel.targets[idx].wheelLabel)"
        } else {
            centerText = viewModel.selection?.formattedSize ?? prefs.tr("Pregătit", "Ready")
        }

        let idleTextColor: Color = isDark ? Color.white.opacity(0.92) : Color.black.opacity(0.88)
        let textColor: Color = isHighlighted ? .white : idleTextColor

        let idleBgColor: Color = isDark
            ? Color(red: 0.13, green: 0.16, blue: 0.23).opacity(0.96)
            : Color.white.opacity(0.96)
        let pillFill: AnyShapeStyle = isHighlighted
            ? AnyShapeStyle(
                LinearGradient(
                    colors: [theme.primaryColor, theme.deepStop],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            )
            : AnyShapeStyle(idleBgColor)

        let idleStroke: Color = isDark ? Color.white.opacity(0.22) : Color.white.opacity(0.92)
        let strokeColor: Color = isHighlighted ? Color.white.opacity(0.90) : idleStroke
        let idleShadow: Color = Color.black.opacity(isDark ? 0.40 : 0.14)
        let shadowColor: Color = isHighlighted ? theme.primaryColor.opacity(0.50) : idleShadow
        let shadowRadius: CGFloat = isHighlighted ? 8.0 : 5.0
        let pillScale: CGFloat = isHighlighted ? 1.10 : 1.0

        return Text(centerText)
            .font(.system(size: 12.5, weight: .heavy, design: .rounded))
            .tracking(0.3)
            .foregroundColor(textColor)
            .multilineTextAlignment(.center)
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .frame(minWidth: 78, minHeight: 34)
            .background(Capsule().fill(pillFill))
            .overlay(Capsule().stroke(strokeColor, lineWidth: 1.4))
            .shadow(color: shadowColor, radius: shadowRadius, x: 0, y: 2)
            .scaleEffect(pillScale)
    }

    // MARK: - Empty Wheel Card

    private func emptyWheelCard(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let message: String = prefs.tr(
            "Nu există conversii disponibile pentru acest tip de fișier.",
            "No conversions available for this file format."
        )
        let textColor: Color = isDark ? Color.white.opacity(0.75) : Color(red: 0.35, green: 0.38, blue: 0.44)
        let cardBg: Color = isDark
            ? Color(red: 0.11, green: 0.13, blue: 0.18).opacity(0.95)
            : Color.white.opacity(0.96)
        let cardStroke: Color = isDark ? Color.white.opacity(0.15) : Color.black.opacity(0.08)
        let shadowColor: Color = Color.black.opacity(isDark ? 0.45 : 0.14)

        return VStack(spacing: 8) {
            Image(systemName: "questionmark.circle")
                .font(.system(size: 24))
                .foregroundColor(theme.primaryColor)
            Text(message)
                .font(.system(size: 12.5, weight: .medium, design: .rounded))
                .foregroundColor(textColor)
                .multilineTextAlignment(.center)
                .lineSpacing(3)
        }
        .padding(22)
        .background(RoundedRectangle(cornerRadius: 18, style: .continuous).fill(cardBg))
        .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(cardStroke, lineWidth: 1))
        .shadow(color: shadowColor, radius: 14, x: 0, y: 6)
    }

    // MARK: - Bottom Status Hint

    private func statusCaptionPill(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let caption: String
        if let idx = viewModel.selectedIndex, idx >= 0, idx < viewModel.targets.count {
            let target = viewModel.targets[idx]
            caption = prefs.tr(
                "Eliberează sau dă click ca să \(target.actionDescription)",
                "Drop or click to \(target.actionDescription)"
            )
        } else {
            caption = prefs.tr(
                "Alege o extensie · ESC sau click în afară pentru închidere",
                "Select an extension · ESC or click outside to close"
            )
        }

        let idleTextColor: Color = isDark ? Color.white.opacity(0.82) : Color(red: 0.22, green: 0.25, blue: 0.30)
        let textColor: Color = (viewModel.selectedIndex != nil) ? theme.primaryColor : idleTextColor
        let pillBg: Color = isDark
            ? Color(red: 0.11, green: 0.13, blue: 0.18).opacity(0.95)
            : Color.white.opacity(0.95)
        let pillStroke: Color = isDark ? Color.white.opacity(0.18) : Color.black.opacity(0.08)
        let shadowColor: Color = Color.black.opacity(isDark ? 0.40 : 0.12)

        return Text(caption)
            .font(.system(size: 11.5, weight: .semibold, design: .rounded))
            .foregroundColor(textColor)
            .padding(.horizontal, 14)
            .padding(.vertical, 6)
            .background(Capsule().fill(pillBg))
            .overlay(Capsule().stroke(pillStroke, lineWidth: 1))
            .shadow(color: shadowColor, radius: 6, x: 0, y: 2)
    }
}

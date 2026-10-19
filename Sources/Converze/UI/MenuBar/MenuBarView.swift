import AppKit
import SwiftUI
import ServiceManagement

// MARK: - Live Interactive Mini Radial Wheel Preview (Perfectly Centered Slice Labels)

public struct MiniWheelPreviewView: View {
    @ObservedObject public var prefs = ConverzePreferences.shared
    @State private var hoveredIdx: Int = 1

    private let previewFormats = ["PNG", "WEBP", "PDF", "JPG", "HEIC", "AVIF"]
    private let metrics = ConverzeWheelMetrics(scale: 0.66)

    public init() {}

    public var body: some View {
        let isDark: Bool = prefs.isDarkMode
        let theme: ConverzeColorTheme = prefs.colorTheme
        let size: CGFloat = metrics.diameter
        let count: Int = previewFormats.count
        let ringFill: Color = isDark
            ? Color(red: 0.08, green: 0.10, blue: 0.16).opacity(0.88)
            : Color(red: 0.90, green: 0.93, blue: 0.97).opacity(0.84)
        let ringShadow: Color = theme.primaryColor.opacity(isDark ? 0.35 : 0.20)

        ZStack(alignment: .center) {
            // 1. Frosted Glass Donut Ring (Exact `size x size` frame)
            ZStack(alignment: .center) {
                ringFill
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
                        LinearGradient(
                            colors: [theme.lightStop.opacity(0.85), theme.primaryColor.opacity(0.85)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ),
                        lineWidth: 2
                    )
                    .frame(width: metrics.backdropRadius * 2.0, height: metrics.backdropRadius * 2.0)
            )
            .shadow(color: ringShadow, radius: 14, x: 0, y: 6)

            // 2. 6 Preview Slices — each in an identical `size x size` ZStack so shape and text share the exact same origin
            ForEach(0..<count, id: \.self) { index in
                previewSliceView(
                    index: index,
                    count: count,
                    size: size,
                    isDark: isDark,
                    theme: theme
                )
            }

            // 3. Center Pill (Centered at `(0, 0)` of the `size x size` wheel)
            centerPillView(theme: theme)
                .zIndex(10)
        }
        .frame(width: size, height: size)
        .padding(10)
    }

    private func previewSliceView(
        index: Int,
        count: Int,
        size: CGFloat,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let shape = ConverzePetalShape(index: index, count: count, metrics: metrics)
        let labelOffset = metrics.labelCenterOffset(index: index, count: count)
        let theta = ConverzeWheelMetrics.centerAngle(index: index, count: count)
        let isSelected: Bool = (hoveredIdx == index)
        let pushRadius: CGFloat = isSelected ? 6.0 : 0.0
        let push = ConverzeWheelMetrics.polarOffset(radius: pushRadius, angle: theta)

        let idleSliceColor: Color = isDark
            ? Color(red: 0.16, green: 0.19, blue: 0.27).opacity(0.95)
            : Color.white.opacity(0.97)
        let sliceFill: AnyShapeStyle = isSelected
            ? AnyShapeStyle(
                LinearGradient(
                    colors: [theme.lightStop, theme.primaryColor, theme.deepStop],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            )
            : AnyShapeStyle(idleSliceColor)

        let idleBorder: Color = isDark ? Color.white.opacity(0.16) : Color.black.opacity(0.12)
        let borderColor: Color = isSelected ? Color.white.opacity(0.92) : idleBorder
        let borderWidth: CGFloat = isSelected ? 1.6 : 0.85
        let shadowColor: Color = isSelected ? theme.primaryColor.opacity(0.55) : Color.black.opacity(0.08)
        let shadowRadius: CGFloat = isSelected ? 9.0 : 2.0

        let idleText: Color = isDark ? Color.white.opacity(0.92) : Color(red: 0.10, green: 0.12, blue: 0.16)
        let textColor: Color = isSelected ? .white : idleText
        let textScale: CGFloat = isSelected ? 1.08 : 1.0
        let sliceZ: Double = isSelected ? 5.0 : 1.0

        return ZStack(alignment: .center) {
            shape
                .fill(sliceFill)
                .overlay(shape.stroke(borderColor, lineWidth: borderWidth))
                .shadow(color: shadowColor, radius: shadowRadius, x: 0, y: 2)

            Text(previewFormats[index])
                .font(.system(size: 10.5, weight: .heavy, design: .rounded))
                .tracking(0.2)
                .foregroundColor(textColor)
                .multilineTextAlignment(.center)
                .fixedSize()
                .scaleEffect(textScale)
                .offset(x: labelOffset.x, y: labelOffset.y)
        }
        .frame(width: size, height: size)
        .offset(x: push.x, y: push.y)
        .zIndex(sliceZ)
        .contentShape(shape)
        .onTapGesture {
            withAnimation(.spring(response: 0.25, dampingFraction: 0.65)) {
                hoveredIdx = index
            }
        }
    }

    private func centerPillView(theme: ConverzeColorTheme) -> some View {
        let label = "→ \(previewFormats[hoveredIdx])"
        return Text(label)
            .font(.system(size: 9.5, weight: .heavy, design: .rounded))
            .foregroundColor(.white)
            .multilineTextAlignment(.center)
            .fixedSize()
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(
                Capsule()
                    .fill(
                        LinearGradient(
                            colors: [theme.primaryColor, theme.deepStop],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
            )
            .overlay(Capsule().stroke(Color.white.opacity(0.88), lineWidth: 1))
            .shadow(color: theme.primaryColor.opacity(0.45), radius: 5)
    }
}

// MARK: - First-Launch Welcome & Setup Wizard (`ConverzeOnboardingView`)

public struct ConverzeOnboardingView: View {
    @ObservedObject public var prefs = ConverzePreferences.shared
    @State private var currentStep: Int = 0
    @State private var launchAtLogin: Bool = (SMAppService.mainApp.status == .enabled)

    public var onFinish: () -> Void

    public init(onFinish: @escaping () -> Void) {
        self.onFinish = onFinish
    }

    public var body: some View {
        let isDark: Bool = prefs.isDarkMode
        let theme: ConverzeColorTheme = prefs.colorTheme
        let mainBg: Color = isDark
            ? Color(red: 0.10, green: 0.12, blue: 0.18)
            : Color(red: 0.98, green: 0.99, blue: 1.00)

        HStack(spacing: 0) {
            leftSidebarPreview(isDark: isDark, theme: theme)
                .frame(width: 270)

            Divider()

            rightWizardContent(isDark: isDark, theme: theme)
        }
        .frame(width: 750, height: 490)
        .background(mainBg)
        .preferredColorScheme(prefs.preferredColorScheme)
    }

    private func leftSidebarPreview(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let sidebarBg: Color = isDark
            ? Color(red: 0.07, green: 0.09, blue: 0.14)
            : Color(red: 0.94, green: 0.96, blue: 0.99)
        let titleColor: Color = isDark ? .white : Color(red: 0.09, green: 0.11, blue: 0.15)

        return VStack(spacing: 14) {
            // App Icon & Centered Brand Header
            VStack(spacing: 6) {
                ConverzeAppIconView(size: 52)

                Text("Converze")
                    .font(.system(size: 20, weight: .heavy, design: .rounded))
                    .foregroundColor(titleColor)
                    .multilineTextAlignment(.center)

                Text(prefs.tr("Convertor intuitiv de fișiere", "Intuitive file converter"))
                    .font(.system(size: 11.5, weight: .medium, design: .rounded))
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
            }

            Spacer(minLength: 2)

            // Interactive Demo Wheel with Perfectly Centered Slice Labels
            MiniWheelPreviewView()

            Text(
                prefs.tr(
                    "Fă click pe feliile din roata de mai sus pentru a testa interacțiunea și culoarea aleasă",
                    "Click the wheel slices above to preview your theme and appearance live"
                )
            )
            .font(.system(size: 11, weight: .medium, design: .rounded))
            .foregroundColor(.secondary)
            .multilineTextAlignment(.center)
            .frame(maxWidth: .infinity, alignment: .center)
            .padding(.horizontal, 10)

            Spacer(minLength: 2)

            // Step Progress Dots
            HStack(spacing: 8) {
                ForEach(0..<3, id: \.self) { step in
                    let isCurr = (step == currentStep)
                    Capsule()
                        .fill(isCurr ? theme.primaryColor : Color.secondary.opacity(0.28))
                        .frame(width: isCurr ? 26 : 8, height: 8)
                }
            }
            .padding(.bottom, 2)
        }
        .padding(20)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .center)
        .background(sidebarBg)
    }

    private func rightWizardContent(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        VStack(alignment: .leading, spacing: 18) {
            Group {
                if currentStep == 0 {
                    stepLanguageAndAppearance(isDark: isDark, theme: theme)
                } else if currentStep == 1 {
                    stepColorPalette(isDark: isDark, theme: theme)
                } else {
                    stepWorkflowAndLaunch(isDark: isDark, theme: theme)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)

            Divider()

            HStack {
                if currentStep > 0 {
                    Button {
                        withAnimation(.spring(response: 0.28, dampingFraction: 0.82)) {
                            currentStep -= 1
                        }
                    } label: {
                        HStack(spacing: 5) {
                            Image(systemName: "chevron.left")
                            Text(prefs.tr("Înapoi", "Back"))
                        }
                        .font(.system(size: 13, weight: .semibold, design: .rounded))
                        .padding(.horizontal, 14)
                        .padding(.vertical, 8)
                    }
                    .buttonStyle(.bordered)
                }

                Spacer()

                if currentStep < 2 {
                    Button {
                        withAnimation(.spring(response: 0.28, dampingFraction: 0.82)) {
                            currentStep += 1
                        }
                    } label: {
                        HStack(spacing: 6) {
                            Text(prefs.tr("Continuă", "Continue"))
                            Image(systemName: "arrow.right")
                        }
                        .font(.system(size: 13, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .padding(.horizontal, 20)
                        .padding(.vertical, 9)
                        .background(
                            Capsule()
                                .fill(
                                    LinearGradient(
                                        colors: [theme.lightStop, theme.primaryColor],
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    )
                                )
                        )
                    }
                    .buttonStyle(.plain)
                } else {
                    Button {
                        prefs.hasCompletedOnboarding = true
                        onFinish()
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "checkmark.circle.fill")
                            Text(prefs.tr("Pornește Converze", "Start using Converze"))
                        }
                        .font(.system(size: 13.5, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .padding(.horizontal, 22)
                        .padding(.vertical, 9)
                        .background(
                            Capsule()
                                .fill(
                                    LinearGradient(
                                        colors: [theme.lightStop, theme.primaryColor],
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    )
                                )
                        )
                        .shadow(color: theme.primaryColor.opacity(0.45), radius: 8, x: 0, y: 3)
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .padding(26)
    }

    // MARK: - Step 1: Language & Light/Dark Appearance (Perfectly Centered Cards)

    private func stepLanguageAndAppearance(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let headingColor: Color = isDark ? .white : Color(red: 0.08, green: 0.10, blue: 0.15)

        return VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 4) {
                Text(prefs.tr("Pasul 1 din 3 · Bine ai venit în Converze", "Step 1 of 3 · Welcome to Converze"))
                    .font(.system(size: 12, weight: .bold, design: .rounded))
                    .foregroundColor(theme.primaryColor)
                Text(prefs.tr("Alege limba și modul de afișare", "Choose Language & Appearance Mode"))
                    .font(.system(size: 22, weight: .bold, design: .rounded))
                    .foregroundColor(headingColor)
                Text(
                    prefs.tr(
                        "Personalizează interfața Converze exact așa cum îți place. Poți modifica oricând aceste opțiuni din Setări.",
                        "Customize Converze the way you like. You can change these anytime in Settings."
                    )
                )
                .font(.system(size: 12))
                .foregroundColor(.secondary)
            }

            VStack(alignment: .leading, spacing: 8) {
                Label(prefs.tr("Limba aplicației", "Application Language"), systemImage: "globe")
                    .font(.system(size: 13, weight: .bold, design: .rounded))

                HStack(spacing: 12) {
                    ForEach(AppLanguage.allCases) { lang in
                        onboardingLanguageCard(lang: lang, isDark: isDark, theme: theme)
                    }
                }
            }

            VStack(alignment: .leading, spacing: 8) {
                Label(prefs.tr("Aspect interfață", "UI Appearance"), systemImage: "circle.lefthalf.filled")
                    .font(.system(size: 13, weight: .bold, design: .rounded))

                HStack(spacing: 10) {
                    ForEach(AppAppearanceMode.allCases) { mode in
                        onboardingAppearanceCard(mode: mode, isDark: isDark, theme: theme)
                    }
                }
            }
        }
    }

    private func onboardingLanguageCard(
        lang: AppLanguage,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let selected: Bool = (prefs.language == lang)
        let titleColor: Color = isDark ? .white : .primary
        let idleBg: Color = isDark ? Color.white.opacity(0.05) : Color.black.opacity(0.04)
        let activeBg: Color = theme.primaryColor.opacity(isDark ? 0.22 : 0.10)
        let cardBg: Color = selected ? activeBg : idleBg
        let strokeColor: Color = selected ? theme.primaryColor : Color.clear

        return Button {
            withAnimation(.spring(response: 0.25, dampingFraction: 0.78)) {
                prefs.language = lang
            }
        } label: {
            VStack(spacing: 4) {
                Text(lang.flagEmoji)
                    .font(.system(size: 24))
                Text(lang.displayName)
                    .font(.system(size: 14, weight: .bold, design: .rounded))
                    .foregroundColor(titleColor)
                    .multilineTextAlignment(.center)
                Text(lang.subtitle)
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
            }
            .padding(.vertical, 10)
            .padding(.horizontal, 12)
            .frame(maxWidth: .infinity, minHeight: 82, alignment: .center)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(cardBg))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(strokeColor, lineWidth: 2))
            .overlay(alignment: .topTrailing) {
                if selected {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundColor(theme.primaryColor)
                        .font(.system(size: 15))
                        .padding(8)
                }
            }
        }
        .buttonStyle(.plain)
    }

    private func onboardingAppearanceCard(
        mode: AppAppearanceMode,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let selected: Bool = (prefs.appearanceMode == mode)
        let iconColor: Color = selected ? theme.primaryColor : .secondary
        let titleColor: Color = isDark ? .white : .primary
        let idleBg: Color = isDark ? Color.white.opacity(0.05) : Color.black.opacity(0.04)
        let activeBg: Color = theme.primaryColor.opacity(isDark ? 0.22 : 0.10)
        let cardBg: Color = selected ? activeBg : idleBg
        let strokeColor: Color = selected ? theme.primaryColor : Color.clear

        return Button {
            withAnimation(.spring(response: 0.25, dampingFraction: 0.78)) {
                prefs.appearanceMode = mode
            }
        } label: {
            VStack(spacing: 6) {
                Image(systemName: mode.sfSymbol)
                    .font(.system(size: 19, weight: .semibold))
                    .foregroundColor(iconColor)

                Text(mode.title(in: prefs.language))
                    .font(.system(size: 12.5, weight: .bold, design: .rounded))
                    .foregroundColor(titleColor)
                    .multilineTextAlignment(.center)

                Text(mode.subtitle(in: prefs.language))
                    .font(.system(size: 10.5))
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
                    .lineLimit(2)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(.vertical, 10)
            .padding(.horizontal, 8)
            .frame(maxWidth: .infinity, minHeight: 92, alignment: .center)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(cardBg))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(strokeColor, lineWidth: 2))
            .overlay(alignment: .topTrailing) {
                if selected {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundColor(theme.primaryColor)
                        .font(.system(size: 14))
                        .padding(7)
                }
            }
        }
        .buttonStyle(.plain)
    }

    // MARK: - Step 2: Color Palette Selection

    private func stepColorPalette(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let headingColor: Color = isDark ? .white : Color(red: 0.08, green: 0.10, blue: 0.15)

        return VStack(alignment: .leading, spacing: 16) {
            VStack(alignment: .leading, spacing: 4) {
                Text(prefs.tr("Pasul 2 din 3 · Alege culoarea dorită", "Step 2 of 3 · Color Customization"))
                    .font(.system(size: 12, weight: .bold, design: .rounded))
                    .foregroundColor(theme.primaryColor)
                Text(prefs.tr("Alege culoarea pentru Converze", "Choose your color"))
                    .font(.system(size: 22, weight: .bold, design: .rounded))
                    .foregroundColor(headingColor)
                Text(
                    prefs.tr(
                        "Culoarea aleasă este folosită pentru opțiunile din roată, haloul luminos, particulele de la drop și notificări.",
                        "Your chosen palette styles active slices, the glowing rim, drop spark particles, and HUD notifications."
                    )
                )
                .font(.system(size: 12))
                .foregroundColor(.secondary)
            }

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                ForEach(ConverzeColorTheme.allCases) { item in
                    onboardingPaletteCard(item: item, isDark: isDark)
                }
            }
        }
    }

    private func onboardingPaletteCard(item: ConverzeColorTheme, isDark: Bool) -> some View {
        let selected: Bool = (prefs.colorTheme == item)
        let titleColor: Color = isDark ? .white : .primary
        let subText: String = selected
            ? prefs.tr("Activă acum", "Currently active")
            : prefs.tr("Click pentru aplicare", "Click to apply")
        let subColor: Color = selected ? item.primaryColor : .secondary
        let idleBg: Color = isDark ? Color.white.opacity(0.05) : Color.black.opacity(0.04)
        let activeBg: Color = item.primaryColor.opacity(isDark ? 0.22 : 0.10)
        let cardBg: Color = selected ? activeBg : idleBg
        let strokeColor: Color = selected ? item.primaryColor : Color.clear

        return Button {
            withAnimation(.spring(response: 0.24, dampingFraction: 0.70)) {
                prefs.colorTheme = item
            }
        } label: {
            HStack(spacing: 12) {
                Circle()
                    .fill(
                        LinearGradient(
                            colors: [item.lightStop, item.primaryColor, item.deepStop],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
                    .frame(width: 34, height: 34)
                    .overlay(Circle().stroke(Color.white.opacity(0.85), lineWidth: 1.5))
                    .shadow(color: item.primaryColor.opacity(0.45), radius: 5, x: 0, y: 2)

                VStack(alignment: .leading, spacing: 2) {
                    Text(item.title(in: prefs.language))
                        .font(.system(size: 13.5, weight: .bold, design: .rounded))
                        .foregroundColor(titleColor)
                    Text(subText)
                        .font(.system(size: 10.5))
                        .foregroundColor(subColor)
                }
                Spacer()
                if selected {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundColor(item.primaryColor)
                        .font(.system(size: 17))
                }
            }
            .padding(12)
            .frame(maxWidth: .infinity, minHeight: 58, alignment: .center)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(cardBg))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(strokeColor, lineWidth: 2))
        }
        .buttonStyle(.plain)
    }

    // MARK: - Step 3: Workflow (`⌥ Option`) & System Process Options

    private func stepWorkflowAndLaunch(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let headingColor: Color = isDark ? .white : Color(red: 0.08, green: 0.10, blue: 0.15)
        let boxBg: Color = isDark ? Color.white.opacity(0.05) : Color.black.opacity(0.04)

        return VStack(alignment: .leading, spacing: 16) {
            VStack(alignment: .leading, spacing: 4) {
                Text(prefs.tr("Pasul 3 din 3 · Procesul de sistem", "Step 3 of 3 · macOS System Process"))
                    .font(.system(size: 12, weight: .bold, design: .rounded))
                    .foregroundColor(theme.primaryColor)
                Text(prefs.tr("Cum convertești orice fișier instant?", "How to convert any file instantly"))
                    .font(.system(size: 22, weight: .bold, design: .rounded))
                    .foregroundColor(headingColor)
            }

            VStack(spacing: 8) {
                workflowStepRow(
                    num: "1",
                    title: prefs.tr("Selectează fișierul", "Select any file"),
                    detail: prefs.tr("Funcționează oriunde te-ai afla în sistemul de operare", "Works anywhere in the OS"),
                    isDark: isDark,
                    theme: theme
                )
                workflowStepRow(
                    num: "2",
                    title: prefs.tr("Ține apăsat ⌥ (Option) și fă click stânga", "Hold ⌥ (Option) and left click on the file"),
                    detail: prefs.tr("Converze se deschide instant.", "Converze opens instantly."),
                    isDark: isDark,
                    theme: theme
                )
                workflowStepRow(
                    num: "3",
                    title: prefs.tr("Alege extensia dorită sau apasă ESC (ori click oriunde) pentru a închide Converze", "Click on desired extension or press ESC / outside click to close Converze"),
                    detail: prefs.tr("Fișierul nou convertit este salvat instantaneu lângă cel original.", "The converted file is saved right next to the original."),
                    isDark: isDark,
                    theme: theme
                )
            }

            VStack(alignment: .leading, spacing: 8) {
                Toggle(
                    prefs.tr("Pornește automat Converze la bootare", "Launch Converze automatically at system start"),
                    isOn: $launchAtLogin
                )
                .font(.system(size: 12.5, weight: .medium))
                .onChange(of: launchAtLogin) { enabled in
                    do {
                        if enabled {
                            try SMAppService.mainApp.register()
                        } else {
                            try SMAppService.mainApp.unregister()
                        }
                    } catch {
                        launchAtLogin = (SMAppService.mainApp.status == .enabled)
                    }
                }

                Toggle(
                    prefs.tr("Deschide o fereastră cu fișierul convertit", "Automatically reveal converted file in a new window"),
                    isOn: $prefs.autoRevealInFinder
                )
                .font(.system(size: 12.5, weight: .medium))
            }
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(boxBg))
        }
    }

    private func workflowStepRow(num: String, title: String, detail: String, isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let rowBg: Color = isDark ? Color.white.opacity(0.04) : Color.black.opacity(0.03)
        let titleColor: Color = isDark ? .white : .primary

        return HStack(spacing: 12) {
            Text(num)
                .font(.system(size: 13, weight: .heavy, design: .rounded))
                .foregroundColor(.white)
                .frame(width: 26, height: 26)
                .background(Circle().fill(theme.primaryColor))

            VStack(alignment: .leading, spacing: 1) {
                Text(title)
                    .font(.system(size: 13, weight: .bold, design: .rounded))
                    .foregroundColor(titleColor)
                Text(detail)
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }
            Spacer()
        }
        .padding(9)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(rowBg))
    }
}

// MARK: - Menu Bar Popover View (Converze)

public struct MenuBarView: View {
    @ObservedObject public var dragMonitor = DragMonitorService.shared
    @ObservedObject public var hudManager = ConversionHUDManager.shared
    @ObservedObject public var prefs = ConverzePreferences.shared
    @State private var isDropTargeted: Bool = false

    public var onOpenPlayground: () -> Void
    public var onOpenSettings: () -> Void

    public init(
        onOpenPlayground: @escaping () -> Void,
        onOpenSettings: @escaping () -> Void
    ) {
        self.onOpenPlayground = onOpenPlayground
        self.onOpenSettings = onOpenSettings
    }

    public var body: some View {
        let theme: ConverzeColorTheme = prefs.colorTheme
        let dropBg: Color = isDropTargeted ? theme.primaryColor.opacity(0.14) : Color.primary.opacity(0.04)
        let dropStroke: Color = isDropTargeted ? theme.primaryColor : Color.primary.opacity(0.14)

        VStack(alignment: .leading, spacing: 12) {
            HStack {
                HStack(spacing: 8) {
                    ConverzeAppIconView(size: 26)
                    VStack(alignment: .leading, spacing: 0) {
                        Text("Converze")
                            .font(.system(size: 14, weight: .bold, design: .rounded))
                        Text(prefs.tr("Un convertor rapid și intuitiv", "System-wide offline converter"))
                            .font(.system(size: 10.5, design: .rounded))
                            .foregroundColor(.secondary)
                    }
                }
                Spacer()
                HStack(spacing: 5) {
                    Circle()
                        .fill(Color.green)
                        .frame(width: 7, height: 7)
                    Text(prefs.tr("Activ", "Active"))
                        .font(.system(size: 10.5, weight: .medium, design: .rounded))
                        .foregroundColor(.secondary)
                }
            }

            VStack(spacing: 6) {
                Image(systemName: "arrow.down.doc.fill")
                    .font(.system(size: 20))
                    .foregroundColor(theme.primaryColor)
                Text(prefs.tr("Apasă tasta ⌥ (Option) și click stânga", "Hold ⌥ (Option) key and left-click to reveal Converze"))
                    .font(.system(size: 11.5, weight: .semibold, design: .rounded))
                    .multilineTextAlignment(.center)
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(dropBg))
            .overlay(
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .strokeBorder(dropStroke, style: StrokeStyle(lineWidth: 1, dash: [5, 3]))
            )
            .onDrop(of: [.fileURL], isTargeted: $isDropTargeted) { providers in
                for provider in providers {
                    _ = provider.loadObject(ofClass: URL.self) { url, _ in
                        if let url {
                            Task { @MainActor in
                                dragMonitor.openKeyboardWheel(for: [url], at: NSEvent.mouseLocation)
                            }
                        }
                    }
                }
                return true
            }
        }
        .padding(14)
        .frame(width: 300)
        .preferredColorScheme(prefs.preferredColorScheme)
    }
}

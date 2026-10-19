import SwiftUI
import ServiceManagement

// MARK: - Intuitive & Visual Settings Window for Converze

public struct SettingsView: View {
    public enum SettingsSection: String, CaseIterable, Identifiable {
        case appearance
        case systemAndLanguage
        case formats

        public var id: String { rawValue }

        public var sfSymbol: String {
            switch self {
            case .appearance: return "paintpalette.fill"
            case .systemAndLanguage: return "gearshape.2.fill"
            case .formats: return "arrow.triangle.2.circlepath.circle.fill"
            }
        }

        public func title(in lang: AppLanguage) -> String {
            switch self {
            case .appearance:
                return lang == .ro ? "Aspect și culori" : "Appearance & Colors"
            case .systemAndLanguage:
                return lang == .ro ? "Limbă și sistem" : "Language & System"
            case .formats:
                return lang == .ro ? "Formate (275)" : "Formats (275)"
            }
        }
    }

    @ObservedObject public var prefs = ConverzePreferences.shared
    @State private var selectedSection: SettingsSection = .appearance
    @State private var launchAtLogin: Bool = (SMAppService.mainApp.status == .enabled)
    @State private var formatSearch: String = ""

    public var onOpenOnboarding: (() -> Void)?
    public var onOpenPlayground: (() -> Void)?

    public init(
        onOpenOnboarding: (() -> Void)? = nil,
        onOpenPlayground: (() -> Void)? = nil
    ) {
        self.onOpenOnboarding = onOpenOnboarding
        self.onOpenPlayground = onOpenPlayground
    }

    public var body: some View {
        let isDark: Bool = prefs.isDarkMode
        let theme: ConverzeColorTheme = prefs.colorTheme
        let mainBg: Color = isDark
            ? Color(red: 0.10, green: 0.12, blue: 0.18)
            : Color(red: 0.98, green: 0.99, blue: 1.00)

        HStack(spacing: 0) {
            leftSidebar(isDark: isDark, theme: theme)
                .frame(width: 255)

            Divider()

            rightContentColumn(isDark: isDark, theme: theme)
        }
        .frame(width: 730, height: 490)
        .background(mainBg)
        .preferredColorScheme(prefs.preferredColorScheme)
    }

    private func leftSidebar(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let sidebarBg: Color = isDark
            ? Color(red: 0.07, green: 0.09, blue: 0.14)
            : Color(red: 0.94, green: 0.96, blue: 0.99)
        let titleColor: Color = isDark ? .white : Color(red: 0.08, green: 0.10, blue: 0.15)
        let cardBg: Color = isDark ? Color.white.opacity(0.04) : Color.white.opacity(0.85)
        let cardStroke: Color = isDark ? Color.white.opacity(0.10) : Color.black.opacity(0.06)

        return VStack(spacing: 12) {
            HStack(spacing: 10) {
                ConverzeAppIconView(size: 38)

                VStack(alignment: .leading, spacing: 1) {
                    Text("Converze")
                        .font(.system(size: 17, weight: .heavy, design: .rounded))
                        .foregroundColor(titleColor)
                    Text(prefs.tr("Setări și personalizare", "Settings & Personalization"))
                        .font(.system(size: 10.5, weight: .medium, design: .rounded))
                        .foregroundColor(.secondary)
                }
                Spacer()
            }

            VStack(spacing: 4) {
                MiniWheelPreviewView()
                Text(prefs.tr("Previzualizare Converze în timp real", "Live Converze Preview"))
                    .font(.system(size: 10.5, weight: .semibold, design: .rounded))
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
            }
            .padding(.vertical, 6)
            .frame(maxWidth: .infinity, alignment: .center)
            .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(cardBg))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(cardStroke, lineWidth: 1))

            Spacer()

            VStack(spacing: 8) {
                if let onOpenOnboarding {
                    Button {
                        onOpenOnboarding()
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "sparkles")
                            Text(prefs.tr("Ghid de bun venit", "Welcome Setup Guide"))
                        }
                        .font(.system(size: 11.5, weight: .semibold, design: .rounded))
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 6)
                    }
                    .buttonStyle(.bordered)
                }

                if let onOpenPlayground {
                    Button {
                        onOpenPlayground()
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "play.circle.fill")
                            Text(prefs.tr("Deschide Playground", "Open Playground"))
                        }
                        .font(.system(size: 11.5, weight: .semibold, design: .rounded))
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 6)
                    }
                    .buttonStyle(.bordered)
                }
            }
        }
        .padding(18)
        .frame(maxHeight: .infinity)
        .background(sidebarBg)
    }

    private func rightContentColumn(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let tabBarBg: Color = isDark ? Color.white.opacity(0.06) : Color.black.opacity(0.05)

        return VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 8) {
                ForEach(SettingsSection.allCases) { sec in
                    sectionTabButton(sec: sec, isDark: isDark, theme: theme)
                }
            }
            .padding(5)
            .background(Capsule().fill(tabBarBg))

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    switch selectedSection {
                    case .appearance:
                        appearanceSection(isDark: isDark, theme: theme)
                    case .systemAndLanguage:
                        languageAndSystemSection(isDark: isDark, theme: theme)
                    case .formats:
                        formatsSection(isDark: isDark, theme: theme)
                    }
                }
                .padding(.bottom, 10)
            }
        }
        .padding(22)
    }

    private func sectionTabButton(sec: SettingsSection, isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let isSel: Bool = (selectedSection == sec)
        let fgColor: Color = isSel ? .white : (isDark ? .white.opacity(0.8) : .primary)
        let bgFill: AnyShapeStyle = isSel
            ? AnyShapeStyle(
                LinearGradient(
                    colors: [theme.lightStop, theme.primaryColor],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            )
            : AnyShapeStyle(Color.clear)

        return Button {
            withAnimation(.spring(response: 0.24, dampingFraction: 0.80)) {
                selectedSection = sec
            }
        } label: {
            HStack(spacing: 6) {
                Image(systemName: sec.sfSymbol)
                    .font(.system(size: 12, weight: .semibold))
                Text(sec.title(in: prefs.language))
                    .font(.system(size: 12.5, weight: .bold, design: .rounded))
            }
            .foregroundColor(fgColor)
            .padding(.horizontal, 14)
            .padding(.vertical, 7)
            .background(Capsule().fill(bgFill))
        }
        .buttonStyle(.plain)
    }

    // MARK: - Section 1: Appearance (White Mode / Dark Mode) & Non-Orange Color Palettes

    private func appearanceSection(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let headingColor: Color = isDark ? .white : .primary

        return VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 8) {
                Text(prefs.tr("Modul de afișare", "Appearance Mode"))
                    .font(.system(size: 13.5, weight: .bold, design: .rounded))
                    .foregroundColor(headingColor)

                HStack(spacing: 10) {
                    ForEach(AppAppearanceMode.allCases) { mode in
                        settingsAppearanceCard(mode: mode, isDark: isDark, theme: theme)
                    }
                }
            }

            Divider()

            VStack(alignment: .leading, spacing: 8) {
                Text(prefs.tr("Selectează culoarea dorită", "Choose your Converze color accent"))
                    .font(.system(size: 13.5, weight: .bold, design: .rounded))
                    .foregroundColor(headingColor)

                LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 10) {
                    ForEach(ConverzeColorTheme.allCases) { item in
                        settingsPaletteCard(item: item, isDark: isDark)
                    }
                }
            }
        }
    }

    private func settingsAppearanceCard(
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
            withAnimation(.spring(response: 0.24, dampingFraction: 0.78)) {
                prefs.appearanceMode = mode
            }
        } label: {
            VStack(spacing: 6) {
                Image(systemName: mode.sfSymbol)
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(iconColor)

                Text(mode.title(in: prefs.language))
                    .font(.system(size: 12.5, weight: .bold, design: .rounded))
                    .foregroundColor(titleColor)
                    .multilineTextAlignment(.center)

                Text(mode.subtitle(in: prefs.language))
                    .font(.system(size: 10))
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
                    .lineLimit(2)
            }
            .padding(11)
            .frame(maxWidth: .infinity, minHeight: 88, alignment: .center)
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

    private func settingsPaletteCard(item: ConverzeColorTheme, isDark: Bool) -> some View {
        let selected: Bool = (prefs.colorTheme == item)
        let titleColor: Color = isDark ? .white : .primary
        let subText: String = selected
            ? prefs.tr("Activă", "Active")
            : prefs.tr("Click pentru aplicare", "Click to apply")
        let subColor: Color = selected ? item.primaryColor : .secondary
        let idleBg: Color = isDark ? Color.white.opacity(0.05) : Color.black.opacity(0.04)
        let activeBg: Color = item.primaryColor.opacity(isDark ? 0.22 : 0.10)
        let cardBg: Color = selected ? activeBg : idleBg
        let strokeColor: Color = selected ? item.primaryColor : Color.clear

        return Button {
            withAnimation(.spring(response: 0.24, dampingFraction: 0.72)) {
                prefs.colorTheme = item
            }
        } label: {
            HStack(spacing: 10) {
                Circle()
                    .fill(
                        LinearGradient(
                            colors: [item.lightStop, item.primaryColor, item.deepStop],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
                    .frame(width: 28, height: 28)
                    .overlay(Circle().stroke(Color.white.opacity(0.85), lineWidth: 1.3))
                    .shadow(color: item.primaryColor.opacity(0.45), radius: 4, x: 0, y: 2)

                VStack(alignment: .leading, spacing: 1) {
                    Text(item.title(in: prefs.language))
                        .font(.system(size: 12.5, weight: .bold, design: .rounded))
                        .foregroundColor(titleColor)
                    Text(subText)
                        .font(.system(size: 10))
                        .foregroundColor(subColor)
                }
                Spacer()
                if selected {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundColor(item.primaryColor)
                }
            }
            .padding(10)
            .background(RoundedRectangle(cornerRadius: 11, style: .continuous).fill(cardBg))
            .overlay(RoundedRectangle(cornerRadius: 11, style: .continuous).stroke(strokeColor, lineWidth: 2))
        }
        .buttonStyle(.plain)
    }

    // MARK: - Section 2: Language (Română / English) & macOS System Process (`⌥ Option`)

    private func languageAndSystemSection(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let headingColor: Color = isDark ? .white : .primary
        let boxBg: Color = isDark ? Color.white.opacity(0.04) : Color.black.opacity(0.03)
        let shortcutBoxBg: Color = theme.primaryColor.opacity(isDark ? 0.14 : 0.08)

        return VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 8) {
                Text(prefs.tr("Limba interfeței (Română / English)", "UI Language (Română / English)"))
                    .font(.system(size: 13.5, weight: .bold, design: .rounded))
                    .foregroundColor(headingColor)

                HStack(spacing: 12) {
                    ForEach(AppLanguage.allCases) { lang in
                        settingsLanguageCard(lang: lang, isDark: isDark, theme: theme)
                    }
                }
            }

            Divider()

            VStack(alignment: .leading, spacing: 10) {
                Text(prefs.tr("Comportament", "Behavior"))
                    .font(.system(size: 13.5, weight: .bold, design: .rounded))
                    .foregroundColor(headingColor)

                VStack(alignment: .leading, spacing: 10) {
                    Toggle(
                        prefs.tr(
                            "Pornește automat Converze la bootare",
                            "Launch Converze automatically at system start"
                        ),
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
                        prefs.tr(
                            "Deschide o fereastră nouă și evidențiază automat fișierul convertit",
                            "Automatically reveal converted file in a new window"
                        ),
                        isOn: $prefs.autoRevealInFinder
                    )
                    .font(.system(size: 12.5, weight: .medium))

                    Toggle(
                        prefs.tr(
                            "Feedback haptic pe trackpad la selectarea formatului",
                            "Trackpad haptic feedback when hovering wheel slices"
                        ),
                        isOn: $prefs.hapticsEnabled
                    )
                    .font(.system(size: 12.5, weight: .medium))
                }
                .padding(14)
                .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(boxBg))
            }

            HStack(spacing: 12) {
                Text("⌥ Option")
                    .font(.system(size: 13, weight: .heavy, design: .monospaced))
                    .foregroundColor(.white)
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(Capsule().fill(theme.primaryColor))

                VStack(alignment: .leading, spacing: 2) {
                    Text(prefs.tr("Scurtătura pentru conversie", "Conversion Shortcut"))
                        .font(.system(size: 12.5, weight: .bold, design: .rounded))
                    Text(
                        prefs.tr(
                            "Ține apăsată tasta ⌥ (Option) și fă click stânga pe fișier. Tasta ESC sau click în afară închide Converze.",
                            "Hold ⌥ (Option key) and left click on the file you want to convert. ESC or outside click closes the wheel."
                        )
                    )
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
                }
                Spacer()
            }
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(shortcutBoxBg))
        }
    }

    private func settingsLanguageCard(
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
            withAnimation(.spring(response: 0.24, dampingFraction: 0.78)) {
                prefs.language = lang
            }
        } label: {
            VStack(spacing: 4) {
                Text(lang.flagEmoji)
                    .font(.system(size: 24))
                Text(lang.displayName)
                    .font(.system(size: 13.5, weight: .bold, design: .rounded))
                    .foregroundColor(titleColor)
                    .multilineTextAlignment(.center)
                Text(lang.subtitle)
                    .font(.system(size: 10.5))
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
            }
            .padding(12)
            .frame(maxWidth: .infinity, minHeight: 80, alignment: .center)
            .background(RoundedRectangle(cornerRadius: 12, style: .continuous).fill(cardBg))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(strokeColor, lineWidth: 2))
            .overlay(alignment: .topTrailing) {
                if selected {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundColor(theme.primaryColor)
                        .padding(8)
                }
            }
        }
        .buttonStyle(.plain)
    }

    // MARK: - Section 3: Supported Formats Directory (275 Conversion Paths)

    private func formatsSection(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let allGroups: [(String, String, [String])] = [
            (prefs.tr("Imagini (13 formate)", "Images (13 formats)"), "photo", FormatMatrix.imageInputList),
            (prefs.tr("Video și audio (8 formate)", "Video & Audio (8 formats)"), "film", FormatMatrix.videoInputList),
            (prefs.tr("Audio (8 formate)", "Audio (8 formats)"), "waveform", FormatMatrix.audioInputList),
            (prefs.tr("Documente & PDF (8 formate)", "Documents & PDF (8 formats)"), "doc.text", ["pdf"] + FormatMatrix.docInputs),
            (prefs.tr("Prezentări & Tabele (8 formate)", "Presentations & Spreadsheets (8 formats)"), "tablecells", FormatMatrix.slideInputs + FormatMatrix.sheetInputs),
            (prefs.tr("Subtitrări, EPUB & Arhive (8 formate)", "Subtitles, EPUB & Archives (8 formats)"), "doc.zipper", ["epub"] + FormatMatrix.subtitleInputs + FormatMatrix.archiveInputList)
        ]
        let searchBg: Color = isDark ? Color.white.opacity(0.06) : Color.black.opacity(0.04)

        return VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(systemName: "magnifyingglass")
                    .foregroundColor(.secondary)
                TextField(
                    prefs.tr("Caută extensie (ex. webp, mp4, pdf, mp3)…", "Search extension (e.g. webp, mp4, pdf, mp3)…"),
                    text: $formatSearch
                )
                .textFieldStyle(.plain)
            }
            .padding(9)
            .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(searchBg))

            ForEach(allGroups, id: \.0) { title, icon, list in
                formatGroupRow(title: title, icon: icon, list: list, isDark: isDark, theme: theme)
            }
        }
    }

    @ViewBuilder
    private func formatGroupRow(
        title: String,
        icon: String,
        list: [String],
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let filtered = list.filter {
            formatSearch.trimmingCharacters(in: .whitespaces).isEmpty ||
            $0.localizedCaseInsensitiveContains(formatSearch)
        }
        if !filtered.isEmpty {
            let rowBg: Color = isDark ? Color.white.opacity(0.04) : Color.black.opacity(0.03)
            let pillBg: Color = theme.primaryColor.opacity(isDark ? 0.20 : 0.12)

            VStack(alignment: .leading, spacing: 6) {
                Label(title, systemImage: icon)
                    .font(.system(size: 12.5, weight: .bold, design: .rounded))
                    .foregroundColor(theme.primaryColor)

                HStack(spacing: 6) {
                    ForEach(filtered.prefix(10), id: \.self) { ext in
                        Text(ext.uppercased())
                            .font(.system(size: 10.5, weight: .heavy, design: .rounded))
                            .foregroundColor(theme.primaryColor)
                            .padding(.horizontal, 8)
                            .padding(.vertical, 4)
                            .background(Capsule().fill(pillBg))
                    }
                }
            }
            .padding(10)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(rowBg))
        }
    }
}

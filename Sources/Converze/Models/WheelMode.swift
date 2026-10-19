import AppKit
import SwiftUI

// MARK: - Language Selection (Română & English)

public enum AppLanguage: String, CaseIterable, Identifiable, Sendable {
    case ro = "ro"
    case en = "en"

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .ro: return "Română"
        case .en: return "English"
        }
    }

    public var flagEmoji: String {
        switch self {
        case .ro: return "🇷🇴"
        case .en: return "🇬🇧"
        }
    }

    public var subtitle: String {
        switch self {
        case .ro: return "Interfață completă în limba română"
        case .en: return "Full UI in English"
        }
    }
}

// MARK: - UI Appearance Mode (White Mode / Dark Mode / System)

public enum AppAppearanceMode: String, CaseIterable, Identifiable, Sendable {
    case light = "light"
    case dark = "dark"
    case system = "system"

    public var id: String { rawValue }

    public func title(in lang: AppLanguage) -> String {
        switch (self, lang) {
        case (.light, .ro): return "Mod light (alb)"
        case (.light, .en): return "Light Mode (White)"
        case (.dark, .ro): return "Mod întunecat (dark)"
        case (.dark, .en): return "Dark Mode"
        case (.system, .ro): return "Automat"
        case (.system, .en): return "System Auto"
        }
    }

    public func subtitle(in lang: AppLanguage) -> String {
        switch (self, lang) {
        case (.light, .ro): return "Efect de sticlă și contrast clar"
        case (.light, .en): return "Crisp frosted white glass interface"
        case (.dark, .ro): return "Sticlă mată cu reflexii de lumină."
        case (.dark, .en): return "Sleek dark obsidian glass with luminous glow"
        case (.system, .ro): return "Aplică automat tema din macOS"
        case (.system, .en): return "Follows your macOS appearance setting"
        }
    }

    public var sfSymbol: String {
        switch self {
        case .light: return "sun.max.fill"
        case .dark: return "moon.stars.fill"
        case .system: return "circle.lefthalf.filled"
        }
    }
}

// MARK: - User-Selectable Color Palettes (Non-Orange Curated Themes)

public enum ConverzeColorTheme: String, CaseIterable, Identifiable, Sendable {
    case sapphire = "sapphire"
    case indigo = "indigo"
    case emerald = "emerald"
    case cyan = "cyan"
    case amethyst = "amethyst"
    case rose = "rose"

    public var id: String { rawValue }

    public func title(in lang: AppLanguage) -> String {
        switch (self, lang) {
        case (.sapphire, .ro): return "Safir"
                case (.sapphire, .en): return "Sapphire"
                case (.indigo, .ro): return "Indigo"
                case (.indigo, .en): return "Indigo"
                case (.emerald, .ro): return "Smarald"
                case (.emerald, .en): return "Emerald"
                case (.cyan, .ro): return "Azuriu"
                case (.cyan, .en): return "Cyan"
                case (.amethyst, .ro): return "Ametist"
                case (.amethyst, .en): return "Amethyst"
                case (.rose, .ro): return "Rubin"
                case (.rose, .en): return "Rose"
        }
    }

    /// Primary vibrant accent color
    public var primaryColor: Color {
        switch self {
        case .sapphire: return Color(red: 0.145, green: 0.388, blue: 0.922) // #2563EB
        case .indigo:   return Color(red: 0.388, green: 0.400, blue: 0.945) // #6366F1
        case .emerald:  return Color(red: 0.063, green: 0.725, blue: 0.506) // #10B981
        case .cyan:     return Color(red: 0.024, green: 0.714, blue: 0.831) // #06B6D4
        case .amethyst: return Color(red: 0.576, green: 0.200, blue: 0.918) // #9333EA
        case .rose:     return Color(red: 0.957, green: 0.247, blue: 0.369) // #F43F5E
        }
    }

    /// Lighter highlight stop for gradients and sparks
    public var lightStop: Color {
        switch self {
        case .sapphire: return Color(red: 0.376, green: 0.647, blue: 0.980) // #60A5FA
        case .indigo:   return Color(red: 0.506, green: 0.549, blue: 0.973) // #818CF8
        case .emerald:  return Color(red: 0.204, green: 0.827, blue: 0.600) // #34D399
        case .cyan:     return Color(red: 0.133, green: 0.827, blue: 0.933) // #22D3EE
        case .amethyst: return Color(red: 0.753, green: 0.518, blue: 0.988) // #C084FC
        case .rose:     return Color(red: 0.984, green: 0.443, blue: 0.522) // #FB7185
        }
    }

    /// Deeper stop for rich 3-stop slice gradients
    public var deepStop: Color {
        switch self {
        case .sapphire: return Color(red: 0.114, green: 0.306, blue: 0.847) // #1D4ED8
        case .indigo:   return Color(red: 0.263, green: 0.220, blue: 0.792) // #4338CA
        case .emerald:  return Color(red: 0.020, green: 0.471, blue: 0.341) // #057A55
        case .cyan:     return Color(red: 0.055, green: 0.455, blue: 0.565) // #0E7490
        case .amethyst: return Color(red: 0.420, green: 0.129, blue: 0.659) // #6B21A8
        case .rose:     return Color(red: 0.745, green: 0.071, blue: 0.235) // #BE123C
        }
    }

    /// Secondary spark / shimmer color
    public var sparkSecondary: Color {
        switch self {
        case .sapphire: return Color(red: 0.58, green: 0.82, blue: 1.0)
        case .indigo:   return Color(red: 0.78, green: 0.82, blue: 1.0)
        case .emerald:  return Color(red: 0.65, green: 0.96, blue: 0.82)
        case .cyan:     return Color(red: 0.65, green: 0.95, blue: 1.0)
        case .amethyst: return Color(red: 0.92, green: 0.78, blue: 1.0)
        case .rose:     return Color(red: 1.0, green: 0.75, blue: 0.82)
        }
    }
}

// MARK: - Global Converze Preferences & Localization Store

@MainActor
public final class ConverzePreferences: ObservableObject {
    public static let shared = ConverzePreferences()

    @Published public var language: AppLanguage {
        didSet {
            UserDefaults.standard.set(language.rawValue, forKey: "converze.language")
            onPreferencesChanged?()
        }
    }

    @Published public var appearanceMode: AppAppearanceMode {
        didSet {
            UserDefaults.standard.set(appearanceMode.rawValue, forKey: "converze.appearanceMode")
            applySystemAppearance()
            onPreferencesChanged?()
        }
    }

    @Published public var colorTheme: ConverzeColorTheme {
        didSet {
            UserDefaults.standard.set(colorTheme.rawValue, forKey: "converze.colorTheme")
            onPreferencesChanged?()
        }
    }

    @Published public var hasCompletedOnboarding: Bool {
        didSet {
            UserDefaults.standard.set(hasCompletedOnboarding, forKey: "converze.hasCompletedOnboarding")
            if hasCompletedOnboarding {
                UserDefaults.standard.set(Self.currentInstallSignature(), forKey: "converze.onboardedInstallSignature")
            }
        }
    }

    @Published public var hapticsEnabled: Bool {
        didSet {
            UserDefaults.standard.set(hapticsEnabled, forKey: "converze.hapticsEnabled")
        }
    }

    @Published public var autoRevealInFinder: Bool {
        didSet {
            UserDefaults.standard.set(autoRevealInFinder, forKey: "converze.autoRevealInFinder")
        }
    }

    @Published public var imageCompressionQuality: Double {
        didSet {
            UserDefaults.standard.set(imageCompressionQuality, forKey: "converze.imageCompressionQuality")
        }
    }

    public var onPreferencesChanged: (() -> Void)?

    private init() {
        let savedLang = UserDefaults.standard.string(forKey: "converze.language") ?? AppLanguage.ro.rawValue
        self.language = AppLanguage(rawValue: savedLang) ?? .ro

        let savedAppearance = UserDefaults.standard.string(forKey: "converze.appearanceMode") ?? AppAppearanceMode.light.rawValue
        self.appearanceMode = AppAppearanceMode(rawValue: savedAppearance) ?? .light

        let savedTheme = UserDefaults.standard.string(forKey: "converze.colorTheme") ?? ConverzeColorTheme.sapphire.rawValue
        self.colorTheme = ConverzeColorTheme(rawValue: savedTheme) ?? .sapphire

        let currentSig = Self.currentInstallSignature()
        let savedSig = UserDefaults.standard.string(forKey: "converze.onboardedInstallSignature") ?? ""
        let savedFlag = UserDefaults.standard.bool(forKey: "converze.hasCompletedOnboarding")
        // If this is a newly installed .app bundle (or onboarding was never completed), show Welcome Screen on first launch
        self.hasCompletedOnboarding = savedFlag && (savedSig == currentSig)

        if UserDefaults.standard.object(forKey: "converze.hapticsEnabled") == nil {
            self.hapticsEnabled = true
        } else {
            self.hapticsEnabled = UserDefaults.standard.bool(forKey: "converze.hapticsEnabled")
        }

        self.autoRevealInFinder = UserDefaults.standard.bool(forKey: "converze.autoRevealInFinder")

        let savedQuality = UserDefaults.standard.double(forKey: "converze.imageCompressionQuality")
        self.imageCompressionQuality = savedQuality > 0.1 ? savedQuality : 0.90

        applySystemAppearance()
    }

    /// True when the active UI should render in Dark Mode
    public var isDarkMode: Bool {
        switch appearanceMode {
        case .light:
            return false
        case .dark:
            return true
        case .system:
            let best = NSApp?.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua])
            return best == .darkAqua
        }
    }

    /// SwiftUI ColorScheme override (`nil` for system auto)
    public var preferredColorScheme: ColorScheme? {
        switch appearanceMode {
        case .light: return .light
        case .dark: return .dark
        case .system: return nil
        }
    }

    public func applySystemAppearance() {
        switch appearanceMode {
        case .light:
            NSApp?.appearance = NSAppearance(named: .aqua)
        case .dark:
            NSApp?.appearance = NSAppearance(named: .darkAqua)
        case .system:
            NSApp?.appearance = nil
        }
    }

    /// Translate between Romanian and English based on the user's selected language
    public func tr(_ ro: String, _ en: String) -> String {
        language == .ro ? ro : en
    }

    /// Unique signature for the current `.app` installation so a fresh install always shows the Welcome Screen on first launch
    public static func currentInstallSignature() -> String {
        if let installID = Bundle.main.object(forInfoDictionaryKey: "ConverzeInstallID") as? String, !installID.isEmpty {
            return installID
        }
        let bundlePath = Bundle.main.bundlePath
        let version = (Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String) ?? "1"
        return "\(bundlePath)#\(version)"
    }
}

// MARK: - Radial Wheel Target (Pure Format Conversion)

public enum WheelTarget: Equatable, Identifiable, Sendable {
    case format(String)

    public var id: String {
        switch self {
        case .format(let fmt): return "fmt_\(fmt)"
        }
    }

    public var wheelLabel: String {
        switch self {
        case .format(let fmt): return FormatMatrix.displayLabel(for: fmt)
        }
    }

    public var targetFormat: String {
        switch self {
        case .format(let fmt): return fmt
        }
    }

    @MainActor
    public var actionDescription: String {
        switch self {
        case .format(let fmt):
            let label = FormatMatrix.displayLabel(for: fmt)
            return ConverzePreferences.shared.tr(
                "convertești în \(label)",
                "convert to \(label)"
            )
        }
    }
}

// MARK: - Converze Application Icon Loader & View

public enum ConverzeAppIconLoader {
    @MainActor
    private static var cachedIcon: NSImage?

    @MainActor
    public static func appIconImage() -> NSImage? {
        if let cached = cachedIcon {
            return cached
        }

        // 1. Check main app bundle resources
        if let url = Bundle.main.url(forResource: "AppIcon", withExtension: "png"),
           let img = NSImage(contentsOf: url) {
            cachedIcon = img
            return img
        }
        if let url = Bundle.main.url(forResource: "AppIcon", withExtension: "icns"),
           let img = NSImage(contentsOf: url) {
            cachedIcon = img
            return img
        }

        // 2. Check source tree Resources folder (works when running directly from Xcode / SwiftPM)
        let sourceRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent() // Models
            .deletingLastPathComponent() // Converze
            .deletingLastPathComponent() // Sources
            .deletingLastPathComponent() // Project root
        let resourcePNG = sourceRoot.appendingPathComponent("Resources/AppIcon.png")
        if let img = NSImage(contentsOf: resourcePNG) {
            cachedIcon = img
            return img
        }
        let resourceICNS = sourceRoot.appendingPathComponent("Resources/AppIcon.icns")
        if let img = NSImage(contentsOf: resourceICNS) {
            cachedIcon = img
            return img
        }

        return NSApp?.applicationIconImage
    }
}

public struct ConverzeAppIconView: View {
    public let size: CGFloat

    public init(size: CGFloat = 38) {
        self.size = size
    }

    public var body: some View {
        if let nsImg = ConverzeAppIconLoader.appIconImage() {
            Image(nsImage: nsImg)
                .resizable()
                .interpolation(.high)
                .antialiased(true)
                .aspectRatio(contentMode: .fit)
                .frame(width: size, height: size)
                .shadow(color: Color.black.opacity(0.24), radius: size * 0.14, x: 0, y: 2)
        } else {
            RoundedRectangle(cornerRadius: size * 0.24, style: .continuous)
                .fill(ConverzePreferences.shared.colorTheme.primaryColor)
                .frame(width: size, height: size)
        }
    }
}


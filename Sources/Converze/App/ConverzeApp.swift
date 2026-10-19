import AppKit
import SwiftUI
import ServiceManagement
import Darwin

// MARK: - Converze macOS System Process & Menu Bar Agent Entry Point

@MainActor
public final class ConverzeAppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private var onboardingWindow: NSWindow?
    private var playgroundWindow: NSWindow?
    private var settingsWindow: NSWindow?

    public func applicationDidFinishLaunching(_ notification: Notification) {
        // Run as a system-wide macOS background agent (.accessory) across all Spaces & fullscreen apps
        NSApp.setActivationPolicy(.accessory)

        // Apply custom attached application icon
        if let appIcon = ConverzeAppIconLoader.appIconImage() {
            NSApp.applicationIconImage = appIcon
        }

        // Apply saved Light / Dark / System appearance
        ConverzePreferences.shared.applySystemAppearance()

        // Rebuild Menu Bar whenever language or preferences change
        ConverzePreferences.shared.onPreferencesChanged = { [weak self] in
            self?.rebuildMenuBarMenu()
        }

        // Setup native Menu Bar status item for Converze
        setupMenuBarMenu()

        // Register macOS Finder Services handler ("Convert with Converze Wheel")
        NSApp.servicesProvider = self
        NSUpdateDynamicServices()

        // Wire Radial Wheel execution callback
        DragMonitorService.shared.onExecuteTarget = { [weak self] target, selection in
            self?.executeWheelTarget(target, selection: selection)
        }

        // Start global system-wide Option-Drag & Option-Selection monitoring across macOS
        DragMonitorService.shared.startMonitoring()

        // Show First-Time Welcome & Setup Screen if not completed yet
        if !ConverzePreferences.shared.hasCompletedOnboarding {
            showOnboardingWindow()
        } else {
            let prefs = ConverzePreferences.shared
            ConversionHUDManager.shared.showActionMessage(
                prefs.tr(
                    "Converze rulează în fundal pe macOS. Ține ⌥ Option pe orice fișier.",
                    "Converze is running system-wide on macOS. Hold ⌥ Option on any file."
                )
            )
        }
    }

    public func applicationWillTerminate(_ notification: Notification) {
        DragMonitorService.shared.stopMonitoring()
    }

    public func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag {
            showSettingsWindow()
        }
        return true
    }

    private func setupMenuBarMenu() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let button = item.button {
            button.image = NSImage(
                systemSymbolName: "arrow.triangle.2.circlepath.circle.fill",
                accessibilityDescription: "Converze"
            )
            button.toolTip = "Converze"
        }
        self.statusItem = item
        rebuildMenuBarMenu()
    }

    private func rebuildMenuBarMenu() {
        guard let item = statusItem else { return }
        let prefs = ConverzePreferences.shared

        let menu = NSMenu(title: "Converze")
        let headerItem = NSMenuItem(
            title: prefs.tr(
                "Converze: Proces de sistem activ (⌥ Option)",
                "Converze: System Process Active (⌥ Option)"
            ),
            action: nil,
            keyEquivalent: ""
        )
        headerItem.isEnabled = false
        menu.addItem(headerItem)

        let subHeaderItem = NSMenuItem(
            title: prefs.tr(
                "Ține apăsat ⌥ (Option) și dă click stânga pe fișier pentru a-l converti",
                "Select any file by holding ⌥ (Option) and left-click on it to convert"
            ),
            action: nil,
            keyEquivalent: ""
        )
        subHeaderItem.isEnabled = false
        menu.addItem(subHeaderItem)
        menu.addItem(.separator())

        let convertSelectedItem = NSMenuItem(
            title: prefs.tr(
                "Convertește fișierul selectat",
                "Convert the selected file"
            ),
            action: #selector(openWheelForFinderSelection),
            keyEquivalent: "c"
        )
        convertSelectedItem.target = self
        menu.addItem(convertSelectedItem)

        let settingsItem = NSMenuItem(
            title: prefs.tr(
                "Setări generale",
                "General settings"
            ),
            action: #selector(openSettingsFromMenu),
            keyEquivalent: ","
        )
        settingsItem.target = self
        menu.addItem(settingsItem)

        let onboardingItem = NSMenuItem(
            title: prefs.tr(
                "Configurare inițială",
                "Initial configuration"
            ),
            action: #selector(openOnboardingFromMenu),
            keyEquivalent: "b"
        )
        onboardingItem.target = self
        menu.addItem(onboardingItem)

        let openPlaygroundItem = NSMenuItem(
            title: prefs.tr(
                "Spațiu de testare",
                "Interactive test playground"
            ),
            action: #selector(openPlaygroundFromMenu),
            keyEquivalent: "p"
        )
        openPlaygroundItem.target = self
        menu.addItem(openPlaygroundItem)

        menu.addItem(.separator())

        let loginItem = NSMenuItem(
            title: prefs.tr(
                "Pornește automat la bootare",
                "Launch automatically at boot"
            ),
            action: #selector(toggleLaunchAtLogin),
            keyEquivalent: ""
        )
        loginItem.target = self
        loginItem.state = (SMAppService.mainApp.status == .enabled) ? .on : .off
        menu.addItem(loginItem)

        menu.addItem(.separator())

        let quitItem = NSMenuItem(
            title: prefs.tr("Închide Converze", "Quit Converze"),
            action: #selector(NSApplication.terminate(_:)),
            keyEquivalent: "q"
        )
        menu.addItem(quitItem)

        item.menu = menu
    }

    @objc private func openWheelForFinderSelection() {
        let prefs = ConverzePreferences.shared
        if let urls = DragMonitorService.queryFinderSelectedFileURLs(), !urls.isEmpty {
            DragMonitorService.shared.openKeyboardWheel(for: urls, at: NSEvent.mouseLocation)
        } else {
            let panel = NSOpenPanel()
            panel.canChooseFiles = true
            panel.canChooseDirectories = true
            panel.allowsMultipleSelection = true
            panel.prompt = prefs.tr("Deschide în Converze", "Open in Converze")
            if panel.runModal() == .OK, !panel.urls.isEmpty {
                DragMonitorService.shared.openKeyboardWheel(for: panel.urls, at: NSEvent.mouseLocation)
            }
        }
    }

    @objc private func toggleLaunchAtLogin() {
        let prefs = ConverzePreferences.shared
        do {
            if SMAppService.mainApp.status == .enabled {
                try SMAppService.mainApp.unregister()
            } else {
                try SMAppService.mainApp.register()
            }
            rebuildMenuBarMenu()
        } catch {
            ConversionHUDManager.shared.showActionMessage(
                prefs.tr(
                    "Opțiunea de lansare la login necesită pornirea aplicației din /Applications",
                    "Launch at login requires running from /Applications"
                )
            )
        }
    }

    @objc private func openOnboardingFromMenu() {
        showOnboardingWindow()
    }

    @objc private func openPlaygroundFromMenu() {
        showPlaygroundWindow()
    }

    @objc private func openSettingsFromMenu() {
        showSettingsWindow()
    }

    public func application(_ sender: NSApplication, openFiles filenames: [String]) {
        let urls = filenames.map { URL(fileURLWithPath: $0) }
        DragMonitorService.shared.openKeyboardWheel(for: urls, at: NSEvent.mouseLocation)
        sender.reply(toOpenOrPrint: .success)
    }

    // Finder Service Handler ("Convert with Converze Wheel")
    @objc public func handleFinderService(
        _ pboard: NSPasteboard,
        userData: String?,
        error: AutoreleasingUnsafeMutablePointer<NSString?>
    ) {
        let urls = RadialInteractionOverlayView.extractRealFileURLs(from: pboard)
        guard !urls.isEmpty else { return }
        DragMonitorService.shared.openKeyboardWheel(for: urls, at: NSEvent.mouseLocation)
    }

    // MARK: - Execute Selected Wheel Slice

    public func executeWheelTarget(_ target: WheelTarget, selection: DraggedSelection) {
        switch target {
        case .format(let fmt):
            let label = FormatMatrix.displayLabel(for: fmt)
            ConversionHUDManager.shared.showProgress(
                title: "Converze: \(selection.displayName) → \(label)…"
            )
            Task {
                do {
                    let results = try await ConversionEngine.shared.convert(selection: selection, to: fmt)
                    await MainActor.run {
                        ConversionHUDManager.shared.showCompleted(results: results)
                        self.syncWithPlaygroundIfNeeded(results: results, selection: selection)
                    }
                } catch {
                    await MainActor.run {
                        ConversionHUDManager.shared.showError(error.localizedDescription)
                    }
                }
            }
        }
    }

    private func syncWithPlaygroundIfNeeded(results: [ConversionResult], selection: DraggedSelection) {
        let workspace = SampleWorkspaceManager.shared
        let sampleDirPath = workspace.sampleFolderURL.standardizedFileURL.path
        let isFromPlayground = selection.urls.contains { url in
            let std = url.standardizedFileURL.path
            return std.hasPrefix(sampleDirPath) || workspace.items.contains { $0.url.standardizedFileURL.path == std }
        }
        guard isFromPlayground else { return }
        for res in results {
            workspace.addConvertedFile(res.outputURL, near: selection.urls.first)
        }
    }

    // MARK: - Window Management

    public func showOnboardingWindow() {
        if onboardingWindow == nil {
            let window = NSWindow(
                contentRect: NSRect(x: 0, y: 0, width: 750, height: 490),
                styleMask: [.titled, .closable],
                backing: .buffered,
                defer: false
            )
            window.title = "Converze Settings"
            window.center()
            window.isReleasedWhenClosed = false
            let view = ConverzeOnboardingView { [weak self] in
                self?.onboardingWindow?.orderOut(nil)
                let prefs = ConverzePreferences.shared
                ConversionHUDManager.shared.showActionMessage(
                    prefs.tr(
                        "Converze este activ.",
                        "Converze is active."
                    )
                )
            }
            window.contentView = NSHostingView(rootView: view)
            self.onboardingWindow = window
        }
        NSApp.activate(ignoringOtherApps: true)
        onboardingWindow?.makeKeyAndOrderFront(nil)
    }

    public func showPlaygroundWindow() {
        if playgroundWindow == nil {
            let window = NSWindow(
                contentRect: NSRect(x: 0, y: 0, width: 1000, height: 660),
                styleMask: [.titled, .closable, .miniaturizable, .resizable],
                backing: .buffered,
                defer: false
            )
            window.title = "Test Playground"
            window.minSize = NSSize(width: 780, height: 520)
            window.center()
            window.isReleasedWhenClosed = false
            window.contentView = NSHostingView(rootView: InteractivePlaygroundView(
                onOpenSettings: { [weak self] in self?.showSettingsWindow() }
            ))
            self.playgroundWindow = window
            SampleWorkspaceManager.shared.playgroundWindow = window
        }
        NSApp.activate(ignoringOtherApps: true)
        playgroundWindow?.makeKeyAndOrderFront(nil)
    }

    public func showSettingsWindow() {
        if settingsWindow == nil {
            let window = NSWindow(
                contentRect: NSRect(x: 0, y: 0, width: 730, height: 490),
                styleMask: [.titled, .closable],
                backing: .buffered,
                defer: false
            )
            window.title = "Settings"
            window.center()
            window.isReleasedWhenClosed = false
            let view = SettingsView(
                onOpenOnboarding: { [weak self] in
                    self?.settingsWindow?.orderOut(nil)
                    self?.showOnboardingWindow()
                },
                onOpenPlayground: { [weak self] in
                    self?.showPlaygroundWindow()
                }
            )
            window.contentView = NSHostingView(rootView: view)
            self.settingsWindow = window
        }
        NSApp.activate(ignoringOtherApps: true)
        settingsWindow?.makeKeyAndOrderFront(nil)
    }
}

@main
public struct ConverzeMain {
    public static func main() {
        setenv("OS_ACTIVITY_MODE", "disable", 1)
        let app = NSApplication.shared
        let delegate = ConverzeAppDelegate()
        app.delegate = delegate
        app.run()
    }
}

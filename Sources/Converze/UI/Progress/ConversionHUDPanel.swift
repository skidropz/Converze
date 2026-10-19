import AppKit
import SwiftUI

// MARK: - Top-Right Floating Progress & Completion HUD Toast (Converze)

@MainActor
public final class ConversionHUDManager: ObservableObject {
    public static let shared = ConversionHUDManager()

    @Published public var isProcessing: Bool = false
    @Published public var statusMessage: String = ""
    @Published public var subtitleMessage: String = ""
    @Published public var latestOutputURL: URL? = nil
    @Published public var recentJobs: [ConversionResult] = []

    private var hudPanel: NSPanel?
    private var dismissTask: Task<Void, Never>?

    private init() {}

    public func showProgress(title: String, subtitle: String? = nil) {
        dismissTask?.cancel()
        isProcessing = true
        statusMessage = title
        subtitleMessage = subtitle ?? ConverzePreferences.shared.tr(
            "Procesare locală pe Mac-ul tău…",
            "Processing locally on your Mac…"
        )
        latestOutputURL = nil
        presentPanel()
    }

    public func showCompleted(results: [ConversionResult]) {
        dismissTask?.cancel()
        isProcessing = false
        recentJobs.insert(contentsOf: results, at: 0)
        if recentJobs.count > 25 {
            recentJobs = Array(recentJobs.prefix(25))
        }

        FeedbackSoundPlayer.shared.playCompletionPop()
        let prefs = ConverzePreferences.shared

        if let first = results.first {
            latestOutputURL = first.outputURL
            if results.count == 1 {
                statusMessage = first.outputURL.lastPathComponent
                subtitleMessage = prefs.tr(
                    String(format: "Salvat lângă fișierul original în %.2fs", first.durationSeconds),
                    String(format: "Saved beside original in %.2fs", first.durationSeconds)
                )
            } else {
                statusMessage = prefs.tr(
                    "\(results.count) fișiere convertite cu succes",
                    "Converted \(results.count) files"
                )
                subtitleMessage = prefs.tr("Salvate lângă fișierele originale", "Saved beside originals")
            }

            if prefs.autoRevealInFinder {
                NSWorkspace.shared.activateFileViewerSelecting(results.map(\.outputURL))
            }
        }

        presentPanel()
        scheduleDismiss(after: 3.6)
    }

    public func showActionMessage(_ message: String) {
        dismissTask?.cancel()
        isProcessing = false
        latestOutputURL = nil
        statusMessage = message
        subtitleMessage = "Converze"
        FeedbackSoundPlayer.shared.playCompletionPop()
        presentPanel()
        scheduleDismiss(after: 2.8)
    }

    public func showError(_ errorMessage: String) {
        dismissTask?.cancel()
        isProcessing = false
        latestOutputURL = nil
        statusMessage = ConverzePreferences.shared.tr(
            "Conversia nu a putut fi finalizată",
            "Conversion could not complete"
        )
        subtitleMessage = errorMessage
        presentPanel()
        scheduleDismiss(after: 4.5)
    }

    private func presentPanel() {
        if hudPanel == nil {
            let rect = NSRect(x: 0, y: 0, width: 356, height: 86)
            let panel = NSPanel(
                contentRect: rect,
                styleMask: [.borderless, .nonactivatingPanel],
                backing: .buffered,
                defer: false
            )
            panel.level = .statusBar
            panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
            panel.isOpaque = false
            panel.backgroundColor = .clear
            panel.hasShadow = false
            panel.contentView = NSHostingView(rootView: ConversionHUDToastView(manager: self))
            self.hudPanel = panel
        }

        guard let panel = hudPanel else { return }

        // Position in TOP-RIGHT corner (dreapta sus), just below the macOS menu bar
        let mouseLoc = NSEvent.mouseLocation
        let activeScreen = NSScreen.screens.first(where: { NSMouseInRect(mouseLoc, $0.frame, false) })
            ?? NSScreen.main
            ?? NSScreen.screens.first

        if let visible = activeScreen?.visibleFrame {
            let origin = NSPoint(
                x: visible.maxX - panel.frame.width - 20,
                y: visible.maxY - panel.frame.height - 16
            )
            panel.setFrameOrigin(origin)
        }
        panel.orderFrontRegardless()
    }

    private func scheduleDismiss(after seconds: Double) {
        dismissTask = Task { @MainActor in
            try? await Task.sleep(nanoseconds: UInt64(seconds * 1_000_000_000))
            guard !Task.isCancelled else { return }
            hudPanel?.orderOut(nil)
        }
    }

    public func dismissNow() {
        dismissTask?.cancel()
        hudPanel?.orderOut(nil)
    }
}

public struct ConversionHUDToastView: View {
    @ObservedObject public var manager: ConversionHUDManager
    @ObservedObject public var prefs = ConverzePreferences.shared

    public init(manager: ConversionHUDManager) {
        self.manager = manager
    }

    public var body: some View {
        let isDark: Bool = prefs.isDarkMode
        let theme: ConverzeColorTheme = prefs.colorTheme
        let iconBg: Color = theme.primaryColor.opacity(isDark ? 0.24 : 0.14)
        let titleColor: Color = isDark ? .white : .primary
        let subColor: Color = isDark ? Color.white.opacity(0.70) : .secondary
        let cardTint: Color = isDark
            ? Color(red: 0.10, green: 0.12, blue: 0.18).opacity(0.92)
            : Color.white.opacity(0.92)
        let strokeColor: Color = isDark ? Color.white.opacity(0.18) : Color.white.opacity(0.85)
        let shadowColor: Color = Color.black.opacity(isDark ? 0.45 : 0.16)

        HStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill(iconBg)
                    .frame(width: 34, height: 34)

                if manager.isProcessing {
                    ProgressView()
                        .controlSize(.small)
                } else if manager.latestOutputURL != nil {
                    Image(systemName: "checkmark.circle.fill")
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(theme.primaryColor)
                } else {
                    ConverzeAppIconView(size: 28)
                }
            }

            VStack(alignment: .leading, spacing: 2) {
                Text(manager.statusMessage)
                    .font(.system(size: 12.5, weight: .bold, design: .rounded))
                    .foregroundColor(titleColor)
                    .lineLimit(1)
                Text(manager.subtitleMessage)
                    .font(.system(size: 11, design: .rounded))
                    .foregroundColor(subColor)
                    .lineLimit(1)
            }

            Spacer(minLength: 4)

            if let outputURL = manager.latestOutputURL {
                Button {
                    NSWorkspace.shared.activateFileViewerSelecting([outputURL])
                    manager.dismissNow()
                } label: {
                    Text(prefs.tr("Arată", "Reveal"))
                        .font(.system(size: 11, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .padding(.horizontal, 11)
                        .padding(.vertical, 5)
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
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 12)
        .frame(width: 344)
        .background(
            ZStack {
                VisualEffectGlassView()
                cardTint
            }
        )
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .stroke(strokeColor, lineWidth: 1)
        )
        .shadow(color: shadowColor, radius: 12, x: 0, y: 5)
    }
}

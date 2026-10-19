import AppKit
import SwiftUI

// MARK: - Sample Workspace Manager for Interactive Desktop Playground

@MainActor
public final class SampleWorkspaceManager: ObservableObject {
    public static let shared = SampleWorkspaceManager()

    public struct PlaygroundItem: Identifiable, Equatable {
        public let id: String
        public var url: URL
        public var position: CGPoint // normalized 0...1
        public var isProcessing: Bool = false
        public var badgeText: String? = nil

        public var name: String { url.lastPathComponent }
        public var ext: String { url.pathExtension.lowercased() }
        public var category: FileCategory { FormatMatrix.category(for: ext) }
    }

    @Published public var items: [PlaygroundItem] = []
    @Published public var selectedItemID: String? = nil
    @Published public var bannerText: String = ""
    public weak var playgroundWindow: NSWindow?

    public let sampleFolderURL: URL

    private init() {
        let base = FileManager.default.temporaryDirectory.appendingPathComponent("Converze_Playground", isDirectory: true)
        try? FileManager.default.createDirectory(at: base, withIntermediateDirectories: true)
        self.sampleFolderURL = base
        createSampleFilesIfNeeded()
    }

    public func createSampleFilesIfNeeded() {
        let img1URL = sampleFolderURL.appendingPathComponent("Peisaj_Alpin.png")
        let img2URL = sampleFolderURL.appendingPathComponent("Design_Studiu.jpg")
        let docURL = sampleFolderURL.appendingPathComponent("Note_Proiect.md")
        let csvURL = sampleFolderURL.appendingPathComponent("Buget_2026.csv")
        let subURL = sampleFolderURL.appendingPathComponent("Subtitrare_Demo.srt")
        let audioURL = sampleFolderURL.appendingPathComponent("Acord_Sintetizator.wav")

        if !FileManager.default.fileExists(atPath: img1URL.path) {
            createSampleImage(at: img1URL, isSecondStyle: false, format: .png)
        }
        if !FileManager.default.fileExists(atPath: img2URL.path) {
            createSampleImage(at: img2URL, isSecondStyle: true, format: .jpeg)
        }
        if !FileManager.default.fileExists(atPath: docURL.path) {
            let md = """
            # Converze — Convertor Rapid pentru macOS
            Trage orice fișier și ține apăsată tasta **Option (⌥)** pentru a deschide roata radială.

            - 275 de conversii locale rapide
            - Procesare 100% privată pe dispozitiv
            - Suport complet White Mode / Dark Mode și Română / English
            """
            try? md.write(to: docURL, atomically: true, encoding: .utf8)
        }
        if !FileManager.default.fileExists(atPath: csvURL.path) {
            let csv = "Produs,Trimestru,Venit,Moneda\nConverze Pro,T1,48500,EUR\nConverze Team,T2,64200,EUR\nConverze Enterprise,T3,91800,EUR\n"
            try? csv.write(to: csvURL, atomically: true, encoding: .utf8)
        }
        if !FileManager.default.fileExists(atPath: subURL.path) {
            let srt = """
            1
            00:00:01,000 --> 00:00:04,000
            Bine ai venit în Converze pentru macOS!

            2
            00:00:04,500 --> 00:00:08,000
            Ține apăsat Option (⌥) în timp ce miști un fișier pentru a-l converti instantaneu.
            """
            try? srt.write(to: subURL, atomically: true, encoding: .utf8)
        }
        if !FileManager.default.fileExists(atPath: audioURL.path) {
            createSampleWAV(at: audioURL)
        }

        items = [
            PlaygroundItem(id: "img1", url: img1URL, position: CGPoint(x: 0.20, y: 0.34)),
            PlaygroundItem(id: "img2", url: img2URL, position: CGPoint(x: 0.38, y: 0.28)),
            PlaygroundItem(id: "doc1", url: docURL, position: CGPoint(x: 0.58, y: 0.33)),
            PlaygroundItem(id: "csv1", url: csvURL, position: CGPoint(x: 0.77, y: 0.36)),
            PlaygroundItem(id: "sub1", url: subURL, position: CGPoint(x: 0.29, y: 0.65)),
            PlaygroundItem(id: "aud1", url: audioURL, position: CGPoint(x: 0.66, y: 0.65))
        ]
    }

    public func addConvertedFile(_ url: URL, near sourceURL: URL?) {
        let resolvedURL = FormatMatrix.resolveFilePathURL(url)
        let resolvedSource = sourceURL.map { FormatMatrix.resolveFilePathURL($0) }

        if let idx = items.firstIndex(where: { $0.url.standardizedFileURL == resolvedURL.standardizedFileURL }) {
            items[idx].url = resolvedURL
            items[idx].badgeText = ConverzePreferences.shared.tr("Convertit", "Converted")
            selectedItemID = items[idx].id
            bannerText = "✅ \(resolvedURL.lastPathComponent)"
            return
        }

        var pos = CGPoint(x: 0.50, y: 0.50)
        if let src = resolvedSource,
           let sourceItem = items.first(where: { $0.url.standardizedFileURL == src.standardizedFileURL }) {
            let offsetX: CGFloat = sourceItem.position.x > 0.72 ? -0.14 : 0.14
            let offsetY: CGFloat = sourceItem.position.y > 0.72 ? -0.08 : 0.08
            pos = CGPoint(
                x: min(0.88, max(0.12, sourceItem.position.x + offsetX)),
                y: min(0.82, max(0.18, sourceItem.position.y + offsetY))
            )
        }

        let newItem = PlaygroundItem(
            id: UUID().uuidString,
            url: resolvedURL,
            position: pos,
            isProcessing: false,
            badgeText: "NEW · \(resolvedURL.pathExtension.uppercased())"
        )
        withAnimation(.spring(response: 0.38, dampingFraction: 0.68)) {
            items.append(newItem)
            selectedItemID = newItem.id
            bannerText = "✅ \(resolvedURL.lastPathComponent)"
        }
    }

    private func createSampleImage(at url: URL, isSecondStyle: Bool, format: NSBitmapImageRep.FileType) {
        let width = 800
        let height = 600
        let colorSpace = CGColorSpaceCreateDeviceRGB()
        guard let ctx = CGContext(
            data: nil,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: 0,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else { return }

        let colors: [CGColor] = isSecondStyle
            ? [
                CGColor(red: 0.14, green: 0.38, blue: 0.92, alpha: 1.0),
                CGColor(red: 0.06, green: 0.72, blue: 0.50, alpha: 1.0),
                CGColor(red: 0.56, green: 0.22, blue: 0.90, alpha: 1.0)
            ]
            : [
                CGColor(red: 0.24, green: 0.60, blue: 0.88, alpha: 1.0),
                CGColor(red: 0.72, green: 0.87, blue: 0.94, alpha: 1.0),
                CGColor(red: 0.26, green: 0.52, blue: 0.30, alpha: 1.0)
            ]

        if let grad = CGGradient(colorsSpace: colorSpace, colors: colors as CFArray, locations: [0.0, 0.55, 1.0]) {
            ctx.drawLinearGradient(grad, start: CGPoint(x: 0, y: height), end: CGPoint(x: width, y: 0), options: [])
        }

        ctx.setFillColor(CGColor(red: 0.95, green: 0.98, blue: 1.0, alpha: 0.90))
        ctx.fillEllipse(in: CGRect(x: 280, y: 200, width: 240, height: 240))

        guard let cgImg = ctx.makeImage() else { return }
        let rep = NSBitmapImageRep(cgImage: cgImg)
        if let data = rep.representation(using: format, properties: [.compressionFactor: 0.92]) {
            try? data.write(to: url)
        }
    }

    private func createSampleWAV(at url: URL) {
        let sampleRate: Int32 = 44100
        let durationSeconds = 2
        let numSamples = Int(sampleRate) * durationSeconds
        var pcmData = [Int16](repeating: 0, count: numSamples)
        for i in 0..<numSamples {
            let t = Double(i) / Double(sampleRate)
            let env = sin(.pi * t / Double(durationSeconds))
            let wave = sin(2.0 * .pi * 440.0 * t) * 0.5 + sin(2.0 * .pi * 659.25 * t) * 0.3
            pcmData[i] = Int16(wave * env * 18000.0)
        }

        let dataSize = Int32(numSamples * 2)
        let fileSize = 36 + dataSize
        var header = Data()
        header.append("RIFF".data(using: .ascii)!)
        header.append(withUnsafeBytes(of: fileSize.littleEndian) { Data($0) })
        header.append("WAVEfmt ".data(using: .ascii)!)
        header.append(withUnsafeBytes(of: Int32(16).littleEndian) { Data($0) })
        header.append(withUnsafeBytes(of: Int16(1).littleEndian) { Data($0) })
        header.append(withUnsafeBytes(of: Int16(1).littleEndian) { Data($0) })
        header.append(withUnsafeBytes(of: sampleRate.littleEndian) { Data($0) })
        header.append(withUnsafeBytes(of: (sampleRate * 2).littleEndian) { Data($0) })
        header.append(withUnsafeBytes(of: Int16(2).littleEndian) { Data($0) })
        header.append(withUnsafeBytes(of: Int16(16).littleEndian) { Data($0) })
        header.append("data".data(using: .ascii)!)
        header.append(withUnsafeBytes(of: dataSize.littleEndian) { Data($0) })

        pcmData.withUnsafeBufferPointer { ptr in
            header.append(UnsafeBufferPointer(
                start: UnsafeRawPointer(ptr.baseAddress!)?.assumingMemoryBound(to: UInt8.self),
                count: Int(dataSize)
            ))
        }
        try? header.write(to: url)
    }
}

// MARK: - Shrunk Mini-File Badge Next to Cursor

public struct ShrunkCursorFileBadge: View {
    public let itemURL: URL
    public let isWheelOpen: Bool
    public let hoveredTarget: WheelTarget?
    public let isSuctionDropping: Bool
    @ObservedObject public var prefs = ConverzePreferences.shared

    public var body: some View {
        let isHoveringSlice: Bool = (hoveredTarget != nil)
        let theme: ConverzeColorTheme = prefs.colorTheme
        let iconDim: CGFloat = isWheelOpen ? 44.0 : 62.0
        let fontSize: CGFloat = isWheelOpen ? 10.5 : 12.0
        let pillColor: Color = isHoveringSlice
            ? theme.primaryColor
            : Color(red: 0.0, green: 0.388, blue: 0.851)

        let badgeScale: CGFloat = {
            if isSuctionDropping { return 0.12 }
            if isWheelOpen { return isHoveringSlice ? 0.84 : 0.72 }
            return 1.0
        }()
        let badgeAngle: Double = isHoveringSlice ? -6.0 : 0.0
        let badgeAlpha: Double = isSuctionDropping ? 0.0 : 0.96

        return VStack(spacing: 4) {
            ZStack(alignment: .bottomTrailing) {
                Image(nsImage: FileIconCache.shared.icon(for: itemURL))
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: iconDim, height: iconDim)
                    .shadow(color: Color.black.opacity(0.32), radius: 8, x: 0, y: 4)

                if let target = hoveredTarget {
                    Text(target.wheelLabel)
                        .font(.system(size: 9.5, weight: .heavy, design: .rounded))
                        .foregroundColor(.white)
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(Capsule().fill(theme.primaryColor))
                        .overlay(Capsule().stroke(Color.white, lineWidth: 1.2))
                        .offset(x: 8, y: 4)
                }
            }

            HStack(spacing: 4) {
                Text(itemURL.lastPathComponent)
                    .font(.system(size: fontSize, weight: .semibold))
                    .foregroundColor(.white)
                    .lineLimit(1)

                if let target = hoveredTarget {
                    Text("→ \(target.wheelLabel)")
                        .font(.system(size: 10.5, weight: .bold, design: .rounded))
                        .foregroundColor(theme.sparkSecondary)
                }
            }
            .padding(.horizontal, 7)
            .padding(.vertical, 2.5)
            .background(Capsule().fill(pillColor))
            .overlay(Capsule().stroke(Color.white.opacity(0.65), lineWidth: 0.8))
            .shadow(color: Color.black.opacity(0.22), radius: 4, x: 0, y: 2)
        }
        .scaleEffect(badgeScale)
        .rotationEffect(.degrees(badgeAngle))
        .opacity(badgeAlpha)
        .animation(.interactiveSpring(response: 0.22, dampingFraction: 0.65), value: isWheelOpen)
        .animation(.interactiveSpring(response: 0.18, dampingFraction: 0.62), value: isHoveringSlice)
        .animation(.easeIn(duration: 0.18), value: isSuctionDropping)
        .allowsHitTesting(false)
    }
}

// MARK: - Interactive Desktop Playground View (Converze)

public struct InteractivePlaygroundView: View {
    @ObservedObject public var workspace = SampleWorkspaceManager.shared
    @ObservedObject public var dragMonitor = DragMonitorService.shared
    @ObservedObject public var prefs = ConverzePreferences.shared
    @StateObject private var inlineWheelVM = RadialWheelViewModel()

    @State private var draggingItem: SampleWorkspaceManager.PlaygroundItem? = nil
    @State private var dragStartNormPos: CGPoint? = nil
    @State private var cursorInCanvas: CGPoint = .zero
    @State private var wheelAnchorInCanvas: CGPoint? = nil
    @State private var isSuctionDropping: Bool = false
    @State private var dropBurstPoint: CGPoint? = nil
    @State private var dropBurstProgress: CGFloat = 0.0

    public var onOpenSettings: (() -> Void)?

    public init(onOpenSettings: (() -> Void)? = nil) {
        self.onOpenSettings = onOpenSettings
    }

    public var body: some View {
        let isDark: Bool = prefs.isDarkMode
        let theme: ConverzeColorTheme = prefs.colorTheme

        VStack(spacing: 0) {
            playgroundTopBar(isDark: isDark, theme: theme)
            Divider()
            GeometryReader { geo in
                playgroundCanvas(size: geo.size, isDark: isDark, theme: theme)
            }
        }
        .preferredColorScheme(prefs.preferredColorScheme)
    }

    private func playgroundTopBar(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let optionHeld: Bool = dragMonitor.isOptionPressed
        let dotColor: Color = optionHeld ? theme.primaryColor : Color.green
        let statusText: String = optionHeld
            ? prefs.tr("⌥ Option Apăsat — Mișcă fișierul!", "⌥ Option Held — Move the file!")
            : prefs.tr("Ține ⌥ Option + Mișcă orice fișier", "Hold ⌥ Option + Move any file")
        let statusTextColor: Color = optionHeld ? theme.primaryColor : .primary
        let statusBgColor: Color = optionHeld ? theme.primaryColor.opacity(0.16) : Color.primary.opacity(0.06)
        let headerBg: Color = isDark
            ? Color(red: 0.09, green: 0.11, blue: 0.16)
            : Color(NSColor.windowBackgroundColor)

        return HStack(spacing: 14) {
            HStack(spacing: 8) {
                ConverzeAppIconView(size: 28)
                Text("Converze")
                    .font(.system(size: 17, weight: .bold, design: .rounded))
                Text(prefs.tr("Convertor Rapid de Sistem macOS", "macOS System-Wide Offline File Converter"))
                    .font(.system(size: 12, weight: .medium, design: .rounded))
                    .foregroundColor(.secondary)
            }

            Spacer()

            HStack(spacing: 6) {
                Circle()
                    .fill(dotColor)
                    .frame(width: 8, height: 8)
                Text(statusText)
                    .font(.system(size: 11.5, weight: .semibold, design: .rounded))
                    .foregroundColor(statusTextColor)
            }
            .padding(.horizontal, 11)
            .padding(.vertical, 5)
            .background(Capsule().fill(statusBgColor))

            Button {
                NSWorkspace.shared.open(workspace.sampleFolderURL)
            } label: {
                Label(prefs.tr("Deschide în Finder", "Open in Finder"), systemImage: "folder")
                    .font(.system(size: 11.5, weight: .medium))
            }
            .controlSize(.small)
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 12)
        .background(headerBg)
    }

    private func playgroundCanvas(
        size: CGSize,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        ZStack {
            // Clicking anywhere on the background closes any open inline wheel
            wallpaperBackground(isDark: isDark, theme: theme)
                .contentShape(Rectangle())
                .onTapGesture {
                    workspace.selectedItemID = nil
                    closeInlineWheel()
                }

            heroBannerView()
                .allowsHitTesting(false)

            ForEach(workspace.items) { item in
                desktopFileIcon(item: item, canvasSize: size)
                    .position(
                        x: item.position.x * size.width,
                        y: item.position.y * size.height
                    )
            }

            if let anchor = wheelAnchorInCanvas, inlineWheelVM.isVisible {
                RadialWheelView(viewModel: inlineWheelVM)
                    .position(x: anchor.x, y: anchor.y)
                    .zIndex(20)
            }

            if draggingItem != nil && wheelAnchorInCanvas == nil {
                floatingOptionPrompt(canvasSize: size, isDark: isDark, theme: theme)
            }

            if let dragged = draggingItem {
                draggedBadgeOverlay(dragged: dragged)
            }

            if let burstPt = dropBurstPoint {
                WheelSliceSparkBurstView(progress: dropBurstProgress, theme: theme)
                    .position(burstPt)
                    .zIndex(35)
            }

            bottomLegendBar(isDark: isDark, theme: theme)
                .allowsHitTesting(false)
        }
        .coordinateSpace(name: "PlaygroundCanvas")
        .contentShape(Rectangle())
        .onAppear {
            setupInlineWheelCallbacks()
        }
        .onChange(of: dragMonitor.isOptionPressed) { optionNow in
            guard optionNow, let dragged = draggingItem else { return }
            openInlineWheel(for: dragged, at: cursorInCanvas, canvasSize: size)
        }
        .onDrop(of: [.fileURL], isTargeted: nil) { providers, location in
            handleExternalFileDrop(providers: providers, location: location, canvasSize: size)
        }
    }

    private func wallpaperBackground(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let gradientColors: [Color] = isDark
            ? [
                Color(red: 0.07, green: 0.09, blue: 0.15),
                theme.deepStop.opacity(0.55),
                Color(red: 0.05, green: 0.07, blue: 0.12)
            ]
            : [
                theme.lightStop.opacity(0.85),
                theme.primaryColor.opacity(0.85),
                theme.deepStop.opacity(0.90)
            ]

        return LinearGradient(
            colors: gradientColors,
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
        .ignoresSafeArea()
    }

    private func heroBannerView() -> some View {
        let titleText: String = prefs.tr(
            "Trage un fișier, ține ⌥ Option apăsat și eliberează pe extensie.",
            "Drag a file, hold ⌥ Option, and drop onto an extension."
        )
        let subText: String = workspace.bannerText.isEmpty
            ? prefs.tr(
                "Funcționează identic și direct în macOS Finder / pe Desktop! (ESC sau click în afară închide roata)",
                "Works identically right inside macOS Finder and on your Desktop! (ESC or outside click closes wheel)"
            )
            : workspace.bannerText

        return VStack(spacing: 6) {
            Text(titleText)
                .font(.system(size: 24, weight: .bold, design: .rounded))
                .foregroundColor(.white)
                .shadow(color: .black.opacity(0.22), radius: 6, x: 0, y: 2)

            Text(subText)
                .font(.system(size: 13, weight: .medium, design: .rounded))
                .foregroundColor(.white.opacity(0.95))
                .padding(.horizontal, 14)
                .padding(.vertical, 5)
                .background(Capsule().fill(Color.black.opacity(0.26)))
        }
        .frame(maxHeight: .infinity, alignment: .top)
        .padding(.top, 20)
    }

    private func floatingOptionPrompt(
        canvasSize: CGSize,
        isDark: Bool,
        theme: ConverzeColorTheme
    ) -> some View {
        let textColor: Color = isDark ? .white : Color.black.opacity(0.84)
        let pillBg: Color = isDark
            ? Color(red: 0.11, green: 0.13, blue: 0.18).opacity(0.95)
            : Color.white.opacity(0.95)
        let posX: CGFloat = min(max(110.0, cursorInCanvas.x), canvasSize.width - 110.0)
        let posY: CGFloat = max(36.0, cursorInCanvas.y - 68.0)

        return HStack(spacing: 6) {
            Text("⌥")
                .font(.system(size: 13, weight: .bold, design: .rounded))
                .foregroundColor(theme.primaryColor)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(
                    RoundedRectangle(cornerRadius: 5, style: .continuous)
                        .fill(theme.primaryColor.opacity(0.16))
                )
            Text(prefs.tr("Ține Option pentru Conversie", "Hold Option to Convert"))
                .font(.system(size: 12, weight: .semibold, design: .rounded))
                .foregroundColor(textColor)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 7)
        .background(Capsule().fill(pillBg))
        .shadow(color: Color.black.opacity(0.20), radius: 8, x: 0, y: 4)
        .position(x: posX, y: posY)
        .zIndex(25)
        .allowsHitTesting(false)
    }

    private func draggedBadgeOverlay(dragged: SampleWorkspaceManager.PlaygroundItem) -> some View {
        let isWheelOpen: Bool = (wheelAnchorInCanvas != nil && inlineWheelVM.isVisible)
        let badgePos: CGPoint
        if isSuctionDropping, let burstPt = dropBurstPoint {
            badgePos = burstPt
        } else if isWheelOpen {
            badgePos = CGPoint(x: cursorInCanvas.x + 26.0, y: cursorInCanvas.y + 22.0)
        } else {
            badgePos = cursorInCanvas
        }

        return ShrunkCursorFileBadge(
            itemURL: dragged.url,
            isWheelOpen: isWheelOpen,
            hoveredTarget: inlineWheelVM.currentlyHoveredTarget,
            isSuctionDropping: isSuctionDropping
        )
        .position(badgePos)
        .zIndex(30)
    }

    private func bottomLegendBar(isDark: Bool, theme: ConverzeColorTheme) -> some View {
        let textColor: Color = isDark ? .white : Color.black.opacity(0.80)
        let barBg: Color = isDark
            ? Color(red: 0.11, green: 0.13, blue: 0.18).opacity(0.92)
            : Color.white.opacity(0.92)

        return HStack(spacing: 14) {
            Text("⌥ Option")
                .font(.system(size: 11.5, weight: .bold, design: .rounded))
                .foregroundColor(.white)
                .padding(.horizontal, 9)
                .padding(.vertical, 4)
                .background(Capsule().fill(theme.primaryColor))

            Text(
                prefs.tr(
                    "275 conversii locale · Imagini, PDF, Audio, Video, Documente, Arhive",
                    "275 local conversions · Images, PDF, Audio, Video, Documents, Archives"
                )
            )
            .font(.system(size: 12, weight: .semibold, design: .rounded))
            .foregroundColor(textColor)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 10)
        .background(RoundedRectangle(cornerRadius: 14, style: .continuous).fill(barBg))
        .shadow(color: Color.black.opacity(0.18), radius: 12, x: 0, y: 4)
        .frame(maxHeight: .infinity, alignment: .bottom)
        .padding(.bottom, 18)
    }

    private func handleExternalFileDrop(
        providers: [NSItemProvider],
        location: CGPoint,
        canvasSize: CGSize
    ) -> Bool {
        for provider in providers {
            _ = provider.loadObject(ofClass: URL.self) { url, _ in
                if let url {
                    Task { @MainActor in
                        let resolved = FormatMatrix.resolveFilePathURL(url)
                        let normPos = CGPoint(
                            x: min(0.9, max(0.1, location.x / max(1, canvasSize.width))),
                            y: min(0.85, max(0.15, location.y / max(1, canvasSize.height)))
                        )
                        let newItem = SampleWorkspaceManager.PlaygroundItem(
                            id: UUID().uuidString,
                            url: resolved,
                            position: normPos
                        )
                        if !workspace.items.contains(where: { $0.url == resolved }) {
                            workspace.items.append(newItem)
                        }
                        openInlineWheel(for: newItem, at: location, canvasSize: canvasSize)
                    }
                }
            }
        }
        return true
    }

    // MARK: - Desktop File Icon with Smooth Option-Drag & Drop-onto-Extension

    private func desktopFileIcon(item: SampleWorkspaceManager.PlaygroundItem, canvasSize: CGSize) -> some View {
        let isSelected: Bool = (workspace.selectedItemID == item.id)
        let isBeingDragged: Bool = (draggingItem?.id == item.id)
        let theme: ConverzeColorTheme = prefs.colorTheme
        let iconBg: Color = isSelected ? Color.black.opacity(0.24) : Color.clear
        let iconOpacity: Double = item.isProcessing ? 0.45 : 1.0
        let labelBg: Color = isSelected ? theme.primaryColor : Color.black.opacity(0.30)
        let itemScale: CGFloat = isBeingDragged ? 0.88 : 1.0
        let itemAlpha: Double = isBeingDragged ? 0.35 : 1.0

        return VStack(spacing: 4) {
            ZStack {
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(iconBg)
                    .frame(width: 74, height: 74)

                Image(nsImage: FileIconCache.shared.icon(for: item.url))
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: 62, height: 62)
                    .shadow(color: .black.opacity(0.25), radius: 4, x: 0, y: 2)
                    .opacity(iconOpacity)

                if item.isProcessing {
                    ProgressView()
                        .controlSize(.small)
                }

                if !item.isProcessing, let badge = item.badgeText {
                    Text(badge)
                        .font(.system(size: 9, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                        .padding(.horizontal, 5)
                        .padding(.vertical, 2)
                        .background(Capsule().fill(theme.primaryColor))
                        .offset(x: 22, y: -24)
                }
            }

            Text(item.name)
                .font(.system(size: 12, weight: .medium))
                .foregroundColor(.white)
                .lineLimit(1)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(RoundedRectangle(cornerRadius: 4, style: .continuous).fill(labelBg))
                .frame(maxWidth: 132)
        }
        .scaleEffect(itemScale)
        .opacity(itemAlpha)
        .animation(.spring(response: 0.22, dampingFraction: 0.75), value: isBeingDragged)
        .onTapGesture(count: 2) {
            NSWorkspace.shared.open(item.url)
        }
        .gesture(
            DragGesture(minimumDistance: 1, coordinateSpace: .named("PlaygroundCanvas"))
                .onChanged { value in
                    handlePlaygroundDragChanged(value: value, item: item, canvasSize: canvasSize)
                }
                .onEnded { value in
                    handlePlaygroundDragEnded(value: value)
                }
        )
    }

    private func handlePlaygroundDragChanged(
        value: DragGesture.Value,
        item: SampleWorkspaceManager.PlaygroundItem,
        canvasSize: CGSize
    ) {
        cursorInCanvas = value.location

        if draggingItem?.id != item.id {
            draggingItem = item
            dragStartNormPos = item.position
            workspace.selectedItemID = item.id
            dragMonitor.isInAppPlaygroundDragActive = true
        }

        if DragMonitorService.isHardwareOptionPressed() {
            openInlineWheel(for: item, at: value.location, canvasSize: canvasSize)
        }

        if let anchor = wheelAnchorInCanvas, inlineWheelVM.isVisible {
            let dx = value.location.x - anchor.x
            let dy = value.location.y - anchor.y
            inlineWheelVM.updateCursor(dx: dx, dy: dy)
        } else if let startPos = dragStartNormPos,
                  let idx = workspace.items.firstIndex(where: { $0.id == item.id }) {
            let newX = min(0.92, max(0.08, startPos.x + value.translation.width / max(1, canvasSize.width)))
            let newY = min(0.86, max(0.14, startPos.y + value.translation.height / max(1, canvasSize.height)))
            workspace.items[idx].position = CGPoint(x: newX, y: newY)
        }
    }

    private func handlePlaygroundDragEnded(value: DragGesture.Value) {
        cursorInCanvas = value.location

        if let anchor = wheelAnchorInCanvas, inlineWheelVM.isVisible {
            let previousSelected = inlineWheelVM.selectedIndex
            let dx = value.location.x - anchor.x
            let dy = value.location.y - anchor.y
            inlineWheelVM.updateCursor(dx: dx, dy: dy)

            let resolvedIndex = inlineWheelVM.selectedIndex ?? previousSelected
            if let selectedIdx = resolvedIndex,
               selectedIdx >= 0,
               selectedIdx < inlineWheelVM.targets.count {
                inlineWheelVM.selectedIndex = selectedIdx
                let sliceOffset = inlineWheelVM.metrics.labelCenterOffset(
                    index: selectedIdx,
                    count: inlineWheelVM.targets.count
                )
                let targetPoint = CGPoint(
                    x: anchor.x + sliceOffset.x,
                    y: anchor.y + sliceOffset.y
                )
                performDropIntoSliceAnimation(at: targetPoint) {
                    inlineWheelVM.triggerSelected()
                }
                return
            }
        }

        finishPlaygroundDrag()
    }

    // MARK: - Inline Wheel & Drop-into-Extension Animations

    private func setupInlineWheelCallbacks() {
        inlineWheelVM.onSelectTarget = { target, selection in
            closeInlineWheel()
            dragMonitor.onExecuteTarget?(target, selection)
        }
        inlineWheelVM.onCancel = {
            finishPlaygroundDrag()
        }
        dragMonitor.onEscapePressed = {
            if inlineWheelVM.isVisible || draggingItem != nil {
                finishPlaygroundDrag()
            }
        }
    }

    private func openInlineWheel(
        for item: SampleWorkspaceManager.PlaygroundItem,
        at point: CGPoint,
        canvasSize: CGSize
    ) {
        let sel = DraggedSelection(urls: [item.url])
        if wheelAnchorInCanvas == nil || !inlineWheelVM.isVisible {
            let padX: CGFloat = 115
            let padY: CGFloat = 115
            let clamped = CGPoint(
                x: canvasSize.width > 260 ? min(max(padX, point.x), canvasSize.width - padX) : point.x,
                y: canvasSize.height > 260 ? min(max(padY, point.y), canvasSize.height - padY) : point.y
            )
            inlineWheelVM.configure(selection: sel, keyboardMode: false, startAnimationImmediately: true)
            wheelAnchorInCanvas = clamped
            inlineWheelVM.isVisible = true
            FeedbackSoundPlayer.shared.playHighlightTick()
        }
    }

    private func performDropIntoSliceAnimation(at sliceCenter: CGPoint, completion: @escaping () -> Void) {
        dropBurstPoint = sliceCenter
        dropBurstProgress = 0.0

        withAnimation(.easeIn(duration: 0.16)) {
            isSuctionDropping = true
        }
        withAnimation(.easeOut(duration: 0.42)) {
            dropBurstProgress = 1.0
        }

        Task { @MainActor in
            try? await Task.sleep(nanoseconds: 160_000_000)
            completion()
            finishPlaygroundDrag()
            try? await Task.sleep(nanoseconds: 280_000_000)
            dropBurstPoint = nil
        }
    }

    private func finishPlaygroundDrag() {
        withAnimation(.spring(response: 0.20, dampingFraction: 0.80)) {
            draggingItem = nil
            dragStartNormPos = nil
            isSuctionDropping = false
            closeInlineWheel()
        }
        dragMonitor.isInAppPlaygroundDragActive = false
    }

    private func closeInlineWheel() {
        inlineWheelVM.stopPhysicsLoop()
        inlineWheelVM.isVisible = false
        inlineWheelVM.selectedIndex = nil
        wheelAnchorInCanvas = nil
    }
}

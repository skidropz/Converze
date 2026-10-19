import AppKit
import Combine
import CoreGraphics
import QuartzCore

// MARK: - Global System-Wide Option + Left-Click / Drag Monitor Service for Converze

@MainActor
public final class DragMonitorService: NSObject, ObservableObject {
    public static let shared = DragMonitorService()

    @Published public var isWheelActive: Bool = false
    @Published public var isOptionPressed: Bool = false
    @Published public var lastProcessedSummary: String = "Converze — Hold ⌥ Option + Left-Click on a file"

    /// Backward-compatible alias returning `isOptionPressed`
    public var isShiftPressed: Bool { isOptionPressed }

    /// Set to `true` while dragging inside the in-app Playground so the floating Finder panel does not overlap
    public var isInAppPlaygroundDragActive: Bool = false

    public let wheelPanel = RadialWheelPanel()

    private var pollTimer: Timer?
    private var globalFlagsMonitor: Any?
    private var localFlagsMonitor: Any?
    private var globalKeyMonitor: Any?
    private var localKeyMonitor: Any?
    private var globalMouseDownMonitor: Any?
    private var localMouseDownMonitor: Any?

    private var wasMouseDown: Bool = false
    private var dragStartMouseLocation: NSPoint = .zero
    private var dragStartTimestamp: TimeInterval = 0
    private var dragStartChangeCount: Int = 0
    private var activeSelection: DraggedSelection? = nil
    private var cachedFinderURLs: [URL] = []
    private var didTriggerWheelInCurrentDrag: Bool = false
    private var didCancelCurrentGesture: Bool = false
    private var didCloseWheelOnCurrentMouseDown: Bool = false
    private var isKeyboardWheelSession: Bool = false

    /// Pre-compiled AppleScript for ultra-fast (~8ms) Finder selection queries
    private static let compiledFinderScript: NSAppleScript? = {
        let source = """
        tell application "Finder"
            set selList to selection
            set posixPaths to {}
            repeat with anItem in selList
                try
                    set end of posixPaths to POSIX path of (anItem as alias)
                end try
            end repeat
            return posixPaths
        end tell
        """
        let script = NSAppleScript(source: source)
        var err: NSDictionary?
        script?.compileAndReturnError(&err)
        return script
    }()

    public var onExecuteTarget: ((WheelTarget, DraggedSelection) -> Void)?
    public var onEscapePressed: (() -> Void)?

    private override init() {
        super.init()
        setupWheelCallbacks()
    }

    private func setupWheelCallbacks() {
        wheelPanel.viewModel.onSelectTarget = { [weak self] target, selection in
            guard let self else { return }
            self.closeWheel(immediately: true)
            self.onExecuteTarget?(target, selection)
        }
        wheelPanel.viewModel.onCancel = { [weak self] in
            self?.cancelAndCloseWheel()
        }
    }

    public func startMonitoring() {
        stopMonitoring()

        // Pre-warm compiled AppleScript
        _ = Self.compiledFinderScript

        // 60Hz synchronous main-RunLoop timer in `.common` mode
        let timer = Timer(
            timeInterval: 1.0 / 60.0,
            target: self,
            selector: #selector(handlePollTimer(_:)),
            userInfo: nil,
            repeats: true
        )
        RunLoop.main.add(timer, forMode: .common)
        pollTimer = timer

        globalFlagsMonitor = NSEvent.addGlobalMonitorForEvents(matching: .flagsChanged) { [weak self] event in
            Task { @MainActor in
                self?.handleFlagsChanged(event)
            }
        }

        localFlagsMonitor = NSEvent.addLocalMonitorForEvents(matching: .flagsChanged) { [weak self] event in
            self?.handleFlagsChanged(event)
            return event
        }

        globalKeyMonitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown) { [weak self] event in
            Task { @MainActor in
                self?.handleKeyDown(event)
            }
        }

        localKeyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            if self?.handleKeyDown(event) == true {
                return nil
            }
            return event
        }

        // Global & Local Mouse-Down Monitors:
        // 1) If the wheel is open and user clicks outside slices -> close wheel immediately.
        // 2) If the wheel is closed and user holds ⌥ Option + Left-Clicks -> open wheel INSTANTLY (0ms)!
        globalMouseDownMonitor = NSEvent.addGlobalMonitorForEvents(
            matching: [.leftMouseDown, .rightMouseDown, .otherMouseDown]
        ) { [weak self] event in
            Task { @MainActor in
                self?.handleGlobalOrLocalMouseDown(event)
            }
        }

        localMouseDownMonitor = NSEvent.addLocalMonitorForEvents(
            matching: [.leftMouseDown, .rightMouseDown, .otherMouseDown]
        ) { [weak self] event in
            self?.handleGlobalOrLocalMouseDown(event)
            return event
        }
    }

    public func stopMonitoring() {
        pollTimer?.invalidate()
        pollTimer = nil
        if let m = globalFlagsMonitor { NSEvent.removeMonitor(m) }
        if let m = localFlagsMonitor { NSEvent.removeMonitor(m) }
        if let m = globalKeyMonitor { NSEvent.removeMonitor(m) }
        if let m = localKeyMonitor { NSEvent.removeMonitor(m) }
        if let m = globalMouseDownMonitor { NSEvent.removeMonitor(m) }
        if let m = localMouseDownMonitor { NSEvent.removeMonitor(m) }
        globalFlagsMonitor = nil
        localFlagsMonitor = nil
        globalKeyMonitor = nil
        localKeyMonitor = nil
        globalMouseDownMonitor = nil
        localMouseDownMonitor = nil
    }

    @objc private func handlePollTimer(_ timer: Timer) {
        pollDragState()
    }

    // MARK: - Instant Mouse-Down Handler (0ms Response on Option + Left-Click & Outside Click Close)

    private func handleGlobalOrLocalMouseDown(_ event: NSEvent) {
        let mouseLoc = NSEvent.mouseLocation

        // 1. If the wheel is currently shown: clicking outside any slice closes the wheel immediately
        if wheelPanel.isShown {
            if !wheelPanel.isPointOnWheelSlice(mouseLoc) {
                didCloseWheelOnCurrentMouseDown = true
                cancelAndCloseWheel()
            }
            return
        }

        // 2. If the wheel is not shown and this is a Left-Click while holding ⌥ Option: open wheel INSTANTLY!
        guard event.type == .leftMouseDown else { return }
        guard !isInAppPlaygroundDragActive else { return }
        guard Self.isHardwareOptionPressed() || event.modifierFlags.contains(.option) else { return }

        didCloseWheelOnCurrentMouseDown = false
        triggerInstantWheelOnOptionClick(at: mouseLoc)
    }

    private func triggerInstantWheelOnOptionClick(at clickPoint: NSPoint) {
        guard !wheelPanel.isShown, !didTriggerWheelInCurrentDrag else { return }

        dragStartMouseLocation = clickPoint
        dragStartTimestamp = CACurrentMediaTime()
        dragStartChangeCount = NSPasteboard(name: .drag).changeCount
        didCancelCurrentGesture = false

        // Step A (0ms): Use pre-cached Finder selection or immediate query so the wheel opens with zero delay
        let initialURLs = !cachedFinderURLs.isEmpty
            ? cachedFinderURLs
            : (Self.queryFinderSelectedFileURLs() ?? [])

        if !initialURLs.isEmpty {
            let sel = DraggedSelection(urls: initialURLs)
            activeSelection = sel
            didTriggerWheelInCurrentDrag = true
            isKeyboardWheelSession = true
            wheelPanel.showWheel(at: clickPoint, selection: sel, keyboardMode: false)
            isWheelActive = true
            FeedbackSoundPlayer.shared.playHighlightTick()
        }

        // Step B (25ms & 75ms): Verify/refresh Finder selection right after Finder processes the click on the icon
        for delay in [0.025, 0.075] {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
                guard let self else { return }
                guard !self.didCancelCurrentGesture, !self.didCloseWheelOnCurrentMouseDown else { return }

                let dragPboard = NSPasteboard(name: .drag)
                var latestURLs: [URL] = []
                if dragPboard.changeCount != self.dragStartChangeCount {
                    latestURLs = RadialInteractionOverlayView.extractRealFileURLs(from: dragPboard)
                }
                if latestURLs.isEmpty {
                    latestURLs = Self.queryFinderSelectedFileURLs() ?? []
                }

                if !latestURLs.isEmpty {
                    self.cachedFinderURLs = latestURLs
                    let newSel = DraggedSelection(urls: latestURLs)
                    self.activeSelection = newSel

                    if !self.wheelPanel.isShown {
                        self.didTriggerWheelInCurrentDrag = true
                        self.isKeyboardWheelSession = true
                        self.wheelPanel.showWheel(at: clickPoint, selection: newSel, keyboardMode: false)
                        self.isWheelActive = true
                        FeedbackSoundPlayer.shared.playHighlightTick()
                    } else if self.wheelPanel.viewModel.selection != newSel {
                        // Clicked on a different file than what was previously selected -> update wheel targets live
                        self.wheelPanel.viewModel.selection = newSel
                        let newTargets = RadialWheelViewModel.resolveTargets(for: newSel)
                        if self.wheelPanel.viewModel.targets != newTargets {
                            self.wheelPanel.viewModel.targets = newTargets
                            self.wheelPanel.viewModel.hoverProgress = Array(repeating: 0.0, count: newTargets.count)
                        }
                    }
                } else if delay >= 0.070 && self.wheelPanel.isShown && !initialURLs.isEmpty {
                    // User Option-clicked on empty Desktop/Finder space (Finder cleared selection) -> close wheel
                    self.closeWheel()
                }
            }
        }
    }

    // MARK: - Direct Hardware Key State Queries

    /// Returns true if Either Option/Alt key (`⌥`) is currently held down at the hardware/HID level
    public nonisolated static func isHardwareOptionPressed() -> Bool {
        let cgFlags = CGEventSource.flagsState(.combinedSessionState)
        if cgFlags.contains(.maskAlternate) {
            return true
        }
        return NSEvent.modifierFlags.contains(.option)
    }

    /// Backward-compatible helper forwarding to `isHardwareOptionPressed()`
    public nonisolated static func isHardwareShiftPressed() -> Bool {
        isHardwareOptionPressed()
    }

    /// Returns true if the Escape (`ESC`, keyCode 53) key is currently pressed at the hardware/HID level
    public nonisolated static func isHardwareEscapePressed() -> Bool {
        CGEventSource.keyState(.combinedSessionState, key: 53)
    }

    // MARK: - Open Wheel Directly for Given URLs

    public func openKeyboardWheel(for urls: [URL], at point: NSPoint? = nil) {
        let valid = urls
            .map { FormatMatrix.resolveFilePathURL($0) }
            .filter { $0.isFileURL && FileManager.default.fileExists(atPath: $0.path) }
        guard !valid.isEmpty else { return }

        let sel = DraggedSelection(urls: valid)
        activeSelection = sel
        isKeyboardWheelSession = true
        didCancelCurrentGesture = false

        let targetPt = point ?? NSEvent.mouseLocation
        wheelPanel.showWheel(at: targetPt, selection: sel, keyboardMode: true)
        isWheelActive = true
        FeedbackSoundPlayer.shared.playHighlightTick()
    }

    public func cancelAndCloseWheel() {
        didCancelCurrentGesture = true
        onEscapePressed?()
        closeWheel()
    }

    public func closeWheel(immediately: Bool = false) {
        isKeyboardWheelSession = false
        didTriggerWheelInCurrentDrag = false
        isWheelActive = false
        wheelPanel.hideWheel(immediately: immediately)
    }

    // MARK: - Core 60fps Polling Loop

    private func pollDragState() {
        // 0. Check hardware Escape key (`ESC`) first — closes the wheel immediately in any app or drag session
        if Self.isHardwareEscapePressed() {
            if wheelPanel.isShown || isWheelActive {
                cancelAndCloseWheel()
            } else {
                onEscapePressed?()
            }
            return
        }

        let currentOption = Self.isHardwareOptionPressed()
        if isOptionPressed != currentOption {
            isOptionPressed = currentOption
            if currentOption {
                // Pre-warm Finder selection as soon as Option is pressed so Left-Click is 0ms instant!
                cachedFinderURLs = Self.queryFinderSelectedFileURLs() ?? []
            } else {
                cachedFinderURLs = []
            }
        }

        let mouseLoc = NSEvent.mouseLocation
        let leftMouseDown = (NSEvent.pressedMouseButtons & 1) != 0
        let dragPboard = NSPasteboard(name: .drag)
        let currentDragChangeCount = dragPboard.changeCount

        // Reset ESC cancel lock once both Option and Left-Click are released
        if !currentOption && !leftMouseDown {
            didCancelCurrentGesture = false
            didCloseWheelOnCurrentMouseDown = false
        }

        // 1. If the wheel is open, always keep updating slice hover from current screen cursor position
        if wheelPanel.isShown {
            wheelPanel.updateHover(fromScreenPoint: mouseLoc)
        }

        // 2. Skip global Finder panel while dragging inside the in-app Playground canvas
        if isInAppPlaygroundDragActive {
            return
        }

        // 3. Detect start of Left-Click (`leftMouseDown` transitions from false -> true)
        if leftMouseDown && !wasMouseDown {
            wasMouseDown = true
            if currentOption && !wheelPanel.isShown && !didCloseWheelOnCurrentMouseDown && !didCancelCurrentGesture {
                triggerInstantWheelOnOptionClick(at: mouseLoc)
            } else if !currentOption {
                dragStartMouseLocation = mouseLoc
                dragStartTimestamp = CACurrentMediaTime()
                dragStartChangeCount = currentDragChangeCount
                activeSelection = nil
                didTriggerWheelInCurrentDrag = false
            }
        }

        // 4. Support starting a normal drag first and then pressing ⌥ Option mid-drag
        if leftMouseDown && currentOption && !didCancelCurrentGesture && !didCloseWheelOnCurrentMouseDown && !wheelPanel.isShown {
            let hasFreshDragPasteboard = (currentDragChangeCount != dragStartChangeCount)
            if activeSelection == nil {
                var urls: [URL] = []
                if hasFreshDragPasteboard {
                    urls = RadialInteractionOverlayView.extractRealFileURLs(from: dragPboard)
                }
                if urls.isEmpty {
                    urls = !cachedFinderURLs.isEmpty ? cachedFinderURLs : (Self.queryFinderSelectedFileURLs() ?? [])
                }
                if !urls.isEmpty {
                    activeSelection = DraggedSelection(urls: urls)
                }
            }

            if let sel = activeSelection, !didTriggerWheelInCurrentDrag {
                didTriggerWheelInCurrentDrag = true
                isKeyboardWheelSession = true
                wheelPanel.showWheel(at: mouseLoc, selection: sel, keyboardMode: false)
                isWheelActive = true
                FeedbackSoundPlayer.shared.playHighlightTick()
            }
        }

        // 5. Left Mouse Button Released:
        // - If the user dragged onto a slice (`selectedIndex != nil`) after holding for >= 0.18s -> convert immediately!
        // - Otherwise (it was a quick Option + Left-Click to open the wheel) -> KEEP the wheel open (`isKeyboardWheelSession = true`)!
        if !leftMouseDown && wasMouseDown {
            wasMouseDown = false
            didCloseWheelOnCurrentMouseDown = false

            if wheelPanel.isShown {
                if hasFreshDragPasteboard(currentDragChangeCount) {
                    let latestURLs = RadialInteractionOverlayView.extractRealFileURLs(from: dragPboard)
                    if !latestURLs.isEmpty {
                        let updatedSel = DraggedSelection(urls: latestURLs)
                        activeSelection = updatedSel
                        wheelPanel.viewModel.selection = updatedSel
                    }
                }

                wheelPanel.updateHover(fromScreenPoint: mouseLoc)
                let holdDuration = CACurrentMediaTime() - dragStartTimestamp

                if wheelPanel.viewModel.phase != .dropping {
                    if wheelPanel.viewModel.selectedIndex != nil && holdDuration >= 0.18 {
                        // User held Option + Left-Click, dragged onto a slice, and released -> convert!
                        wheelPanel.viewModel.triggerSelected()
                    } else {
                        // User did an Option + Left-Click to open the wheel -> keep it open so they can hover & click a slice!
                        isKeyboardWheelSession = true
                    }
                }
            }

            activeSelection = nil
            didTriggerWheelInCurrentDrag = false
        }
    }

    private func hasFreshDragPasteboard(_ currentCount: Int) -> Bool {
        currentCount != dragStartChangeCount
    }

    // MARK: - Modifier Key Handling (`⌥ Option` pressed)

    private func handleFlagsChanged(_ event: NSEvent) {
        let optionNow = event.modifierFlags.contains(.option)
        isOptionPressed = optionNow

        if optionNow {
            // Pre-cache Finder selection immediately when Option is pressed so the subsequent Left-Click is 0ms!
            cachedFinderURLs = Self.queryFinderSelectedFileURLs() ?? []
        } else {
            cachedFinderURLs = []
        }

        guard !isInAppPlaygroundDragActive else { return }
        guard !didCancelCurrentGesture else { return }

        // Require Left-Click to be held down! Never open on Option key alone.
        let leftMouseDown = (NSEvent.pressedMouseButtons & 1) != 0
        guard leftMouseDown else { return }

        if activeSelection == nil {
            let dragPboard = NSPasteboard(name: .drag)
            var urls: [URL] = []
            if dragPboard.changeCount != dragStartChangeCount {
                urls = RadialInteractionOverlayView.extractRealFileURLs(from: dragPboard)
            }
            if urls.isEmpty {
                urls = !cachedFinderURLs.isEmpty ? cachedFinderURLs : (Self.queryFinderSelectedFileURLs() ?? [])
            }
            if !urls.isEmpty {
                activeSelection = DraggedSelection(urls: urls)
            }
        }

        guard let sel = activeSelection else { return }
        let mouseLoc = NSEvent.mouseLocation

        if optionNow && !wheelPanel.isShown {
            didTriggerWheelInCurrentDrag = true
            isKeyboardWheelSession = true
            wheelPanel.showWheel(at: mouseLoc, selection: sel, keyboardMode: false)
            isWheelActive = true
            FeedbackSoundPlayer.shared.playHighlightTick()
        }
    }

    @discardableResult
    private func handleKeyDown(_ event: NSEvent) -> Bool {
        if event.keyCode == 53 { // Escape
            if wheelPanel.isShown || isWheelActive {
                cancelAndCloseWheel()
                return true
            }
            onEscapePressed?()
        }

        guard wheelPanel.isShown else { return false }

        switch event.keyCode {
        case 36, 76: // Return / Enter
            wheelPanel.viewModel.triggerSelected()
            return true
        case 123, 126: // Left / Up Arrow
            wheelPanel.viewModel.moveSelection(by: -1)
            return true
        case 124, 125: // Right / Down Arrow
            wheelPanel.viewModel.moveSelection(by: 1)
            return true
        default:
            return false
        }
    }

    /// Fast pre-compiled AppleScript query for currently selected file URLs in Finder
    public static func queryFinderSelectedFileURLs() -> [URL]? {
        guard let script = compiledFinderScript else { return nil }
        var errorDict: NSDictionary?
        let descriptor = script.executeAndReturnError(&errorDict)
        guard errorDict == nil, descriptor.numberOfItems > 0 else { return nil }

        var urls: [URL] = []
        for idx in 1...descriptor.numberOfItems {
            if let path = descriptor.atIndex(idx)?.stringValue, !path.isEmpty {
                let resolved = URL(fileURLWithPath: path).standardizedFileURL
                if FileManager.default.fileExists(atPath: resolved.path) {
                    urls.append(resolved)
                }
            }
        }
        return urls.isEmpty ? nil : urls
    }
}

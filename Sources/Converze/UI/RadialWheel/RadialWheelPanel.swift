import AppKit
import SwiftUI
import UniformTypeIdentifiers

// MARK: - Floating Non-Activating NSPanel for the Converze Radial Wheel

@MainActor
public final class RadialWheelPanel: NSPanel {
    public let viewModel = RadialWheelViewModel()
    private var interactionOverlay: RadialInteractionOverlayView!

    /// Screen coordinate where the center of the wheel was anchored
    public private(set) var anchorPoint: NSPoint = .zero

    public var isShown: Bool {
        viewModel.isVisible && isVisible && viewModel.phase != .closing && viewModel.phase != .hidden
    }

    public init() {
        let panelSize = NSSize(width: 440, height: 440)
        super.init(
            contentRect: NSRect(origin: .zero, size: panelSize),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        self.level = .popUpMenu
        self.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient, .ignoresCycle]
        self.isOpaque = false
        self.backgroundColor = .clear
        self.hasShadow = false
        self.hidesOnDeactivate = false
        self.isMovableByWindowBackground = false
        self.acceptsMouseMovedEvents = true
        self.ignoresMouseEvents = false

        let container = NSView(frame: NSRect(origin: .zero, size: panelSize))
        container.wantsLayer = true
        container.layer?.backgroundColor = NSColor.clear.cgColor

        let hostingView = NSHostingView(rootView: RadialWheelView(viewModel: viewModel))
        hostingView.frame = container.bounds
        hostingView.autoresizingMask = [.width, .height]
        container.addSubview(hostingView)

        let overlay = RadialInteractionOverlayView(
            frame: container.bounds,
            viewModel: viewModel
        )
        overlay.autoresizingMask = [.width, .height]
        container.addSubview(overlay)
        self.interactionOverlay = overlay

        self.contentView = container
    }

    /// Convert a screen coordinate (`NSScreen`, +Y up) into wheel-local offset (`+dx` right, `+dy` down, matching SwiftUI)
    public func wheelOffset(forScreenPoint screenPoint: NSPoint) -> CGPoint {
        let dx = screenPoint.x - anchorPoint.x
        let dy = anchorPoint.y - screenPoint.y
        return CGPoint(x: dx, y: dy)
    }

    /// Returns true if the given screen coordinate lies inside the active petal ring of the wheel
    public func isPointOnWheelSlice(_ screenPoint: NSPoint) -> Bool {
        let delta = wheelOffset(forScreenPoint: screenPoint)
        return viewModel.metrics.hitTestSlice(dx: delta.x, dy: delta.y, count: viewModel.targets.count) != nil
    }

    /// Returns true if the given screen coordinate lies inside the center cutout hole of the wheel
    public func isPointInCenterHole(_ screenPoint: NSPoint) -> Bool {
        let delta = wheelOffset(forScreenPoint: screenPoint)
        return hypot(delta.x, delta.y) <= viewModel.metrics.holeRadius
    }

    /// Show the radial wheel centered at `screenPoint` with a smooth 120Hz spring opening animation
    public func showWheel(
        at screenPoint: NSPoint,
        selection: DraggedSelection,
        keyboardMode: Bool = false
    ) {
        let size = self.frame.size
        var origin = NSPoint(
            x: screenPoint.x - size.width / 2.0,
            y: screenPoint.y - size.height / 2.0
        )

        if let screen = NSScreen.screens.first(where: { NSMouseInRect(screenPoint, $0.frame, false) }) ?? NSScreen.main {
            let visible = screen.visibleFrame
            origin.x = max(visible.minX + 8, min(origin.x, visible.maxX - size.width - 8))
            origin.y = max(visible.minY + 8, min(origin.y, visible.maxY - size.height - 8))
        }

        self.anchorPoint = NSPoint(
            x: origin.x + size.width / 2.0,
            y: origin.y + size.height / 2.0
        )

        self.setFrameOrigin(origin)
        self.alphaValue = 1.0

        // Configure frame-0 state BEFORE ordering front, then start 120Hz physics loop on display
        viewModel.isVisible = true
        viewModel.configure(
            selection: selection,
            keyboardMode: keyboardMode,
            startAnimationImmediately: false
        )

        self.orderFrontRegardless()
        viewModel.startPhysicsLoop()
    }

    /// Update hovered slice from current screen mouse position (with flipped Y to match SwiftUI's top-down coordinates)
    public func updateHover(fromScreenPoint screenPoint: NSPoint) {
        guard viewModel.isVisible, viewModel.phase != .dropping, viewModel.phase != .closing else { return }
        let delta = wheelOffset(forScreenPoint: screenPoint)
        viewModel.updateCursor(dx: delta.x, dy: delta.y)
    }

    /// Hide the radial wheel with a smooth closing animation
    public func hideWheel(immediately: Bool = false) {
        guard viewModel.isVisible || isVisible else { return }
        if immediately || viewModel.phase == .dropping {
            viewModel.stopPhysicsLoop()
            viewModel.isVisible = false
            viewModel.phase = .hidden
            viewModel.selectedIndex = nil
            self.orderOut(nil)
        } else {
            viewModel.animateClose { [weak self] in
                guard let self else { return }
                self.viewModel.isVisible = false
                self.viewModel.selectedIndex = nil
                self.orderOut(nil)
            }
        }
    }

    override public func cancelOperation(_ sender: Any?) {
        viewModel.onCancel?()
    }

    override public var canBecomeKey: Bool {
        false
    }

    override public var canBecomeMain: Bool {
        false
    }
}

// MARK: - Native AppKit Dragging Destination & Mouse/Keyboard Overlay (Flipped Top-Down Coordinates)

@MainActor
public final class RadialInteractionOverlayView: NSView {
    private weak var viewModel: RadialWheelViewModel?
    private var trackingArea: NSTrackingArea?

    /// Top-down coordinate system (`+Y` points down) so `bounds` matches SwiftUI and `ConverzeWheelMetrics` 1:1
    override public var isFlipped: Bool {
        true
    }

    public init(frame frameRect: NSRect, viewModel: RadialWheelViewModel) {
        self.viewModel = viewModel
        super.init(frame: frameRect)
        setupDragTypes()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    private func setupDragTypes() {
        registerForDraggedTypes([
            .fileURL,
            NSPasteboard.PasteboardType("NSFilenamesPboardType"),
            .URL,
            .string
        ])
    }

    override public func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let existing = trackingArea {
            removeTrackingArea(existing)
        }
        let area = NSTrackingArea(
            rect: bounds,
            options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect],
            owner: self,
            userInfo: nil
        )
        addTrackingArea(area)
        trackingArea = area
    }

    // MARK: - NSDraggingDestination (Accepts Finder File Drops Over the Wheel)

    override public func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        updateFromDraggingInfo(sender)
        return .copy
    }

    override public func draggingUpdated(_ sender: NSDraggingInfo) -> NSDragOperation {
        updateFromDraggingInfo(sender)
        return .copy
    }

    override public func draggingExited(_ sender: NSDraggingInfo?) {
        viewModel?.selectedIndex = nil
    }

    override public func prepareForDragOperation(_ sender: NSDraggingInfo) -> Bool {
        true
    }

    override public func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        guard let vm = viewModel else { return false }

        let previousSelectedIndex = vm.selectedIndex
        updateFromDraggingInfo(sender)

        let pboard = sender.draggingPasteboard
        let urls = Self.extractRealFileURLs(from: pboard)
        if !urls.isEmpty {
            let newSel = DraggedSelection(urls: urls)
            if vm.selection != newSel {
                vm.selection = newSel
                let newTargets = RadialWheelViewModel.resolveTargets(for: newSel)
                if vm.targets != newTargets {
                    vm.targets = newTargets
                }
            }
        }

        if let finalIdx = vm.selectedIndex ?? previousSelectedIndex,
           finalIdx >= 0,
           finalIdx < vm.targets.count {
            vm.selectedIndex = finalIdx
            vm.triggerSelected()
            return true
        }

        // Dropped outside the slices -> cancel and close
        vm.onCancel?()
        return false
    }

    private func updateFromDraggingInfo(_ sender: NSDraggingInfo) {
        guard let vm = viewModel else { return }
        // Because `isFlipped == true`, `convert(_:from: nil)` converts window coords (+Y up) to top-down (+Y down)
        let pt = convert(sender.draggingLocation, from: nil)
        let center = CGPoint(x: bounds.midX, y: bounds.midY)
        let dx = pt.x - center.x
        let dy = pt.y - center.y
        vm.updateCursor(dx: dx, dy: dy)
    }

    /// Resolve Finder File Reference URLs (`file:///.file/id=...`) into standard POSIX file paths
    public static func extractRealFileURLs(from pboard: NSPasteboard) -> [URL] {
        var collected: [URL] = []

        if let nsurls = pboard.readObjects(
            forClasses: [NSURL.self],
            options: [.urlReadingFileURLsOnly: true]
        ) as? [NSURL], !nsurls.isEmpty {
            for item in nsurls {
                let resolved = (item.filePathURL ?? (item as URL)).standardizedFileURL
                if resolved.isFileURL, !resolved.path.hasPrefix("/.file/") {
                    collected.append(resolved)
                }
            }
        }

        if collected.isEmpty,
           let items = pboard.pasteboardItems {
            for item in items {
                if let str = item.string(forType: .fileURL),
                   let rawURL = URL(string: str) {
                    let resolved = FormatMatrix.resolveFilePathURL(rawURL)
                    if resolved.isFileURL, !resolved.path.hasPrefix("/.file/") {
                        collected.append(resolved)
                    }
                }
            }
        }

        if collected.isEmpty,
           let legacyPaths = pboard.propertyList(
               forType: NSPasteboard.PasteboardType("NSFilenamesPboardType")
           ) as? [String] {
            for path in legacyPaths {
                let url = URL(fileURLWithPath: path).standardizedFileURL
                collected.append(url)
            }
        }

        return collected
    }

    // MARK: - Direct Mouse Hover & Click Handling (Including Click Outside Wheel to Close)

    override public func acceptsFirstMouse(for event: NSEvent?) -> Bool {
        true
    }

    override public func mouseMoved(with event: NSEvent) {
        updateHoverFromMouseEvent(event)
    }

    override public func mouseDragged(with event: NSEvent) {
        updateHoverFromMouseEvent(event)
    }

    override public func mouseDown(with event: NSEvent) {
        updateHoverFromMouseEvent(event)
        if viewModel?.selectedIndex != nil {
            viewModel?.triggerSelected()
        } else {
            // Clicked inside the panel window but outside the wheel slices -> close the wheel!
            viewModel?.onCancel?()
        }
    }

    override public func rightMouseDown(with event: NSEvent) {
        updateHoverFromMouseEvent(event)
        if viewModel?.selectedIndex == nil {
            viewModel?.onCancel?()
        }
    }

    private func updateHoverFromMouseEvent(_ event: NSEvent) {
        guard let vm = viewModel else { return }
        // Because `isFlipped == true`, `pt` has +Y pointing down, matching SwiftUI 1:1
        let pt = convert(event.locationInWindow, from: nil)
        let center = CGPoint(x: bounds.midX, y: bounds.midY)
        vm.updateCursor(dx: pt.x - center.x, dy: pt.y - center.y)
    }

    override public var acceptsFirstResponder: Bool {
        true
    }

    override public func keyDown(with event: NSEvent) {
        guard let vm = viewModel else { return }
        switch event.keyCode {
        case 53: // Escape
            vm.onCancel?()
        case 36, 76: // Return / Enter
            vm.triggerSelected()
        case 123, 126: // Left / Up Arrow
            vm.moveSelection(by: -1)
        case 124, 125: // Right / Down Arrow
            vm.moveSelection(by: 1)
        default:
            super.keyDown(with: event)
        }
    }
}

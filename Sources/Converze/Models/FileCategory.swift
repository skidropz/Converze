import Foundation
import AppKit
import UniformTypeIdentifiers

// MARK: - File Category & Format Matrix (Converze for Mac)

public enum FileCategory: String, CaseIterable, Identifiable, Sendable {
    case image = "Images"
    case video = "Video"
    case audio = "Audio"
    case pdf = "PDF"
    case document = "Documents"
    case slides = "Slides"
    case spreadsheet = "Spreadsheets"
    case epub = "EPUB"
    case subtitle = "Subtitles"
    case archive = "Archives"
    case folder = "Folder"
    case other = "Other"

    public var id: String { rawValue }

    public var sfSymbol: String {
        switch self {
        case .image: return "photo"
        case .video: return "film"
        case .audio: return "waveform"
        case .pdf: return "doc.richtext"
        case .document: return "doc.text"
        case .slides: return "rectangle.on.rectangle"
        case .spreadsheet: return "tablecells"
        case .epub: return "book.closed"
        case .subtitle: return "captions.bubble"
        case .archive: return "doc.zipper"
        case .folder: return "folder"
        case .other: return "doc"
        }
    }
}

public struct FormatMatrix {
    // Exact format arrays for Converze for Mac
    public static let imageOutputs = ["jpg", "png", "webp", "heic", "tiff", "avif", "bmp", "pdf", "docx"]
    public static let imageInputs: Set<String> = ["jpg", "jpeg", "png", "webp", "heic", "heif", "tiff", "tif", "svg", "avif", "bmp"]

    public static let audioFormats = ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"]
    public static let audioInputs: Set<String> = ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "aif", "wma", "caf"]

    public static let videoBaseFormats = ["mp4", "mov", "mkv", "webm", "avi", "wmv"]
    public static let videoInputs: Set<String> = ["mp4", "mov", "mkv", "webm", "avi", "wmv", "m4v"]

    public static let subtitleFormats = ["srt", "vtt"]
    public static let documentInputs: Set<String> = ["docx", "doc", "pages", "md", "markdown", "rtf", "rtfd", "textclipping"]
    public static let documentOutputs = ["docx", "pages", "pdf", "txt", "md", "html", "epub", "png", "jpg"]

    public static let spreadsheetFormats = ["xlsx", "xls", "ods", "csv", "tsv"]
    public static let slideFormats = ["pptx", "ppt", "key"]
    public static let archiveInputs: Set<String> = ["zip", "tar", "gz", "tgz", "rar"]
    public static let archiveOutputs = ["zip", "tar", "gz"]

    // Ordered format lists for UI display in SettingsView
    public static let imageInputList: [String] = ["png", "jpg", "webp", "heic", "avif", "tiff", "bmp", "svg", "gif"]
    public static let videoInputList: [String] = ["mp4", "mov", "mkv", "webm", "avi", "wmv", "m4v"]
    public static let audioInputList: [String] = ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"]
    public static let docInputs: [String] = ["docx", "doc", "pages", "md", "txt", "rtf", "html"]
    public static let slideInputs: [String] = ["pptx", "ppt", "key"]
    public static let sheetInputs: [String] = ["xlsx", "xls", "ods", "csv", "tsv"]
    public static let subtitleInputs: [String] = ["srt", "vtt"]
    public static let archiveInputList: [String] = ["zip", "tar", "gz", "tgz", "rar"]

    /// Input-only formats that cannot be chosen as conversion targets
    public static let inputOnlyFormats: Set<String> = ["rar", "ppt", "xls", "rtf", "rtfd", "textclipping", "svg"]

    /// Resolve macOS Finder File Reference URLs (`file:///.file/id=6571367.2773272/`) to real POSIX file path URLs (`file:///Users/...`)
    public static func resolveFilePathURL(_ url: URL) -> URL {
        if let pathURL = (url as NSURL).filePathURL {
            return pathURL.standardizedFileURL
        }
        if !url.path.isEmpty && !url.path.hasPrefix("/.file/") {
            return URL(fileURLWithPath: url.path).standardizedFileURL
        }
        return url.standardizedFileURL
    }

    /// Normalize a file extension or compound extension (.tar.gz / .tgz)
    public static func normalizedExtension(for url: URL) -> String {
        let resolved = resolveFilePathURL(url)
        let name = resolved.lastPathComponent.lowercased()
        if name.hasSuffix(".tar.gz") || name.hasSuffix(".tgz") {
            return "gz"
        }
        let ext = resolved.pathExtension.lowercased()
        switch ext {
        case "jpeg": return "jpg"
        case "tif": return "tiff"
        case "aif": return "aiff"
        case "markdown": return "md"
        case "m4v": return "mp4"
        default: return ext
        }
    }

    /// Detect FileCategory for a normalized extension
    public static func category(for ext: String, isDirectory: Bool = false) -> FileCategory {
        if isDirectory && ext != "rtfd" && ext != "pages" {
            return .folder
        }
        let clean = ext.lowercased()
        if imageInputs.contains(clean) { return .image }
        if audioInputs.contains(clean) { return .audio }
        if videoInputs.contains(clean) || clean == "gif" { return .video }
        if clean == "pdf" { return .pdf }
        if ["ppt", "pptx", "key"].contains(clean) { return .slides }
        if spreadsheetFormats.contains(clean) { return .spreadsheet }
        if clean == "epub" { return .epub }
        if subtitleFormats.contains(clean) { return .subtitle }
        if archiveInputs.contains(clean) { return .archive }
        if documentInputs.contains(clean) || clean == "txt" || clean == "html" { return .document }
        return .other
    }

    /// Exact conversion targets `z(e)` for a given normalized format extension
    public static func conversionTargets(for ext: String, isDirectory: Bool = false) -> [String] {
        if isDirectory && ext != "rtfd" && ext != "pages" {
            // Folders can be archived directly into ZIP, TAR, GZIP
            return archiveOutputs
        }
        let e = ext.lowercased()
        var targets: [String] = []

        if imageInputs.contains(e) {
            // When source is PNG/JPG/etc., show up to 8 slices on the Converze wheel
            targets = imageOutputs
        } else if audioInputs.contains(e) {
            targets = audioFormats
        } else if videoInputs.contains(e) {
            targets = videoBaseFormats + ["gif", "mp3"]
        } else if e == "gif" {
            targets = videoBaseFormats
        } else if e == "pdf" {
            targets = ["docx", "pages", "pptx", "xlsx", "jpg", "png", "epub", "txt", "md"]
        } else if ["ppt", "pptx"].contains(e) {
            targets = ["pdf", "jpg", "png"]
        } else if spreadsheetFormats.contains(e) {
            targets = spreadsheetFormats + ["pdf"]
        } else if documentInputs.contains(e) {
            targets = documentOutputs
        } else if e == "epub" {
            targets = ["pdf", "docx", "pages", "txt", "md", "html"]
        } else if e == "txt" {
            targets = ["docx", "pages", "pdf", "md", "html", "epub", "jpg", "png"]
        } else if subtitleFormats.contains(e) {
            targets = subtitleFormats + ["txt"]
        } else if archiveInputs.contains(e) {
            targets = archiveOutputs
        }

        let filtered = targets.filter { $0 != e && !inputOnlyFormats.contains($0) }
        // Keep maximum 8 formats on standard image wheel so it matches conversion-wheel.png
        if imageInputs.contains(e) && filtered.count > 8 {
            return Array(filtered.prefix(8))
        }
        return filtered
    }

    /// Display label on radial wheel slice ("gz" -> "GZIP", otherwise uppercase)
    public static func displayLabel(for format: String) -> String {
        if format.lowercased() == "gz" {
            return "GZIP"
        }
        return format.uppercased()
    }

    /// Generate collision-free output URL next to the original file (`Ge` in Converze)
    public static func uniqueOutputURL(for sourceURL: URL, targetFormat: String, suffixLabel: String? = nil, isFolderOutput: Bool = false) -> URL {
        let resolvedSource = resolveFilePathURL(sourceURL)
        let directory = resolvedSource.deletingLastPathComponent()
        let filename = resolvedSource.lastPathComponent
        let baseName: String = {
            if filename.lowercased().hasSuffix(".tar.gz") {
                return String(filename.dropLast(7))
            } else if filename.lowercased().hasSuffix(".tgz") {
                return String(filename.dropLast(4))
            } else {
                let deleted = resolvedSource.deletingPathExtension().lastPathComponent
                return deleted.isEmpty ? filename : deleted
            }
        }()

        let extPart: String = {
            if isFolderOutput { return "" }
            if targetFormat.lowercased() == "gz" { return ".tar.gz" }
            return ".\(targetFormat.lowercased())"
        }()

        let stem = suffixLabel != nil ? "\(baseName) \(suffixLabel!)" : baseName
        let fm = FileManager.default

        var candidate = directory.appendingPathComponent("\(stem)\(extPart)")
        var counter = 2
        while fm.fileExists(atPath: candidate.path) {
            candidate = directory.appendingPathComponent("\(stem) \(counter)\(extPart)")
            counter += 1
        }
        return candidate
    }
}

// MARK: - Dragged Item Info

public struct DraggedSelection: Equatable, Sendable {
    public let urls: [URL]
    public let primaryExtension: String
    public let category: FileCategory
    public let totalBytes: Int64
    public let isDirectory: Bool

    public init(urls: [URL]) {
        let resolvedURLs = urls.map { FormatMatrix.resolveFilePathURL($0) }
        self.urls = resolvedURLs
        guard let first = resolvedURLs.first else {
            self.primaryExtension = ""
            self.category = .other
            self.totalBytes = 0
            self.isDirectory = false
            return
        }

        var isDir: ObjCBool = false
        FileManager.default.fileExists(atPath: first.path, isDirectory: &isDir)
        let ext = FormatMatrix.normalizedExtension(for: first)
        self.isDirectory = isDir.boolValue && ext != "rtfd" && ext != "pages"
        self.primaryExtension = ext
        self.category = FormatMatrix.category(for: ext, isDirectory: self.isDirectory)

        var sum: Int64 = 0
        for url in resolvedURLs {
            if let attrs = try? FileManager.default.attributesOfItem(atPath: url.path),
               let size = attrs[.size] as? NSNumber {
                sum += size.int64Value
            }
        }
        self.totalBytes = sum
    }

    public var count: Int { urls.count }

    public var displayName: String {
        guard let first = urls.first else { return "No file" }
        if urls.count == 1 {
            return first.lastPathComponent
        }
        return "\(urls.count) items"
    }

    /// Formatted file size for the center pill of the radial wheel (e.g. "2.2 MB", "830 KB")
    public var formattedSize: String {
        if totalBytes <= 0 {
            return urls.count > 1 ? "\(urls.count) files" : (isDirectory ? "Folder" : "File")
        }
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useKB, .useMB, .useGB]
        formatter.countStyle = .file
        formatter.includesUnit = true
        formatter.isAdaptive = true
        return formatter.string(fromByteCount: totalBytes)
    }
}

// MARK: - In-Memory File Icon Cache (Prevents repeated LaunchServices / Sandbox Extension -21 lookups)

@MainActor
public final class FileIconCache {
    public static let shared = FileIconCache()
    private var cache: [String: NSImage] = [:]

    private init() {}

    public func icon(for url: URL) -> NSImage {
        let key = url.path
        if let cached = cache[key] {
            return cached
        }
        let ext = url.pathExtension.lowercased()
        let img: NSImage
        if !ext.isEmpty, let utType = UTType(filenameExtension: ext) {
            img = NSWorkspace.shared.icon(for: utType)
        } else {
            img = NSWorkspace.shared.icon(forFile: url.path)
        }
        img.size = NSSize(width: 64, height: 64)
        cache[key] = img
        return img
    }
}


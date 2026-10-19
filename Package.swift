// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "Converze",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .executable(
            name: "Converze",
            targets: ["Converze"]
        )
    ],
    targets: [
        .executableTarget(
            name: "Converze",
            path: "Sources/Converze",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("SwiftUI"),
                .linkedFramework("UniformTypeIdentifiers"),
                .linkedFramework("ImageIO"),
                .linkedFramework("CoreImage"),
                .linkedFramework("AVFoundation"),
                .linkedFramework("PDFKit"),
                .linkedFramework("Vision"),
                .linkedFramework("Quartz"),
                .linkedFramework("AudioToolbox")
            ]
        )
    ]
)

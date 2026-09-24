// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "bloqr-compiler",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .library(name: "BloqrCompilerCore", targets: ["BloqrCompilerCore"]),
        .executable(name: "bloqr-compiler", targets: ["bloqr-compiler"]),
    ],
    dependencies: [
        .package(url: "https://github.com/apple/swift-argument-parser.git", from: "1.3.0"),
        .package(url: "https://github.com/jpsim/Yams.git", from: "5.1.0"),
        .package(url: "https://github.com/LebJe/TOMLKit.git", from: "0.5.5"),
        .package(url: "https://github.com/kylef/JSONSchema.swift.git", from: "0.6.0"),
    ],
    targets: [
        .target(
            name: "BloqrCompilerCore",
            dependencies: [
                "Yams",
                "TOMLKit",
                .product(name: "JSONSchema", package: "JSONSchema"),
            ],
            resources: [
                .copy("compiler-config.schema.json")
            ]
        ),
        .executableTarget(
            name: "bloqr-compiler",
            dependencies: [
                "BloqrCompilerCore",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
            ]
        ),
        .testTarget(
            name: "BloqrCompilerCoreTests",
            dependencies: ["BloqrCompilerCore"]
        ),
    ]
)

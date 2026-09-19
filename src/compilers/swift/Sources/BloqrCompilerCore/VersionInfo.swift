import Foundation

/// Platform-specific information.
public struct PlatformInfo: Sendable {
    public var osName: String
    public var architecture: String

    public init(osName: String, architecture: String) {
        self.osName = osName
        self.architecture = architecture
    }
}

/// JSR package specifier for the compiler CLI, run via `deno run` - the same
/// `@bloqr/compiler-core` package the Rust/.NET/Python wrappers shell out to.
let jsrPackageSpecifier = "jsr:@bloqr/compiler-core/cli"

/// Deno permission flags this wrapper grants the compiler subprocess, matching the other
/// wrappers' invocation (read/write/env/net/run).
let denoPermissions = ["run", "--allow-read", "--allow-write", "--allow-env", "--allow-net", "--allow-run"]

/// Component version information, mirroring the other wrappers' `VersionInfo`.
public struct VersionInfo: Sendable {
    public var moduleVersion: String
    public var swiftWrapperVersion: String
    public var denoVersion: String?
    public var compilerVersion: String?
    public var compilerPath: String?
    public var platform: PlatformInfo

    public var hasCompiler: Bool { compilerPath != nil }

    /// Collects version information about the toolchain: this wrapper's own version, Deno (if
    /// on `PATH`), and the `@bloqr/compiler-core` JSR package version Deno resolves.
    public static func collect() -> VersionInfo {
        var denoVersion: String?
        var compilerVersion: String?
        var compilerPath: String?

        if let deno = findCommand("deno") {
            compilerPath = "\(deno) run \(jsrPackageSpecifier)"
            denoVersion = commandOutput(deno, ["--version"])?.components(separatedBy: .newlines).first

            var versionArgs = denoPermissions
            versionArgs.append(jsrPackageSpecifier)
            versionArgs.append("--version")
            compilerVersion = commandOutput(deno, versionArgs)?.components(separatedBy: .newlines).first
        }

        #if arch(arm64)
        let arch = "arm64"
        #elseif arch(x86_64)
        let arch = "x86_64"
        #else
        let arch = "unknown"
        #endif

        #if os(macOS)
        let osName = "macOS"
        #elseif os(Linux)
        let osName = "Linux"
        #else
        let osName = "unknown"
        #endif

        return VersionInfo(
            moduleVersion: bloqrCompilerSwiftVersion,
            swiftWrapperVersion: bloqrCompilerSwiftVersion,
            denoVersion: denoVersion,
            compilerVersion: compilerVersion,
            compilerPath: compilerPath,
            platform: PlatformInfo(osName: osName, architecture: arch)
        )
    }
}

/// This wrapper's own semantic version - independent of `@bloqr/compiler-core`'s version, the
/// same way each of the other four wrappers versions its own CLI/package separately. See
/// `docs/architecture/versioning-strategy.md`.
public let bloqrCompilerSwiftVersion = "1.0.0"

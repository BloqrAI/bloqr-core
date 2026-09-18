import ArgumentParser
import BloqrCompilerCore
import Foundation

/// Filenames searched for when no `-c/--config` is given, in this order, mirroring the other
/// wrappers' `find_default_config()`.
let defaultConfigNames = ["compiler-config.json", "compiler-config.yaml", "compiler-config.toml"]

func findConfigInAncestors() -> URL? {
    var dir = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
    while true {
        for name in defaultConfigNames {
            let candidate = dir.appendingPathComponent(name)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return candidate
            }
        }
        let parent = dir.deletingLastPathComponent()
        if parent.path == dir.path { return nil }
        dir = parent
    }
}

func findDefaultConfig() -> URL? {
    let repoSpecific = URL(fileURLWithPath: "src/compilers/typescript/compiler-config.json")
    for name in defaultConfigNames + [repoSpecific.path] {
        let candidate = URL(fileURLWithPath: name)
        if FileManager.default.fileExists(atPath: candidate.path) {
            return candidate
        }
    }
    return findConfigInAncestors()
}

func printConfigNotFoundError() {
    FileHandle.standardError.write(Data("[ERROR] No configuration file specified or found.\n\n".utf8))
    FileHandle.standardError.write(Data("Searched for configuration files:\n".utf8))
    for name in defaultConfigNames + ["src/compilers/typescript/compiler-config.json"] {
        FileHandle.standardError.write(Data("  - \(name)\n".utf8))
    }
    FileHandle.standardError.write(Data("\nSearch started from: \(FileManager.default.currentDirectoryPath)\n".utf8))
    FileHandle.standardError.write(Data("Also checked all parent directories up to filesystem root.\n\n".utf8))
    FileHandle.standardError.write(Data("Solutions:\n".utf8))
    FileHandle.standardError.write(Data("  1. Use -c/--config to specify a configuration file\n".utf8))
    FileHandle.standardError.write(Data("  2. Create a compiler-config.json in the current or parent directory\n".utf8))
}

func parseFormat(_ raw: String?) -> ConfigFormat? {
    guard let raw else { return nil }
    return try? ConfigFormat.from(extension: raw)
}

func printVersionInfo() {
    let info = VersionInfo.collect()
    print("")
    print("╔════════════════════════════════════════════════════════════╗")
    print("║     AdGuard Filter Rules Compiler (Swift API)               ║")
    print("╚════════════════════════════════════════════════════════════╝")
    print("")
    print("  Version:      \(info.moduleVersion)")
    print("")
    print("  Platform:")
    print("    OS:         \(info.platform.osName)")
    print("    Arch:       \(info.platform.architecture)")
    print("")
    print("  Dependencies:")
    print("    Deno:       \(info.denoVersion ?? "Not found")")
    print("    Compiler:   \(info.compilerVersion ?? "Not found")")
    if let path = info.compilerPath {
        print("    Path:       \(path)")
    }
    print("")
}

func printConfig(path: URL, format: ConfigFormat?) -> Int32 {
    do {
        let config = try ConfigReader.readConfig(path: path, format: format)
        print("")
        print("╔════════════════════════════════════════════════════════════╗")
        print("║                    Configuration Details                   ║")
        print("╚════════════════════════════════════════════════════════════╝")
        print("")
        print("  File:         \(path.path)")
        print("  Format:       \(config.sourceFormat?.displayName ?? "")")
        print("")
        print("  Name:         \(config.name)")
        print("  Version:      \(config.version)")
        print("  License:      \(config.license)")
        if !config.description.isEmpty {
            print("  Description:  \(config.description)")
        }
        print("")
        print("  Sources:      \(config.sources.count) total")
        print("    Local:      \(config.localSourcesCount)")
        print("    Remote:     \(config.remoteSourcesCount)")
        print("")
        if !config.transformations.isEmpty {
            print("  Transformations:")
            for t in config.transformations { print("    - \(t)") }
            print("")
        }
        print("  Source Details:")
        for (i, source) in config.sources.enumerated() {
            print("    [\(i)] \(source.name)")
            print("        Type:   \(source.type.rawValue)")
            print("        Source: \(source.source)")
        }
        print("")
        return 0
    } catch {
        FileHandle.standardError.write(Data("[ERROR] Failed to read configuration: \(error)\n".utf8))
        return 1
    }
}

@discardableResult
func runCompile(
    configPath: URL,
    output: URL?,
    copyToRules: Bool,
    format: ConfigFormat?,
    debug: Bool,
    validate: Bool,
    failOnWarnings: Bool,
    allowUnvalidatedOutput: Bool,
    engine: String?,
    browserOutput: URL?
) -> Int32 {
    if allowUnvalidatedOutput {
        FileHandle.standardError.write(Data(
            "  [WARN] --allow-unvalidated-output set: compiled output will NOT be checked by " +
            "rules-validator. Not recommended outside deliberate debugging.\n".utf8
        ))
    }

    let options = CompileOptions(
        outputPath: output,
        copyToRules: copyToRules,
        format: format,
        debug: debug,
        validate: validate,
        failOnWarnings: failOnWarnings,
        allowUnvalidatedOutput: allowUnvalidatedOutput,
        engine: engine,
        browserOutputPath: browserOutput
    )

    print("")
    print("╔════════════════════════════════════════════════════════════╗")
    print("║                  Compiling Filter Rules                    ║")
    print("╚════════════════════════════════════════════════════════════╝")
    print("")
    print("  Config: \(configPath.path)")
    print("")

    do {
        let result = try BloqrCompiler(options: options).compile(configPath: configPath)
        if result.success {
            print("  \u{2713} Compilation successful!")
            print("")
            print("  Results:")
            print("    Filter:     \(result.configName) v\(result.configVersion)")
            print("    Rules:      \(result.ruleCount)")
            print("    Output:     \(result.outputPath.path)")
            print("    Hash:       \(result.hashShort)...")
            print("    Elapsed:    \(result.elapsedFormatted)")

            if let browserPath = result.browserOutputPath {
                print("")
                print("  Browser-syntax artifact:")
                print("    Output:     \(browserPath.path)")
                if let count = result.browserRuleCount {
                    print("    Rules:      \(count)")
                }
                if let hash = result.browserOutputHash {
                    print("    Hash:       \(String(hash.prefix(8)))...")
                }
            }

            if result.copiedToRules {
                print("")
                print("  \u{2713} Copied to:  \(result.rulesDestination?.path ?? "")")
            }
            print("")
            return 0
        } else {
            FileHandle.standardError.write(Data(
                "  \u{2717} Compilation failed: \(result.errorMessage ?? "Unknown error")\n".utf8
            ))
            if !result.stderr.isEmpty {
                FileHandle.standardError.write(Data("\n  Stderr:\n".utf8))
                for line in result.stderr.split(separator: "\n") {
                    FileHandle.standardError.write(Data("    \(line)\n".utf8))
                }
            }
            FileHandle.standardError.write(Data("\n".utf8))
            return 1
        }
    } catch {
        FileHandle.standardError.write(Data("  \u{2717} Error: \(error)\n\n".utf8))
        return 1
    }
}

/// Options shared by the root command and every subcommand, mirroring the other wrappers'
/// global CLI flags (`-c/--config`, `-o/--output`, `-r/--copy-to-rules`, `-f/--format`,
/// `-d/--debug`).
struct GlobalOptions: ParsableArguments {
    @Option(name: [.short, .long], help: "Path to configuration file")
    var config: String?

    @Option(name: [.short, .long], help: "Path to output file")
    var output: String?

    @Flag(name: [.customShort("r"), .long], help: "Copy output to rules directory")
    var copyToRules: Bool = false

    @Option(name: [.short, .long], help: "Force configuration format (json, yaml, toml)")
    var format: String?

    @Flag(name: [.short, .long], help: "Enable debug output")
    var debug: Bool = false

    init() {}
}

struct Compile: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "compile",
        abstract: "Compile filter rules from configuration"
    )

    @OptionGroup var global: GlobalOptions

    @Flag(name: .long, help: "Validate configuration before compiling")
    var validate: Bool = false

    @Flag(name: .long, help: "Fail compilation on validation warnings")
    var failOnWarnings: Bool = false

    @Flag(name: .long, help: "Opt out of the mandatory rules-validator syntax check on compiled output")
    var allowUnvalidatedOutput: Bool = false

    @Option(name: .long, help: "Compilation engine/grammar to use (\"dns\" or \"browser\")")
    var engine: String?

    @Option(name: .long, help: "Output path for the browser-syntax artifact")
    var browserOutput: String?

    func run() throws {
        let resolvedFormat = parseFormat(global.format)
        guard let configPath = global.config.map({ URL(fileURLWithPath: $0) }) ?? findDefaultConfig() else {
            printConfigNotFoundError()
            throw ExitCode.failure
        }

        let exitCode = runCompile(
            configPath: configPath,
            output: global.output.map { URL(fileURLWithPath: $0) },
            copyToRules: global.copyToRules,
            format: resolvedFormat,
            debug: global.debug,
            validate: validate,
            failOnWarnings: failOnWarnings,
            allowUnvalidatedOutput: allowUnvalidatedOutput,
            engine: engine,
            browserOutput: browserOutput.map { URL(fileURLWithPath: $0) }
        )
        if exitCode != 0 { throw ExitCode.failure }
    }
}

struct ShowConfig: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "config",
        abstract: "Show configuration details without compiling"
    )

    @OptionGroup var global: GlobalOptions

    func run() throws {
        let resolvedFormat = parseFormat(global.format)
        guard let configPath = global.config.map({ URL(fileURLWithPath: $0) }) ?? findDefaultConfig() else {
            printConfigNotFoundError()
            throw ExitCode.failure
        }
        if printConfig(path: configPath, format: resolvedFormat) != 0 {
            throw ExitCode.failure
        }
    }
}

struct ShowVersion: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "version",
        abstract: "Show version information for all components"
    )

    func run() throws {
        printVersionInfo()
    }
}

struct BloqrCompilerCLI: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "bloqr-compiler",
        abstract: "Compile AdGuard filter rules using @bloqr/compiler-core (via Deno)",
        version: bloqrCompilerSwiftVersion,
        subcommands: [Compile.self, ShowConfig.self, ShowVersion.self]
    )

    @OptionGroup var global: GlobalOptions

    func run() throws {
        let resolvedFormat = parseFormat(global.format)
        guard let configPath = global.config.map({ URL(fileURLWithPath: $0) }) ?? findDefaultConfig() else {
            printConfigNotFoundError()
            throw ExitCode.failure
        }

        let exitCode = runCompile(
            configPath: configPath,
            output: global.output.map { URL(fileURLWithPath: $0) },
            copyToRules: global.copyToRules,
            format: resolvedFormat,
            debug: global.debug,
            validate: false,
            failOnWarnings: false,
            allowUnvalidatedOutput: false,
            engine: nil,
            browserOutput: nil
        )
        if exitCode != 0 { throw ExitCode.failure }
    }
}

BloqrCompilerCLI.main()

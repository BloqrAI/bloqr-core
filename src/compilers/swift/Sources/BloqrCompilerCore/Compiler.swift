import Foundation
import CryptoKit

/// Compiles filter rules by shelling out to Deno + the `@bloqr/compiler-core` JSR package -
/// the same underlying compiler the Rust, .NET, and Python wrappers invoke. This wrapper does
/// not reimplement filter-list compilation; see `docs/RESTRUCTURING_RETROSPECTIVE.md` for why
/// every non-TypeScript wrapper in this repository follows that shape.
public struct BloqrCompiler: Sendable {
    public var options: CompileOptions

    public init(options: CompileOptions = CompileOptions()) {
        self.options = options
    }

    /// Compiles filter rules from the configuration at `configPath`.
    public func compile(configPath: URL) throws -> CompilerResult {
        try Self.compileRules(configPath: configPath, options: options)
    }

    /// Compiles filter rules from the configuration at `configPath` using `options`.
    ///
    /// Mirrors `compile_rules()` in the other wrappers: read config, optionally validate,
    /// write a temp JSON config if the source wasn't already JSON (the underlying compiler
    /// only accepts JSON), shell out to Deno, verify the output, and optionally copy it to the
    /// rules directory.
    public static func compileRules(configPath: URL, options: CompileOptions) throws -> CompilerResult {
        let start = Date()
        var result = CompilerResult()
        result.startTime = start

        let resolvedConfigPath = configPath.resolvingSymlinksInPath().standardizedFileURL

        let config = try ConfigReader.readConfig(path: resolvedConfigPath, format: options.format)
        result.configName = config.name
        result.configVersion = config.version

        if options.validate {
            try config.validate()
        }

        // Absolute-ize before use: this path is handed to the Deno subprocess (which runs with
        // the config's directory as its current directory, not this process's) and is also
        // used for existence/hashing checks in *this* process, so a relative path must resolve
        // identically in both places.
        let outputPath = Self.absoluteURL(
            options.outputPath ?? Self.generateOutputPath(configPath: resolvedConfigPath)
        )
        result.outputPath = outputPath

        // Convert to a canonical, comment-free `.json` path if needed - the underlying
        // compiler only accepts strict JSON at a `.json`-named path (a `.jsonc` extension
        // isn't recognized by its own extension-based format detection, and its `JSON.parse`
        // rejects `//`/`/* */` comments outright, even though this wrapper's own JSONC
        // preprocessing accepts them). This is keyed on the *resolved config path's own
        // extension and raw contents*, not just the parsed source format: a forced
        // `--format json` read of a `config.txt` still needs a `.json`-named temp file too.
        var tempConfigPath: URL?
        let compileConfigPath: URL
        let configExtension = resolvedConfigPath.pathExtension.lowercased()

        func writeTempConfig(_ content: String) throws -> URL {
            let temp = FileManager.default.temporaryDirectory
                .appendingPathComponent("compiler-config-\(UUID().uuidString).json")
            do {
                try content.write(to: temp, atomically: true, encoding: .utf8)
            } catch {
                throw CompilerError.fileSystem(
                    context: "writing temp config to \(temp.path)",
                    underlying: error.localizedDescription
                )
            }
            if options.debug {
                FileHandle.standardError.write(Data("[DEBUG] Created temp JSON config: \(temp.path)\n".utf8))
                FileHandle.standardError.write(Data("[DEBUG] Config content:\n\(content)\n".utf8))
            }
            return temp
        }

        if configExtension == "json" || configExtension == "jsonc" {
            // Re-read and de-comment the *raw* file text rather than re-serializing the
            // parsed `config` model: this wrapper's `CompilerConfig` doesn't model every
            // schema field (e.g. `output`/`hashVerification`/`archiving`), and going through
            // it would silently drop them for every plain, comment-free `.json` config -
            // reusing the raw text (byte-for-byte when there was nothing to strip) keeps
            // those fields intact.
            let rawContent: String
            do {
                rawContent = try String(contentsOf: resolvedConfigPath, encoding: .utf8)
            } catch {
                throw CompilerError.fileSystem(
                    context: "reading configuration from \(resolvedConfigPath.path)",
                    underlying: error.localizedDescription
                )
            }
            let strippedContent = try stripJSONCComments(rawContent)

            if configExtension == "json" && strippedContent == rawContent {
                // Already a canonical, comment-free `.json` file - hand it to Deno unchanged.
                compileConfigPath = resolvedConfigPath
            } else {
                let temp = try writeTempConfig(strippedContent)
                compileConfigPath = temp
                tempConfigPath = temp
            }
        } else {
            // YAML/TOML - there's no raw JSON text to preserve, so re-serialize the parsed
            // model (which, like the Rust/.NET/Python wrappers' own YAML/TOML structs,
            // doesn't carry schema fields this wrapper doesn't model either).
            let json = try ConfigReader.toJSON(config)
            let temp = try writeTempConfig(json)
            compileConfigPath = temp
            tempConfigPath = temp
        }
        // Scoped to cover every exit path below (directory creation, missing Deno, process
        // launch failure, and the ordinary success path alike) - not just the two explicit
        // `runProcess` outcomes, which previously left the temp file behind on any earlier
        // throw.
        defer {
            if let tempConfigPath { try? FileManager.default.removeItem(at: tempConfigPath) }
        }

        let outputDir = outputPath.deletingLastPathComponent()
        try Self.createDirectory(outputDir)

        let browserOutputPath = Self.absoluteURL(
            options.browserOutputPath ?? Self.deriveBrowserOutputPath(outputPath)
        )
        // Clear any pre-existing artifact at this path before compiling: the existence check
        // after the run is how we detect whether *this* invocation produced a browser-syntax
        // artifact, and a stale file left over from an earlier mixed-engine compile would
        // otherwise be misreported as this run's output.
        try? FileManager.default.removeItem(at: browserOutputPath)

        let (command, args) = try Self.compilerCommand(
            configPath: compileConfigPath.path,
            outputPath: outputPath.path,
            engine: options.engine,
            browserOutputPath: options.browserOutputPath != nil ? browserOutputPath.path : nil
        )

        if options.debug {
            FileHandle.standardError.write(Data("[DEBUG] Running: \(command) \(args.joined(separator: " "))\n".utf8))
        }

        let processOutput = try runProcess(
            command: command,
            arguments: args,
            currentDirectory: resolvedConfigPath.deletingLastPathComponent()
        )

        result.stdout = processOutput.stdout
        result.stderr = processOutput.stderr

        if processOutput.exitCode != 0 {
            result.errorMessage = "compiler exited with code \(processOutput.exitCode): " +
                processOutput.stderr.trimmingCharacters(in: .whitespacesAndNewlines)
            result.endTime = Date()
            result.elapsedMs = Self.elapsedMs(since: start)
            return result
        }

        guard FileManager.default.fileExists(atPath: outputPath.path) else {
            result.errorMessage = "output file was not created"
            result.endTime = Date()
            result.elapsedMs = Self.elapsedMs(since: start)
            return result
        }

        result.ruleCount = Self.countRules(path: outputPath)
        result.outputHash = try Self.computeHash(path: outputPath)

        // Mandatory rules-validator syntax check - fail-closed by default (see
        // `RulesValidator.validateOutput`'s doc comment). Mirrors the other wrappers: an
        // unvalidated compiled output is never silently treated as successful.
        if let abortReason = RulesValidator.validateOutput(
            path: outputPath,
            allowUnvalidated: options.allowUnvalidatedOutput,
            failOnWarnings: options.failOnWarnings
        ) {
            result.errorMessage = abortReason
            result.success = false
            result.endTime = Date()
            result.elapsedMs = Self.elapsedMs(since: start)
            return result
        }

        result.success = true

        if FileManager.default.fileExists(atPath: browserOutputPath.path) {
            result.browserRuleCount = Self.countRules(path: browserOutputPath)
            result.browserOutputHash = try Self.computeHash(path: browserOutputPath)

            if let abortReason = RulesValidator.validateOutput(
                path: browserOutputPath,
                allowUnvalidated: options.allowUnvalidatedOutput,
                failOnWarnings: options.failOnWarnings,
                engine: "browser"
            ) {
                result.errorMessage = "browser-syntax artifact failed syntax validation (DNS " +
                    "artifact was already published successfully at \(outputPath.path)): \(abortReason)"
                result.success = false
                result.endTime = Date()
                result.elapsedMs = Self.elapsedMs(since: start)
                return result
            }

            result.browserOutputPath = browserOutputPath
        }

        if options.copyToRules {
            let rulesDir = options.rulesDirectory ?? Self.rulesDirectory(configPath: resolvedConfigPath)
            try Self.createDirectory(rulesDir)
            let destination = rulesDir.appendingPathComponent("adguard_user_filter.txt")
            do {
                if FileManager.default.fileExists(atPath: destination.path) {
                    try FileManager.default.removeItem(at: destination)
                }
                try FileManager.default.copyItem(at: outputPath, to: destination)
            } catch {
                throw CompilerError.fileSystem(
                    context: "copying \(outputPath.path) to \(destination.path)",
                    underlying: error.localizedDescription
                )
            }
            result.copiedToRules = true
            result.rulesDestination = destination
        }

        result.endTime = Date()
        result.elapsedMs = Self.elapsedMs(since: start)
        return result
    }

    // MARK: - Helpers

    /// Resolves a possibly-relative URL against this process's current working directory, so
    /// it means the same thing here as it does to a subprocess launched with a *different*
    /// current directory (the Deno compiler runs with the config's own directory as its cwd).
    static func absoluteURL(_ url: URL) -> URL {
        guard !url.path.hasPrefix("/") else { return url.standardizedFileURL }
        let base = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        return base.appendingPathComponent(url.path).standardizedFileURL
    }

    static func elapsedMs(since start: Date) -> UInt64 {
        UInt64(max(0, Date().timeIntervalSince(start) * 1000))
    }

    static func createDirectory(_ url: URL) throws {
        do {
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        } catch {
            throw CompilerError.fileSystem(
                context: "creating directory \(url.path)",
                underlying: error.localizedDescription
            )
        }
    }

    /// Resolves the command and arguments to invoke the compiler, mirroring the other
    /// wrappers' `get_compiler_command()` - same JSR package, same permission flags, same
    /// flag names, so the two are drop-in equivalent for a given config.
    static func compilerCommand(
        configPath: String,
        outputPath: String,
        engine: String?,
        browserOutputPath: String?
    ) throws -> (String, [String]) {
        guard let deno = findCommand("deno") else {
            throw CompilerError.compilerNotFound
        }

        var args = denoPermissions
        args.append(jsrPackageSpecifier)
        args.append(contentsOf: ["--config", configPath])
        args.append(contentsOf: ["--output", outputPath])

        if let engine, engine.lowercased() != "auto" {
            args.append(contentsOf: ["--engine", engine])
        }
        if let browserOutputPath {
            args.append(contentsOf: ["--browser-output", browserOutputPath])
        }

        return (deno, args)
    }

    /// Derives the default output path for the browser-syntax artifact from the DNS/primary
    /// output path: `.txt` is replaced with `.browser.txt`; any other extension (or none) has
    /// `.browser.txt` appended. Mirrors the other wrappers' `deriveBrowserOutputPath`.
    static func deriveBrowserOutputPath(_ outputPath: URL) -> URL {
        let path = outputPath.path
        if path.hasSuffix(".txt") {
            return URL(fileURLWithPath: String(path.dropLast(4)) + ".browser.txt")
        }
        return URL(fileURLWithPath: path + ".browser.txt")
    }

    static func generateOutputPath(configPath: URL) -> URL {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyyMMdd-HHmmss"
        let timestamp = formatter.string(from: Date())
        let outputDir = configPath.deletingLastPathComponent().appendingPathComponent("output")
        return outputDir.appendingPathComponent("compiled-\(timestamp).txt")
    }

    static func rulesDirectory(configPath: URL) -> URL {
        configPath
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("rules")
    }

    /// Counts non-empty, non-comment lines in a compiled output file.
    public static func countRules(path: URL) -> Int {
        guard let data = try? Data(contentsOf: path), let text = String(data: data, encoding: .utf8) else {
            return 0
        }
        return text.split(separator: "\n", omittingEmptySubsequences: false)
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty && !$0.hasPrefix("!") && !$0.hasPrefix("#") }
            .count
    }

    /// Computes the SHA-384 hash of a file, matching the other wrappers' hex-encoded digest.
    public static func computeHash(path: URL) throws -> String {
        guard let data = try? Data(contentsOf: path) else {
            throw CompilerError.fileSystem(context: "reading \(path.path) for hashing", underlying: "file not found")
        }
        let digest = SHA384.hash(data: data)
        return digest.map { String(format: "%02x", $0) }.joined()
    }
}

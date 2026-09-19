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
    public func compile(configPath: URL) throws(CompilerError) -> CompilerResult {
        try Self.compileRules(configPath: configPath, options: options)
    }

    /// Compiles filter rules from the configuration at `configPath`, asynchronously.
    ///
    /// Mirrors the other wrappers' async entry points (Rust's `compile_rules_async`,
    /// .NET's `CompileAsync`, Python's `compile_rules_async`): same pipeline as
    /// `compile(configPath:)`, just off the calling task.
    public func compile(configPath: URL) async throws(CompilerError) -> CompilerResult {
        try await Self.compileRules(configPath: configPath, options: options)
    }

    /// Compiles filter rules from the configuration at `configPath` using `options`.
    ///
    /// Mirrors `compile_rules()` in the other wrappers: read config, optionally validate,
    /// write a temp JSON config if the source wasn't already JSON (the underlying compiler
    /// only accepts JSON), shell out to Deno, verify the output, and optionally copy it to the
    /// rules directory.
    public static func compileRules(configPath: URL, options: CompileOptions) throws(CompilerError) -> CompilerResult {
        let start = Date()
        var result = CompilerResult()
        result.startTime = start

        // Absolute-ize before resolving symlinks: `resolvingSymlinksInPath()` does not make a
        // relative URL absolute on its own, and this path is both handed to the Deno
        // subprocess (relative to *its* working directory, the config's own parent) and used
        // to derive that very working directory in this process - a relative `-c
        // configs/config.json` would otherwise resolve to `configs/configs/config.json` from
        // Deno's point of view.
        let resolvedConfigPath = Self.absoluteURL(configPath).resolvingSymlinksInPath().standardizedFileURL

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
        // preprocessing accepts them). This is keyed on `config.sourceFormat` - the format
        // actually used to parse the file, honoring an explicit `--format` override - not the
        // path's extension: `-f yaml -c config.json` must still be re-serialized as JSON
        // rather than handing Deno the raw YAML text under a `.json`-named path.
        var tempConfigPath: URL?
        let compileConfigPath: URL
        let configExtension = resolvedConfigPath.pathExtension.lowercased()

        func writeTempConfig(_ content: String) throws(CompilerError) -> URL {
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

        if config.sourceFormat == .json {
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
            // Parsed as YAML or TOML (whatever the file's own extension says) - there's no
            // raw JSON text to preserve, so re-serialize the parsed model (which, like the
            // Rust/.NET/Python wrappers' own YAML/TOML structs, doesn't carry schema fields
            // this wrapper doesn't model either).
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
        // Record whether an artifact already sits at this path, and its modification date, so
        // that after the run we can tell "this invocation just wrote it" apart from "a stale
        // file was already here" *without* ever deleting anything up front. Deleting it first
        // (an earlier version of this check did) is unsafe: `--browser-output` can point at an
        // arbitrary pre-existing file, and the underlying compiler simply leaves it untouched
        // for a DNS-only config - deleting it first would destroy that unrelated file for
        // nothing, since nothing would recreate it.
        let previousBrowserModDate: Date? = {
            guard let attributes = try? FileManager.default.attributesOfItem(atPath: browserOutputPath.path) else {
                return nil
            }
            return attributes[.modificationDate] as? Date
        }()

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
        // unvalidated compiled output is never silently treated as successful. Pass through
        // the resolved engine: `outputPath` holds browser-syntax content - not just when
        // `--engine browser` is forced, but also when every source resolves to the browser
        // engine via its own `engine:`/`config.defaultEngine` - and validating that against
        // the DNS grammar default would reject valid cosmetic/extended-CSS rules. This can't
        // account for pure content-sniffing auto-detection (the same auto-detection
        // `@bloqr/compiler-core` itself does, which this wrapper doesn't reimplement), only
        // what the configuration itself declares.
        if let abortReason = RulesValidator.validateOutput(
            path: outputPath,
            allowUnvalidated: options.allowUnvalidatedOutput,
            failOnWarnings: options.failOnWarnings,
            engine: Self.primaryArtifactEngine(config: config, options: options)
        ) {
            result.errorMessage = abortReason
            result.success = false
            result.endTime = Date()
            result.elapsedMs = Self.elapsedMs(since: start)
            return result
        }

        result.success = true

        // A browser-syntax artifact exists at this path *from this run* only if either it
        // didn't exist before and does now, or it existed before and the compiler rewrote it
        // (its modification date changed) - never from bare existence alone, which a
        // pre-existing file (a stale artifact from an earlier mixed-engine run, or an
        // unrelated file `--browser-output` happened to point at) would satisfy without this
        // invocation having produced anything.
        let browserModDateNow: Date? = {
            guard let attributes = try? FileManager.default.attributesOfItem(atPath: browserOutputPath.path) else {
                return nil
            }
            return attributes[.modificationDate] as? Date
        }()
        let browserProducedThisRun: Bool
        switch (previousBrowserModDate, browserModDateNow) {
        case (nil, .some):
            browserProducedThisRun = true
        case let (.some(previous), .some(now)):
            browserProducedThisRun = now != previous
        default:
            browserProducedThisRun = false
        }

        if browserProducedThisRun {
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
            if destination.standardizedFileURL.path != outputPath.standardizedFileURL.path {
                // Copy to a staging path first and swap it in with `replaceItemAt`, so a
                // failed copy (disk full, permissions) can never leave `destination` deleted
                // with nothing in its place - the previous good rules file stays intact until
                // the new one is fully written.
                let staging = rulesDir.appendingPathComponent(".adguard_user_filter.txt.\(UUID().uuidString).tmp")
                do {
                    try FileManager.default.copyItem(at: outputPath, to: staging)
                    // `replaceItemAt` requires the destination to already exist (it fails
                    // otherwise) - a fresh rules directory has no prior
                    // `adguard_user_filter.txt` to replace, so move the staged file into place
                    // directly on a first run.
                    if FileManager.default.fileExists(atPath: destination.path) {
                        _ = try FileManager.default.replaceItemAt(destination, withItemAt: staging)
                    } else {
                        try FileManager.default.moveItem(at: staging, to: destination)
                    }
                } catch {
                    try? FileManager.default.removeItem(at: staging)
                    throw CompilerError.fileSystem(
                        context: "copying \(outputPath.path) to \(destination.path)",
                        underlying: error.localizedDescription
                    )
                }
            }
            // Else: the requested output path already *is* the rules destination (e.g.
            // `-o <rules-dir>/adguard_user_filter.txt`) - the compiler just wrote it there
            // directly, so there's nothing left to copy.
            result.copiedToRules = true
            result.rulesDestination = destination
        }

        result.endTime = Date()
        result.elapsedMs = Self.elapsedMs(since: start)
        return result
    }

    /// Compiles filter rules from the configuration at `configPath` using `options`,
    /// asynchronously.
    ///
    /// Runs the synchronous `compileRules(configPath:options:)` pipeline (config read, Deno
    /// subprocess, hashing, syntax validation) on the global concurrent GCD queue, bridged back
    /// via a checked continuation - deliberately *not* `Task.detached`, which only detaches
    /// actor/priority inheritance and still schedules its operation on Swift Concurrency's
    /// cooperative thread pool. That pool is sized for non-blocking work; this pipeline calls
    /// `Process.waitUntilExit()`/`DispatchGroup.wait()` under the hood, and blocking a
    /// cooperative thread on those can starve every other async task sharing the pool. A GCD
    /// global queue has no such ceiling on blocked threads, so a caller on Swift Concurrency's
    /// cooperative pool - a SwiftUI view, a Vapor route handler, an `async` CLI command - is
    /// never at risk of that starvation. This wrapper shells out to a subprocess for the actual
    /// compilation (see the type-level doc comment above), so there is no natively-async Deno
    /// invocation to await here; offloading the whole synchronous pipeline is what the other
    /// wrappers' own async entry points do too.
    public static func compileRules(
        configPath: URL,
        options: CompileOptions
    ) async throws(CompilerError) -> CompilerResult {
        // `withCheckedThrowingContinuation` is untyped-throws in the standard library (there is
        // no typed-throws overload as of Swift 6.0), so bridge its `any Error` back to
        // `CompilerError` explicitly rather than widening this function's own signature to
        // match it. The `catch { }` branch is unreachable in practice: the continuation is only
        // ever resumed with the error the synchronous `compileRules(configPath:options:)` threw,
        // which is always a `CompilerError`.
        do {
            return try await withCheckedThrowingContinuation { continuation in
                DispatchQueue.global(qos: .userInitiated).async {
                    do {
                        let result = try compileRules(configPath: configPath, options: options)
                        continuation.resume(returning: result)
                    } catch {
                        continuation.resume(throwing: error)
                    }
                }
            }
        } catch let error as CompilerError {
            throw error
        } catch {
            throw CompilerError.serialization(String(describing: error))
        }
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

    static func createDirectory(_ url: URL) throws(CompilerError) {
        do {
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        } catch {
            throw CompilerError.fileSystem(
                context: "creating directory \(url.path)",
                underlying: error.localizedDescription
            )
        }
    }

    /// Best-effort guess at which grammar the primary output artifact ends up in, from what
    /// the configuration itself declares - not from sniffing the compiled content the way
    /// `@bloqr/compiler-core` itself does, which this wrapper doesn't reimplement. Returns
    /// `"browser"` when it's confident every source resolves to the browser engine, `nil`
    /// otherwise (letting `RulesValidator` fall back to its DNS default).
    ///
    /// Mirrors the TypeScript engine's own precedence (`EngineDetector.detectSourceEngine`,
    /// see `docs/architecture/dual-engine-compilation.md`): an explicit per-source `engine`
    /// wins; otherwise a `hosts`-type source is unconditionally DNS *before*
    /// `config.defaultEngine` is even considered (hosts-file syntax has no browser-grammar
    /// equivalent), and only then does `defaultEngine` apply.
    static func primaryArtifactEngine(config: CompilerConfig, options: CompileOptions) -> String? {
        if let engine = options.engine, engine.lowercased() != "auto" {
            return engine
        }
        guard !config.sources.isEmpty else { return nil }
        let allBrowser = config.sources.allSatisfy { source in
            if let explicitEngine = source.engine { return explicitEngine == .browser }
            if source.type == .hosts { return false }
            return config.defaultEngine == .browser
        }
        return allBrowser ? "browser" : nil
    }

    /// Resolves the command and arguments to invoke the compiler, mirroring the other
    /// wrappers' `get_compiler_command()` - same JSR package, same permission flags, same
    /// flag names, so the two are drop-in equivalent for a given config.
    static func compilerCommand(
        configPath: String,
        outputPath: String,
        engine: String?,
        browserOutputPath: String?
    ) throws(CompilerError) -> (String, [String]) {
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

    /// Generates a default output path when the caller didn't pass `options.outputPath`.
    ///
    /// The timestamp component is for readability, not uniqueness: a second-resolution
    /// `yyyyMMdd-HHmmss` alone would let two concurrent compilations for configs in the same
    /// directory within the same second collide on one output path, with one process
    /// overwriting the other's output while it's still being hashed or copied - a real risk now
    /// that `compileRules(configPath:options:)` has an `async` entry point inviting concurrent
    /// use (e.g. `async let`) rather than only ever running one compilation at a time from a
    /// single CLI invocation. A short random suffix rules that out without changing the
    /// filename's readable shape.
    static func generateOutputPath(configPath: URL) -> URL {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyyMMdd-HHmmss"
        let timestamp = formatter.string(from: Date())
        let uniqueSuffix = UUID().uuidString.prefix(8).lowercased()
        let outputDir = configPath.deletingLastPathComponent().appendingPathComponent("output")
        return outputDir.appendingPathComponent("compiled-\(timestamp)-\(uniqueSuffix).txt")
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
        // Normalize CRLF to LF *before* splitting: Swift's `String` treats "\r\n" as a single
        // extended grapheme cluster, so `split(separator: "\n")` never matches at a CRLF
        // boundary and a whole CRLF file collapses into one giant "line". Normalizing first
        // avoids that entirely, rather than trying to trim a lone "\n" out from inside a
        // grapheme cluster after the fact.
        let normalized = text.replacingOccurrences(of: "\r\n", with: "\n")
        return normalized.split(separator: "\n", omittingEmptySubsequences: false)
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty && !$0.hasPrefix("!") && !$0.hasPrefix("#") }
            .count
    }

    /// Computes the SHA-384 hash of a file, matching the other wrappers' hex-encoded digest.
    public static func computeHash(path: URL) throws(CompilerError) -> String {
        guard let data = try? Data(contentsOf: path) else {
            throw CompilerError.fileSystem(context: "reading \(path.path) for hashing", underlying: "file not found")
        }
        let digest = SHA384.hash(data: data)
        return digest.map { String(format: "%02x", $0) }.joined()
    }
}

import XCTest
@testable import BloqrCompilerCore

final class CompilerHelperTests: XCTestCase {
    func testDeriveBrowserOutputPathReplacesTxtSuffix() {
        let derived = BloqrCompiler.deriveBrowserOutputPath(URL(fileURLWithPath: "/tmp/output.txt"))
        XCTAssertEqual(derived.path, "/tmp/output.browser.txt")
    }

    func testDeriveBrowserOutputPathAppendsWhenNoTxtSuffix() {
        let derived = BloqrCompiler.deriveBrowserOutputPath(URL(fileURLWithPath: "/tmp/output"))
        XCTAssertEqual(derived.path, "/tmp/output.browser.txt")
    }

    func testAbsoluteURLLeavesAbsolutePathsUnchanged() {
        let url = URL(fileURLWithPath: "/tmp/foo/bar.txt")
        XCTAssertEqual(BloqrCompiler.absoluteURL(url).path, "/tmp/foo/bar.txt")
    }

    func testAbsoluteURLResolvesRelativePathAgainstCurrentDirectory() {
        let cwd = FileManager.default.currentDirectoryPath
        let resolved = BloqrCompiler.absoluteURL(URL(fileURLWithPath: "output.txt"))
        XCTAssertEqual(resolved.path, cwd + "/output.txt")
    }

    func testCountRulesTrimsCarriageReturns() throws {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let path = dir.appendingPathComponent("rules.txt")
        // CRLF line endings: a blank CRLF line must not be counted as a rule.
        try "rule1\r\n\r\nrule2\r\n".write(to: path, atomically: true, encoding: .utf8)

        XCTAssertEqual(BloqrCompiler.countRules(path: path), 2)
    }

    func testCompileAsyncPropagatesConfigReadErrors() async {
        let missingConfig = FileManager.default.temporaryDirectory
            .appendingPathComponent("bloqr-compiler-missing-\(UUID().uuidString).json")

        do {
            _ = try await BloqrCompiler().compile(configPath: missingConfig)
            XCTFail("expected compile(configPath:) to throw for a missing config file")
        } catch {
            // `compile(configPath:)` is `throws(CompilerError)`, so `error` is already
            // concretely typed here - no `as? CompilerError` cast needed or possible.
            guard case .configNotFound = error else {
                return XCTFail("expected CompilerError.configNotFound, got \(error)")
            }
        }
    }

    func testStaticCompileRulesAsyncMatchesSyncErrorForMissingConfig() async {
        let missingConfig = FileManager.default.temporaryDirectory
            .appendingPathComponent("bloqr-compiler-missing-\(UUID().uuidString).json")
        let options = CompileOptions()

        let syncResult = Result { try BloqrCompiler.compileRules(configPath: missingConfig, options: options) }
        let asyncResult = await Task {
            try await BloqrCompiler.compileRules(configPath: missingConfig, options: options)
        }.result

        guard case .failure(let syncError as CompilerError) = syncResult,
              case .failure(let asyncError as CompilerError) = asyncResult,
              case .configNotFound = syncError,
              case .configNotFound = asyncError
        else {
            return XCTFail("expected both the sync and async APIs to throw CompilerError.configNotFound")
        }
    }

    func testPrimaryArtifactEngineHonorsForcedEngine() {
        let config = CompilerConfig(name: "Test", sources: [FilterSource(source: "https://example.com")])
        let options = CompileOptions(engine: "browser")
        XCTAssertEqual(BloqrCompiler.primaryArtifactEngine(config: config, options: options), "browser")
    }

    func testPrimaryArtifactEngineInfersBrowserFromAllSources() {
        let config = CompilerConfig(
            name: "Test",
            sources: [
                FilterSource(source: "https://a.example.com", engine: .browser),
                FilterSource(source: "https://b.example.com", engine: .browser),
            ]
        )
        XCTAssertEqual(BloqrCompiler.primaryArtifactEngine(config: config, options: CompileOptions()), "browser")
    }

    func testPrimaryArtifactEngineIsNilForMixedSources() {
        let config = CompilerConfig(
            name: "Test",
            sources: [
                FilterSource(source: "https://a.example.com", engine: .browser),
                FilterSource(source: "https://b.example.com", engine: .dns),
            ]
        )
        XCTAssertNil(BloqrCompiler.primaryArtifactEngine(config: config, options: CompileOptions()))
    }

    func testPrimaryArtifactEngineHostsTypeOverridesDefaultEngine() {
        // A hosts-type source is unconditionally DNS, even with defaultEngine: browser and no
        // explicit per-source engine - matching EngineDetector.detectSourceEngine's precedence.
        let config = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com/hosts.txt", type: .hosts)],
            defaultEngine: .browser
        )
        XCTAssertNil(BloqrCompiler.primaryArtifactEngine(config: config, options: CompileOptions()))
    }
}

import XCTest
@testable import BloqrCompilerCore

final class CompilerConfigTests: XCTestCase {
    func testValidateRequiresName() {
        let config = CompilerConfig(name: "", sources: [FilterSource(source: "https://example.com")])
        XCTAssertThrowsError(try config.validate())
    }

    func testValidateRequiresSources() {
        let config = CompilerConfig(name: "Test")
        XCTAssertThrowsError(try config.validate())
    }

    func testValidateSucceeds() throws {
        let config = CompilerConfig(name: "Test", sources: [FilterSource(source: "https://example.com")])
        XCTAssertNoThrow(try config.validate())
    }

    /// Explicit list (not `CompilerConfig.validTransformations` itself) so this test
    /// actually checks the documented contract - an accidental omission or typo in the
    /// implementation's allowlist would otherwise still pass trivially.
    static let documentedTransformations = [
        "RemoveComments", "Compress", "RemoveModifiers", "Validate", "ValidateAllowIp",
        "Deduplicate", "InvertAllow", "RemoveEmptyLines", "TrimLines", "InsertFinalNewLine",
        "ConvertToAscii",
    ]

    func testValidateAcceptsEveryDocumentedTransformation() throws {
        let config = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com")],
            transformations: Self.documentedTransformations
        )
        XCTAssertNoThrow(try config.validate())
    }

    func testValidateRejectsUnknownGlobalTransformation() {
        let config = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com")],
            transformations: ["NotARealTransformation"]
        )
        XCTAssertThrowsError(try config.validate())
    }

    func testValidateRejectsCommercialOnlyTransformation() {
        // ConflictDetection/RuleOptimizer exist in the TypeScript reference
        // implementation's TransformationType enum but are commercial-only and not
        // implemented anywhere in this OSS repo (see issue #502) - accepting them
        // here would silently no-op since nothing applies them at compile time.
        // Each is asserted independently so an accidental allowlist entry for one
        // can't slip through unnoticed because only the other was tested.
        let conflictDetectionConfig = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com")],
            transformations: ["ConflictDetection"]
        )
        XCTAssertThrowsError(try conflictDetectionConfig.validate())

        let ruleOptimizerConfig = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com")],
            transformations: ["RuleOptimizer"]
        )
        XCTAssertThrowsError(try ruleOptimizerConfig.validate())
    }

    func testValidateRejectsUnknownPerSourceTransformation() {
        let config = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com", transformations: ["NotARealTransformation"])]
        )
        XCTAssertThrowsError(try config.validate())
    }

    func testFilterSourceIsURL() {
        let urlSource = FilterSource(source: "https://example.com/list.txt")
        XCTAssertTrue(urlSource.isURL)
        XCTAssertFalse(urlSource.isLocal)

        let localSource = FilterSource(source: "./local/list.txt")
        XCTAssertFalse(localSource.isURL)
        XCTAssertTrue(localSource.isLocal)
    }

    func testSourcesCount() {
        let config = CompilerConfig(
            name: "Test",
            sources: [
                FilterSource(source: "./local1.txt"),
                FilterSource(source: "https://example.com/list.txt"),
                FilterSource(source: "./local2.txt"),
            ]
        )
        XCTAssertEqual(config.localSourcesCount, 2)
        XCTAssertEqual(config.remoteSourcesCount, 1)
    }

    func testEngineKindArgumentParsing() {
        XCTAssertEqual(EngineKind(argument: "dns"), .dns)
        XCTAssertEqual(EngineKind(argument: "DNS"), .dns)
        XCTAssertEqual(EngineKind(argument: "browser"), .browser)
        XCTAssertNil(EngineKind(argument: "auto"))
    }

    func testCompilerErrorDescriptions() {
        XCTAssertTrue(CompilerError.compilerNotFound.description.contains("deno"))
        XCTAssertTrue(CompilerError.configNotFound(path: "/tmp/x.json").description.contains("/tmp/x.json"))
    }
}

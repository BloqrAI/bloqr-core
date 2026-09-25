import XCTest
@testable import BloqrCompilerCore

/// Tests for JSON Schema config validation, including a drift guard between the bundled
/// schema resource (`Sources/BloqrCompilerCore/Resources/compiler-config.schema.json`) and
/// the repo-root canonical schema (`schemas/compiler-config.schema.json`) - see issue #518.
final class SchemaValidationTests: XCTestCase {
    /// Repo root, resolved from this test file's own path so it works regardless of the
    /// current working directory the test runner is invoked from.
    private static var repoRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent() // SchemaValidationTests.swift -> BloqrCompilerCoreTests/
            .deletingLastPathComponent() // BloqrCompilerCoreTests/ -> Tests/
            .deletingLastPathComponent() // Tests/ -> swift/
            .deletingLastPathComponent() // swift/ -> compilers/
            .deletingLastPathComponent() // compilers/ -> src/
            .deletingLastPathComponent() // src/ -> repo root
    }

    func testBundledSchemaCopyStaysInSyncWithCanonical() throws {
        let canonicalPath = Self.repoRoot
            .appendingPathComponent("schemas/compiler-config.schema.json")
        let canonical = try String(contentsOf: canonicalPath, encoding: .utf8)
        XCTAssertEqual(
            SchemaValidation.schemaJSONText, canonical,
            "Sources/BloqrCompilerCore/compiler-config.schema.json has drifted from " +
                "the repo-root schemas/compiler-config.schema.json - copy the canonical file " +
                "over the bundled one (cp schemas/compiler-config.schema.json " +
                "src/compilers/swift/Sources/BloqrCompilerCore/compiler-config.schema.json)."
        )
    }

    func testAcceptsMinimalValidConfiguration() throws {
        let config = CompilerConfig(name: "Test", sources: [FilterSource(source: "https://example.com/list.txt")])
        XCTAssertNoThrow(try SchemaValidation.assertValid(config))
    }

    func testAcceptsConflictDetectionAndRuleOptimizerTransformations() throws {
        // Regression coverage: the canonical schema's enum includes these (the TS reference
        // implementation supports them) even though this wrapper's own CompilerConfig.validate()
        // rejects them as unsupported by this engine - both are correct at their own layer.
        let config = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com/list.txt")],
            transformations: ["ConflictDetection", "RuleOptimizer"]
        )
        XCTAssertNoThrow(try SchemaValidation.assertValid(config))
    }

    func testRejectsConfigMissingRequiredFields() {
        let config = CompilerConfig(name: "", sources: [])
        XCTAssertThrowsError(try SchemaValidation.assertValid(config))
    }

    func testRejectsInvalidTransformationEnumValue() {
        let config = CompilerConfig(
            name: "Test",
            sources: [FilterSource(source: "https://example.com/list.txt")],
            transformations: ["NotARealTransformation"]
        )
        XCTAssertThrowsError(try SchemaValidation.assertValid(config))
    }

    func testRejectsNonURIHomepage() {
        // Regression coverage: JSONSchema.swift must actually enforce the "format" keyword
        // (homepage's format: "uri") for this to catch anything - confirmed it does by default
        // (unlike ajv/jsonschema-python/the Rust jsonschema crate, which all needed an extra
        // step to enable format validation).
        let config = CompilerConfig(
            name: "Test",
            homepage: "not a uri",
            sources: [FilterSource(source: "https://example.com/list.txt")]
        )
        XCTAssertThrowsError(try SchemaValidation.assertValid(config))
    }

    func testAcceptsValidHomepageURI() {
        let config = CompilerConfig(
            name: "Test",
            homepage: "https://github.com/BloqrAI/bloqr-core",
            sources: [FilterSource(source: "https://example.com/list.txt")]
        )
        XCTAssertNoThrow(try SchemaValidation.assertValid(config))
    }
}

/// Integration coverage: proves `ConfigReader.readConfig()` itself - not just
/// `SchemaValidation.assertValid()` called directly - rejects a schema-invalid config.
final class ConfigReaderSchemaValidationTests: XCTestCase {
    private func writeConfig(_ json: String) throws -> URL {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: dir) }
        let path = dir.appendingPathComponent("config.json")
        try json.write(to: path, atomically: true, encoding: .utf8)
        return path
    }

    func testReadConfigRejectsSchemaInvalidTransformation() throws {
        let path = try writeConfig("""
        {
          "name": "Test",
          "sources": [{"source": "https://example.com/list.txt"}],
          "transformations": ["NotARealTransformation"]
        }
        """)
        XCTAssertThrowsError(try ConfigReader.readConfig(path: path))
    }

    func testReadConfigRejectsUnrecognizedSourcePropertyDroppedByDecoding() throws {
        // Regression coverage: CompilerConfig/FilterSource's explicit CodingKeys silently drop
        // any key they don't model, so validating only the re-encoded CompilerConfig can't catch
        // a config that adds an unrecognized property - ConfigReader.readConfig must validate the
        // raw parsed JSON too (see SchemaValidation.assertValid(rawValue:)).
        let path = try writeConfig("""
        {
          "name": "Test",
          "sources": [{"source": "https://example.com/list.txt", "bogus": true}]
        }
        """)
        XCTAssertThrowsError(try ConfigReader.readConfig(path: path))
    }

    func testReadConfigAcceptsSchemaValidConfiguration() throws {
        let path = try writeConfig("""
        {
          "name": "Test",
          "sources": [{"source": "https://example.com/list.txt"}],
          "transformations": ["Deduplicate"]
        }
        """)
        let config = try ConfigReader.readConfig(path: path)
        XCTAssertEqual(config.name, "Test")
    }
}

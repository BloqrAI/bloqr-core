import XCTest
@testable import BloqrCompilerCore

final class ConfigReaderTests: XCTestCase {
    func testReadJSONConfig() throws {
        let json = """
        {"name": "Test", "version": "1.0.0", "sources": [{"source": "test.txt"}]}
        """
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let path = dir.appendingPathComponent("config.json")
        try json.write(to: path, atomically: true, encoding: .utf8)

        let config = try ConfigReader.readConfig(path: path)
        XCTAssertEqual(config.name, "Test")
        XCTAssertEqual(config.version, "1.0.0")
        XCTAssertEqual(config.sourceFormat, .json)
        XCTAssertEqual(config.sources.count, 1)
    }

    func testReadYAMLConfig() throws {
        let yaml = "name: YAML Test\nversion: 2.0.0\nsources:\n  - source: test.txt\n"
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let path = dir.appendingPathComponent("config.yaml")
        try yaml.write(to: path, atomically: true, encoding: .utf8)

        let config = try ConfigReader.readConfig(path: path)
        XCTAssertEqual(config.name, "YAML Test")
        XCTAssertEqual(config.version, "2.0.0")
        XCTAssertEqual(config.sourceFormat, .yaml)
    }

    func testConfigNotFound() {
        let path = URL(fileURLWithPath: "/nonexistent/compiler-config.json")
        XCTAssertThrowsError(try ConfigReader.readConfig(path: path)) { error in
            guard case CompilerError.configNotFound = error else {
                return XCTFail("expected configNotFound, got \(error)")
            }
        }
    }

    func testFormatFromExtension() throws {
        XCTAssertEqual(try ConfigFormat.from(fileExtension: "json"), .json)
        XCTAssertEqual(try ConfigFormat.from(fileExtension: "yaml"), .yaml)
        XCTAssertEqual(try ConfigFormat.from(fileExtension: "yml"), .yaml)
        XCTAssertEqual(try ConfigFormat.from(fileExtension: "toml"), .toml)
        XCTAssertThrowsError(try ConfigFormat.from(fileExtension: "txt"))
    }

    func testReadJSONCConfigWithComments() throws {
        let jsonc = """
        {
          // top-level name
          "name": "Test", // trailing comment
          /* version */
          "version": "1.0.0",
          "sources": [{"source": "https://example.com/list.txt" /* inline */}]
        }
        """
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let path = dir.appendingPathComponent("config.jsonc")
        try jsonc.write(to: path, atomically: true, encoding: .utf8)

        let config = try ConfigReader.readConfig(path: path)
        XCTAssertEqual(config.name, "Test")
        XCTAssertEqual(config.version, "1.0.0")
        XCTAssertEqual(config.sourceFormat, .json)
        XCTAssertEqual(config.sources.first?.source, "https://example.com/list.txt")
    }

    func testStripJSONCCommentsPreservesStringsWithSlashes() throws {
        let input = #"{"source": "https://example.com/list.txt", "n": 1} // trailing"#
        let stripped = try stripJSONCComments(input)
        XCTAssertTrue(stripped.contains("https://example.com/list.txt"))
        XCTAssertFalse(stripped.contains("trailing"))
    }

    func testStripJSONCCommentsRejectsUnterminatedBlockComment() {
        let input = #"{"name": "x"} /* unterminated"#
        XCTAssertThrowsError(try stripJSONCComments(input))
    }

    func testStripJSONCCommentsDoesNotMergeAdjacentTokens() throws {
        // `{"n": 1/*comment*/0}` was never valid JSONC (no separator between the two number
        // tokens) - but without a whitespace placeholder left behind, stripping the comment
        // would silently produce `{"n": 10}`, a corrupted value that parses without error.
        // With the placeholder, it becomes `{"n": 1 0}`, which correctly fails to parse
        // instead of silently succeeding with the wrong number.
        let input = #"{"n": 1/*comment*/0}"#
        let stripped = try stripJSONCComments(input)
        XCTAssertFalse(stripped.contains("10"))
        XCTAssertThrowsError(try JSONSerialization.jsonObject(with: Data(stripped.utf8)))
    }

    func testToJSONRoundTrip() throws {
        var config = CompilerConfig(name: "Test", version: "1.0.0")
        config.sources = [FilterSource(name: "Local", source: "./rules.txt")]
        let json = try ConfigReader.toJSON(config)
        XCTAssertTrue(json.contains("\"name\""))
        XCTAssertTrue(json.contains("Test"))
    }

    func testToYAMLRoundTrip() throws {
        // Regression test for a real bug this file's typed-throws pass (`throws(CompilerError)`)
        // surfaced: the old toYAML round-tripped through `JSONSerialization.jsonObject` into a
        // type-erased `[String: Any]` for `Yams.dump(object:)`, which actually failed at
        // runtime ("Failed to represent 1.0.0") because JSONSerialization's Foundation-bridged
        // values (e.g. NSString) aren't always ones Yams's `Any`-based encoder recognizes -
        // there was no test exercising toYAML at all before this one, so nothing had caught it.
        // toYAML now encodes `CompilerConfig` directly via `YAMLEncoder`, the same `Encodable`
        // path toJSON/toTOML use, which both fixes the runtime failure and gives toYAML proper
        // `CompilerError` wrapping for whatever it can still throw. Round-tripping back through
        // `ConfigReader.parse` confirms the fixed output actually parses back correctly.
        var config = CompilerConfig(name: "Test", version: "1.0.0")
        config.sources = [FilterSource(name: "Local", source: "./rules.txt")]
        let yaml = try ConfigReader.toYAML(config)
        XCTAssertTrue(yaml.contains("name: Test"))

        let reparsed = try ConfigReader.parse(yaml, format: .yaml)
        XCTAssertEqual(reparsed.name, "Test")
        XCTAssertEqual(reparsed.sources.first?.source, "./rules.txt")
    }
}

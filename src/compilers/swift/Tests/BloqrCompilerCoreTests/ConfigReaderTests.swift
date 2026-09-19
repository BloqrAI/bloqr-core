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
        XCTAssertEqual(try ConfigFormat.from(extension: "json"), .json)
        XCTAssertEqual(try ConfigFormat.from(extension: "yaml"), .yaml)
        XCTAssertEqual(try ConfigFormat.from(extension: "yml"), .yaml)
        XCTAssertEqual(try ConfigFormat.from(extension: "toml"), .toml)
        XCTAssertThrowsError(try ConfigFormat.from(extension: "txt"))
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

    func testToJSONRoundTrip() throws {
        var config = CompilerConfig(name: "Test", version: "1.0.0")
        config.sources = [FilterSource(name: "Local", source: "./rules.txt")]
        let json = try ConfigReader.toJSON(config)
        XCTAssertTrue(json.contains("\"name\""))
        XCTAssertTrue(json.contains("Test"))
    }
}

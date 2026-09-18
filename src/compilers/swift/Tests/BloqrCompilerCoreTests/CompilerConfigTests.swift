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

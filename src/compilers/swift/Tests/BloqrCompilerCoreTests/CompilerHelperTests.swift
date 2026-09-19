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
}

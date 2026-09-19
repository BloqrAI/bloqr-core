import XCTest
@testable import BloqrCompilerCore

final class RulesValidatorTests: XCTestCase {
    func testSyntaxValidationResultDecodesSnakeCase() throws {
        let json = """
        {"is_valid": false, "valid_rules": 3, "invalid_rules": 1, "messages": ["bad rule"]}
        """
        let result = try JSONDecoder().decode(SyntaxValidationResult.self, from: Data(json.utf8))
        XCTAssertFalse(result.isValid)
        XCTAssertEqual(result.validRules, 3)
        XCTAssertEqual(result.invalidRules, 1)
        XCTAssertEqual(result.messages, ["bad rule"])
    }

    func testValidateOutputFailsClosedWhenValidatorMissing() {
        // With no `bloqr-validate` on PATH (as in this sandboxed test environment), the
        // default (fail-closed) behavior must refuse to treat the file as validated.
        let reason = RulesValidator.validateOutput(
            path: URL(fileURLWithPath: "/nonexistent/output.txt"),
            allowUnvalidated: false,
            failOnWarnings: false
        )
        XCTAssertNotNil(reason)
    }

    func testValidateOutputAllowsUnvalidatedOptOut() {
        let reason = RulesValidator.validateOutput(
            path: URL(fileURLWithPath: "/nonexistent/output.txt"),
            allowUnvalidated: true,
            failOnWarnings: false
        )
        XCTAssertNil(reason)
    }
}

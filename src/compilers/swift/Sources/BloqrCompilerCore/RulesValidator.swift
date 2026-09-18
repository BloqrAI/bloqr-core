import Foundation

/// Mirrors `bloqr-validate file <path> --json`'s stdout shape
/// (`bloqr-validator-core`'s `SyntaxValidationResult`, snake_case-serialized).
struct SyntaxValidationResult: Decodable {
    let isValid: Bool
    let validRules: Int
    let invalidRules: Int
    let messages: [String]

    enum CodingKeys: String, CodingKey {
        case isValid = "is_valid"
        case validRules = "valid_rules"
        case invalidRules = "invalid_rules"
        case messages
    }
}

/// Runs the mandatory `bloqr-validate` (from `bloqr-validator-core-cli`) syntax check on
/// compiled output, mirroring the other wrappers' fail-closed-by-default behavior (see
/// `validate_output_with_events` in `src/compilers/rust/core/src/compiler.rs`): any
/// error-level finding aborts compilation, and so does a validator run failure - a validator
/// that couldn't be run tells us nothing about the output's safety, so it is never treated as
/// "no findings" unless `allowUnvalidated` is set.
enum RulesValidator {
    /// Returns `nil` if compilation should proceed, or an abort reason if it should not.
    static func validateOutput(path: URL, allowUnvalidated: Bool, failOnWarnings: Bool) -> String? {
        guard let validate = findCommand("bloqr-validate") else {
            return allowUnvalidated ? nil : (
                "bloqr-validate could not be found on PATH (pass --allow-unvalidated-output to " +
                "bypass this check; not recommended)"
            )
        }

        guard let output = try? runProcess(
            command: validate,
            arguments: ["file", path.path, "--json"],
            currentDirectory: nil
        ) else {
            return allowUnvalidated ? nil : (
                "bloqr-validate could not run against \(path.path) (pass " +
                "--allow-unvalidated-output to bypass this check; not recommended)"
            )
        }

        guard let data = output.stdout.data(using: .utf8),
              let result = try? JSONDecoder().decode(SyntaxValidationResult.self, from: data) else {
            return allowUnvalidated ? nil : (
                "bloqr-validate produced unparseable output for \(path.path) (pass " +
                "--allow-unvalidated-output to bypass this check; not recommended)"
            )
        }

        if allowUnvalidated { return nil }

        if !result.isValid {
            if result.messages.isEmpty {
                return "Output file failed rules-validator syntax validation " +
                    "(\(result.invalidRules) invalid rule(s) of \(result.validRules + result.invalidRules))."
            }
            return "rules-validator syntax validation failed for \(path.path): " +
                result.messages.joined(separator: "; ")
        }

        if failOnWarnings && !result.messages.isEmpty {
            return "rules-validator reported warnings for \(path.path) (--fail-on-warnings set): " +
                result.messages.joined(separator: "; ")
        }

        return nil
    }
}

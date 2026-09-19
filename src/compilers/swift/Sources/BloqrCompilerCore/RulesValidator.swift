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

/// Mirrors `bloqr-validate`'s `--json` error envelope (`{"error": "..."}`), emitted instead of
/// a `SyntaxValidationResult` when the validator itself couldn't run against the file (see
/// `src/validation/cli/src/main.rs`'s `JsonError`).
private struct ValidatorJSONError: Decodable {
    let error: String
}

/// Runs the mandatory `bloqr-validate` (from `bloqr-validator-core-cli`) syntax check on
/// compiled output, mirroring the other wrappers' fail-closed-by-default behavior (see
/// `validate_output_with_events` in `src/compilers/rust/core/src/compiler.rs`): any
/// error-level finding aborts compilation, and so does a validator run failure - a validator
/// that couldn't be run tells us nothing about the output's safety, so it is never treated as
/// "no findings" unless `allowUnvalidated` is set.
enum RulesValidator {
    /// Returns `nil` if compilation should proceed, or an abort reason if it should not.
    ///
    /// - Parameter engine: the syntax grammar to validate against - `nil`/`"dns"` for the
    ///   primary DNS/hosts-style output (the CLI's own default), `"browser"` for the
    ///   browser-syntax artifact (which contains cosmetic/extended-CSS rules the DNS grammar
    ///   would reject as invalid).
    static func validateOutput(
        path: URL,
        allowUnvalidated: Bool,
        failOnWarnings: Bool,
        engine: String? = nil
    ) -> String? {
        guard let validate = findCommand("bloqr-validate") else {
            return allowUnvalidated ? nil : (
                "bloqr-validate could not be found on PATH (pass --allow-unvalidated-output to " +
                "bypass this check; not recommended)"
            )
        }

        // An isolated, per-invocation hash-db path: `bloqr-validate file` defaults
        // `--hash-db` to `data/input/.hashes.json` relative to the *caller's* working
        // directory and writes to it after every check, which would otherwise pollute an
        // unrelated project directory (or fail outright in a read-only one) purely as a
        // side effect of this wrapper's own mandatory syntax check.
        let hashDbPath = FileManager.default.temporaryDirectory
            .appendingPathComponent("bloqr-validate-hashes-\(UUID().uuidString).json")
        defer { try? FileManager.default.removeItem(at: hashDbPath) }

        var arguments = ["file", path.path, "--json", "--hash-db", hashDbPath.path]
        if let engine, engine.lowercased() == "browser" {
            arguments.append(contentsOf: ["--engine", "browser"])
        }

        guard let output = try? runProcess(command: validate, arguments: arguments, currentDirectory: nil) else {
            return allowUnvalidated ? nil : (
                "bloqr-validate could not run against \(path.path) (pass " +
                "--allow-unvalidated-output to bypass this check; not recommended)"
            )
        }

        guard let data = output.stdout.data(using: .utf8) else {
            return allowUnvalidated ? nil : (
                "bloqr-validate produced unparseable output for \(path.path) (pass " +
                "--allow-unvalidated-output to bypass this check; not recommended)"
            )
        }

        guard let result = try? JSONDecoder().decode(SyntaxValidationResult.self, from: data) else {
            // Not a SyntaxValidationResult - check whether it's the CLI's `{"error": "..."}`
            // envelope (emitted when the validator couldn't run against the file at all, as
            // opposed to running and finding it invalid) so the real cause isn't discarded.
            if let jsonError = try? JSONDecoder().decode(ValidatorJSONError.self, from: data) {
                return allowUnvalidated ? nil : (
                    "bloqr-validate could not validate \(path.path): \(jsonError.error) (pass " +
                    "--allow-unvalidated-output to bypass this check; not recommended)"
                )
            }
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

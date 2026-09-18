import Foundation

/// Options controlling a compilation run, mirroring the other wrappers' `CompileOptions`.
public struct CompileOptions: Sendable {
    public var outputPath: URL?
    public var copyToRules: Bool
    public var rulesDirectory: URL?
    public var format: ConfigFormat?
    public var debug: Bool
    public var validate: Bool
    public var failOnWarnings: Bool
    public var allowUnvalidatedOutput: Bool
    public var engine: String?
    public var browserOutputPath: URL?

    public init(
        outputPath: URL? = nil,
        copyToRules: Bool = false,
        rulesDirectory: URL? = nil,
        format: ConfigFormat? = nil,
        debug: Bool = false,
        validate: Bool = false,
        failOnWarnings: Bool = false,
        allowUnvalidatedOutput: Bool = false,
        engine: String? = nil,
        browserOutputPath: URL? = nil
    ) {
        self.outputPath = outputPath
        self.copyToRules = copyToRules
        self.rulesDirectory = rulesDirectory
        self.format = format
        self.debug = debug
        self.validate = validate
        self.failOnWarnings = failOnWarnings
        self.allowUnvalidatedOutput = allowUnvalidatedOutput
        self.engine = engine
        self.browserOutputPath = browserOutputPath
    }
}

/// Result of a compilation operation, mirroring the other wrappers' `CompilerResult`.
public struct CompilerResult: Sendable {
    public var success: Bool = false
    public var configName: String = ""
    public var configVersion: String = ""
    public var ruleCount: Int = 0
    public var outputPath: URL = URL(fileURLWithPath: "")
    public var outputHash: String = ""
    public var copiedToRules: Bool = false
    public var rulesDestination: URL?
    public var browserOutputPath: URL?
    public var browserRuleCount: Int?
    public var browserOutputHash: String?
    public var errorMessage: String?
    public var stdout: String = ""
    public var stderr: String = ""
    public var startTime: Date = Date()
    public var endTime: Date = Date()
    public var elapsedMs: UInt64 = 0

    public init() {}

    public var hashShort: String { String(outputHash.prefix(8)) }

    public var elapsedFormatted: String {
        if elapsedMs < 1000 { return "\(elapsedMs)ms" }
        return String(format: "%.2fs", Double(elapsedMs) / 1000.0)
    }
}

/**
 * Interactive Console Application for Rules Compiler
 * Provides menu-driven interface matching .NET version functionality
 */

import { confirm, input, select } from '@inquirer/prompts';
import {
  chalk,
  createKeyValueTable,
  createTable,
  dim,
  displayTable,
  formatElapsed,
  showError,
  showInfo,
  showRule,
  showSuccess,
  showWelcomeBanner,
  truncate,
  withSpinner,
} from './utils.ts';
import { findDefaultConfig, readConfiguration, toJson } from '../orchestration/config-reader.ts';
import { runCompiler } from '../orchestration/compiler.ts';
import { validateConfiguration } from '../orchestration/validation.ts';
import { createLogger } from '../orchestration/logger.ts';
import { getVersionInfo } from '../orchestration/cli.ts';
import type {
  CompilerResult,
  ConfigurationFormat,
  ExtendedConfiguration,
  Logger,
} from '../orchestration/types.ts';

/**
 * Available transformations with descriptions
 */
const TRANSFORMATIONS = [
  { name: 'RemoveComments', description: 'Removes all comment lines (! or #)' },
  { name: 'Compress', description: 'Converts hosts format to adblock syntax' },
  { name: 'RemoveModifiers', description: 'Removes unsupported modifiers from rules' },
  { name: 'Validate', description: 'Removes dangerous/incompatible rules' },
  { name: 'ValidateAllowIp', description: 'Like Validate but allows IP address rules' },
  { name: 'Deduplicate', description: 'Removes duplicate rules' },
  { name: 'InvertAllow', description: 'Converts @@exceptions to blocking rules' },
  { name: 'RemoveEmptyLines', description: 'Removes blank lines' },
  { name: 'TrimLines', description: 'Trims whitespace from lines' },
  { name: 'InsertFinalNewLine', description: 'Ensures file ends with newline' },
  { name: 'ConvertToAscii', description: 'Converts IDN to punycode' },
];

/**
 * Menu choices for the interactive mode
 */
const MENU_CHOICES = [
  { name: 'View Configuration', value: 'view-config' },
  { name: 'Validate Configuration', value: 'validate-config' },
  { name: 'Compile Rules', value: 'compile' },
  { name: 'Compile Rules (Verbose)', value: 'compile-verbose' },
  { name: 'Compile and Copy to Rules', value: 'compile-copy' },
  { name: 'Show Available Transformations', value: 'transformations' },
  { name: 'Version Info', value: 'version' },
  { name: 'Exit', value: 'exit' },
];

/**
 * Console application options
 */
export interface ConsoleAppOptions {
  /** Default configuration path */
  configPath?: string;
  /** Force configuration format */
  format?: ConfigurationFormat;
  /** Enable debug logging */
  debug?: boolean;
}

/**
 * Interactive Console Application
 */
export class ConsoleApplication {
  private readonly logger: Logger;
  private readonly options: ConsoleAppOptions;
  private configPath?: string;

  /**
   * Creates a new `ConsoleApplication`.
   * @param options Initial configuration path, format override, and debug logging flag.
   */
  constructor(options: ConsoleAppOptions = {}) {
    this.options = options;
    this.configPath = options.configPath;
    this.logger = createLogger(options.debug ?? false);
  }

  /**
   * Run the console application
   */
  async run(): Promise<number> {
    showWelcomeBanner();

    let running = true;
    let exitCode = 0;

    while (running) {
      try {
        const choice = await select({
          message: chalk.green('What would you like to do?'),
          choices: MENU_CHOICES,
          pageSize: 12,
        });

        console.log();

        switch (choice) {
          case 'view-config':
            await this.viewConfiguration();
            break;
          case 'validate-config':
            await this.validateConfigurationInteractive();
            break;
          case 'compile':
            await this.compileRules(false, false);
            break;
          case 'compile-verbose':
            await this.compileRules(false, true);
            break;
          case 'compile-copy':
            await this.compileRules(true, false);
            break;
          case 'transformations':
            this.showTransformations();
            break;
          case 'version':
            this.showVersionInfo();
            break;
          case 'exit':
            running = false;
            break;
        }
      } catch (error) {
        if (error instanceof Error && error.message.includes('User force closed')) {
          running = false;
        } else {
          showError(error instanceof Error ? error.message : String(error));
          exitCode = 1;
        }
      }

      if (running) {
        console.log();
        await this.waitForEnter();
        console.log();
      }
    }

    showInfo('Goodbye!');
    return exitCode;
  }

  /**
   * Prompt for configuration path
   */
  private async promptForConfigPath(): Promise<string | undefined> {
    if (this.configPath) {
      const useDefault = await confirm({
        message: `Use config: ${this.configPath}?`,
        default: true,
      });
      if (useDefault) {
        return this.configPath;
      }
    }

    const defaultConfig = findDefaultConfig();
    const defaultHint = defaultConfig ? ` (default: ${defaultConfig})` : '';

    const configPath = await input({
      message: `Enter config path${defaultHint}:`,
      default: defaultConfig || undefined,
    });

    return configPath || defaultConfig || undefined;
  }

  /**
   * View configuration details
   */
  private async viewConfiguration(): Promise<void> {
    const configPath = await this.promptForConfigPath();

    if (!configPath) {
      showError('No configuration file specified');
      return;
    }

    await withSpinner('Reading configuration...', () => {
      const config = readConfiguration(configPath, this.options.format, this.logger);
      this.displayConfiguration(config);
      return Promise.resolve();
    });
  }

  /**
   * Display configuration in a table
   */
  private displayConfiguration(config: ExtendedConfiguration): void {
    const table = createKeyValueTable();

    const configRecord = config as unknown as Record<string, unknown>;

    table.push(['Name', config.name || dim('Not set')]);
    table.push(['Description', String(configRecord['description'] ?? dim('Not set'))]);
    table.push(['Version', String(configRecord['version'] ?? dim('Not set'))]);
    table.push(['License', String(configRecord['license'] ?? dim('Not set'))]);
    table.push(['Homepage', truncate(String(configRecord['homepage'] ?? dim('Not set')), 50)]);
    table.push(['Sources', String(config.sources?.length ?? 0)]);

    const transformations = configRecord['transformations'];
    table.push([
      'Transformations',
      Array.isArray(transformations) && transformations.length > 0
        ? transformations.join(', ')
        : dim('None'),
    ]);

    const inclusions = configRecord['inclusions'];
    table.push([
      'Inclusions',
      Array.isArray(inclusions) && inclusions.length > 0
        ? `${inclusions.length} patterns`
        : dim('None'),
    ]);

    const exclusions = configRecord['exclusions'];
    table.push([
      'Exclusions',
      Array.isArray(exclusions) && exclusions.length > 0
        ? `${exclusions.length} patterns`
        : dim('None'),
    ]);

    displayTable(table);

    // Display sources table
    if (config.sources && config.sources.length > 0) {
      console.log();
      console.log(chalk.green('Sources:'));

      const sourcesTable = createTable(['Name', 'Type', 'Source', 'Transformations']);

      for (const source of config.sources) {
        const sourceRecord = source as unknown as Record<string, unknown>;
        sourcesTable.push([
          String(sourceRecord['name'] ?? dim('[unnamed]')),
          String(sourceRecord['type'] ?? 'adblock'),
          truncate(String(source.source || ''), 40),
          Array.isArray(sourceRecord['transformations']) &&
            sourceRecord['transformations'].length > 0
            ? sourceRecord['transformations'].join(', ')
            : dim('None'),
        ]);
      }

      displayTable(sourcesTable);
    }

    // Show JSON representation
    console.log();
    console.log(chalk.green('JSON Representation:'));
    console.log(dim(toJson(config)));
  }

  /**
   * Validate configuration
   */
  private async validateConfigurationInteractive(): Promise<void> {
    const configPath = await this.promptForConfigPath();

    if (!configPath) {
      showError('No configuration file specified');
      return;
    }

    await withSpinner('Validating configuration...', () => {
      const config = readConfiguration(configPath, this.options.format, this.logger);
      const result = validateConfiguration(config);
      this.displayValidationResult(result);
      return Promise.resolve();
    });
  }

  /**
   * Display validation result
   */
  private displayValidationResult(
    result: { valid: boolean; errors: string[]; warnings: string[] },
  ): void {
    console.log();

    if (result.valid && result.warnings.length === 0) {
      showSuccess('Configuration is valid!');
      return;
    }

    if (result.valid) {
      console.log(chalk.yellow('Configuration is valid but has warnings:'));
    } else {
      console.log(chalk.red('Configuration has errors:'));
    }

    if (result.errors.length > 0) {
      const errorsTable = createTable(['Field', 'Error']);
      for (const error of result.errors) {
        const [field, ...messageParts] = error.split(':');
        errorsTable.push([chalk.red(field), messageParts.join(':').trim()]);
      }
      displayTable(errorsTable);
    }

    if (result.warnings.length > 0) {
      console.log();
      const warningsTable = createTable(['Field', 'Warning']);
      for (const warning of result.warnings) {
        const [field, ...messageParts] = warning.split(':');
        warningsTable.push([chalk.yellow(field), messageParts.join(':').trim()]);
      }
      displayTable(warningsTable);
    }
  }

  /**
   * Compile rules
   */
  private async compileRules(copyToRules: boolean, verbose: boolean): Promise<void> {
    const configPath = await this.promptForConfigPath();

    if (!configPath) {
      showError('No configuration file specified');
      return;
    }

    const result = await withSpinner('Compiling rules...', async () => {
      return await runCompiler({
        configPath,
        copyToRules,
        format: this.options.format,
        logger: verbose ? this.logger : createLogger(false),
      });
    });

    this.displayResult(result);
  }

  /**
   * Display compilation result
   */
  private displayResult(result: CompilerResult): void {
    console.log();

    if (result.success) {
      showSuccess('Compilation successful!');
    } else {
      showError('Compilation failed!');
      if (result.errorMessage) {
        console.log(chalk.red(`Error: ${result.errorMessage}`));
      }
      return;
    }

    const table = createKeyValueTable();

    table.push(['Config Name', result.configName]);
    table.push(['Config Version', result.configVersion || dim('Not set')]);
    table.push(['Rule Count', result.ruleCount.toLocaleString()]);
    table.push(['Output Path', truncate(result.outputPath, 60)]);
    table.push([
      'Output Hash',
      result.outputHash?.length >= 32
        ? result.outputHash.substring(0, 32) + '...'
        : result.outputHash || dim('N/A'),
    ]);
    table.push(['Elapsed Time', formatElapsed(result.elapsedMs)]);

    if (result.copiedToRules) {
      table.push(['Copied To', truncate(result.rulesDestination || 'N/A', 60)]);
    }

    displayTable(table);
  }

  /**
   * Show available transformations
   */
  private showTransformations(): void {
    showRule('Available Transformations');
    console.log();

    const table = createTable(['Transformation', 'Description']);

    for (const t of TRANSFORMATIONS) {
      table.push([chalk.green(t.name), t.description]);
    }

    displayTable(table);

    console.log();
    console.log(
      dim(
        'Note: Transformations are always applied in a fixed order regardless of configuration order.',
      ),
    );
  }

  /**
   * Show version information
   */
  private showVersionInfo(): void {
    const info = getVersionInfo();

    const table = createKeyValueTable();

    table.push(['Module', info.moduleVersion]);
    table.push(['Runtime', info.nodeVersion]);
    table.push(['Platform', `${info.platform.os} ${info.platform.arch}`]);
    table.push([
      'TypeScript',
      (Deno as unknown as { version: { typescript: string } }).version.typescript,
    ]);
    table.push(['V8', (Deno as unknown as { version: { v8: string } }).version.v8]);

    displayTable(table);
  }

  /**
   * Wait for user to press Enter
   */
  private async waitForEnter(): Promise<void> {
    await input({
      message: 'Press Enter to continue...',
    });
  }
}

/**
 * Run interactive console application
 */
export async function runInteractive(options: ConsoleAppOptions = {}): Promise<number> {
  const app = new ConsoleApplication(options);
  return await app.run();
}

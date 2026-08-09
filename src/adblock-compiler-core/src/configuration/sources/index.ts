/**
 * Pluggable configuration source abstraction.
 *
 * Each `IConfigurationSource` produces a `Partial<IConfiguration>` that is
 * merged in order by `ConfigurationManager`.  The merge strategy is:
 * - Scalars: last source wins
 * - Arrays: last source fully replaces (no append)
 *
 * ## Precedence (lowest → highest)
 * 1. `ObjectConfigurationSource` / `FileConfigurationSource`  (base)
 * 2. `EnvConfigurationSource`                                  (applied by manager)
 * 3. Any additional sources appended to the pipeline           (highest)
 *
 * @module
 */

import process from 'node:process';
import type { IConfiguration, IFileSystem } from '../../types/index.ts';
import { ConfigurationLoader } from '../ConfigurationLoader.ts';

// ── Interface ─────────────────────────────────────────────────────────────────

/**
 * A single configuration layer.  Implementations represent different origins
 * (file on disk, plain object, environment variables, CLI flags, etc.).
 */
export interface IConfigurationSource {
  /** Identifies the origin for diagnostics and logging. */
  readonly sourceType: string;
  /**
   * Asynchronously produce a partial configuration.
   * Keys absent from the return value are not merged (they do not override).
   */
  load(): Promise<Partial<IConfiguration>>;
}

// ── FileConfigurationSource ───────────────────────────────────────────────────

/**
 * Loads a JSON configuration file from the file system.
 *
 * @example
 * ```ts
 * const src = new FileConfigurationSource('./blocklist.json');
 * ```
 */
export class FileConfigurationSource implements IConfigurationSource {
  readonly sourceType = 'file';
  private readonly loader: ConfigurationLoader;

  constructor(private readonly path: string, fileSystem?: IFileSystem) {
    this.loader = new ConfigurationLoader(fileSystem);
  }

  async load(): Promise<Partial<IConfiguration>> {
    return await this.loader.loadFromFile(this.path);
  }
}

// ── ObjectConfigurationSource ─────────────────────────────────────────────────

/**
 * Wraps a plain JavaScript/TypeScript object as a configuration source.
 * Useful for programmatic and test scenarios.
 *
 * @example
 * ```ts
 * const src = new ObjectConfigurationSource({ name: 'My list', sources: [] });
 * ```
 */
export class ObjectConfigurationSource implements IConfigurationSource {
  readonly sourceType = 'object';

  constructor(private readonly config: Partial<IConfiguration>) {}

  async load(): Promise<Partial<IConfiguration>> {
    return this.config;
  }
}

// ── EnvConfigurationSource ────────────────────────────────────────────────────

/**
 * Reads `ADBLOCK_CONFIG_*` environment variables and maps them to scalar
 * configuration fields.  Array fields (sources, transformations, exclusions)
 * are not overridden via environment variables — use `ObjectConfigurationSource`
 * with an inline object for that.
 *
 * | Variable                      | Maps to                  |
 * |-------------------------------|--------------------------|
 * | `ADBLOCK_CONFIG_NAME`         | `name`                   |
 * | `ADBLOCK_CONFIG_DESCRIPTION`  | `description`            |
 * | `ADBLOCK_CONFIG_HOMEPAGE`     | `homepage`               |
 * | `ADBLOCK_CONFIG_LICENSE`      | `license`                |
 * | `ADBLOCK_CONFIG_VERSION`      | `version`                |
 *
 * @param envReader  - Optional injected reader for testability (defaults to
 *                     `Deno.env.get` under Deno, `process.env` under
 *                     Node.js/Bun).
 *
 * @example
 * ```ts
 * // Testable:
 * const src = new EnvConfigurationSource((k) => ({ ADBLOCK_CONFIG_NAME: 'Test' }[k]));
 * ```
 */
export class EnvConfigurationSource implements IConfigurationSource {
  readonly sourceType = 'env';
  private readonly envReader: (key: string) => string | undefined;

  constructor(envReader?: (key: string) => string | undefined) {
    this.envReader = envReader ??
      ((key) => (typeof Deno !== 'undefined' && Deno.env ? Deno.env.get(key) : process.env[key]));
  }

  async load(): Promise<Partial<IConfiguration>> {
    const config: Partial<IConfiguration> = {};

    const name = this.envReader('ADBLOCK_CONFIG_NAME');
    if (name) config.name = name;

    const description = this.envReader('ADBLOCK_CONFIG_DESCRIPTION');
    if (description) config.description = description;

    const homepage = this.envReader('ADBLOCK_CONFIG_HOMEPAGE');
    if (homepage) config.homepage = homepage;

    const license = this.envReader('ADBLOCK_CONFIG_LICENSE');
    if (license) config.license = license;

    const version = this.envReader('ADBLOCK_CONFIG_VERSION');
    if (version) config.version = version;

    return config;
  }
}

// ── CliConfigurationSource ────────────────────────────────────────────────────

/**
 * Builds a minimal `IConfiguration` from `--input` CLI flags.
 * Used internally by `ConfigurationManager.fromCliArgs()`.
 *
 * @example
 * ```ts
 * const src = new CliConfigurationSource(['https://example.com/hosts.txt'], 'hosts');
 * ```
 */
export class CliConfigurationSource implements IConfigurationSource {
  readonly sourceType = 'cli';

  constructor(
    private readonly inputs: string[],
    private readonly inputType: string = 'hosts',
  ) {}

  async load(): Promise<Partial<IConfiguration>> {
    const loader = new ConfigurationLoader();
    return loader.createFromInputs(this.inputs, this.inputType) as Partial<IConfiguration>;
  }
}

// ── OverrideConfigurationSource ───────────────────────────────────────────────

/**
 * Parses an inline JSON string and applies it as the highest-priority layer.
 * Typically wired to the `--override` CLI flag.
 *
 * @example
 * ```ts
 * const src = new OverrideConfigurationSource('{"name":"CI Build"}');
 * ```
 */
export class OverrideConfigurationSource implements IConfigurationSource {
  readonly sourceType = 'override';
  private readonly parsed: Partial<IConfiguration>;

  constructor(json: string) {
    let parsed: unknown;
    try {
      parsed = JSON.parse(json);
    } catch {
      throw new Error(`--override value is not valid JSON: ${json}`);
    }

    if (parsed === null || Array.isArray(parsed) || typeof parsed !== 'object') {
      throw new Error(
        `--override JSON must be an object (e.g. {"key":"value"}), but received: ${json}`,
      );
    }

    this.parsed = parsed as Partial<IConfiguration>;
  }

  async load(): Promise<Partial<IConfiguration>> {
    return this.parsed;
  }
}

// Re-export all source types together for convenience.
export type { IConfiguration } from '../../types/index.ts';
export type { IFileSystem, SourceType, TransformationType } from '../../types/index.ts';

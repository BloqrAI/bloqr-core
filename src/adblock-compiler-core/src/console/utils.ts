/**
 * Console utility functions for interactive mode
 * Provides styled output, spinners, tables, and prompts
 */

import {
  bgBlue,
  blue,
  bold,
  cyan,
  dim,
  gray,
  green,
  red,
  underline,
  white,
  yellow,
} from '@std/fmt/colors';
import Table from 'cli-table3';
import ora from 'ora';
import figlet from 'figlet';

/** Format a value for display */
export function formatValue(value: unknown): string {
  if (value === null || value === undefined) {
    return dim('N/A');
  }
  if (typeof value === 'boolean') {
    return value ? green('Yes') : red('No');
  }
  if (typeof value === 'number') {
    return cyan(value.toLocaleString());
  }
  if (Array.isArray(value)) {
    return value.length > 0 ? value.join(', ') : dim('None');
  }
  return String(value);
}

/** Create a standard table with headers */
export function createTable(headers: string[]): Table.Table {
  return new Table({
    head: headers.map((h) => bold(white(h))),
    style: {
      head: [],
      border: [],
    },
  });
}

/** Create a key-value table (2 columns) */
export function createKeyValueTable(): Table.Table {
  return new Table({
    head: [bold(green('Property')), bold(green('Value'))],
    style: {
      head: [],
      border: [],
    },
  });
}

/** Display a table */
export function displayTable(table: Table.Table): void {
  console.log(table.toString());
}

/** Display a success message */
export function showSuccess(message: string): void {
  console.log(green('✓ ' + message));
}

/** Display an error message */
export function showError(message: string): void {
  console.log(red('✗ ' + message));
}

/** Display a warning message */
export function showWarning(message: string): void {
  console.log(yellow('⚠ ' + message));
}

/** Display an info message */
export function showInfo(message: string): void {
  console.log(blue('ℹ ' + message));
}

/** Display a header */
export function showHeader(title: string): void {
  console.log();
  console.log(bold(underline(title)));
  console.log();
}

/** Display a panel with key-value pairs */
export function showPanel(title: string, data: Record<string, unknown>): void {
  console.log();
  console.log(bold(bgBlue(white(` ${title} `))));
  console.log();
  for (const [key, value] of Object.entries(data)) {
    console.log(`  ${bold(key)}: ${formatValue(value)}`);
  }
  console.log();
}

/** Display a rule/separator line */
export function showRule(title?: string): void {
  const width = 60;
  if (title) {
    const padding = Math.max(0, Math.floor((width - title.length - 2) / 2));
    const line = '─'.repeat(padding);
    console.log(gray(`${line} ${green(title)} ${line}`));
  } else {
    console.log(gray('─'.repeat(width)));
  }
}

/** Display no items message */
export function showNoItems(type: string): void {
  console.log(dim(`  No ${type} found.`));
}

/** Create a spinner */
export function createSpinner(text: string): import('ora').Ora {
  return ora({
    text,
    spinner: 'dots',
  });
}

/** Execute with spinner */
export async function withSpinner<T>(text: string, action: () => Promise<T>): Promise<T> {
  const spinner = createSpinner(text);
  spinner.start();
  try {
    const result = await action();
    spinner.succeed();
    return result;
  } catch (error) {
    spinner.fail();
    throw error;
  }
}

/** Generate ASCII art banner */
export function generateBanner(text: string): string {
  try {
    return figlet.textSync(text, {
      font: 'Small',
      horizontalLayout: 'default',
      verticalLayout: 'default',
    });
  } catch {
    // Fallback if figlet fails
    return text;
  }
}

/** Display welcome banner */
export function showWelcomeBanner(): void {
  console.clear();
  console.log(green(generateBanner('Rules Compiler')));
  showRule('AdGuard Filter Rules Compiler');
  console.log();
}

/** Truncate string to max length */
export function truncate(str: string, maxLength: number): string {
  if (str.length <= maxLength) return str;
  return str.substring(0, maxLength - 3) + '...';
}

/** Format elapsed time */
export function formatElapsed(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  return `${(ms / 60000).toFixed(1)}m`;
}

/** Format file size */
export function formatBytes(bytes: number): string {
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let unitIndex = 0;
  let value = bytes;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex++;
  }
  return `${value.toFixed(1)} ${units[unitIndex]}`;
}

/** Color text based on status */
export function colorStatus(
  status: 'success' | 'error' | 'warning' | 'info',
  text: string,
): string {
  switch (status) {
    case 'success':
      return green(text);
    case 'error':
      return red(text);
    case 'warning':
      return yellow(text);
    case 'info':
      return blue(text);
    default:
      return text;
  }
}

/** Export dim/bold for direct use (also used internally above) */
export { bold, dim };

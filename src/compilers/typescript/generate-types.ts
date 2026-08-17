#!/usr/bin/env -S deno run --allow-read --allow-write
/**
 * Generate TypeScript declaration files (.d.ts)
 * 
 * This script creates .d.ts files that re-export from the original .ts files.
 * Since Deno uses TypeScript natively, the .ts files serve as the source of truth.
 */

async function* walkDir(dir: string): AsyncGenerator<string> {
  for await (const entry of Deno.readDir(dir)) {
    const path = `${dir}/${entry.name}`;
    if (entry.isDirectory && !entry.name.startsWith('.') && entry.name !== 'node_modules' && entry.name !== 'coverage') {
      yield* walkDir(path);
    } else if (entry.isFile && entry.name.endsWith('.ts') && !entry.name.endsWith('.test.ts')) {
      yield path;
    }
  }
}

async function ensureDir(path: string) {
  try {
    await Deno.mkdir(path, { recursive: true });
  } catch (err) {
    if (!(err instanceof Deno.errors.AlreadyExists)) {
      throw err;
    }
  }
}

function dirname(path: string): string {
  const parts = path.split('/');
  parts.pop();
  return parts.join('/');
}

/**
 * Calculate relative path from 'from' directory to 'to' file
 * Note: This is simplified for the specific use case where:
 * - from = src directory
 * - to = file within src directory
 * Result will always be a forward path (no ../)
 */
function relative(from: string, to: string): string {
  const fromParts = from.split('/');
  const toParts = to.split('/');
  
  // Remove common prefix
  while (fromParts.length && toParts.length && fromParts[0] === toParts[0]) {
    fromParts.shift();
    toParts.shift();
  }
  
  return toParts.join('/');
}

const projectRoot = Deno.cwd();
const srcDir = `${projectRoot}/src`;
const distDir = `${projectRoot}/dist`;

console.log("🔨 Generating TypeScript declaration files...\n");
console.log(`📁 Source: ${srcDir}`);
console.log(`📁 Output: ${distDir}\n`);

// Clean dist directory
try {
  await Deno.remove(distDir, { recursive: true });
} catch {
  // Directory might not exist
}

await ensureDir(distDir);

let fileCount = 0;

// Process all TypeScript files
for await (const filePath of walkDir(srcDir)) {
  const relativePath = relative(srcDir, filePath);
  const outputPath = `${distDir}/${relativePath.replace(/\.ts$/, '.d.ts')}`;
  
  // Create declaration file content
  const dtsContent = `/**
 * Type definitions for ${relativePath.replace(/\.ts$/, '')}
 * AUTO-GENERATED - do not edit manually
 * 
 * This file re-exports types from the TypeScript source file.
 * For Deno projects, the .ts files themselves serve as type definitions.
 */

export * from '../src/${relativePath}';
`;
  
  // Ensure output directory exists
  const outputDir = dirname(outputPath);
  await ensureDir(outputDir);
  
  // Write the declaration file
  await Deno.writeTextFile(outputPath, dtsContent);
  
  fileCount++;
  console.log(`✓ ${relativePath.replace(/\.ts$/, '.d.ts')}`);
}

console.log(`\n✅ Generated ${fileCount} declaration files successfully!`);
console.log(`📁 Output: ${distDir}\n`);

#!/usr/bin/env -S deno run --allow-read --allow-run

/**
 * Enforce near-complete JSDoc coverage on @bloqr/compiler-core's public API.
 *
 * `deno doc --lint` (wired into `deno task check:slow-types` and every CI
 * run) only flags top-level exported symbols with zero JSDoc at all — it
 * does not look at exported enum members, or at public properties/methods
 * of exported interfaces and classes. JSR's own package-score "Has docs for
 * most symbols" check does look at that finer granularity, so a change that
 * passes `deno doc --lint` cleanly can still drop the JSR score (this
 * happened in practice: PR #310 added several undocumented enum members and
 * the score fell from ~88% to 61%).
 *
 * This script reproduces JSR's finer-grained view by walking `deno doc
 * --json` output for **every published `.ts` file** — not just the six
 * `exports`-map entrypoints. That distinction matters: `deno.json`'s
 * `publish.include` (every `.ts` file under `src/`) ships several files
 * that aren't transitively re-exported from any of the six entrypoints (e.g.
 * `src/utils/ErrorUtils.ts`'s `ErrorCode` enum, `src/types/websocket.ts`),
 * so a scan scoped only to entrypoint-reachable symbols — this script's
 * first version — undercounts real gaps JSR still scores against. Checking
 * JSDoc presence on:
 *   - every top-level exported symbol in every published file
 *   - every public (non-private/protected) property and method of an
 *     exported interface or class
 *   - every member of an exported enum
 *   - every property of an exported type alias that resolves to an inline
 *     object type literal
 *
 * `kind: "reference"` declarations (e.g. `export default someFunction`,
 * which deno_doc resolves to the original declaration rather than the
 * re-export site) are skipped — their documentation lives at the
 * referenced declaration, which is already checked wherever it's declared.
 *
 * Usage:
 *   deno task lint:docs
 *   deno run --allow-read --allow-run scripts/check-symbol-docs.ts [--threshold=98]
 */

interface DocLocation {
  filename: string;
  line: number;
  col: number;
}

interface DocMember {
  name: string;
  jsDoc?: unknown;
  location: DocLocation;
  accessibility?: 'public' | 'private' | 'protected';
}

interface DocDeclaration {
  declarationKind: string;
  kind: string;
  jsDoc?: unknown;
  location: DocLocation;
  def?: {
    properties?: DocMember[];
    methods?: DocMember[];
    members?: DocMember[];
    tsType?: {
      kind?: string;
      value?: { properties?: DocMember[] };
    };
  };
}

interface DocSymbol {
  name: string;
  declarations: DocDeclaration[];
}

interface DocJson {
  nodes: Record<string, { symbols: DocSymbol[] }>;
}

interface Gap {
  file: string;
  line: number;
  name: string;
}

/**
 * Every published `.ts` source file (mirrors `publish.include`/`exclude` in
 * deno.json: everything under `src/`, minus `*.test.ts`/`*.bench.ts`), not
 * just the `exports`-map entrypoints — see the module doc comment for why
 * that distinction matters here.
 */
async function getPublishedFiles(): Promise<string[]> {
  const files: string[] = [];
  async function walk(dir: string): Promise<void> {
    for await (const entry of Deno.readDir(dir)) {
      const path = `${dir}/${entry.name}`;
      if (entry.isDirectory) {
        await walk(path);
      } else if (
        entry.isFile && path.endsWith('.ts') && !path.endsWith('.test.ts') &&
        !path.endsWith('.bench.ts')
      ) {
        files.push(path);
      }
    }
  }
  await walk('src');
  if (files.length === 0) {
    throw new Error('Found zero published .ts files under src/');
  }
  return files;
}

async function docJson(entrypoint: string): Promise<DocJson> {
  const command = new Deno.Command(Deno.execPath(), {
    args: ['doc', '--json', entrypoint],
    stdout: 'piped',
    stderr: 'inherit',
  });
  const { success, stdout } = await command.output();
  if (!success) {
    throw new Error(`deno doc --json ${entrypoint} failed`);
  }
  return JSON.parse(new TextDecoder().decode(stdout));
}

function checkMembers(
  ownerName: string,
  members: DocMember[] | undefined,
  gaps: Gap[],
  seen: Set<string>,
  total: { count: number },
): void {
  for (const member of members ?? []) {
    if (member.accessibility === 'private' || member.accessibility === 'protected') {
      continue;
    }
    const key = `${member.location.filename}:${member.location.line}:${member.location.col}`;
    if (seen.has(key)) continue;
    seen.add(key);
    total.count++;
    if (!member.jsDoc) {
      gaps.push({
        file: member.location.filename,
        line: member.location.line,
        name: `${ownerName}.${member.name}`,
      });
    }
  }
}

async function main(): Promise<void> {
  const thresholdArg = Deno.args.find((a) => a.startsWith('--threshold='));
  const threshold = thresholdArg ? Number(thresholdArg.split('=')[1]) : 98;

  const publishedFiles = await getPublishedFiles();
  const seen = new Set<string>();
  const gaps: Gap[] = [];
  const total = { count: 0 };

  for (const file of publishedFiles) {
    const doc = await docJson(file);
    for (const mod of Object.values(doc.nodes)) {
      for (const sym of mod.symbols) {
        for (const decl of sym.declarations) {
          if (decl.declarationKind !== 'export' || decl.kind === 'reference') {
            continue;
          }
          const key = `${decl.location.filename}:${decl.location.line}:${decl.location.col}`;
          if (seen.has(key)) continue;
          seen.add(key);
          total.count++;
          if (!decl.jsDoc) {
            gaps.push({ file: decl.location.filename, line: decl.location.line, name: sym.name });
          }

          const def = decl.def;
          if (!def) continue;
          if (decl.kind === 'interface' || decl.kind === 'class') {
            checkMembers(sym.name, def.properties, gaps, seen, total);
            checkMembers(sym.name, def.methods, gaps, seen, total);
          } else if (decl.kind === 'enum') {
            checkMembers(sym.name, def.members, gaps, seen, total);
          } else if (decl.kind === 'typeAlias' && def.tsType?.kind === 'typeLiteral') {
            checkMembers(sym.name, def.tsType.value?.properties, gaps, seen, total);
          }
        }
      }
    }
  }

  const documented = total.count - gaps.length;
  const pct = total.count === 0 ? 100 : (100 * documented) / total.count;

  console.log(`Symbol documentation: ${documented}/${total.count} (${pct.toFixed(1)}%)`);

  if (gaps.length > 0) {
    console.log('\nUndocumented symbols:');
    for (const gap of gaps.sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line)) {
      const relFile = gap.file.replace(/^file:\/\/.*\/src\/adblock-compiler-core\//, '');
      console.log(`  ${relFile}:${gap.line}  ${gap.name}`);
    }
  }

  if (pct < threshold) {
    console.error(
      `\nSymbol documentation coverage ${
        pct.toFixed(1)
      }% is below the required ${threshold}% threshold.`,
    );
    Deno.exit(1);
  }

  console.log(`\nMeets the ${threshold}% threshold.`);
}

if (import.meta.main) {
  await main();
}

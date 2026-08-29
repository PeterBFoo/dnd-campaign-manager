import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import * as ts from 'typescript';

const appRoot = resolve(process.cwd(), 'src/app');
const allowedRootFiles = new Set(['app.component.ts', 'app.config.ts', 'app.routes.ts']);

function productionTypeScriptFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      return entry.name === 'architecture' ? [] : productionTypeScriptFiles(path);
    }
    return entry.name.endsWith('.ts') && !entry.name.endsWith('.spec.ts') ? [path] : [];
  });
}

function moduleSpecifiers(file: string): string[] {
  const source = ts.createSourceFile(
    file,
    readFileSync(file, 'utf8'),
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS,
  );
  const specifiers: string[] = [];

  function visit(node: ts.Node): void {
    if ((ts.isImportDeclaration(node) || ts.isExportDeclaration(node))
      && node.moduleSpecifier
      && ts.isStringLiteral(node.moduleSpecifier)) {
      specifiers.push(node.moduleSpecifier.text);
    }
    if (ts.isCallExpression(node)
      && node.expression.kind === ts.SyntaxKind.ImportKeyword
      && node.arguments.length === 1
      && ts.isStringLiteral(node.arguments[0])) {
      specifiers.push(node.arguments[0].text);
    }
    ts.forEachChild(node, visit);
  }

  visit(source);
  return specifiers;
}

function existingTypeScriptFile(candidate: string): string | null {
  const candidates = extname(candidate)
    ? [candidate]
    : [`${candidate}.ts`, join(candidate, 'index.ts')];
  return candidates.find((path) => existsSync(path) && statSync(path).isFile()) ?? null;
}

function resolveInternalImport(source: string, specifier: string): string | null {
  if (specifier.startsWith('.')) {
    return existingTypeScriptFile(resolve(dirname(source), specifier));
  }
  if (specifier === '@modules/access') {
    return join(appRoot, 'modules/access/public-api.ts');
  }
  if (specifier === '@modules/access/entry') {
    return join(appRoot, 'modules/access/access-entry/public-api.ts');
  }
  if (specifier === '@modules/platform') {
    return join(appRoot, 'modules/platform/public-api.ts');
  }
  if (specifier === '@modules/campaigns') {
    return join(appRoot, 'modules/campaigns/public-api.ts');
  }
  if (specifier === '@modules/characters') {
    return join(appRoot, 'modules/characters/public-api.ts');
  }
  if (specifier === '@modules/adventure-catalog') {
    return join(appRoot, 'modules/adventure-catalog/public-api.ts');
  }
  if (specifier.startsWith('@shared/')) {
    return existingTypeScriptFile(join(appRoot, 'shared', specifier.slice('@shared/'.length)));
  }
  return null;
}

function appRelative(file: string): string {
  return relative(appRoot, file).split(sep).join('/');
}

function moduleName(file: string): string | null {
  const match = /^modules\/([^/]+)\//.exec(appRelative(file));
  return match?.[1] ?? null;
}

function boundaryViolation(source: string, target: string): string | null {
  const sourcePath = appRelative(source);
  const targetPath = appRelative(target);
  const sourceModule = moduleName(source);
  const targetModule = moduleName(target);

  if (sourcePath.startsWith('shared/') && !targetPath.startsWith('shared/')) {
    return `${sourcePath} cannot depend on ${targetPath}`;
  }

  if (sourcePath.startsWith('shell/') && targetModule && !targetPath.endsWith('/public-api.ts')) {
    return `${sourcePath} must use the public API of ${targetModule}`;
  }

  if (sourceModule && targetPath.startsWith('shell/')) {
    return `${sourcePath} cannot depend on shell`;
  }

  if (sourceModule && targetModule && sourceModule !== targetModule && !targetPath.endsWith('/public-api.ts')) {
    return `${sourcePath} must use the public API of ${targetModule}`;
  }

  if (sourceModule
    && sourceModule === targetModule
    && targetPath.endsWith('/public-api.ts')
    && !sourcePath.endsWith('/public-api.ts')) {
    return `${sourcePath} cannot import its own public API`;
  }

  if (sourceModule
    && sourcePath.includes('/api/')
    && targetModule === sourceModule
    && !targetPath.includes('/api/')) {
    return `${sourcePath} API code cannot depend on ${targetPath}`;
  }

  const sourceIsRoot = !sourcePath.includes('/');
  if (sourceIsRoot && targetModule && !targetPath.endsWith('/public-api.ts')) {
    const isRouteEntrypoint = sourcePath === 'app.routes.ts'
      && (targetPath.endsWith('/access.routes.ts')
        || targetPath.endsWith('/campaigns.routes.ts')
        || targetPath.endsWith('/characters.routes.ts')
        || targetPath.endsWith('/combat.routes.ts')
        || targetPath.endsWith('/journal.routes.ts')
        || targetPath.endsWith('/missions.routes.ts')
        || targetPath.endsWith('/adventure-catalog.routes.ts'));
    if (!isRouteEntrypoint) {
      return `${sourcePath} must use the public API of ${targetModule}`;
    }
  }

  return null;
}

function findCycles(graph: ReadonlyMap<string, readonly string[]>): string[][] {
  const visited = new Set<string>();
  const active = new Set<string>();
  const stack: string[] = [];
  const cycles: string[][] = [];

  function visit(node: string): void {
    if (active.has(node)) {
      const start = stack.indexOf(node);
      cycles.push([...stack.slice(start), node]);
      return;
    }
    if (visited.has(node)) {
      return;
    }

    visited.add(node);
    active.add(node);
    stack.push(node);
    for (const target of graph.get(node) ?? []) {
      visit(target);
    }
    stack.pop();
    active.delete(node);
  }

  for (const node of graph.keys()) {
    visit(node);
  }
  return cycles;
}

describe('frontend module boundaries', () => {
  it('keeps the production graph within the approved boundaries and free of cycles', () => {
    const files = productionTypeScriptFiles(appRoot);
    const graph = new Map<string, string[]>();
    const violations: string[] = [];

    for (const source of files) {
      const targets = moduleSpecifiers(source)
        .map((specifier) => resolveInternalImport(source, specifier))
        .filter((target): target is string => target !== null);
      graph.set(source, targets);
      for (const target of targets) {
        const violation = boundaryViolation(source, target);
        if (violation) {
          violations.push(violation);
        }
      }
    }

    const unexpectedRootFiles = files
      .map(appRelative)
      .filter((file) => !file.includes('/') && !allowedRootFiles.has(file));
    const wildcardExports = files
      .filter((file) => file.endsWith('/public-api.ts'))
      .filter((file) => /export\s+\*\s+from/.test(readFileSync(file, 'utf8')))
      .map(appRelative);
    const cycles = findCycles(graph).map((cycle) => cycle.map(appRelative).join(' -> '));

    expect(unexpectedRootFiles, 'functional TypeScript files in app root').toEqual([]);
    expect(wildcardExports, 'wildcard exports in public APIs').toEqual([]);
    expect(violations, 'dependency boundary violations').toEqual([]);
    expect(cycles, 'dependency cycles').toEqual([]);
  });

  it('detects upward and deep dependencies', () => {
    expect(boundaryViolation(
      join(appRoot, 'shared/http/problem-details.ts'),
      join(appRoot, 'modules/access/public-api.ts'),
    )).toContain('cannot depend');
    expect(boundaryViolation(
      join(appRoot, 'shell/home/home.page.ts'),
      join(appRoot, 'modules/access/session/session.store.ts'),
    )).toContain('public API');
    expect(boundaryViolation(
      join(appRoot, 'modules/access/bootstrap/bootstrap.page.ts'),
      join(appRoot, 'modules/platform/status/platform-status.store.ts'),
    )).toContain('public API');
  });

  it('detects dependency cycles', () => {
    const graph = new Map<string, string[]>([
      ['a', ['b']],
      ['b', ['c']],
      ['c', ['a']],
    ]);

    expect(findCycles(graph)).toEqual([['a', 'b', 'c', 'a']]);
  });
});

// The surface every script test imports: `test` registers a case, and `ScriptTests` runs the registered cases of
// a file in the order they were declared, each reported as its own TUnit test. `readRepoFile` reads a file by
// its path from the repository root, through the host.

const registered = (globalThis.__scriptTests ??= []);

/** Register one case. The body may be async; a rejected promise fails the case like a throw does. */
export function test(name, body) {
  registered.push({ name, body });
}

/** The text of a repository file, by its path from the repository root. */
export function readRepoFile(path) {
  return globalThis.__host.readRepoFile(path);
}

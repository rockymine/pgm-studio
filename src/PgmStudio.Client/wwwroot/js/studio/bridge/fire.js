/**
 * Push a named event at the Blazor host, tolerating a host that never wired it.
 *
 * The tolerance is the whole point, and it takes two catches rather than one. A `[JSInvokable]` the host does
 * not declare fails *asynchronously* — `invokeMethodAsync` returns a rejected promise — so guarding only the
 * synchronous throw leaves an unhandled rejection, which surfaces as a console error and, in the e2e sweep,
 * as a faulted page. Not every feed has a listener by design: a phase that pulls its state on demand
 * (`getThemes`, `getDressing`) needs the bridge to announce a change without caring whether anyone is
 * listening yet.
 *
 * One helper because both bridges need exactly this and had drifted — one guarded both, one only the throw,
 * and the difference only showed when a new feed was added to the wrong one.
 */
export function fireTo(dotnetRef, name, ...args) {
  try { dotnetRef?.invokeMethodAsync(name, ...args)?.catch(() => { /* host may not wire it */ }); }
  catch { /* host may not wire it */ }
}

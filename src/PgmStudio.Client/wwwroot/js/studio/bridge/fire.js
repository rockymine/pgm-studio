/**
 * Push a named event at the Blazor host, tolerating a host that has gone.
 *
 * The tolerance takes two catches rather than one. A host torn down mid-call fails *asynchronously* —
 * `invokeMethodAsync` returns a rejected promise — so guarding only the synchronous throw leaves an unhandled
 * rejection, which surfaces as a console error and, in the e2e sweep, as a faulted page.
 *
 * A name the host never declared is not tolerated quietly. Blazor rejects it with "does not contain a public
 * invokable method", and on a development host (`localhost`) that is a `console.error` naming the event, which
 * the e2e sweep turns into a failed page: an event nobody listens for is a feed that goes nowhere, and the
 * bridge that fires it is the one to change. A deployed host stays silent.
 */
export function fireTo(dotnetRef, name, ...args) {
  try {
    dotnetRef?.invokeMethodAsync(name, ...args)?.catch((error) => {
      if (inDevelopment() && /does not contain a public invokable method/.test(String(error?.message ?? error)))
        console.error(`[bridge] the host declares no [JSInvokable] "${name}", so this event goes nowhere`);
    });
  } catch { /* host may be gone */ }
}

const inDevelopment = () => ["localhost", "127.0.0.1", "[::1]"].includes(globalThis.location?.hostname);

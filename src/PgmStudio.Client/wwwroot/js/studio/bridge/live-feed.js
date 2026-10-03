// live-feed.js — the one way a bridge asks the server a question about the document it holds: POST the
// document, answer with the reply, and drop any reply a newer ask has overtaken.

export const UNREACHABLE = "Couldn't reach the studio. Check your connection and try again.";

/** The sentence out of a refusal envelope {error, message, findings[]}, or the status when a body is not one. */
export async function refusalText(res) {
  try {
    const body = await res.json();
    return body?.message || body?.error || `The studio returned an error (HTTP ${res.status}).`;
  } catch { return `The studio returned an error (HTTP ${res.status}).`; }
}

/**
 * One endpoint, asked of the live document.
 *
 * `url` and `body` are values or functions answering one; a function is read when the request is sent, so a
 * debounced feed asks about the document as it is when the edits settle. An empty `url` means there is
 * nothing to ask and nothing is sent. `when`, if given, gates a *scheduled* request at the moment it would
 * fire.
 *
 * The three outcomes are separate callbacks, each optional:
 * - `onAnswer(data, sent)` — the reply, with the body it answers;
 * - `onRefused(sentence, status)` — the server said no, with the sentence out of its envelope;
 * - `onUnreachable(sentence)` — the request failed or its reply was unreadable.
 *
 * Only the latest request is ever answered: a reply to an older one, or to one `cancel` has since dropped,
 * reaches none of the callbacks.
 *
 * @returns {{schedule(options?: {now?: boolean}): void, request(): Promise<void>, cancel(): void}}
 */
export function liveFeed({ url, body, delay = 0, when, onAnswer, onRefused, onUnreachable }) {
  let timer = null;
  let latest = 0;
  const read = (value) => (typeof value === "function" ? value() : value);

  async function send() {
    const target = read(url);
    if (!target) return;
    const mine = ++latest;
    const sent = read(body);
    let res;
    try {
      res = await fetch(target, { method: "POST", headers: { "Content-Type": "application/json" }, body: sent });
    } catch { if (mine === latest) onUnreachable?.(UNREACHABLE); return; }
    if (mine !== latest) return;
    if (!res.ok) {
      const sentence = await refusalText(res);
      if (mine === latest) onRefused?.(sentence, res.status);
      return;
    }
    let data;
    try { data = await res.json(); } catch { if (mine === latest) onUnreachable?.(UNREACHABLE); return; }
    if (mine === latest) await onAnswer?.(data, sent);
  }

  return {
    /** Ask once the edits settle: each call restarts the wait, and `now` fires on the next tick instead. */
    schedule({ now = false } = {}) {
      clearTimeout(timer);
      timer = setTimeout(() => { timer = null; if (!when || when()) send(); }, now ? 0 : delay);
    },
    /** Ask immediately, dropping any wait in progress. Resolves when the request has been answered (its `onAnswer` included) or dropped. */
    request() {
      clearTimeout(timer);
      timer = null;
      return send();
    },
    /** Drop the wait in progress and the answer to any request in flight. */
    cancel() {
      clearTimeout(timer);
      timer = null;
      latest++;
    },
  };
}

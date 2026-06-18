/** Fetch JSON with no-store caching; returns null on any non-OK response (incl. 404). */
export async function fetchJson(url) {
  const r = await fetch(url, { cache: "no-store" });
  if (!r.ok) return null;
  return r.json();
}

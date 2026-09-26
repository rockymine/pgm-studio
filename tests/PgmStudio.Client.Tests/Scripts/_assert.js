// Strict assertions for the script tests: equality is `Object.is`, deep equality compares prototypes, own
// enumerable keys, Maps, Sets, Dates, RegExps and typed arrays structurally, and every failure throws an
// AssertionError carrying both sides.

export class AssertionError extends Error {
  constructor(message, actual, expected) {
    super(message);
    this.name = "AssertionError";
    this.actual = actual;
    this.expected = expected;
  }
}

function show(value, seen = new Set()) {
  if (typeof value === "string") return JSON.stringify(value);
  if (typeof value === "bigint") return `${value}n`;
  if (typeof value === "function") return `[Function ${value.name || "anonymous"}]`;
  if (typeof value === "symbol") return value.toString();
  if (Object.is(value, -0)) return "-0";
  if (value === null || typeof value !== "object") return String(value);
  if (seen.has(value)) return "[Circular]";
  seen.add(value);
  try {
    if (value instanceof Error) return `${value.name}: ${value.message}`;
    if (value instanceof RegExp) return String(value);
    if (value instanceof Date) return value.toISOString();
    if (value instanceof Map) return `Map(${[...value].map(([k, v]) => `${show(k, seen)} => ${show(v, seen)}`).join(", ")})`;
    if (value instanceof Set) return `Set(${[...value].map(v => show(v, seen)).join(", ")})`;
    if (Array.isArray(value) || ArrayBuffer.isView(value)) return `[${Array.from(value, v => show(v, seen)).join(", ")}]`;
    const keys = Object.keys(value);
    return `{ ${keys.map(k => `${k}: ${show(value[k], seen)}`).join(", ")} }`;
  } finally {
    seen.delete(value);
  }
}

function fail(message, fallback, actual, expected) {
  if (message instanceof Error) throw message;
  throw new AssertionError(message ?? fallback, actual, expected);
}

function deepEqual(a, b, pairs) {
  if (Object.is(a, b)) return true;
  if (typeof a !== "object" || typeof b !== "object" || a === null || b === null) return false;
  if (Object.getPrototypeOf(a) !== Object.getPrototypeOf(b)) return false;
  if (pairs.get(a) === b) return true;
  pairs.set(a, b);

  if (a instanceof Date) return Object.is(a.getTime(), b.getTime());
  if (a instanceof RegExp) return String(a) === String(b);
  if (a instanceof Error && (a.name !== b.name || a.message !== b.message)) return false;
  if (ArrayBuffer.isView(a)) {
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) if (!Object.is(a[i], b[i])) return false;
  }
  if (a instanceof Map) {
    if (a.size !== b.size) return false;
    for (const [key, value] of a) if (!b.has(key) || !deepEqual(value, b.get(key), pairs)) return false;
  }
  if (a instanceof Set) {
    if (a.size !== b.size) return false;
    for (const value of a) {
      if (b.has(value)) continue;
      if (typeof value !== "object" || ![...b].some(other => deepEqual(value, other, pairs))) return false;
    }
  }
  if (Array.isArray(a) && a.length !== b.length) return false;

  const keysA = Object.keys(a);
  const keysB = Object.keys(b);
  if (keysA.length !== keysB.length) return false;
  for (const key of keysA) {
    if (!Object.prototype.hasOwnProperty.call(b, key)) return false;
    if (!deepEqual(a[key], b[key], pairs)) return false;
  }
  return true;
}

function matchesExpected(error, expected) {
  if (expected === undefined) return true;
  if (expected instanceof RegExp) return expected.test(String(error));
  if (typeof expected === "function") {
    if (expected.prototype !== undefined && error instanceof expected) return true;
    if (Error.isPrototypeOf(expected) || expected === Error) return false;
    return expected.call({}, error) === true;
  }
  if (typeof expected === "object" && expected !== null)
    return Object.keys(expected).every(key => expected[key] instanceof RegExp && typeof error?.[key] === "string"
      ? expected[key].test(error[key])
      : deepEqual(error?.[key], expected[key], new Map()));
  return false;
}

function ok(value, message) {
  if (!value) fail(message, `expected a truthy value, got ${show(value)}`, value, true);
}

const assert = Object.assign((value, message) => ok(value, message), {
  AssertionError,
  ok,

  equal(actual, expected, message) {
    if (!Object.is(actual, expected)) fail(message, `expected ${show(expected)}, got ${show(actual)}`, actual, expected);
  },

  notEqual(actual, expected, message) {
    if (Object.is(actual, expected)) fail(message, `expected anything but ${show(expected)}`, actual, expected);
  },

  deepEqual(actual, expected, message) {
    if (!deepEqual(actual, expected, new Map()))
      fail(message, `expected deep equality\n  actual:   ${show(actual)}\n  expected: ${show(expected)}`, actual, expected);
  },

  notDeepEqual(actual, expected, message) {
    if (deepEqual(actual, expected, new Map()))
      fail(message, `expected the two to differ, both are ${show(actual)}`, actual, expected);
  },

  match(text, pattern, message) {
    if (typeof text !== "string" || !pattern.test(text))
      fail(message, `expected ${show(text)} to match ${pattern}`, text, pattern);
  },

  throws(body, expected, message) {
    if (typeof expected === "string") { message = expected; expected = undefined; }
    try {
      body();
    } catch (error) {
      if (!matchesExpected(error, expected))
        fail(message, `the thrown ${show(error)} does not match ${show(expected)}`, error, expected);
      return;
    }
    fail(message, "expected the function to throw", undefined, expected);
  },

  doesNotThrow(body, message) {
    try {
      body();
    } catch (error) {
      fail(message, `expected no throw, got ${show(error)}`, error, undefined);
    }
  },

  fail(message) {
    fail(message, "failed", undefined, undefined);
  },
});

export default assert;

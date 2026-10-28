import '@testing-library/jest-dom/vitest'

// Node 25+ defines its own experimental `localStorage` global, which is unusable without a
// --localstorage-file flag and shadows jsdom's implementation. Provide a simple in-memory one.
if (typeof globalThis.localStorage?.clear !== 'function') {
  const store = new Map<string, string>()
  const memoryStorage: Storage = {
    get length() {
      return store.size
    },
    clear: () => store.clear(),
    getItem: (key) => store.get(key) ?? null,
    key: (index) => [...store.keys()][index] ?? null,
    removeItem: (key) => void store.delete(key),
    setItem: (key, value) => void store.set(key, String(value)),
  }
  Object.defineProperty(globalThis, 'localStorage', { value: memoryStorage, configurable: true })
}

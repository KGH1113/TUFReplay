export function isMockModeEnabled() {
  return (
    import.meta.env.DEV &&
    (import.meta.env.VITE_USE_MOCK_ACTIVITY === "true" ||
      (typeof window !== "undefined" &&
        new URLSearchParams(window.location.search).get("mock") === "1"))
  );
}

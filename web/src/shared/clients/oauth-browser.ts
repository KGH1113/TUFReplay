export interface OAuthCallback {
  code: string;
  state: string;
}

export function takeOAuthCallback(): OAuthCallback | null {
  if (typeof window === "undefined") return null;
  const url = new URL(window.location.href);
  const code = url.searchParams.get("code");
  const state = url.searchParams.get("state");
  if (!code || !state) return null;
  url.searchParams.delete("code");
  url.searchParams.delete("state");
  window.history.replaceState(null, "", url);
  return { code, state };
}

export function prepareOAuthWindow(): (url: string | null) => void {
  const popup = window.open("about:blank", "tuf-replay-oauth");
  if (popup) popup.opener = null;
  return (url) => {
    if (!url) popup?.close();
    else if (popup) popup.location.href = url;
    else window.location.assign(url);
  };
}

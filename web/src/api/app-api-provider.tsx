import { createContext, type ReactNode, useContext } from "react";

import type { AppApi } from "@/api/app-api";

interface ApiRuntime {
  api: Promise<AppApi>;
  mockEnabled: boolean;
}

const ApiContext = createContext<ApiRuntime | null>(null);

export function ApiProvider({
  api,
  mockEnabled,
  children,
}: {
  api: Promise<AppApi>;
  mockEnabled: boolean;
  children: ReactNode;
}) {
  return <ApiContext.Provider value={{ api, mockEnabled }}>{children}</ApiContext.Provider>;
}

export function useApiPromise() {
  const runtime = useContext(ApiContext);
  if (!runtime) throw new Error("ApiProvider is missing");
  return runtime.api;
}

export function useMockEnabled() {
  const runtime = useContext(ApiContext);
  if (!runtime) throw new Error("ApiProvider is missing");
  return runtime.mockEnabled;
}

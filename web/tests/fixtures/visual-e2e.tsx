import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRoot } from "react-dom/client";
import type { AppApi } from "@/api/app-api";
import { ApiProvider } from "@/api/app-api-provider";
import { createVisualApi } from "@/api/visual/create-visual-api";
import { VisualPresetLibrary } from "@/components/submission/visual-preset-library";
import { initializeI18n } from "@/i18n/i18n";
import type { AdofaiIpcClients } from "@/shared/clients/adofai-ipc-client";
import "@/index.css";
import { VisualE2ERun } from "./visual-e2e-run";

const namespace = {
  async call(method: string, params: object) {
    const response = await fetch("/harness/visual-ipc", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ method, params }),
    });
    if (!response.ok) throw new Error(await response.text());
    return response.json();
  },
};
const visual = createVisualApi({ namespace, pickerNamespace: namespace } as AdofaiIpcClients);
// Unused domains deliberately fail instead of returning fabricated successes.
const api = new Proxy(
  { visual },
  {
    get(target, key) {
      if (key === "visual") return target.visual;
      if (key === "then") return undefined;
      throw new Error(`Unavailable in this focused E2E: ${String(key)}`);
    },
  },
) as AppApi;
await initializeI18n();
document.documentElement.classList.add("dark");
const root = document.getElementById("root");
if (!root) throw new Error("Missing test root");
createRoot(root).render(
  <ApiProvider api={Promise.resolve(api)} mockEnabled={false}>
    <QueryClientProvider client={new QueryClient()}>
      <main className="mx-auto max-w-4xl py-12">
        <h1 className="px-4 pb-6 text-2xl font-semibold">TUFReplay 로컬 등록 테스트</h1>
        <p className="px-4 pb-6 text-sm text-muted-foreground">
          실제 C# 가져오기 · 로컬 제출 API · PostgreSQL 저장
        </p>
        <VisualPresetLibrary accountKey="00000000-0000-4000-8000-000000000041" />
        <VisualE2ERun />
      </main>
    </QueryClientProvider>
  </ApiProvider>,
);

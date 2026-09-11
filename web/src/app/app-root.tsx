import { QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { ApiProvider } from "@/api/app-api-provider";
import { getProductionApi } from "@/api/create-production-api";
import { isMockModeEnabled } from "@/app/mock-mode";
import { createAppQueryClient } from "@/app/query-client";
import { createMockApi } from "@/mocks/create-mock-api";
import { ActivityPage } from "@/pages/activity/activity-page";

const mockEnabled = isMockModeEnabled();
const api = mockEnabled ? Promise.resolve(createMockApi()) : getProductionApi();

export function AppRoot() {
  const [queryClient] = useState(createAppQueryClient);
  return (
    <ApiProvider api={api} mockEnabled={mockEnabled}>
      <QueryClientProvider client={queryClient}>
        <ActivityPage />
      </QueryClientProvider>
    </ApiProvider>
  );
}

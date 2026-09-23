import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useApiPromise } from "@/api/app-api-provider";
import type { VisualPresetImport } from "@/api/visual/visual-api";
import { visualResponseBelongsToAccount } from "@/models/visual/visual-model";
import type { VisualRegistrationReporter } from "@/models/visual/visual-registration-model";
import { TUFREPLAY_WEB_BUILD } from "@/shared/config/tufreplay-build-info";
import { visualKeys } from "@/state/visual/visual-queries";

export function useVisualLibrary(
  enabled = true,
  sourcesEnabled = false,
  accountKey: string | null = null,
) {
  enabled = enabled && accountKey !== null && TUFREPLAY_WEB_BUILD.flavor === "auto-submission";
  const api = useApiPromise();
  const cache = useQueryClient();
  const currentAccountKey = useRef(accountKey);
  currentAccountKey.current = accountKey;

  useEffect(() => {
    const filters = {
      queryKey: visualKeys.all,
      predicate: (query: { queryKey: readonly unknown[] }) =>
        accountKey === null || query.queryKey[2] !== accountKey,
    };
    void cache.cancelQueries(filters).then(() => {
      if (currentAccountKey.current === accountKey) cache.removeQueries(filters);
    });
  }, [accountKey, cache]);

  const presets = useQuery({
    queryKey: visualKeys.presets(accountKey),
    queryFn: async () => {
      const requestedAccountKey = accountKey;
      const result = await (await api).visual.listPresets();
      if (!visualResponseBelongsToAccount(requestedAccountKey, currentAccountKey.current))
        throw new Error("visual_account_changed");
      return result;
    },
    enabled,
    retry: false,
  });
  const sources = useQuery({
    queryKey: visualKeys.sources(accountKey),
    queryFn: async () => {
      const requestedAccountKey = accountKey;
      const result = await (await api).visual.getSources();
      if (!visualResponseBelongsToAccount(requestedAccountKey, currentAccountKey.current))
        throw new Error("visual_account_changed");
      return result;
    },
    enabled: enabled && sourcesEnabled,
    retry: false,
  });
  const importPreset = useMutation({
    mutationFn: async ({
      input,
      onProgress,
    }: {
      input: VisualPresetImport;
      onProgress: VisualRegistrationReporter;
    }) => {
      if (currentAccountKey.current !== accountKey || accountKey === null)
        throw new Error("visual_account_changed");
      const result = await (await api).visual.registerPreset(input, onProgress);
      if (currentAccountKey.current !== accountKey) throw new Error("visual_account_changed");
      return result;
    },
    onSuccess: (result) => {
      if (result.state === "completed")
        void cache.invalidateQueries({ queryKey: visualKeys.presets(accountKey) });
    },
  });
  const inspectPreset = useMutation({
    mutationFn: async (input: VisualPresetImport) => {
      if (currentAccountKey.current !== accountKey || accountKey === null)
        throw new Error("visual_account_changed");
      const result = await (await api).visual.inspectPreset(input);
      if (currentAccountKey.current !== accountKey) throw new Error("visual_account_changed");
      return result;
    },
  });
  const removePreset = useMutation({
    mutationFn: async (id: string) => {
      if (currentAccountKey.current !== accountKey || accountKey === null)
        throw new Error("visual_account_changed");
      await (await api).visual.removePreset(id);
      if (currentAccountKey.current !== accountKey) throw new Error("visual_account_changed");
    },
    onSuccess: () => {
      void cache.invalidateQueries({ queryKey: visualKeys.presets(accountKey) });
    },
  });
  return { presets, sources, importPreset, inspectPreset, removePreset };
}

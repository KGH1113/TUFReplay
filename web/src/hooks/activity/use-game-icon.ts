import { useQuery } from "@tanstack/react-query";
import { useApiPromise } from "@/api/app-api-provider";
import type { GameIconName } from "@/models/activity/game-icon";
import { ApiError } from "@/shared/errors/api-error";

// The game refuses to copy an icon while it judges input; ask again after the attempt.
const PAUSED_RETRY_DELAY_MS = 10_000;

/**
 * Image source for a run-flag icon: ADOFAI's own icon copied from the running game, or
 * TUFReplay's drawing when the game cannot provide it. Null while loading, so the slot
 * stays empty instead of switching pictures.
 */
export function useGameIcon(icon: GameIconName, fallbackSrc: string): string | null {
  const apiPromise = useApiPromise();
  const query = useQuery({
    queryKey: ["game-icon", icon],
    queryFn: async ({ signal }) => {
      const png = (await (await apiPromise).gameIcons?.getIcon(icon, { signal })) ?? null;
      // Kept for the page's lifetime: four small images shared by every run row.
      return png === null ? null : URL.createObjectURL(png);
    },
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: Number.POSITIVE_INFINITY,
    retry: (_, error) => error instanceof ApiError && error.code === "game_icon_paused",
    retryDelay: PAUSED_RETRY_DELAY_MS,
    refetchOnReconnect: false,
    refetchOnWindowFocus: false,
  });
  if (query.isPending) return null;
  return query.data ?? fallbackSrc;
}

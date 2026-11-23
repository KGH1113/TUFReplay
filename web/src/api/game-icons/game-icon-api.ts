import type { GameIconName } from "@/models/activity/game-icon";

export interface GameIconApi {
  /** ADOFAI's own icon as a PNG, or null when this ADOFAI build has none. */
  getIcon(icon: GameIconName, options?: { signal?: AbortSignal }): Promise<Blob | null>;
}

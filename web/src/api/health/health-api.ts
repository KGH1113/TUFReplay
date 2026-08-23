import type { Health } from "@/models/health/health-model";

export interface HealthApi {
  get(): Promise<Health>;
}

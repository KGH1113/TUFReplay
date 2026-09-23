import type { z } from "zod";
import type {
  visualRegistrationProgressSchema,
  visualRegistrationSchema,
} from "@/schemas/visual/visual-schema";

export type VisualRegistrationProgress = z.infer<typeof visualRegistrationProgressSchema>;
export type VisualRegistrationResult = Exclude<
  z.infer<typeof visualRegistrationSchema>,
  { state: "running" | "failed" }
>;
export type VisualRegistrationReporter = (progress: VisualRegistrationProgress) => void;

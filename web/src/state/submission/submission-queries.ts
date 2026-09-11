export const submissionKeys = {
  all: ["submission"] as const,
  status: ["submission", "status"] as const,
  runs: ["submission", "runs"] as const,
  run: (id: string) => ["submission", "run", id] as const,
};

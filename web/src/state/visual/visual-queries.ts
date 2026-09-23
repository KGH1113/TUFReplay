export const visualKeys = {
  all: ["visual"] as const,
  presets: (accountKey: string | null) => ["visual", "presets", accountKey] as const,
  sources: (accountKey: string | null) => ["visual", "sources", accountKey] as const,
};

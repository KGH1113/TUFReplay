const KIBIBYTE = 1024;

export function formatFileSize(bytes: number) {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  if (bytes < KIBIBYTE) return `${Math.round(bytes)} B`;
  if (bytes < KIBIBYTE ** 2) return `${formatValue(bytes / KIBIBYTE)} KB`;
  if (bytes < KIBIBYTE ** 3) return `${formatValue(bytes / KIBIBYTE ** 2)} MB`;
  return `${formatValue(bytes / KIBIBYTE ** 3)} GB`;
}

function formatValue(value: number) {
  return value >= 100 ? Math.round(value).toString() : value.toFixed(1);
}

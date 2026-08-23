const KIBIBYTE = 1024;

export function formatFileSize(bytes: number, locale: string) {
  if (!Number.isFinite(bytes) || bytes <= 0) return `0 B`;
  if (bytes < KIBIBYTE) return `${Math.round(bytes).toLocaleString(locale)} B`;
  if (bytes < KIBIBYTE ** 2) return `${formatValue(bytes / KIBIBYTE, locale)} KB`;
  if (bytes < KIBIBYTE ** 3) return `${formatValue(bytes / KIBIBYTE ** 2, locale)} MB`;
  return `${formatValue(bytes / KIBIBYTE ** 3, locale)} GB`;
}

function formatValue(value: number, locale: string) {
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: value >= 100 ? 0 : 1,
    maximumFractionDigits: value >= 100 ? 0 : 1,
  }).format(value);
}

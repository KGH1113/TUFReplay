export function formatDateTime(
  value: string | null | undefined,
  locale: string,
  emptyValue: string,
) {
  if (!value) return emptyValue;

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return date.toLocaleString(locale);
}

export function formatBytes(bytes: number | null | undefined, locale: string) {
  const value = bytes ?? 0;

  if (value < 1024) return `${value.toLocaleString(locale)} B`;
  if (value < 1024 * 1024) return `${formatDecimal(value / 1024, locale)} KB`;
  return `${formatDecimal(value / 1024 / 1024, locale)} MB`;
}

export function formatNumber(value: number | null | undefined, locale: string) {
  return (value ?? 0).toLocaleString(locale);
}

function formatDecimal(value: number, locale: string) {
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(value);
}

export function formatDateTime(value: string | null | undefined) {
  if (!value) return "No clear time";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return date.toLocaleString();
}

export function formatBytes(bytes: number | null | undefined) {
  const value = bytes ?? 0;

  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

export function formatNumber(value: number | null | undefined) {
  return (value ?? 0).toLocaleString();
}

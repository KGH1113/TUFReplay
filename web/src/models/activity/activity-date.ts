export function formatDayLabel(date: string, locale: string) {
  const parsed = new Date(`${date}T00:00:00`);
  if (Number.isNaN(parsed.getTime())) return date;
  return parsed.toLocaleDateString(locale, { month: "short", day: "numeric" });
}

/** Calendar-stamp parts for a `YYYY-MM-DD` day key. */
export function dayStampParts(date: string, locale: string) {
  const parsed = parseDate(date);
  if (!parsed)
    return {
      weekday: "",
      day: date,
      monthKey: date,
      monthLabel: date,
      fullLabel: date,
      shortLabel: date,
    };
  return {
    weekday: parsed.toLocaleDateString(locale, { weekday: "short" }),
    day: String(parsed.getDate()),
    monthKey: date.slice(0, 7),
    monthLabel: formatMonthLabel(parsed, locale),
    fullLabel: parsed.toLocaleDateString(locale, {
      weekday: "long",
      month: "long",
      day: "numeric",
      year: "numeric",
    }),
    shortLabel: formatDayLabel(date, locale),
  };
}

export function parseDate(date: string) {
  const parsed = new Date(`${date}T00:00:00`);
  if (Number.isNaN(parsed.getTime())) return null;
  return parsed;
}

export function getMonthStart(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

export function addMonths(date: Date, amount: number) {
  return new Date(date.getFullYear(), date.getMonth() + amount, 1);
}

export function getCalendarWeeks(month: Date) {
  const firstVisibleDate = new Date(month.getFullYear(), month.getMonth(), 1 - month.getDay());

  return Array.from({ length: 6 }, (_, weekIndex) =>
    Array.from({ length: 7 }, (_, dayIndex) => {
      const date = new Date(firstVisibleDate);
      date.setDate(firstVisibleDate.getDate() + weekIndex * 7 + dayIndex);
      return date;
    }),
  );
}

export function formatDateKey(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function formatMonthLabel(date: Date, locale: string) {
  return date.toLocaleDateString(locale, { month: "long", year: "numeric" });
}

export function formatTime(
  value: string | null | undefined,
  locale: string,
  emptyValue: string,
  timeZone?: string,
) {
  if (!value) return emptyValue;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleTimeString(locale, { hour: "2-digit", minute: "2-digit", timeZone });
}

export function formatTimeWithOffset(
  value: string | null | undefined,
  locale: string,
  emptyValue: string,
  timeZone?: string,
) {
  const { time, offset } = formatTimeWithOffsetParts(value, locale, emptyValue, timeZone);
  return offset ? `${time} (${offset})` : time;
}

export function formatTimeWithOffsetParts(
  value: string | null | undefined,
  locale: string,
  emptyValue: string,
  timeZone?: string,
) {
  if (!value) return { time: emptyValue, offset: "" };
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return { time: value, offset: "" };
  const time = parsed.toLocaleTimeString(locale, {
    hour: "2-digit",
    hourCycle: "h23",
    minute: "2-digit",
    timeZone,
  });
  const offset = new Intl.DateTimeFormat(locale, { timeZone, timeZoneName: "shortOffset" })
    .formatToParts(parsed)
    .find((part) => part.type === "timeZoneName")?.value;
  return { time, offset: offset ?? "" };
}

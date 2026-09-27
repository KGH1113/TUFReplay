/** Match the rating forms accepted by TUF's submission validator. */
export function isValidFeelingRating(value: string): boolean {
  const number = "(?:[1-9]|1[0-9]|20)";
  const pgu = `[PGUpgu]${number}`;
  const legacy = "(?:[1-9]|1[0-7]|1[8-9]\\+?|20(?:\\.[0-9])?\\+?|21(?:\\.[0-4])?\\+?)";
  const q = "[qQ][2-4]\\+?";
  const allowed = new RegExp(
    `^(?:${pgu}(?:[-~](?:${pgu}|${number}))?|${legacy}(?:[-~]${legacy})?|${q}(?:[-~]${q})?|-2|-21|Marathon|MA|Impossible|Censored|P0)$`,
  );
  const rating = value.trim();
  return rating.length > 0 && rating.length <= 60 && allowed.test(rating);
}

/**
 * Collects single-key loads made in the same moment into batched requests, so a list that
 * renders many rows at once asks for their data in one call instead of one call per row.
 */
export function createBatchLoader<Value>(
  loadMany: (keys: string[]) => Promise<Map<string, Value>>,
  { maxBatchSize = 50, delayMs = 16 }: { maxBatchSize?: number; delayMs?: number } = {},
) {
  type Waiter = { resolve: (value: Value | null) => void; reject: (cause: unknown) => void };
  let pending = new Map<string, Waiter[]>();
  let scheduled = false;

  const flush = () => {
    const batch = pending;
    pending = new Map();
    scheduled = false;
    const keys = [...batch.keys()];
    for (let start = 0; start < keys.length; start += maxBatchSize) {
      const chunk = keys.slice(start, start + maxBatchSize);
      loadMany(chunk).then(
        (values) => {
          for (const key of chunk)
            for (const waiter of batch.get(key) ?? []) waiter.resolve(values.get(key) ?? null);
        },
        (cause: unknown) => {
          for (const key of chunk) for (const waiter of batch.get(key) ?? []) waiter.reject(cause);
        },
      );
    }
  };

  return {
    load(key: string) {
      return new Promise<Value | null>((resolve, reject) => {
        const waiters = pending.get(key);
        if (waiters) waiters.push({ resolve, reject });
        else pending.set(key, [{ resolve, reject }]);
        if (scheduled) return;
        scheduled = true;
        setTimeout(flush, delayMs);
      });
    },
  };
}

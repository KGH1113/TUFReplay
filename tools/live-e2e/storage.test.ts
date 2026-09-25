import { expect, test } from "bun:test";
import { localOrigin, storageEnvironment } from "./storage";

test("local infrastructure overrides every production storage destination", () => {
  const env = storageEnvironment();
  expect(env.TUF_REPLAY_STORAGE).toBe("s3-local");
  expect(env.R2_LOCAL_FALLBACK).toBe("false");
  expect(env.R2_ENDPOINT).toBe("http://127.0.0.1:9000");
  expect(env.REPLAY_CDN_ORIGIN).toBe("http://127.0.0.1:8787");
  expect(Object.keys(env).sort()).toEqual([
    "R2_ACCESS_KEY_ID", "R2_BUCKET", "R2_ENDPOINT", "R2_LOCAL_FALLBACK", "R2_SECRET_ACCESS_KEY",
    "REPLAY_CDN_ORIGIN", "REPLAY_CDN_SIGNING_SECRET", "TUF_REPLAY_STORAGE",
  ].sort());
});

test("service addresses cannot point to production or disguise a different host", () => {
  expect(localOrigin("http://127.0.0.1:9001/")).toBe("http://127.0.0.1:9001");
  for (const url of ["https://account.r2.cloudflarestorage.com", "http://192.168.1.1", "http://127.0.0.1.evil", "http://user@127.0.0.1", "http://127.0.0.1/path", "http://127.0.0.1?x=1"])
    expect(() => localOrigin(url)).toThrow();
});

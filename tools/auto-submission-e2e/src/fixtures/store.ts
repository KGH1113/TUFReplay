import { join } from "node:path";
import { dataDir } from "../config";
import type { Fixture } from "../types";

export const fixtureDir = join(dataDir, "fixtures");
export async function fixtures(): Promise<Fixture[]> {
  const file = Bun.file(join(fixtureDir, "index.json"));
  return (await file.exists()) ? file.json() : [];
}
export async function fixture(id: string) {
  const item = (await fixtures()).find((item) => item.id === id);
  if (!item) throw new Error("Unknown fixture; run bun run e2e:prepare first");
  return item;
}
export function fixturePath(item: Fixture, name: "inputs.csv" | "hits.csv" | "chart.zip") {
  return join(fixtureDir, item.id, name);
}

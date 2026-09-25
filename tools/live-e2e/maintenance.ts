import { join } from "node:path";
import { cp, copyFile, mkdir } from "node:fs/promises";
import { root, data } from "./config";
import { requireStorage, storageEnvironment } from "./storage";

// Use the server's real migration/asset registration tasks, with no local fallback.
// Copies are idempotent and preserve the original local files.
export async function prepareObjectStorage() {
  await requireStorage();
  // Match the packaged asset layout: MapleStory's source is in the Unity project.
  const assets = join(data, "default-assets");
  await mkdir(assets, { recursive: true });
  await cp(join(root, "TUFReplay/Visual/Assets"), assets, { recursive: true });
  await copyFile(join(root, "TUFReplay.Unity/Assets/Fonts/MAPLESTORY_OTF_BOLD.OTF"), join(assets, "Fonts/MAPLESTORY_OTF_BOLD.OTF"));
  for (const args of [
    ["migrate_artifacts"],
    ["seed_visual_assets", `directory:${assets}`],
  ]) {
    console.log(`Local object storage: ${args[0]}`);
    const child = Bun.spawn(["cargo", "run", "--", "task", "--environment", "local-game", ...args], {
      cwd: join(root, "server"),
      env: { ...process.env, ...storageEnvironment() },
      stdout: "inherit", stderr: "inherit",
    });
    if (await child.exited !== 0) throw new Error(`Local object storage preparation failed: ${args[0]}`);
  }
  console.log("Local object storage: existing files verified and bundled defaults registered");
}

if (import.meta.main) await prepareObjectStorage();

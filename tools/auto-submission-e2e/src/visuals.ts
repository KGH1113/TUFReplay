import { join } from "node:path";
import { apiBase, currentAccessToken, dataDir, root } from "./config";

/** Local Unity boundary only. Parsing/inspection run in the production C#
 * adapters; all storage/authorization uses the actual Rust API and local TUF. */
export async function visualIpc(request: Request): Promise<Response> {
  const { method, params = {} } = await request.json();
  if (method === "visual.sources.get") return Response.json({ sources: [
    { source: "jipper-resourcepack", version: "1.5.2.0", available: true, kinds: ["keyviewer", "overlay"] },
    { source: "dmnote", version: "2.0.2", available: true, kinds: ["keyviewer"] },
  ] });
  let path = "/api/v1/visual-presets";
  let verb = "GET";
  let body: string | undefined;
  if (method === "visual.presets.inspect" || method === "visual.presets.import") {
    if (!["dmnote", "jipper-resourcepack"].includes(params.source)) throw new Error("Source fixture unavailable");
    const child = Bun.spawn(["bash", join(root, "../../scripts/run.sh"), "visual-import"], { stdin: "pipe", stdout: "pipe", stderr: "pipe" });
    child.stdin.write(JSON.stringify({ ...params, inspect: method.endsWith("inspect"), assets: params.assetsJson ? JSON.parse(params.assetsJson) : [], installation: join(dataDir, "visuals", "JipperResourcePack") }));
    child.stdin.end();
    const [result, exit] = await Promise.all([new Response(child.stdout).json(), child.exited]);
    if (exit !== 0) throw new Error("Build the C# importer with scripts/run.sh mod-check first");
    if (result.error || method.endsWith("inspect")) return Response.json(result);
    body = JSON.stringify({ name: params.name, bundle: result });
    verb = "POST";
  } else if (method === "visual.presets.remove") {
    if (!/^[a-f0-9-]{36}$/.test(params.id)) throw new Error("Invalid preset id");
    path += `/${params.id}`;
    verb = "DELETE";
  } else if (method !== "visual.presets.list") throw new Error("Unsupported visual method");
  const response = await fetch(`${apiBase}${path}`, { method: verb, headers: { Authorization: `Bearer ${currentAccessToken()}`, "Content-Type": "application/json" }, body });
  const result = await response.json();
  return Response.json(response.ok ? result : { error: { code: result.description ?? result.error ?? "visual_bundle_invalid", message: JSON.stringify(result) } });
}

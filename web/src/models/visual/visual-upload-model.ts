const MAX_ASSET_BYTES = 48 * 1024 * 1024;
const MAX_REQUEST_BYTES = 64 * 1024 * 1024;

export async function readVisualUploads(files: Record<string, File>, presetJson?: string) {
  const inputs = Object.entries(files);
  let bytes = new TextEncoder().encode(presetJson ?? "").length;
  for (const [, file] of inputs) {
    if (file.size === 0 || file.size > MAX_ASSET_BYTES) throw new Error("visual_payload_too_large");
    bytes += Math.ceil(file.size / 3) * 4 + 2048;
  }
  if (bytes > MAX_REQUEST_BYTES - 4096) throw new Error("visual_payload_too_large");
  const uploads: Array<{ reference: string; data_base64: string }> = [];
  for (const [reference, file] of inputs) {
    const data = new Uint8Array(await file.arrayBuffer());
    const chunks: string[] = [];
    for (let offset = 0; offset < data.length; offset += 8192) {
      chunks.push(String.fromCharCode(...data.subarray(offset, offset + 8192)));
    }
    uploads.push({ reference, data_base64: btoa(chunks.join("")) });
  }
  return uploads;
}

import { AwsClient } from "aws4fetch";

/** Local-only adapter for the read operations used by the production R2 Worker. */
export function s3Bucket(endpoint, bucket) {
  const client = new AwsClient({
    accessKeyId: "S3RVER", secretAccessKey: "S3RVER", service: "s3", region: "auto",
  });
  async function read(key, method, options) {
    const headers = new Headers();
    if (options?.range?.has("Range")) headers.set("Range", options.range.get("Range"));
    const response = await client.fetch(
      `${endpoint}/${bucket}/${key.split("/").map(encodeURIComponent).join("/")}`,
      { method, headers },
    );
    if (response.status === 404) return null;
    if (!response.ok) throw new Error(`Local S3 ${method} failed: ${response.status}`);
    const range = /^bytes (\d+)-(\d+)\/(\d+)$/.exec(response.headers.get("Content-Range") ?? "");
    return {
      body: response.body,
      size: range ? Number(range[3]) : Number(response.headers.get("Content-Length")),
      httpEtag: response.headers.get("ETag"),
      ...(range ? { range: { offset: Number(range[1]), length: Number(range[2]) - Number(range[1]) + 1 } } : {}),
    };
  }
  return { head: (key) => read(key, "HEAD"), get: (key, options) => read(key, "GET", options) };
}

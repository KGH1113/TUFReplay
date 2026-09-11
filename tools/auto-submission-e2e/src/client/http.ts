import { apiBase, accessToken } from "../config";
import { log } from "../logs";

export async function request(
  path: string,
  method = "GET",
  body?: unknown,
  token = accessToken,
): Promise<any> {
  log("client", `${method} ${path}`, body);
  const response = await fetch(`${apiBase}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    redirect: "error",
    signal: AbortSignal.timeout(15000),
  });
  const text = await response.text();
  let value: any;
  try {
    value = JSON.parse(text);
  } catch {
    value = { raw: text.slice(0, 1000) };
  }
  log("client", `${response.status} ${path}`, value);
  if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${text.slice(0, 1000)}`);
  return value;
}

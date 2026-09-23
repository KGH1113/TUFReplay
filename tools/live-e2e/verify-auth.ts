import assert from "node:assert/strict";
import { createHash, randomBytes } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { callback, clientId, data, owner } from "./config";

// Exercise public login/consent/PKCE endpoints. Never seed tokens or log credentials.
const api = "http://127.0.0.1:3002";
const cookies = new Map<string, string>();
const checks: string[] = [];
let refreshToken: string | undefined;
async function request(path: string, init: RequestInit = {}, session = false) {
	const headers = new Headers(init.headers);
	if (session) {
		headers.set("Cookie", [...cookies].map(([k, v]) => `${k}=${v}`).join("; "));
		const csrf = cookies.get("csrfToken");
		if (csrf) headers.set("x-csrf-token", csrf);
	}
	const response = await fetch(`${api}${path}`, {
		...init,
		headers,
		redirect: "manual",
		signal: AbortSignal.timeout(15000),
	});
	if (session)
		for (const cookie of response.headers.getSetCookie()) {
			const pair = cookie.split(";", 1)[0] ?? "";
			const split = pair.indexOf("=");
			cookies.set(pair.slice(0, split), pair.slice(split + 1));
		}
	return response;
}
function pass(name: string) {
	checks.push(name);
	console.log(`PASS ${name}`);
}
function location(response: Response) {
	const value = response.headers.get("location");
	assert.ok(value, "Missing redirect location");
	return new URL(value);
}
function token(body: Record<string, string>) {
	return request("/oauth/token", {
		method: "POST",
		body: new URLSearchParams({ client_id: clientId, ...body }),
	});
}
try {
	const credentials = JSON.parse(await readFile(`${data}/login.json`, "utf8"));
	const verifier = randomBytes(32).toString("base64url");
	const state = randomBytes(24).toString("hex");
	const params = new URLSearchParams({
		client_id: clientId,
		redirect_uri: callback,
		response_type: "code",
		scope: "User.Read.Public User.Submission.Create",
		state,
		code_challenge: createHash("sha256").update(verifier).digest("base64url"),
		code_challenge_method: "S256",
	});
	const authorize = `/oauth/authorize?${params}`;
	const anonymous = await request(authorize);
	assert.equal(anonymous.status, 302);
	assert.equal(location(anonymous).origin, "http://127.0.0.1:5176");
	assert.equal(location(anonymous).pathname, "/login");
	pass("unauthenticated OAuth redirects to local TUF login");
	const login = await request(
		"/v2/auth/login",
		{
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({
				emailOrUsername: credentials.username,
				password: credentials.password,
			}),
		},
		true,
	);
	assert.equal(login.status, 200, `Login returned ${login.status}`);
	assert.ok(cookies.has("csrfToken"));
	pass("password login establishes a real session and CSRF token");
	const authorized = await request(authorize, {}, true);
	assert.equal(authorized.status, 302);
	let destination = location(authorized);
	if (destination.pathname === "/oauth/consent") {
		const info = await request("/v2/oauth/consent", {}, true);
		assert.equal(info.status, 200);
		const consent = await info.json();
		assert.equal(consent.redirectUri, callback);
		assert.ok(consent.scopes.includes("User.Submission.Create"));
		const approved = await request(
			"/v2/oauth/consent/approve",
			{ method: "POST" },
			true,
		);
		assert.equal(approved.status, 200);
		destination = new URL((await approved.json()).redirectTo);
	}
	assert.equal(destination.origin + destination.pathname, callback);
	assert.equal(destination.searchParams.get("state"), state);
	const code = destination.searchParams.get("code");
	assert.ok(code);
	pass(
		"consent returns authorization code and unchanged state to registered callback",
	);
	const grant = {
		grant_type: "authorization_code",
		code,
		redirect_uri: callback,
	};
	const wrong = await token({
		...grant,
		code_verifier: randomBytes(32).toString("base64url"),
	});
	assert.equal(wrong.status, 400);
	assert.equal((await wrong.json()).error, "invalid_grant");
	pass("incorrect PKCE verifier is rejected");
	const exchanged = await token({ ...grant, code_verifier: verifier });
	assert.equal(exchanged.status, 200);
	const tokens = await exchanged.json();
	refreshToken = tokens.refresh_token;
	assert.ok(tokens.access_token && refreshToken);
	pass("PKCE exchange succeeds with the correct verifier");
	const identity = await fetch("http://127.0.0.1:5151/api/v1/account", {
		headers: { Authorization: `Bearer ${tokens.access_token}` },
		signal: AbortSignal.timeout(15000),
	});
	assert.equal(identity.status, 200);
	const account = await identity.json();
	assert.equal(account.owner_id, owner);
	assert.equal(account.can_submit, true);
	assert.equal(account.client_id, clientId);
	pass(
		"submission server validates real OAuth identity and trusted tester permission",
	);
	const denied = await fetch("http://127.0.0.1:5151/api/v1/account", {
		signal: AbortSignal.timeout(15000),
	});
	assert.equal(denied.status, 401);
	pass("unauthenticated submission access is rejected");
	assert.ok(refreshToken);
	const renewed = await token({
		grant_type: "refresh_token",
		refresh_token: refreshToken,
	});
	assert.equal(renewed.status, 200);
	const rotated = await renewed.json();
	assert.ok(rotated.refresh_token && rotated.refresh_token !== refreshToken);
	refreshToken = rotated.refresh_token;
	pass("refresh token rotates through the real OAuth endpoint");
	// Code reuse intentionally revokes its grant family; test it after identity/refresh.
	const replayed = await token({ ...grant, code_verifier: verifier });
	assert.equal(replayed.status, 400);
	pass("reused authorization code is rejected");
} finally {
	if (refreshToken) {
		const revoked = await request("/oauth/revoke", {
			method: "POST",
			body: new URLSearchParams({ client_id: clientId, token: refreshToken }),
		});
		assert.equal(revoked.status, 200);
		const denied = await token({
			grant_type: "refresh_token",
			refresh_token: refreshToken,
		});
		assert.equal(denied.status, 400);
		pass("test grant is revoked and cannot refresh");
	}
}
await writeFile(
	`${data}/auth-verification.json`,
	`${JSON.stringify({ at: new Date().toISOString(), checks }, null, 2)}\n`,
	{ mode: 0o600 },
);

import { describe, expect, it } from "bun:test";
import { assertBackendEnvironment } from "./config";

const local = {
	NODE_ENV: "development",
	DB_HOST: "127.0.0.1",
	DB_PORT: "3307",
	DB_DATABASE: "tuf_web_test",
};
describe("local E2E database boundary", () => {
	it("accepts only the dedicated local database", () => {
		expect(() => assertBackendEnvironment(local)).not.toThrow();
		for (const override of [
			{ NODE_ENV: "production" },
			{ DB_HOST: "database.example.com" },
			{ DB_PORT: "3306" },
			{ DB_DATABASE: "tuf" },
		])
			expect(() =>
				assertBackendEnvironment({ ...local, ...override }),
			).toThrow();
	});
	it("rejects missing configuration instead of falling back to a server default", () => {
		for (const key of Object.keys(local)) {
			const incomplete: Record<string, string> = { ...local };
			delete incomplete[key];
			expect(() => assertBackendEnvironment(incomplete)).toThrow();
		}
	});
});

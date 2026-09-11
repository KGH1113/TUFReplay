import { createServer } from "node:net";

export async function requireFreePorts(ports: number[]) {
  for (const port of ports) {
    await new Promise<void>((resolve, reject) => {
      const server = createServer();
      server.once("error", () =>
        reject(
          new Error(
            `Port ${port} is already in use. Stop the other E2E session first; use E2E_UI_PORT for UI port conflicts.`,
          ),
        ),
      );
      server.listen(port, "127.0.0.1", () =>
        server.close((error) => (error ? reject(error) : resolve())),
      );
    });
  }
}

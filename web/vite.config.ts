import { fileURLToPath, URL } from "node:url";

import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, normalizePath, type Plugin } from "vite";

const ipcContractDirectory = fileURLToPath(new URL("./vendor/tuf-replay-ipc/", import.meta.url));

/**
 * A new contract needs a new game connection, which only a fresh page makes.
 * Hot-swapping the module would leave the old connection open beside a new one.
 */
function reloadOnIpcContractChange(): Plugin {
  const directory = normalizePath(ipcContractDirectory);
  return {
    name: "tuf-replay:reload-on-ipc-contract-change",
    handleHotUpdate({ file, server }) {
      if (!file.startsWith(directory)) return;
      server.ws.send({ type: "full-reload" });
      return [];
    },
  };
}

const tufApiProxy = {
  "/api/tuf": {
    target: "https://api.tuforums.com",
    changeOrigin: true,
    rewrite: (path: string) => path.replace(/^\/api\/tuf/, ""),
  },
};

export default defineConfig({
  plugins: [reloadOnIpcContractChange(), react(), tailwindcss()],
  build: {
    chunkSizeWarningLimit: 1024,
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
      // The generated contract changes with every `ipc-generate`. Load it from its source folder
      // instead of node_modules, which the dev server does not watch: a stale copy keeps the old
      // contract hash, and the mod rejects it as a contract mismatch.
      "@tuf-replay/ipc": `${ipcContractDirectory}index.js`,
    },
  },
  server: {
    allowedHosts: [
      "tufreplay.impl1113.dev",
      "tufreplay-dev.impl1113.dev",
      "tufreplay-auto.impl1113.dev",
      "guhyeons-macbook-pro.tail234c02.ts.net",
    ],
    proxy: tufApiProxy,
  },
  preview: { proxy: tufApiProxy },
});

import { fileURLToPath, URL } from "node:url";

import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

const tufApiProxy = {
  "/api/tuf": {
    target: "https://api.tuforums.com",
    changeOrigin: true,
    rewrite: (path: string) => path.replace(/^\/api\/tuf/, ""),
  },
};

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    chunkSizeWarningLimit: 1024,
  },
  optimizeDeps: {
    // The generated contract package changes with every `ipc-generate`; pre-bundling would keep
    // serving the previous contract hash, which the mod rejects as a contract mismatch.
    exclude: ["@tuf-replay/ipc"],
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
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

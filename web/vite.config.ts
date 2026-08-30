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
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    allowedHosts: ["tufreplay.impl1113.dev"],
    proxy: tufApiProxy,
  },
  preview: { proxy: tufApiProxy },
});

import { mergeConfig } from "vite";
import base from "./vite.config";

export default mergeConfig(base, {
  define: { "import.meta.env.VITE_TUFREPLAY_BUILD_FLAVOR": JSON.stringify("auto-submission") },
  server: {
    host: "127.0.0.1",
    port: 5189,
    strictPort: true,
    proxy: { "/harness": { target: "http://127.0.0.1:5152", changeOrigin: true } },
  },
});

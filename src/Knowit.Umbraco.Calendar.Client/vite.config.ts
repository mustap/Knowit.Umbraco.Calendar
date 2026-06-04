import { defineConfig } from "vite";

// Builds the backoffice bundle straight into the RCL's wwwroot so `dotnet pack`
// ships the compiled JS as a static web asset. `dist/` is git-ignored — it is a
// build artifact produced from this TypeScript source.
const OUT_DIR =
  "../Knowit.Umbraco.Calendar/wwwroot/App_Plugins/Knowit.Umbraco.Calendar/dist";

export default defineConfig({
  build: {
    lib: {
      entry: "src/index.ts",
      formats: ["es"],
      fileName: () => "index.js",
    },
    outDir: OUT_DIR,
    emptyOutDir: true,
    rollupOptions: {
      // The backoffice provides these at runtime — do not bundle them.
      external: [/^@umbraco/],
    },
  },
});

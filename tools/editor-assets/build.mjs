import { build } from "esbuild";
import { copyFile, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("./", import.meta.url));
const outdir = fileURLToPath(new URL("../../src/EgitimPlatformu.Web/wwwroot/lib/monaco/", import.meta.url));
await mkdir(outdir, { recursive: true });
await build({
    absWorkingDir: root,
    entryPoints: { editor: "editor-entry.js", "editor.worker": "node_modules/monaco-editor/esm/vs/editor/editor.worker.js" },
    outdir,
    bundle: true,
    minify: true,
    format: "esm",
    target: "es2022",
    loader: { ".ttf": "file" },
    assetNames: "[name]-[hash]",
    legalComments: "eof"
});
await copyFile(new URL("node_modules/monaco-editor/LICENSE", import.meta.url), `${outdir}/LICENSE.txt`);
await copyFile(new URL("node_modules/monaco-editor/ThirdPartyNotices.txt", import.meta.url), `${outdir}/ThirdPartyNotices.txt`);

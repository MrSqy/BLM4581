import * as monaco from "monaco-editor/editor/editor.api.js";
import "monaco-editor/editor/contrib/bracketMatching/browser/bracketMatching.js";
import "monaco-editor/editor/contrib/clipboard/browser/clipboard.js";
import "monaco-editor/editor/contrib/comment/browser/comment.js";
import "monaco-editor/editor/contrib/find/browser/findController.js";
import "monaco-editor/editor/contrib/folding/browser/folding.js";
import "monaco-editor/editor/contrib/indentation/browser/indentation.js";
import "monaco-editor/editor/contrib/linesOperations/browser/linesOperations.js";
import "monaco-editor/editor/contrib/multicursor/browser/multicursor.js";
import "monaco-editor/editor/contrib/toggleTabFocusMode/browser/toggleTabFocusMode.js";
import "monaco-editor/languages/definitions/python/register.js";

self.MonacoEnvironment = {
    getWorker() {
        return new Worker(new URL("./editor.worker.js", import.meta.url), { type: "module", name: "monaco-editor" });
    }
};

export { monaco };

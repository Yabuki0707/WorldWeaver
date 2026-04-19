import { analyzeRegion } from "./region_metrics.ts";
import { resolveInputPathArgOrPrompt, resolveLanguageArgOrPrompt } from "./region_cli.ts";
import { getMessages } from "./region_i18n.ts";
import { findRegionFiles, parseRegionFile } from "./region_parser.ts";
import { renderFreeChainOnly } from "./region_render.ts";

// 只关注空闲链结构的专用入口。
async function main(): Promise<void> {
  let lang = "zh";
  try {
    lang = await resolveLanguageArgOrPrompt(process.argv.slice(2));
    const messages = getMessages(lang);
    if (process.argv.includes("--help") || process.argv.includes("-h")) {
      console.log(messages.usageFree);
      console.log(messages.usageFreeHint);
      return;
    }

    const rawArg = process.argv[2];
    const inputPath = await resolveInputPathArgOrPrompt(
      process.argv.slice(2),
      messages.promptInputFreePath,
    );
    if (!inputPath) {
      console.error(messages.invalidPathCancelled);
      process.exitCode = 1;
      return;
    }

    let recursive = !process.argv.includes("--no-recursive");
    let maxChainPreview = 80;
    for (let index = !rawArg || rawArg.startsWith("--") ? 2 : 3; index < process.argv.length; index += 1) {
      const arg = process.argv[index];
      if (arg === "--lang" && index + 1 < process.argv.length) {
        index += 1;
        continue;
      }
      if (arg === "--max-chain-preview" && index + 1 < process.argv.length) {
        maxChainPreview = Math.max(1, Number.parseInt(process.argv[index + 1] ?? "80", 10) || 80);
        index += 1;
        continue;
      }
      if (arg === "--no-recursive") {
        recursive = false;
      }
    }

    const regionFiles = findRegionFiles(inputPath, { recursive });
    const analyses = regionFiles.map((regionFilePath) => analyzeRegion(parseRegionFile(regionFilePath)));
    console.log(renderFreeChainOnly(analyses, lang, maxChainPreview));
  } catch (error) {
    console.error(`${getMessages(lang).freeChainFailed}: ${(error as Error).message}`);
    process.exitCode = 1;
  }
}

void main();

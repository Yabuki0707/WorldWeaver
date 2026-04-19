import { analyzeRegion } from "./region_metrics.ts";
import { resolveInputPathArgOrPrompt, resolveLanguageArgOrPrompt } from "./region_cli.ts";
import { getMessages } from "./region_i18n.ts";
import { findRegionFiles, parseRegionFile } from "./region_parser.ts";
import { renderHumanReadable } from "./region_render.ts";
import { toSerializableAnalysis } from "./region_serialize.ts";
import type { CliOptions } from "./region_types.ts";

// 主入口帮助文本。
function printUsage(lang: string): void {
  const messages = getMessages(lang);
  console.log(messages.usageInspector);
  console.log(messages.usageInspectorHint);
}

// 解析 CLI 参数；这里保持轻量，不引入额外参数库。
async function parseCliArgs(argv: string[], lang: string): Promise<CliOptions | null> {
  const messages = getMessages(lang);
  if (argv.includes("--help") || argv.includes("-h")) {
    printUsage(lang);
    return null;
  }

  const inputPath = await resolveInputPathArgOrPrompt(argv, messages.promptInputAnalyzePath);
  if (!inputPath) {
    console.error(messages.invalidPathCancelled);
    return null;
  }
  let recursive = true;
  let json = false;
  let maxChainPreview = 48;

  for (let index = argv[0] && !argv[0].startsWith("--") ? 1 : 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--lang" && index + 1 < argv.length) {
      index += 1;
      continue;
    }
    if (arg === "--no-recursive") {
      recursive = false;
      continue;
    }
    if (arg === "--json") {
      json = true;
      continue;
    }
    if (arg === "--max-chain-preview" && index + 1 < argv.length) {
      maxChainPreview = Math.max(1, Number.parseInt(argv[index + 1] ?? "48", 10) || 48);
      index += 1;
    }
  }

  return {
    inputPath,
    lang,
    recursive,
    json,
    maxChainPreview,
  };
}

// 主报告入口；支持终端可视化与 JSON 两种输出模式。
async function main(): Promise<void> {
  let lang = "zh";
  try {
    lang = await resolveLanguageArgOrPrompt(process.argv.slice(2));
    const options = await parseCliArgs(process.argv.slice(2), lang);
    if (!options) {
      return;
    }

    const regionFiles = findRegionFiles(options.inputPath, { recursive: options.recursive });
    const analyses = regionFiles.map((regionFilePath) => analyzeRegion(parseRegionFile(regionFilePath)));

    if (options.json) {
      console.log(JSON.stringify(analyses.map(toSerializableAnalysis), null, 2));
      return;
    }

    console.log(renderHumanReadable(analyses, options.lang, options.maxChainPreview));
  } catch (error) {
    console.error(`${getMessages(lang).inspectorFailed}: ${(error as Error).message}`);
    process.exitCode = 1;
  }
}

void main();

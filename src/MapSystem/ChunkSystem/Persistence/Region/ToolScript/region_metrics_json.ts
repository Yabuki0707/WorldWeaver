import { analyzeRegion } from "./region_metrics.ts";
import { resolveInputPathArgOrPrompt, resolveLanguageArgOrPrompt } from "./region_cli.ts";
import { getMessages } from "./region_i18n.ts";
import { findRegionFiles, parseRegionFile } from "./region_parser.ts";
import { toSerializableAnalysis } from "./region_serialize.ts";

// 机器可读导出入口，默认输出压缩后的 JSON 结构。
async function main(): Promise<void> {
  let lang = "zh";
  try {
    lang = await resolveLanguageArgOrPrompt(process.argv.slice(2));
    const messages = getMessages(lang);
    if (process.argv.includes("--help") || process.argv.includes("-h")) {
      console.log(messages.usageJson);
      console.log(messages.usageJsonHint);
      return;
    }

    const inputPath = await resolveInputPathArgOrPrompt(
      process.argv.slice(2),
      messages.promptInputJsonPath,
    );
    if (!inputPath) {
      console.error(messages.exportCancelled);
      process.exitCode = 1;
      return;
    }

    const recursive = !process.argv.includes("--no-recursive");
    const regionFiles = findRegionFiles(inputPath, { recursive });
    const analyses = regionFiles.map((regionFilePath) => analyzeRegion(parseRegionFile(regionFilePath)));
    console.log(JSON.stringify(analyses.map(toSerializableAnalysis), null, 2));
  } catch (error) {
    console.error(`${getMessages(lang).jsonFailed}: ${(error as Error).message}`);
    process.exitCode = 1;
  }
}

void main();

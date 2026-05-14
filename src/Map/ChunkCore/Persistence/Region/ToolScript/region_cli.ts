import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import { getMessages, normalizeLanguageCode, type LanguageCode } from "./region_i18n.ts";

function normalizeInputPath(rawValue: string | undefined): string | null {
  if (!rawValue) {
    return null;
  }

  const trimmed = rawValue.trim();
  if (trimmed.length === 0) {
    return null;
  }

  if (
    (trimmed.startsWith("\"") && trimmed.endsWith("\"")) ||
    (trimmed.startsWith("'") && trimmed.endsWith("'"))
  ) {
    const unquoted = trimmed.slice(1, -1).trim();
    return unquoted.length > 0 ? unquoted : null;
  }

  return trimmed;
}

export function findLanguageArg(argv: string[]): LanguageCode | null {
  for (let index = 0; index < argv.length; index += 1) {
    if (argv[index] === "--lang" && index + 1 < argv.length) {
      return normalizeLanguageCode(argv[index + 1]);
    }
  }
  return null;
}

// 参考 tile_builder.ts 的交互方式：未显式指定语言时，先让用户选择语言。
export async function resolveLanguageArgOrPrompt(argv: string[]): Promise<LanguageCode> {
  if (argv.includes("--help") || argv.includes("-h")) {
    return "zh";
  }

  const argLanguage = findLanguageArg(argv);
  if (argLanguage) {
    return argLanguage;
  }

  const baseMessages = getMessages("zh");
  const rl = createInterface({ input, output });
  try {
    console.log(baseMessages.selectLanguageTitle);
    console.log(baseMessages.selectLanguageZh);
    console.log(baseMessages.selectLanguageZhTw);
    console.log(baseMessages.selectLanguageEn);
    console.log(baseMessages.selectLanguageJa);
    const answer = (await rl.question("> ")).trim();
    if (answer === "2") return "zh_tw";
    if (answer === "3") return "en";
    if (answer === "4") return "ja";
    return "zh";
  } finally {
    rl.close();
  }
}

export async function resolveInputPathArgOrPrompt(argv: string[], promptText: string): Promise<string | null> {
  const firstArg = argv[0];
  if (firstArg && !firstArg.startsWith("--")) {
    return normalizeInputPath(firstArg);
  }

  const rl = createInterface({ input, output });
  try {
    const answer = await rl.question(promptText);
    return normalizeInputPath(answer);
  } finally {
    rl.close();
  }
}

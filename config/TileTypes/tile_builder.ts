import * as fs from "fs";
import * as path from "path";
import * as readline from "readline";

// -----------------------------------------------------------------------------
// 基础配置
// -----------------------------------------------------------------------------

// 输出目录：与当前脚本同目录（assets/tiles）
const TILES_DIR = __dirname;

// -----------------------------------------------------------------------------
// 多语言文案结构
// -----------------------------------------------------------------------------

interface Messages {
  title: string;
  exitHint: string;
  saveHint: string;
  inputHint: string;
  tileName: string;
  tileNameError: string;
  textureId: string;
  textureIdError: string;
  isPassable: string;
  isPassableError: string;
  exit: string;
  created: string;
  fieldLabel: string;
  valueLabel: string;
  statusLabel: string;
  waiting: string;
  editing: string;
  ready: string;
  boolHint: string;
  continueHint: string;
}

// 英文共享文案（默认）
const SHARED_MESSAGES: Messages = {
  title: "=== Tile Builder ===",
  exitHint: "Leave tile name empty to exit",
  saveHint: "Each row is one tile; fields fill downward",
  inputHint: "Submit the current field; invalid input redraws that row",
  tileName: "Name",
  tileNameError: "Use snake_case like deep_sea",
  textureId: "Texture ID",
  textureIdError: "Must be an integer",
  isPassable: "Passable",
  isPassableError: "Use t/f, true/false, y/n",
  exit: "Exited",
  created: "Created: ",
  fieldLabel: "Field",
  valueLabel: "Value",
  statusLabel: "Status",
  waiting: "⏳ Waiting",
  editing: "✍️ Editing",
  ready: "✅ Done",
  boolHint: "t/f/1/0",
  continueHint: "Press Enter to continue...",
};

// 多语言文案（已使用正常字符，不再使用 \uXXXX 转义）
const MESSAGES: Record<string, Messages> = {
  zh: {
    title: "=== Tile Builder ===",
    exitHint: "名称留空即可退出",
    saveHint: "每行代表一个 tile，属性会向下填写",
    inputHint: "输入当前字段后回车，输入错误只刷新当前行",
    tileName: "名称",
    tileNameError: "请使用 snake_case，例如 deep_sea",
    textureId: "纹理ID",
    textureIdError: "必须是整数",
    isPassable: "可通行",
    isPassableError: "请输入 t/f、true/false、y/n、1/0、yeah",
    exit: "已退出",
    created: "已创建: ",
    fieldLabel: "字段",
    valueLabel: "值",
    statusLabel: "状态",
    waiting: "⏳ 待输入",
    editing: "✍️ 输入中",
    ready: "✅ 完成",
    boolHint: "t/f/1/0",
    continueHint: "按回车继续...",
  },
  zh_tw: {
    title: "=== Tile Builder ===",
    exitHint: "名稱留空即可退出",
    saveHint: "每行代表一個 tile，屬性會向下填寫",
    inputHint: "輸入當前欄位後按 Enter，輸入錯誤只刷新當前行",
    tileName: "名稱",
    tileNameError: "請使用 snake_case，例如 deep_sea",
    textureId: "紋理ID",
    textureIdError: "必須是整數",
    isPassable: "可通行",
    isPassableError: "請輸入 t/f、true/false、y/n、1/0、yeah",
    exit: "已退出",
    created: "已建立: ",
    fieldLabel: "欄位",
    valueLabel: "值",
    statusLabel: "狀態",
    waiting: "⏳ 待輸入",
    editing: "✍️ 輸入中",
    ready: "✅ 完成",
    boolHint: "t/f/1/0",
    continueHint: "按 Enter 繼續...",
  },
  en: SHARED_MESSAGES,
  ja: {
    title: "=== Tile Builder ===",
    exitHint: "名前を空欄にすると終了",
    saveHint: "1 行が 1 つの tile で、属性は下に向かって入力します",
    inputHint: "現在の項目を Enter で確定、エラーはその行だけ再描画します",
    tileName: "名前",
    tileNameError: "deep_sea のような snake_case を使ってください",
    textureId: "テクスチャ ID",
    textureIdError: "整数を入力してください",
    isPassable: "通行可",
    isPassableError: "t/f、true/false、y/n、1/0、yeah を入力してください",
    exit: "終了しました",
    created: "作成: ",
    fieldLabel: "項目",
    valueLabel: "値",
    statusLabel: "状態",
    waiting: "⏳ 待機",
    editing: "✍️ 入力中",
    ready: "✅ 済",
    boolHint: "t/f/1/0",
    continueHint: "Enter で続行...",
  },
};

// -----------------------------------------------------------------------------
// 数据结构
// -----------------------------------------------------------------------------

interface TileData {
  tileTypeName: string;
  tileTypeTextureId: number;
  isPassable: boolean;
}

interface TableRow {
  field: string;
  value: string;
  status: string;
}

// 字段名称前缀 emoji（用于提升输入界面的可读性）
const FIELD_EMOJI = {
  tileName: "🧱",
  textureId: "🖼️",
  isPassable: "🚶",
} as const;

// -----------------------------------------------------------------------------
// 基础校验与转换
// -----------------------------------------------------------------------------

function isSnakeCase(name: string): boolean {
  return /^[a-z][a-z0-9_]*$/.test(name);
}

function parseBool(value: string): boolean | null {
  const normalized = value.trim().toLowerCase();
  if (["t", "true", "y", "yes", "1", "yeah"].includes(normalized)) return true;
  if (["f", "false", "n", "no", "0"].includes(normalized)) return false;
  return null;
}

function createTile(tileName: string, textureId: number, isPassable: boolean): TileData {
  return {
    tileTypeName: tileName,
    tileTypeTextureId: textureId,
    isPassable,
  };
}

// -----------------------------------------------------------------------------
// 文件写入
// -----------------------------------------------------------------------------

// 保存单个 tile 配置，并同步追加到输出日志，方便批量录入后回看。
function saveTile(tileData: TileData, lang: string): void {
  const filePath = path.join(TILES_DIR, `${tileData.tileTypeName}.json`);
  fs.writeFileSync(filePath, JSON.stringify(tileData, null, 2), "utf-8");
  console.log(`${MESSAGES[lang].created}${filePath}`);
  logTile(tileData);
}

function logTile(tileData: TileData): void {
  const outputFile = path.join(TILES_DIR, "tile_builder_output.txt");
  const line = `${tileData.tileTypeName} | ${tileData.tileTypeTextureId} | ${tileData.isPassable}\n`;
  fs.appendFileSync(outputFile, line, "utf-8");
}

// -----------------------------------------------------------------------------
// 交互输入
// -----------------------------------------------------------------------------

async function selectLanguage(rl: readline.ReadLine): Promise<string> {
  console.log("language / 语言 / 語言 / 言語:");
  console.log("[1] 简体中文");
  console.log("[2] 繁體中文");
  console.log("[3] English");
  console.log("[4] 日本語");
  return new Promise((resolve) => {
    rl.question("> ", (choice) => {
      const selected = choice.trim();
      if (selected === "1") resolve("zh");
      else if (selected === "2") resolve("zh_tw");
      else if (selected === "4") resolve("ja");
      else resolve("en");
    });
  });
}

function question(rl: readline.ReadLine, prompt: string): Promise<string> {
  return new Promise((resolve) => {
    rl.question(prompt, resolve);
  });
}

// -----------------------------------------------------------------------------
// 文本表格宽度处理（修复中日韩/emoji 导致的列错位）
// -----------------------------------------------------------------------------

// 手动读取下一个 Unicode 码点（兼容低编译目标，不依赖 /u 正则与 \p 字符类）
function readCodePoint(value: string, startIndex: number): { codePoint: number; nextIndex: number; char: string } {
  const first = value.charCodeAt(startIndex);
  const hasNext = startIndex + 1 < value.length;
  if (first >= 0xd800 && first <= 0xdbff && hasNext) {
    const second = value.charCodeAt(startIndex + 1);
    if (second >= 0xdc00 && second <= 0xdfff) {
      const codePoint = ((first - 0xd800) << 10) + (second - 0xdc00) + 0x10000;
      return {
        codePoint,
        nextIndex: startIndex + 2,
        char: value.slice(startIndex, startIndex + 2),
      };
    }
  }

  return {
    codePoint: first,
    nextIndex: startIndex + 1,
    char: value.charAt(startIndex),
  };
}

// 是否为组合附加符号（近似处理）
function isCombiningMark(codePoint: number): boolean {
  return (
    (codePoint >= 0x0300 && codePoint <= 0x036f) ||
    (codePoint >= 0x1ab0 && codePoint <= 0x1aff) ||
    (codePoint >= 0x1dc0 && codePoint <= 0x1dff) ||
    (codePoint >= 0x20d0 && codePoint <= 0x20ff) ||
    (codePoint >= 0xfe20 && codePoint <= 0xfe2f)
  );
}

// 是否为全角字符（中日韩宽字符近似处理）
function isFullWidth(codePoint: number): boolean {
  return (
    (codePoint >= 0x1100 && codePoint <= 0x115f) ||
    (codePoint >= 0x2e80 && codePoint <= 0xa4cf) ||
    (codePoint >= 0xac00 && codePoint <= 0xd7a3) ||
    (codePoint >= 0xf900 && codePoint <= 0xfaff) ||
    (codePoint >= 0xfe10 && codePoint <= 0xfe19) ||
    (codePoint >= 0xfe30 && codePoint <= 0xfe6f) ||
    (codePoint >= 0xff00 && codePoint <= 0xff60) ||
    (codePoint >= 0xffe0 && codePoint <= 0xffe6)
  );
}

// 是否为 emoji（近似处理，覆盖常见 emoji 区段）
function isEmoji(codePoint: number): boolean {
  return (
    (codePoint >= 0x1f300 && codePoint <= 0x1faff) ||
    (codePoint >= 0x2600 && codePoint <= 0x27bf) ||
    codePoint === 0xfe0f
  );
}

// 计算单个 Unicode 字符的显示宽度（终端等宽字体近似规则）
function codePointDisplayWidth(codePoint: number): number {
  // 组合附加符号与变体选择符不占宽度
  if (isCombiningMark(codePoint) || codePoint === 0xfe0f) {
    return 0;
  }

  // emoji 按双宽处理
  if (isEmoji(codePoint)) {
    return 2;
  }

  // 全角字符按双宽处理（中日韩常见字符）
  if (isFullWidth(codePoint)) {
    return 2;
  }

  // 其余按单宽处理
  return 1;
}

// 计算字符串显示宽度（用于表格对齐）
function getDisplayWidth(value: string): number {
  let width = 0;
  let index = 0;

  while (index < value.length) {
    const unit = readCodePoint(value, index);
    width += codePointDisplayWidth(unit.codePoint);
    index = unit.nextIndex;
  }

  return width;
}

// 按“显示宽度”进行裁剪并补齐空格，避免 emoji 和中日韩字符挤乱表格
function truncate(value: string, width: number): string {
  const fullWidth = getDisplayWidth(value);
  if (fullWidth <= width) {
    return value + " ".repeat(width - fullWidth);
  }

  if (width <= 3) {
    let result = "";
    let used = 0;

    let index = 0;
    while (index < value.length) {
      const unit = readCodePoint(value, index);
      const charWidth = codePointDisplayWidth(unit.codePoint);
      if (used + charWidth > width) break;
      result += unit.char;
      used += charWidth;
      index = unit.nextIndex;
    }

    return result + " ".repeat(Math.max(0, width - used));
  }

  const ellipsis = "...";
  const ellipsisWidth = 3;
  const targetWidth = width - ellipsisWidth;
  let result = "";
  let used = 0;

  let index = 0;
  while (index < value.length) {
    const unit = readCodePoint(value, index);
    const charWidth = codePointDisplayWidth(unit.codePoint);
    if (used + charWidth > targetWidth) break;
    result += unit.char;
    used += charWidth;
    index = unit.nextIndex;
  }

  const finalText = result + ellipsis;
  return finalText + " ".repeat(Math.max(0, width - getDisplayWidth(finalText)));
}

function separator(widths: number[]): string {
  return `+${widths.map((width) => "-".repeat(width + 2)).join("+")}+`;
}

function renderCell(value: string, width: number): string {
  return ` ${truncate(value, width)} `;
}

// 每次输入后整屏重绘，模拟“文本表格”输入界面。
function renderTable(
  msg: Messages,
  rows: TableRow[],
  currentPrompt: string,
  inputValue: string,
  error = ""
): void {
  const widths = [18, 24, 26];
  const lines: string[] = [];
  const border = separator(widths);

  lines.push(msg.title);
  lines.push(msg.saveHint);
  lines.push(msg.inputHint);
  lines.push(`${msg.exitHint}\n`);
  lines.push(border);
  lines.push(
    `|${renderCell(msg.fieldLabel, widths[0])}|${renderCell(msg.valueLabel, widths[1])}|${renderCell(
      msg.statusLabel,
      widths[2]
    )}|`
  );
  lines.push(border);

  for (const row of rows) {
    lines.push(
      `|${renderCell(row.field, widths[0])}|${renderCell(row.value, widths[1])}|${renderCell(
        row.status,
        widths[2]
      )}|`
    );
  }

  lines.push(border);
  lines.push(`✍️ : ${currentPrompt}: ${inputValue}`);
  if (error) {
    lines.push(`! ${error}`);
  }

  // 清屏后重绘（ANSI）
  process.stdout.write("\x1Bc");
  process.stdout.write(`${lines.join("\n")}\n`);
}

// 通用字段输入循环：校验失败时仅保留当前字段的草稿和错误状态，再次重绘当前表格。
async function promptField(
  rl: readline.ReadLine,
  msg: Messages,
  rows: TableRow[],
  rowIndex: number,
  promptLabel: string,
  validate: (input: string) => string | null
): Promise<string> {
  let draft = "";
  let error = "";

  while (true) {
    renderTable(msg, rows, promptLabel, draft, error);
    const input = (await question(rl, "> ")).trim();
    const validationError = validate(input);

    if (!validationError) {
      rows[rowIndex].value = input;
      rows[rowIndex].status = msg.ready;
      return input;
    }

    draft = input;
    error = validationError;
    rows[rowIndex].status = `${msg.editing}: ${validationError}`;
  }
}

// 名称字段单独处理，保留“留空直接退出”的行为。
async function promptTileName(rl: readline.ReadLine, msg: Messages, rows: TableRow[]): Promise<string | null> {
  let draft = "";
  let error = "";
  const promptLabel = `${FIELD_EMOJI.tileName} ${msg.tileName}`;

  while (true) {
    renderTable(msg, rows, promptLabel, draft, error);
    const input = (await question(rl, "> ")).trim();

    if (!input) {
      return null;
    }

    if (isSnakeCase(input)) {
      rows[0].value = input;
      rows[0].status = msg.ready;
      return input;
    }

    draft = input;
    error = msg.tileNameError;
    rows[0].status = `${msg.editing}: ${msg.tileNameError}`;
  }
}

// -----------------------------------------------------------------------------
// 主流程
// -----------------------------------------------------------------------------

async function main(): Promise<void> {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  });

  const lang = await selectLanguage(rl);
  const msg = MESSAGES[lang];

  while (true) {
    // 一个 tile 拆成三行，按属性向下录入；
    // 状态列展示“待输入 / 输入中 / 完成 / 错误”。
    const rows: TableRow[] = [
      { field: `${FIELD_EMOJI.tileName} ${msg.tileName}`, value: "", status: msg.editing },
      { field: `${FIELD_EMOJI.textureId} ${msg.textureId}`, value: "", status: msg.waiting },
      { field: `${FIELD_EMOJI.isPassable} ${msg.isPassable}`, value: "", status: `${msg.waiting} (${msg.boolHint})` },
    ];

    const tileName = await promptTileName(rl, msg, rows);
    if (tileName === null) {
      process.stdout.write("\x1Bc");
      console.log(msg.exit);
      break;
    }

    rows[1].status = msg.editing;
    const textureIdInput = await promptField(rl, msg, rows, 1, `${FIELD_EMOJI.textureId} ${msg.textureId}`, (input) => {
      return /^-?\d+$/.test(input) ? null : msg.textureIdError;
    });
    const textureId = parseInt(textureIdInput, 10);

    rows[2].status = msg.editing;
    const passableInput = await promptField(
      rl,
      msg,
      rows,
      2,
      `${FIELD_EMOJI.isPassable} ${msg.isPassable}`,
      (input) => {
        return parseBool(input) === null ? msg.isPassableError : null;
      }
    );
    const parsedPassable = parseBool(passableInput);
    const isPassable = parsedPassable as boolean;
    rows[2].value = String(isPassable);
    rows[2].status = msg.ready;

    renderTable(msg, rows, "OK", "");
    saveTile(createTile(tileName, textureId, isPassable), lang);

    await question(rl, `\n${msg.continueHint}`);
  }

  rl.close();
}

void main();

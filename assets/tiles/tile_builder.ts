import * as fs from "fs";
import * as path from "path";
import * as readline from "readline";

const TILES_DIR = __dirname;

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
  rowLabel: string;
  fieldLabel: string;
  valueLabel: string;
  statusLabel: string;
  waiting: string;
  editing: string;
  ready: string;
  boolHint: string;
  continueHint: string;
}

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
  rowLabel: "Row",
  fieldLabel: "Field",
  valueLabel: "Value",
  statusLabel: "Status",
  waiting: "Waiting",
  editing: "Editing",
  ready: "Done",
  boolHint: "t/f",
  continueHint: "Press Enter to continue...",
};

const MESSAGES: Record<string, Messages> = {
  zh: {
    title: "=== Tile Builder ===",
    exitHint: "\u540d\u79f0\u7559\u7a7a\u5373\u53ef\u9000\u51fa",
    saveHint: "\u6bcf\u884c\u4ee3\u8868\u4e00\u4e2a tile\uff0c\u5c5e\u6027\u4f1a\u5411\u4e0b\u586b\u5199",
    inputHint: "\u8f93\u5165\u5f53\u524d\u5b57\u6bb5\u540e\u56de\u8f66\uff0c\u8f93\u5165\u9519\u8bef\u53ea\u5237\u65b0\u5f53\u524d\u884c",
    tileName: "\u540d\u79f0",
    tileNameError: "\u8bf7\u4f7f\u7528 snake_case\uff0c\u4f8b\u5982 deep_sea",
    textureId: "\u7eb9\u7406ID",
    textureIdError: "\u5fc5\u987b\u662f\u6574\u6570",
    isPassable: "\u53ef\u901a\u884c",
    isPassableError: "\u8bf7\u8f93\u5165 t/f\u3001true/false\u3001y/n",
    exit: "\u5df2\u9000\u51fa",
    created: "\u5df2\u521b\u5efa: ",
    rowLabel: "\u884c",
    fieldLabel: "\u5b57\u6bb5",
    valueLabel: "\u503c",
    statusLabel: "\u72b6\u6001",
    waiting: "\u5f85\u8f93\u5165",
    editing: "\u8f93\u5165\u4e2d",
    ready: "\u5b8c\u6210",
    boolHint: "t/f",
    continueHint: "\u6309\u56de\u8f66\u7ee7\u7eed...",
  },
  en: SHARED_MESSAGES,
  ja: {
    title: "=== Tile Builder ===",
    exitHint: "\u540d\u524d\u3092\u7a7a\u6b04\u306b\u3059\u308b\u3068\u7d42\u4e86",
    saveHint: "1 \u884c\u304c 1 \u3064\u306e tile \u3067\u3001\u5c5e\u6027\u306f\u4e0b\u306b\u5411\u304b\u3063\u3066\u5165\u529b\u3057\u307e\u3059",
    inputHint: "\u73fe\u5728\u306e\u9805\u76ee\u3092 Enter \u3067\u78ba\u5b9a\u3001\u30a8\u30e9\u30fc\u306f\u305d\u306e\u884c\u3060\u3051\u518d\u63cf\u753b\u3057\u307e\u3059",
    tileName: "\u540d\u524d",
    tileNameError: "deep_sea \u306e\u3088\u3046\u306a snake_case \u3092\u4f7f\u3063\u3066\u304f\u3060\u3055\u3044",
    textureId: "\u30c6\u30af\u30b9\u30c1\u30e3ID",
    textureIdError: "\u6574\u6570\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044",
    isPassable: "\u901a\u884c\u53ef",
    isPassableError: "t/f\u3001true/false\u3001y/n \u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044",
    exit: "\u7d42\u4e86\u3057\u307e\u3057\u305f",
    created: "\u4f5c\u6210: ",
    rowLabel: "\u884c",
    fieldLabel: "\u9805\u76ee",
    valueLabel: "\u5024",
    statusLabel: "\u72b6\u614b",
    waiting: "\u5f85\u6a5f",
    editing: "\u5165\u529b\u4e2d",
    ready: "\u5b8c\u4e86",
    boolHint: "t/f",
    continueHint: "Enter \u3067\u7d9a\u884c...",
  },
};

interface TileData {
  tileTypeName: string;
  tileTypeTextureId: number;
  isPassable: boolean;
}

interface TableRow {
  rowNumber: number;
  field: string;
  value: string;
  status: string;
}

function isSnakeCase(name: string): boolean {
  return /^[a-z][a-z0-9_]*$/.test(name);
}

function parseBool(value: string): boolean | null {
  const normalized = value.trim().toLowerCase();
  if (["t", "true", "y", "yes"].includes(normalized)) return true;
  if (["f", "false", "n", "no"].includes(normalized)) return false;
  return null;
}

function createTile(tileName: string, textureId: number, isPassable: boolean): TileData {
  return {
    tileTypeName: tileName,
    tileTypeTextureId: textureId,
    isPassable,
  };
}

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

async function selectLanguage(rl: readline.ReadLine): Promise<string> {
  console.log(
    "language / \u8bed\u8a00 / \u8a00\u8a9e: [1] \u4e2d\u6587 [2] English [3] \u65e5\u672c\u8a9e"
  );
  return new Promise((resolve) => {
    rl.question("> ", (choice) => {
      const selected = choice.trim();
      if (selected === "1") resolve("zh");
      else if (selected === "3") resolve("ja");
      else resolve("en");
    });
  });
}

function question(rl: readline.ReadLine, prompt: string): Promise<string> {
  return new Promise((resolve) => {
    rl.question(prompt, resolve);
  });
}

function truncate(value: string, width: number): string {
  if (value.length <= width) {
    return value.padEnd(width, " ");
  }

  if (width <= 3) {
    return value.slice(0, width);
  }

  return `${value.slice(0, width - 3)}...`;
}

function separator(widths: number[]): string {
  return `+${widths.map((width) => "-".repeat(width + 2)).join("+")}+`;
}

function renderCell(value: string, width: number): string {
  return ` ${truncate(value, width)} `;
}

// 每次输入后整屏重绘，模拟“文本表格”输入界面。
function renderTable(msg: Messages, rows: TableRow[], currentPrompt: string, inputValue: string, error = ""): void {
  const widths = [6, 14, 24, 24];
  const lines: string[] = [];
  const border = separator(widths);

  lines.push(msg.title);
  lines.push(msg.saveHint);
  lines.push(msg.inputHint);
  lines.push(`${msg.exitHint}\n`);
  lines.push(border);
  lines.push(
    `|${renderCell(msg.rowLabel, widths[0])}|${renderCell(msg.fieldLabel, widths[1])}|${renderCell(
      msg.valueLabel,
      widths[2]
    )}|${renderCell(msg.statusLabel, widths[3])}|`
  );
  lines.push(border);

  for (const row of rows) {
    lines.push(
      `|${renderCell(String(row.rowNumber), widths[0])}|${renderCell(row.field, widths[1])}|${renderCell(
        row.value,
        widths[2]
      )}|${renderCell(row.status, widths[3])}|`
    );
  }

  lines.push(border);
  lines.push(`${currentPrompt}: ${inputValue}`);
  if (error) {
    lines.push(`! ${error}`);
  }

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

  while (true) {
    renderTable(msg, rows, msg.tileName, draft, error);
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

async function main(): Promise<void> {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  });

  const lang = await selectLanguage(rl);
  const msg = MESSAGES[lang];
  let rowNumber = 1;

  while (true) {
    // 一个 tile 拆成三行，按属性向下录入；状态列用于显示“待输入 / 输入中 / 完成 / 错误”。
    const rows: TableRow[] = [
      { rowNumber, field: msg.tileName, value: "", status: msg.editing },
      { rowNumber, field: msg.textureId, value: "", status: msg.waiting },
      { rowNumber, field: msg.isPassable, value: "", status: `${msg.waiting} (${msg.boolHint})` },
    ];

    const tileName = await promptTileName(rl, msg, rows);
    if (tileName === null) {
      process.stdout.write("\x1Bc");
      console.log(msg.exit);
      break;
    }

    rows[1].status = msg.editing;
    const textureIdInput = await promptField(rl, msg, rows, 1, msg.textureId, (input) => {
      return /^-?\d+$/.test(input) ? null : msg.textureIdError;
    });
    const textureId = parseInt(textureIdInput, 10);

    rows[2].status = msg.editing;
    const passableInput = await promptField(rl, msg, rows, 2, msg.isPassable, (input) => {
      return parseBool(input) === null ? msg.isPassableError : null;
    });
    const isPassable = parseBool(passableInput) as boolean;
    rows[2].value = String(isPassable);
    rows[2].status = msg.ready;

    renderTable(msg, rows, "OK", "");
    saveTile(createTile(tileName, textureId, isPassable), lang);

    rowNumber += 1;
    await question(rl, `\n${msg.continueHint}`);
  }

  rl.close();
}

main();

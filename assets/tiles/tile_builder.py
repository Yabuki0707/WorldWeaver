import json
import os
import re
from typing import Any

TILES_DIR = os.path.dirname(os.path.abspath(__file__))

# 多语言提示文本
MESSAGES = {
    "zh": {
        "title": "=== 地块生成器 ===",
        "exit_hint": "输入空行结束",
        "tile_name": "地块名称 (str, snake_case): ",
        "tile_name_error": "格式错误，应使用蛇形命名法，如: deep_sea, shallow_sea\n",
        "texture_id": "纹理ID (int): ",
        "texture_id_error": "请输入有效数字",
        "is_passable": "是否可通行 (t/f): ",
        "is_passable_error": "请输入 t/f",
        "exit": "已退出",
        "created": "Created: ",
    },
    "en": {
        "title": "=== Tile Generator ===",
        "exit_hint": "Press empty line to exit",
        "tile_name": "Tile name (str, snake_case): ",
        "tile_name_error": "Invalid format, use snake_case like: deep_sea, shallow_sea\n",
        "texture_id": "Texture ID (int): ",
        "texture_id_error": "Please enter a valid number",
        "is_passable": "Passable (t/f): ",
        "is_passable_error": "Please enter t/f",
        "exit": "Exited",
        "created": "Created: ",
    },
    "ja": {
        "title": "=== タイルジェネレーター ===",
        "exit_hint": "空行で終了",
        "tile_name": "タイル名 (str, snake_case): ",
        "tile_name_error": "形式エラー。snake_caseを使用してください 例: deep_sea, shallow_sea\n",
        "texture_id": "テクスチャID (int): ",
        "texture_id_error": "有効な数値を入力してください",
        "is_passable": "通行可能 (t/f): ",
        "is_passable_error": "t/fを入力してください",
        "exit": "終了",
        "created": "作成: ",
    },
}

# 检测字符串是否符合蛇形命名法（小写字母、数字、下划线）
def is_snake_case(name: str) -> bool:
    return bool(re.match(r'^[a-z][a-z0-9_]*$', name))

# 解析布尔值，支持 t/f/true/false/y/n/yes/no
def parse_bool(value: str) -> bool | None:
    value = value.strip().lower()
    if value in ("t", "true", "y", "yes"):
        return True
    elif value in ("f", "false", "n", "no"):
        return False
    return None

# 创建地块数据字典
def create_tile(tile_name: str, texture_id: int, is_passable: bool) -> dict[str, Any]:
    return {
        "tileTypeName": tile_name,
        "tileTypeTextureId": texture_id,
        "isPassable": is_passable
    }

# 将地块数据保存为 JSON 文件
def save_tile(tile_data: dict[str, Any]):
    file_path = os.path.join(TILES_DIR, f"{tile_data['tileTypeName']}.json")
    with open(file_path, "w", encoding="utf-8") as f:
        json.dump(tile_data, f, indent=2, ensure_ascii=False)
    print(f"{MESSAGES[lang]['created']}{file_path}")
    log_tile(tile_data)

# 记录创建的 tile 信息
def log_tile(tile_data: dict[str, Any]):
    output_file = os.path.join(TILES_DIR, "tile_builder_output.txt")
    with open(output_file, "a", encoding="utf-8") as f:
        f.write(f"{tile_data['tileTypeName']} | {tile_data['tileTypeTextureId']} | {tile_data['isPassable']}\n")

# 选择语言
def select_language() -> str:
    print("language / 语言 / 言語 : [1] 中文 [2] English [3] 日本語")
    while True:
        choice = input("> ").strip()
        if choice == "1":
            return "zh"
        elif choice == "2":
            return "en"
        elif choice == "3":
            return "ja"

# 主函数：交互式创建地块 JSON 文件
def main():
    global lang
    lang = select_language()
    msg = MESSAGES[lang]

    print(f"\n{msg['title']}")
    print(f"{msg['exit_hint']}\n")

    while True:
        tile_name = input(msg["tile_name"]).strip()
        if not tile_name:
            print(msg["exit"])
            break

        if not is_snake_case(tile_name):
            print(msg["tile_name_error"])
            continue

        while True:
            try:
                texture_id = int(input(msg["texture_id"]).strip())
                break
            except ValueError:
                print(msg["texture_id_error"])

        while True:
            is_passable_input = input(msg["is_passable"]).strip()
            result = parse_bool(is_passable_input)
            if result is not None:
                is_passable = result
                break
            print(msg["is_passable_error"])

        tile = create_tile(tile_name, texture_id, is_passable)
        save_tile(tile)
        print()

if __name__ == "__main__":
    main()
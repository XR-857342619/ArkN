# -*- coding: utf-8 -*-
"""
Excel 配置数据检查工具

适用数据格式与 zhou-master 工程的导表规则一致：
  Excel 第 1 行：中文说明
  Excel 第 2 行：字段名
  Excel 第 3 行：字段类型
  Excel 第 4 行起：数据，首列为 Id，Id 以 # 开头表示注释行

支持：
  1. 一次选择一个/多个 xlsx/xlsm，多个文件会合并成同一套配置进行跨文件检查；
  2. 重复 Id、无效跨表引用、基础数据类型检查；
  3. System.Collections.Generic.Dictionary / Data 字段的 JSON 合法性检查；
  4. Tkinter GUI 显示结果，并在 exe 同级目录生成 txt 报告；
  5. 每一项检查可独立开关。

用法：
  GUI:    ExcelConfigChecker.exe
  CLI:    ExcelConfigChecker.exe --cli a.xlsx b.xlsx --out report.txt
"""

from __future__ import annotations

import argparse
import json
import math
import os
import queue
import re
import shutil
import subprocess
import sys
import threading
import traceback
from dataclasses import dataclass, field as dc_field
from datetime import datetime
from typing import Any, Callable, Iterable, Optional

try:
    import openpyxl
except Exception as _openpyxl_import_error:  # pragma: no cover - 仅打包异常时触发
    openpyxl = None
    OPENPYXL_IMPORT_ERROR = str(_openpyxl_import_error)
else:
    OPENPYXL_IMPORT_ERROR = ""


APP_NAME = "Excel 配置数据检查工具"
APP_VERSION = "1.1.0"
REPORT_PREFIX = "ExcelConfigCheckReport"
SUPPORTED_EXCEL_EXTENSIONS = (".xlsx", ".xlsm")

# Type 合法性检查：这些表的 Type 列会被拿来和 type_whitelist.txt 对比。
TYPE_CHECK_TABLES = (
    "UnitData",
    "SkillData",
    "BuffData",
    "ModifyData",
    "BulletData",
)
TYPE_TABLE_RUNTIME_NAMESPACE = {
    "UnitData": "Units",
    "SkillData": "Skills",
    "BuffData": "Buffs",
    "ModifyData": "Modifys",
    "BulletData": "Bullets",
}
TYPE_WHITELIST_FILENAME = "type_whitelist.txt"

# 与 Unity 工程导表规则保持一致：第 1 行说明、第 2 行字段名、第 3 行类型、第 4 行起数据。
HEADER_NAME_ROW = 2
HEADER_TYPE_ROW = 3
DATA_START_ROW = 4

# 防止异常 Excel 的 dimension 过大导致扫描卡死；正常配置表远小于这些上限。
MAX_SCAN_COLUMNS = 1024
MAX_SCAN_ROWS = 200000

CATEGORY_READ = "读取错误"
CATEGORY_DUP = "重复ID"
CATEGORY_REF = "无效引用"
CATEGORY_TYPE = "基础类型"
CATEGORY_TYPE_WHITELIST = "Type合法性"
CATEGORY_JSON = "JSON合法性"
CATEGORY_NOTE = "提示"
CATEGORY_ORDER = [
    CATEGORY_READ,
    CATEGORY_DUP,
    CATEGORY_REF,
    CATEGORY_TYPE,
    CATEGORY_TYPE_WHITELIST,
    CATEGORY_JSON,
    CATEGORY_NOTE,
]

SEVERITY_ERROR = "错误"
SEVERITY_WARNING = "警告"

# 工程中常见表名。类型匹配到这些表名（或形如 XxxData）时，视为跨表引用字段。
KNOWN_PROJECT_TABLES = {
    "CardData",
    "UnitData",
    "SkillData",
    "SkillJsonData",
    "BuffData",
    "BulletData",
    "ModifyData",
    "EffectData",
    "ContractData",
    "SpineData",
    "DungeonLevelData",
    "EventData",
    "RewardData",
    "RelicData",
    "MapTileData",
    "SystemData",
}

INT_TYPES = {
    "int",
    "int32",
    "int64",
    "long",
    "uint",
    "uint32",
    "uint64",
    "ulong",
    "short",
    "int16",
    "ushort",
    "uint16",
    "byte",
    "sbyte",
}
FLOAT_TYPES = {"float", "single", "double", "decimal"}
BOOL_TYPES = {"bool", "boolean"}
STRING_TYPES = {"string", "system.string"}
NO_CHECK_TYPES = {
    "object",
    "system.object",
    "object[]",
    "system.object[]",
}

# Unity 向量/矩形类型：类型 -> (分量数, 是否要求整数分量)
VECTOR_COMPONENTS = {
    "UnityEngine.Vector2": (2, False),
    "UnityEngine.Vector2Int": (2, True),
    "UnityEngine.Vector3": (3, False),
    "UnityEngine.Vector3Int": (3, True),
    "UnityEngine.Rect": (4, False),
}

INT_RE = re.compile(r"^[+-]?\d+$")
FLOAT_RE = re.compile(
    r"^[+-]?(?:(?:\d+(?:\.\d*)?)|(?:\.\d+))(?:[eE][+-]?\d+)?$"
)


# ---------------------------------------------------------------------------
# 基础工具函数
# ---------------------------------------------------------------------------


def get_app_dir() -> str:
    """获取工具所在目录（打包后为 exe 所在目录，源码运行时为脚本目录）。"""
    if getattr(sys, "frozen", False):
        return os.path.dirname(os.path.abspath(sys.executable))
    return os.path.dirname(os.path.abspath(__file__))


def display_file(path: str) -> str:
    """报告中显示文件名：优先显示 上一级目录/文件名，便于区分不同作者目录。"""
    if not path:
        return ""
    path = os.path.abspath(path)
    base = os.path.basename(path)
    parent = os.path.basename(os.path.dirname(path))
    return f"{parent}/{base}" if parent else base


def cell_to_text(value: Any) -> str:
    """把 Excel 单元格值转成与导表器接近的字符串形式。"""
    if value is None:
        return ""
    if isinstance(value, bool):
        return "True" if value else "False"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        if math.isfinite(value) and value.is_integer():
            return str(int(value))
        return format(value, ".15g")
    if isinstance(value, datetime):
        return value.isoformat(sep=" ")
    return str(value)


def is_empty_cell(value: Any) -> bool:
    return value is None or (isinstance(value, str) and value == "")


def shorten(value: str, max_length: int = 100) -> str:
    text = value.replace("\r", "\\r").replace("\n", "\\n").replace("\t", "\\t")
    if len(text) > max_length:
        return text[: max_length - 3] + "..."
    return text


def normalize_base_type(base_type: str) -> str:
    """去掉可空标记，例如 float? -> float、UnitData? -> UnitData。"""
    base_type = (base_type or "").strip()
    while base_type.endswith("?"):
        base_type = base_type[:-1].strip()
    return base_type


def split_array_type(field_type: str) -> tuple[str, bool]:
    field_type = (field_type or "").strip()
    if field_type.endswith("[]"):
        return normalize_base_type(field_type[:-2]), True
    return normalize_base_type(field_type), False


def split_list_value(value: str) -> list[str]:
    """按逗号/换行拆分数组或引用列表，保留空元素用于报错。"""
    text = value.replace("\r\n", "\n").replace("\r", "\n")
    return re.split(r"[,\n]", text)


def is_json_like_field(field_name: str, base_type: str) -> bool:
    """判断字段是否是 Dictionary/Data 这类需要校验 JSON 的字段。"""
    low = normalize_base_type(base_type).replace(" ", "").lower()
    name_low = (field_name or "").strip().lower()

    if "dictionary" in low:
        return True
    if low == "data":
        return True
    if name_low == "data" and (
        low in {"object", "system.object"}
        or low.startswith("system.collections")
        or low.startswith("system.collections.generic")
    ):
        return True
    return False


def looks_like_reference_type(base_type: str) -> bool:
    base_type = normalize_base_type(base_type)
    if not base_type:
        return False
    if base_type in KNOWN_PROJECT_TABLES:
        return True
    # 该项目配置表统一以 Data 结尾；不把 System.* 等 CLR 类型误判为表名。
    if base_type.endswith("Data") and not base_type.startswith("System."):
        return True
    return False


def is_int_text(text: str) -> bool:
    return bool(INT_RE.match((text or "").strip()))


def is_float_text(text: str) -> bool:
    text = (text or "").strip()
    if not FLOAT_RE.match(text):
        return False
    try:
        return math.isfinite(float(text))
    except Exception:
        return False


def is_int_cell(raw_value: Any, text: str) -> bool:
    if isinstance(raw_value, bool):
        return False
    if isinstance(raw_value, int):
        return True
    if isinstance(raw_value, float):
        return math.isfinite(raw_value) and raw_value.is_integer()
    return is_int_text(text)


def is_float_cell(raw_value: Any, text: str) -> bool:
    if isinstance(raw_value, bool):
        return False
    if isinstance(raw_value, (int, float)):
        return math.isfinite(float(raw_value))
    return is_float_text(text)


def is_bool_text(text: str) -> bool:
    return (text or "").strip().lower() in {"0", "1", "true", "false"}


def normalize_paths(paths: Iterable[str]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for path in paths:
        if not path:
            continue
        absolute = os.path.abspath(path)
        key = os.path.normcase(absolute)
        if key in seen:
            continue
        seen.add(key)
        result.append(absolute)
    return result


def make_report_path(out_path: Optional[str] = None) -> str:
    if out_path:
        return os.path.abspath(out_path)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")[:-3]
    return os.path.join(get_app_dir(), f"{REPORT_PREFIX}_{timestamp}.txt")


def write_report(report: "CheckReport", path: str) -> str:
    path = os.path.abspath(path)
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)
    # utf-8-sig：Windows 记事本打开中文不乱码。
    with open(path, "w", encoding="utf-8-sig", newline="") as handle:
        handle.write(report.to_text())
        handle.write("\n")
    return path


# ---------------------------------------------------------------------------
# 数据结构
# ---------------------------------------------------------------------------


@dataclass
class CheckOptions:
    duplicate_id: bool = True
    invalid_reference: bool = True
    basic_type: bool = True
    type_whitelist: bool = True
    dictionary_json: bool = True

    def any_enabled(self) -> bool:
        return any(
            (
                self.duplicate_id,
                self.invalid_reference,
                self.basic_type,
                self.type_whitelist,
                self.dictionary_json,
            )
        )

    def switch_lines(self) -> list[str]:
        return [
            f"[{'开' if self.duplicate_id else '关'}] 重复 Id 检查",
            f"[{'开' if self.invalid_reference else '关'}] 无效引用检查",
            f"[{'开' if self.basic_type else '关'}] 基础数据类型检查",
            f"[{'开' if self.type_whitelist else '关'}] Type 合法性检查",
            f"[{'开' if self.dictionary_json else '关'}] Dictionary/Data JSON 检查",
        ]


@dataclass
class ScanStats:
    files_requested: int = 0
    files_loaded: int = 0
    sheets: int = 0
    rows_seen: int = 0
    valid_rows: int = 0
    ignored_empty_id_rows: int = 0
    ignored_comment_rows: int = 0
    ignored_unit_placeholder_rows: int = 0
    ignored_temp_files: int = 0
    field_values: int = 0
    reference_fields_checked: int = 0
    basic_fields_checked: int = 0
    type_fields_checked: int = 0
    json_fields_checked: int = 0
    enum_fields: int = 0
    enum_type_names: dict[str, int] = dc_field(default_factory=dict)
    missing_target_tables: dict[str, int] = dc_field(default_factory=dict)
    unknown_types: dict[str, int] = dc_field(default_factory=dict)
    type_whitelist_path: str = ""
    type_whitelist_loaded: bool = False
    type_whitelist_counts: dict[str, int] = dc_field(default_factory=dict)
    type_whitelist_missing_sections: list[str] = dc_field(default_factory=list)


@dataclass
class Issue:
    category: str
    severity: str
    file: str = ""
    sheet: str = ""
    row: Optional[int] = None
    row_id: str = ""
    field_name: str = ""
    message: str = ""

    def location(self) -> str:
        parts: list[str] = []
        if self.file:
            parts.append(f'Excel "{display_file(self.file)}"')
        if self.sheet:
            parts.append(f'Sheet "{self.sheet}"')
        if self.row:
            parts.append(f"第 {self.row} 行")
        if self.row_id:
            parts.append(f'Id "{self.row_id}"')
        if self.field_name:
            parts.append(f'字段 "{self.field_name}"')
        return " → ".join(parts)

    def to_line(self) -> str:
        location = self.location()
        prefix = f"[{self.severity}][{self.category}]"
        return f"{prefix} {location}：{self.message}" if location else f"{prefix} {self.message}"


@dataclass
class TableInfo:
    name: str
    id_set: set[str] = dc_field(default_factory=set)
    first_seen: dict[str, tuple[str, int]] = dc_field(default_factory=dict)
    source_files: set[str] = dc_field(default_factory=set)


@dataclass
class FieldRecord:
    file: str
    sheet: str
    row: int
    row_id: str
    field_name: str
    field_type: str
    raw_value: Any
    text_value: str


@dataclass
class CheckReport:
    generated_at: str
    files: list[str]
    options: CheckOptions
    stats: ScanStats
    issues: list[Issue] = dc_field(default_factory=list)
    tool_version: str = APP_VERSION

    def add(self, issue: Issue) -> None:
        self.issues.append(issue)

    @property
    def error_count(self) -> int:
        return sum(1 for issue in self.issues if issue.severity == SEVERITY_ERROR)

    @property
    def warning_count(self) -> int:
        return sum(1 for issue in self.issues if issue.severity == SEVERITY_WARNING)

    @property
    def passed(self) -> bool:
        return self.error_count == 0

    def category_count(self, category: str) -> int:
        return sum(1 for issue in self.issues if issue.category == category)

    def sorted_issues(self) -> list[Issue]:
        file_order = {os.path.normcase(path): index for index, path in enumerate(self.files)}

        def key(issue: Issue) -> tuple:
            try:
                category_index = CATEGORY_ORDER.index(issue.category)
            except ValueError:
                category_index = len(CATEGORY_ORDER)
            return (
                category_index,
                file_order.get(os.path.normcase(issue.file), 999999),
                issue.sheet or "",
                issue.row or 0,
                issue.field_name or "",
                issue.message or "",
            )

        return sorted(self.issues, key=key)

    def summary_lines(self) -> list[str]:
        stats = self.stats
        lines: list[str] = []
        lines.append("=" * 78)
        lines.append("Excel 配置数据检查报告")
        lines.append("=" * 78)
        lines.append(f"生成时间：{self.generated_at}")
        lines.append(f"工具版本：{APP_NAME} v{self.tool_version}")
        lines.append(f"检查文件数：{len(self.files)}")
        lines.append("检查文件：")
        if self.files:
            for index, path in enumerate(self.files, 1):
                lines.append(f"  [{index}] {path}")
        else:
            lines.append("  (未选择文件)")

        lines.append("")
        lines.append("检查项开关：")
        lines.extend("  " + item for item in self.options.switch_lines())

        lines.append("")
        lines.append("扫描统计：")
        lines.append(
            f"  文件：请求 {stats.files_requested} 个，成功加载 {stats.files_loaded} 个；"
            f"Excel 临时文件（~$）跳过 {stats.ignored_temp_files} 个"
        )
        lines.append(f"  Sheet：{stats.sheets} 个")
        lines.append(
            f"  数据行：扫描 {stats.rows_seen} 行，有效配置行 {stats.valid_rows} 行；"
            f"空 Id 跳过 {stats.ignored_empty_id_rows} 行；"
            f"# 注释跳过 {stats.ignored_comment_rows} 行；"
            f"UnitData Id=0 占位跳过 {stats.ignored_unit_placeholder_rows} 行"
        )
        lines.append(
            f"  字段值：{stats.field_values} 个；引用校验 {stats.reference_fields_checked} 个；"
            f"基础类型校验 {stats.basic_fields_checked} 个；Type 校验 {stats.type_fields_checked} 个；"
            f"JSON 校验 {stats.json_fields_checked} 个"
        )
        if self.options.type_whitelist:
            if stats.type_whitelist_loaded:
                lines.append(f"  Type 白名单：{stats.type_whitelist_path}")
                lines.append(
                    "    合法 Type 数量："
                    + "、".join(
                        f"{table}({stats.type_whitelist_counts.get(table, 0)})"
                        for table in TYPE_CHECK_TABLES
                    )
                )
                if stats.type_whitelist_missing_sections:
                    lines.append(
                        "    缺少分区："
                        + "、".join(stats.type_whitelist_missing_sections)
                    )
            else:
                lines.append(
                    f"  Type 白名单：未加载（{stats.type_whitelist_path or '未配置'}）"
                )
        if stats.enum_fields:
            names = "、".join(
                f"{name}({count})"
                for name, count in sorted(
                    stats.enum_type_names.items(), key=lambda kv: (-kv[1], kv[0])
                )[:8]
            )
            lines.append(f"  枚举字段：{stats.enum_fields} 个（值域未校验，例如：{names}）")
        if stats.missing_target_tables:
            lines.append(
                "  未提供目标表："
                + "、".join(
                    f"{name}({count} 个字段)"
                    for name, count in sorted(stats.missing_target_tables.items())
                )
            )
        if stats.unknown_types:
            lines.append(
                "  未识别类型："
                + "、".join(
                    f"{name}({count})"
                    for name, count in sorted(
                        stats.unknown_types.items(), key=lambda kv: (-kv[1], kv[0])
                    )[:12]
                )
            )

        lines.append("")
        lines.append("结果汇总：")
        lines.append(f"  错误：{self.error_count}")
        lines.append(f"  警告：{self.warning_count}")
        lines.append("  分类统计：")
        for category in CATEGORY_ORDER:
            lines.append(f"    {category}：{self.category_count(category)}")
        lines.append(f"  判定：{'通过' if self.passed else '未通过'}")

        lines.append("")
        lines.append("说明：")
        lines.append("  1. 多个 Excel 会合并为同一套配置，Id 与跨表引用在所有所选文件之间统一解析。")
        lines.append("  2. 只选择部分 Excel 时，指向未选表中 Id 的引用会被判为无效引用，请按需选择完整表集。")
        lines.append("  3. 空 Id 行按导表规则跳过，只做计数，不作为错误；# 开头 Sheet/Id 视为注释。")
        lines.append("  4. UnitData 的 Id=0 视为导出器占位行，不参与重复 Id 检查。")
        lines.append("  5. 若引用目标表未包含在所选 Excel 中，该项引用校验会跳过并给出警告。")
        lines.append("  6. Type 合法性检查依据工具同级目录的 type_whitelist.txt；")
        lines.append("     SkillData 的 Type=Json 表示 JsonSkill，属于特殊合法值。")
        lines.append("  7. 枚举值域、字符串内引号/换行等未纳入本工具的基础类型检查。")
        lines.append("  8. 仅支持 .xlsx / .xlsm；旧版 .xls 请先另存为 .xlsx。")
        lines.append("  9. 若单元格是公式且没有缓存值，读取结果可能为空；请先用 Excel 打开并保存一次。")
        return lines

    def detail_lines(self) -> list[str]:
        lines: list[str] = []
        lines.append("-" * 78)
        lines.append("详细检查结果")
        lines.append("-" * 78)
        if not self.issues:
            lines.append("未发现配置问题。")
            return lines

        for category in CATEGORY_ORDER:
            group = [issue for issue in self.sorted_issues() if issue.category == category]
            if not group:
                continue
            lines.append(f"【{category}】{len(group)} 条")
            for issue in group:
                lines.append("  " + issue.to_line())
            lines.append("")
        return lines

    def to_text(self) -> str:
        return "\n".join(self.summary_lines() + [""] + self.detail_lines())


# ---------------------------------------------------------------------------
# Type 白名单配置
# ---------------------------------------------------------------------------


def get_type_whitelist_path() -> str:
    """工具同级目录下的 Type 白名单配置文件。"""
    return os.path.join(get_app_dir(), TYPE_WHITELIST_FILENAME)


def get_bundled_type_whitelist_path() -> str:
    """打包后 PyInstaller 解压目录中的内置白名单（用于首次自动生成）。"""
    if getattr(sys, "frozen", False) and hasattr(sys, "_MEIPASS"):
        return os.path.join(getattr(sys, "_MEIPASS"), TYPE_WHITELIST_FILENAME)
    return os.path.join(get_app_dir(), TYPE_WHITELIST_FILENAME)


def read_text_with_fallback(path: str) -> str:
    with open(path, "rb") as handle:
        data = handle.read()
    for encoding in ("utf-8-sig", "gb18030"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            continue
    return data.decode("utf-8", errors="replace")


def build_minimal_type_whitelist_text() -> str:
    """内置白名单缺失时的最小可用模板。"""
    lines = [
        "###############################################################################",
        "# Excel 配置 Type 合法性列表",
        "#",
        "# 每行一个合法 Type；# 或 ; 开头为注释；[表名] 表示分区。",
        "# 可用工具的“扫描合法 Excel 更新配置...”自动补充。",
        "###############################################################################",
    ]
    for table in TYPE_CHECK_TABLES:
        namespace = TYPE_TABLE_RUNTIME_NAMESPACE[table]
        lines.extend(
            [
                "",
                f"[{table}]",
                f"# 对应 {namespace}.<Type>；请在每个分区内每行填写一个合法 Type。",
            ]
        )
    return "\n".join(lines) + "\n"


def ensure_type_whitelist_file(path: Optional[str] = None) -> str:
    """确保白名单文件存在；不存在时从内置模板复制生成。"""
    path = os.path.abspath(path or get_type_whitelist_path())
    if os.path.isfile(path) and os.path.getsize(path) > 0:
        return path

    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)

    bundled = os.path.abspath(get_bundled_type_whitelist_path())
    if os.path.isfile(bundled) and bundled != path:
        try:
            shutil.copyfile(bundled, path)
            return path
        except Exception:
            pass

    with open(path, "w", encoding="utf-8-sig", newline="\r\n") as handle:
        handle.write(build_minimal_type_whitelist_text())
    return path


def load_type_whitelist(path: str) -> tuple[dict[str, set[str]], set[str], dict[str, list[str]]]:
    """读取白名单。

    返回：(表名 -> 合法 Type 集合, 出现过的分区集合, 表名 -> 重复条目列表)
    支持 UTF-8 BOM / GB18030，方便用户用记事本直接编辑。
    """
    if not os.path.isfile(path):
        raise FileNotFoundError(path)

    text = read_text_with_fallback(path)
    result: dict[str, set[str]] = {table: set() for table in TYPE_CHECK_TABLES}
    found_sections: set[str] = set()
    duplicates: dict[str, list[str]] = {table: [] for table in TYPE_CHECK_TABLES}
    current: Optional[str] = None

    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or line.startswith(";"):
            continue
        if line.startswith("[") and line.endswith("]"):
            section = line[1:-1].strip()
            current = section if section in result else None
            if current is not None:
                found_sections.add(current)
            continue
        if current is None:
            continue

        value = line
        # 兼容 `普通Buff = 备注` 这种写法：等号左侧是 Type，右侧可写说明。
        if "=" in line:
            left, right = line.split("=", 1)
            value = left.strip() if left.strip() else right.strip()
        value = value.strip()
        if not value:
            continue
        if value in result[current]:
            duplicates[current].append(value)
        else:
            result[current].add(value)

    return result, found_sections, duplicates


def scan_types_from_excel(files: Iterable[str]) -> tuple[dict[str, list[str]], list[str]]:
    """扫描一个或多个 Excel 的 5 张配置表，收集其中出现的全部非空 Type。"""
    result: dict[str, list[str]] = {table: [] for table in TYPE_CHECK_TABLES}
    seen: dict[str, set[str]] = {table: set() for table in TYPE_CHECK_TABLES}
    warnings: list[str] = []

    if openpyxl is None:
        return result, [f"openpyxl 加载失败：{OPENPYXL_IMPORT_ERROR}"]

    for path in normalize_paths(files):
        filename = os.path.basename(path)
        if filename.startswith("~$") or "$" in filename:
            warnings.append(f"{filename}：跳过 Excel 临时文件")
            continue
        extension = os.path.splitext(path)[1].lower()
        if extension not in SUPPORTED_EXCEL_EXTENSIONS:
            warnings.append(f"{filename}：不支持的文件格式 {extension or '<无扩展名>'}")
            continue
        if not os.path.isfile(path):
            warnings.append(f"{filename}：文件不存在")
            continue

        workbook = None
        try:
            workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
            for worksheet in workbook.worksheets:
                sheet_name = worksheet.title
                if sheet_name not in TYPE_CHECK_TABLES or sheet_name.startswith("#"):
                    continue
                max_column = worksheet.max_column or 0
                if max_column <= 0:
                    max_column = 1
                scan_columns = min(max_column, MAX_SCAN_COLUMNS)
                rows_iter = worksheet.iter_rows(
                    min_row=1, max_col=scan_columns, values_only=True
                )
                _row1 = next(rows_iter, None)
                field_name_row = next(rows_iter, None)
                _type_row = next(rows_iter, None)
                if field_name_row is None:
                    warnings.append(f"{filename} / {sheet_name}：Sheet 数据不足，已跳过")
                    continue

                type_column: Optional[int] = None
                for column in range(1, scan_columns):
                    if (
                        column < len(field_name_row)
                        and cell_to_text(field_name_row[column]).strip() == "Type"
                    ):
                        type_column = column
                        break
                if type_column is None:
                    warnings.append(f"{filename} / {sheet_name}：未找到 Type 列，已跳过")
                    continue

                for row_values in rows_iter:
                    raw_id = row_values[0] if len(row_values) > 0 else None
                    id_text = cell_to_text(raw_id)
                    if id_text == "" or id_text.startswith("#"):
                        continue
                    if sheet_name == "UnitData" and id_text == "0":
                        continue
                    raw_type = (
                        row_values[type_column]
                        if type_column < len(row_values)
                        else None
                    )
                    type_text = cell_to_text(raw_type).strip()
                    if not type_text:
                        continue
                    if type_text not in seen[sheet_name]:
                        seen[sheet_name].add(type_text)
                        result[sheet_name].append(type_text)
        except Exception as exc:
            warnings.append(f"{filename}：读取失败 {exc}")
        finally:
            if workbook is not None:
                try:
                    workbook.close()
                except Exception:
                    pass

    return result, warnings


@dataclass
class TypeWhitelistMergeResult:
    path: str
    added: dict[str, list[str]] = dc_field(default_factory=dict)
    created_sections: list[str] = dc_field(default_factory=list)

    @property
    def total_added(self) -> int:
        return sum(len(items) for items in self.added.values())


def merge_types_into_config(path: str, scanned: dict[str, list[str]]) -> TypeWhitelistMergeResult:
    """把扫描到的 Type 合并进白名单文件，保留用户原有注释和条目。"""
    path = os.path.abspath(path)
    ensure_type_whitelist_file(path)
    existing, _found_sections, _duplicates = load_type_whitelist(path)
    text = read_text_with_fallback(path)
    lines = text.splitlines()

    additions: dict[str, list[str]] = {}
    for table in TYPE_CHECK_TABLES:
        new_items: list[str] = []
        for item in scanned.get(table, []):
            value = (item or "").strip()
            if value and value not in existing[table] and value not in new_items:
                new_items.append(value)
        additions[table] = new_items

    result = TypeWhitelistMergeResult(path=path)
    if not any(additions.values()):
        return result

    # 找出已有分区范围；用于把新 Type 插到对应分区末尾。
    section_ranges: dict[str, tuple[int, int]] = {}
    current: Optional[str] = None
    start = 0
    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]"):
            if current is not None:
                section_ranges[current] = (start, index)
            current = stripped[1:-1].strip()
            start = index + 1
    if current is not None:
        section_ranges[current] = (start, len(lines))

    inserts: list[tuple[int, list[str], str]] = []
    for table in TYPE_CHECK_TABLES:
        new_items = additions[table]
        if not new_items or table not in section_ranges:
            continue
        section_start, section_end = section_ranges[table]
        insert_at = section_end
        while insert_at > section_start and not lines[insert_at - 1].strip():
            insert_at -= 1
        inserts.append((insert_at, new_items, table))

    # 从后往前插入，避免前面的插入导致后面的索引偏移。
    for insert_at, new_items, table in sorted(
        inserts, key=lambda item: item[0], reverse=True
    ):
        lines[insert_at:insert_at] = new_items
        result.added[table] = new_items

    # 配置文件缺少对应分区时，在文件末尾补分区。
    for table in TYPE_CHECK_TABLES:
        new_items = additions[table]
        if not new_items or table in section_ranges:
            continue
        if lines and lines[-1].strip():
            lines.append("")
        namespace = TYPE_TABLE_RUNTIME_NAMESPACE[table]
        lines.append(f"[{table}]")
        lines.append(
            f"# 对应 {namespace}.<Type>；由“扫描合法 Excel 更新配置”自动补充"
        )
        lines.extend(new_items)
        result.added[table] = new_items
        result.created_sections.append(table)

    with open(path, "w", encoding="utf-8-sig", newline="\r\n") as handle:
        handle.write("\n".join(lines) + "\n")
    return result


# ---------------------------------------------------------------------------
# 检查核心
# ---------------------------------------------------------------------------


def validate_json_field(record: FieldRecord, stats: ScanStats) -> list[Issue]:
    text = (record.text_value or "").strip()
    if not text:
        return []

    duplicate_keys: list[str] = []

    def object_pairs_hook(pairs: list[tuple[str, Any]]) -> dict:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                duplicate_keys.append(key)
            result[key] = value
        return result

    def reject_constant(value: str) -> None:
        raise ValueError(f"不支持的 JSON 常量 {value}")

    stats.json_fields_checked += 1
    try:
        parsed = json.loads(
            text,
            object_pairs_hook=object_pairs_hook,
            parse_constant=reject_constant,
        )
    except Exception as exc:
        return [
            Issue(
                category=CATEGORY_JSON,
                severity=SEVERITY_ERROR,
                file=record.file,
                sheet=record.sheet,
                row=record.row,
                row_id=record.row_id,
                field_name=record.field_name,
                message=f"JSON 解析失败：{exc}；值：{shorten(text)}",
            )
        ]

    issues: list[Issue] = []
    if not isinstance(parsed, dict):
        issues.append(
            Issue(
                category=CATEGORY_JSON,
                severity=SEVERITY_ERROR,
                file=record.file,
                sheet=record.sheet,
                row=record.row,
                row_id=record.row_id,
                field_name=record.field_name,
                message=f"顶层必须是 JSON 对象，实际为 {type(parsed).__name__}；值：{shorten(text)}",
            )
        )
    if duplicate_keys:
        unique_keys = list(dict.fromkeys(duplicate_keys))
        issues.append(
            Issue(
                category=CATEGORY_JSON,
                severity=SEVERITY_WARNING,
                file=record.file,
                sheet=record.sheet,
                row=record.row,
                row_id=record.row_id,
                field_name=record.field_name,
                message="JSON 对象包含重复键："
                + "、".join(unique_keys[:8])
                + (" ..." if len(unique_keys) > 8 else ""),
            )
        )
    return issues


def _basic_issue(record: FieldRecord, message: str) -> Issue:
    return Issue(
        category=CATEGORY_TYPE,
        severity=SEVERITY_ERROR,
        file=record.file,
        sheet=record.sheet,
        row=record.row,
        row_id=record.row_id,
        field_name=record.field_name,
        message=message,
    )


def _validate_numeric_array(record: FieldRecord, base_type: str, is_int: bool) -> list[Issue]:
    parts = split_list_value(record.text_value)
    if any(part.strip() == "" for part in parts):
        return [_basic_issue(record, f"{base_type}[] 数组存在空元素；值：{shorten(record.text_value)}")]
    for index, part in enumerate(parts, 1):
        if is_int:
            if not is_int_text(part):
                return [
                    _basic_issue(
                        record,
                        f"{base_type}[] 第 {index} 个元素不是合法整数（值：{shorten(part.strip())}）",
                    )
                ]
        else:
            if not is_float_text(part):
                return [
                    _basic_issue(
                        record,
                        f"{base_type}[] 第 {index} 个元素不是合法浮点数（值：{shorten(part.strip())}）",
                    )
                ]
    return []


def _validate_vector(record: FieldRecord, type_name: str, components: int, integer_parts: bool) -> list[Issue]:
    parts = record.text_value.split(",")
    if len(parts) != components:
        return [
            _basic_issue(
                record,
                f"{type_name} 需要 {components} 个分量，实际 {len(parts)} 个；值：{shorten(record.text_value)}",
            )
        ]
    for index, part in enumerate(parts, 1):
        if integer_parts:
            ok = is_int_text(part)
            expected = "整数"
        else:
            ok = is_float_text(part)
            expected = "数字"
        if not ok:
            return [
                _basic_issue(
                    record,
                    f"{type_name} 第 {index} 个分量不是合法{expected}（值：{shorten(part.strip())}）",
                )
            ]
    return []


def _validate_vector_array(record: FieldRecord, type_name: str, components: int, integer_parts: bool) -> list[Issue]:
    points = record.text_value.split("#")
    if any(point.strip() == "" for point in points):
        return [_basic_issue(record, f"{type_name}[] 存在空点；值：{shorten(record.text_value)}")]
    for point_index, point in enumerate(points, 1):
        parts = point.split(",")
        if len(parts) != components:
            return [
                _basic_issue(
                    record,
                    f"{type_name}[] 第 {point_index} 个点需要 {components} 个分量，实际 {len(parts)} 个；值：{shorten(point.strip())}",
                )
            ]
        for part_index, part in enumerate(parts, 1):
            if integer_parts:
                ok = is_int_text(part)
                expected = "整数"
            else:
                ok = is_float_text(part)
                expected = "数字"
            if not ok:
                return [
                    _basic_issue(
                        record,
                        f"{type_name}[] 第 {point_index} 个点第 {part_index} 个分量不是合法{expected}（值：{shorten(part.strip())}）",
                    )
                ]
    return []


def validate_basic_field(record: FieldRecord, base_type: str, is_array: bool, stats: ScanStats) -> list[Issue]:
    """根据第 3 行类型检查基础数据；无法识别的类型只做统计，不报错。"""
    low_type = normalize_base_type(base_type)
    low_key = low_type.lower()

    if low_key in STRING_TYPES:
        return []
    if low_key in NO_CHECK_TYPES or (is_array and low_key in {"object", "system.object"}):
        return []

    if low_type.endswith("Enum"):
        stats.enum_fields += 1
        stats.enum_type_names[low_type] = stats.enum_type_names.get(low_type, 0) + 1
        return []

    # 向量/矩形（含数组）
    if low_type in VECTOR_COMPONENTS:
        components, integer_parts = VECTOR_COMPONENTS[low_type]
        stats.basic_fields_checked += 1
        if is_array:
            return _validate_vector_array(record, low_type, components, integer_parts)
        return _validate_vector(record, low_type, components, integer_parts)

    if is_array:
        if low_key in INT_TYPES:
            stats.basic_fields_checked += 1
            return _validate_numeric_array(record, low_type, is_int=True)
        if low_key in FLOAT_TYPES:
            stats.basic_fields_checked += 1
            return _validate_numeric_array(record, low_type, is_int=False)
        if low_key in BOOL_TYPES:
            stats.basic_fields_checked += 1
            parts = split_list_value(record.text_value)
            if any(part.strip() == "" for part in parts):
                return [_basic_issue(record, f"{low_type}[] 数组存在空元素；值：{shorten(record.text_value)}")]
            for index, part in enumerate(parts, 1):
                if not is_bool_text(part):
                    return [
                        _basic_issue(
                            record,
                            f"{low_type}[] 第 {index} 个元素不是合法 bool（只接受 0/1/true/false，值：{shorten(part.strip())}）",
                        )
                    ]
            return []
        # string[] 不检查元素内容；其它数组类型统一归为未识别。
    else:
        if low_key in INT_TYPES:
            stats.basic_fields_checked += 1
            if not is_int_cell(record.raw_value, record.text_value):
                return [_basic_issue(record, f"不是合法整数（值：{shorten(record.text_value)}）")]
            return []
        if low_key in FLOAT_TYPES:
            stats.basic_fields_checked += 1
            if not is_float_cell(record.raw_value, record.text_value):
                return [_basic_issue(record, f"不是合法浮点数（值：{shorten(record.text_value)}）")]
            return []
        if low_key in BOOL_TYPES:
            stats.basic_fields_checked += 1
            if isinstance(record.raw_value, bool):
                return []
            if not is_bool_text(record.text_value):
                return [
                    _basic_issue(
                        record,
                        f"不是合法 bool（只接受 0/1/true/false，值：{shorten(record.text_value)}）",
                    )
                ]
            return []

    stats.unknown_types[low_type or "<空类型>"] = stats.unknown_types.get(low_type or "<空类型>", 0) + 1
    return []


def _references_from_value(value: str, is_array: bool) -> tuple[list[str], bool]:
    if not is_array:
        trimmed = (value or "").strip()
        return ([trimmed] if trimmed else []), False
    parts = split_list_value(value or "")
    has_empty = any(part.strip() == "" for part in parts)
    return [part.strip() for part in parts if part.strip() != ""], has_empty


def validate_reference_field(
    record: FieldRecord,
    base_type: str,
    is_array: bool,
    tables: dict[str, TableInfo],
    stats: ScanStats,
) -> list[Issue]:
    issues: list[Issue] = []
    target = tables.get(base_type)
    if target is None:
        return issues
    refs, has_empty = _references_from_value(record.text_value, is_array)
    stats.reference_fields_checked += 1
    if is_array and has_empty:
        issues.append(
            Issue(
                category=CATEGORY_REF,
                severity=SEVERITY_ERROR,
                file=record.file,
                sheet=record.sheet,
                row=record.row,
                row_id=record.row_id,
                field_name=record.field_name,
                message=f"{base_type}[] 引用列表存在空项；值：{shorten(record.text_value)}",
            )
        )
    for ref in refs:
        if ref not in target.id_set:
            issues.append(
                Issue(
                    category=CATEGORY_REF,
                    severity=SEVERITY_ERROR,
                    file=record.file,
                    sheet=record.sheet,
                    row=record.row,
                    row_id=record.row_id,
                    field_name=record.field_name,
                    message=f'无效 ID "{ref}"（目标表 {base_type} 中不存在）',
                )
            )
    return issues


def run_checks(
    files: Iterable[str],
    options: Optional[CheckOptions] = None,
    progress_callback: Optional[Callable[[str], None]] = None,
    type_whitelist_path: Optional[str] = None,
) -> CheckReport:
    """执行完整检查并返回报告对象。"""
    if options is None:
        options = CheckOptions()
    files = normalize_paths(files)
    report = CheckReport(
        generated_at=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        files=files,
        options=options,
        stats=ScanStats(files_requested=len(files)),
    )

    if openpyxl is None:
        report.add(
            Issue(
                category=CATEGORY_READ,
                severity=SEVERITY_ERROR,
                message=f"openpyxl 加载失败，无法读取 Excel：{OPENPYXL_IMPORT_ERROR}",
            )
        )
        return report

    if not files:
        report.add(
            Issue(
                category=CATEGORY_READ,
                severity=SEVERITY_ERROR,
                message="未选择任何 Excel 文件。",
            )
        )
        return report

    if not options.any_enabled():
        report.add(
            Issue(
                category=CATEGORY_NOTE,
                severity=SEVERITY_WARNING,
                message="所有检查项均已关闭，本次仅扫描并统计 Excel 数据，不执行任何校验。",
            )
        )

    # Type 合法性白名单：在扫描前加载；缺失时自动从内置模板生成到工具同级目录。
    type_whitelist: dict[str, set[str]] = {table: set() for table in TYPE_CHECK_TABLES}
    if options.type_whitelist:
        whitelist_path = os.path.abspath(
            type_whitelist_path or get_type_whitelist_path()
        )
        report.stats.type_whitelist_path = whitelist_path
        try:
            ensure_type_whitelist_file(whitelist_path)
            type_whitelist, found_sections, _duplicates = load_type_whitelist(
                whitelist_path
            )
            report.stats.type_whitelist_loaded = True
            report.stats.type_whitelist_counts = {
                table: len(type_whitelist[table]) for table in TYPE_CHECK_TABLES
            }
            missing_sections = [
                table
                for table in TYPE_CHECK_TABLES
                if table not in found_sections
            ]
            empty_sections = [
                table
                for table in TYPE_CHECK_TABLES
                if table in found_sections and not type_whitelist[table]
            ]
            report.stats.type_whitelist_missing_sections = missing_sections
            if missing_sections:
                report.add(
                    Issue(
                        category=CATEGORY_NOTE,
                        severity=SEVERITY_WARNING,
                        message="Type 白名单缺少分区，已跳过对应表的 Type 合法性检查："
                        + "、".join(missing_sections)
                        + f"。配置文件：{whitelist_path}",
                    )
                )
            if empty_sections:
                report.add(
                    Issue(
                        category=CATEGORY_NOTE,
                        severity=SEVERITY_WARNING,
                        message="Type 白名单分区为空，已跳过对应表的 Type 合法性检查："
                        + "、".join(empty_sections)
                        + f"。配置文件：{whitelist_path}",
                    )
                )
        except Exception as exc:
            report.add(
                Issue(
                    category=CATEGORY_NOTE,
                    severity=SEVERITY_WARNING,
                    message=f"Type 白名单加载失败，已跳过 Type 合法性检查：{exc}",
                )
            )

    tables: dict[str, TableInfo] = {}
    field_records: list[FieldRecord] = []
    need_field_records = options.invalid_reference or options.basic_type

    total_files = len(files)
    for file_index, path in enumerate(files, 1):
        filename = os.path.basename(path)
        if progress_callback:
            progress_callback(f"正在扫描（{file_index}/{total_files}）：{filename}")

        # Excel 打开文件时生成的 ~$xxx.xlsx 临时文件无法读取，按导表器规则跳过。
        if filename.startswith("~$") or "$" in filename:
            report.stats.ignored_temp_files += 1
            continue

        extension = os.path.splitext(path)[1].lower()
        if extension not in SUPPORTED_EXCEL_EXTENSIONS:
            report.add(
                Issue(
                    category=CATEGORY_READ,
                    severity=SEVERITY_ERROR,
                    file=path,
                    message=f"不支持的文件格式 {extension or '<无扩展名>'}，请使用 .xlsx / .xlsm。",
                )
            )
            continue
        if not os.path.isfile(path):
            report.add(
                Issue(
                    category=CATEGORY_READ,
                    severity=SEVERITY_ERROR,
                    file=path,
                    message="文件不存在或不是普通文件。",
                )
            )
            continue

        workbook = None
        try:
            workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
            report.stats.files_loaded += 1
        except Exception as exc:
            report.add(
                Issue(
                    category=CATEGORY_READ,
                    severity=SEVERITY_ERROR,
                    file=path,
                    message=f"Excel 打开失败：{exc}",
                )
            )
            if workbook is not None:
                try:
                    workbook.close()
                except Exception:
                    pass
            continue

        try:
            for worksheet in workbook.worksheets:
                sheet_name = worksheet.title
                if sheet_name.startswith("#"):
                    continue
                report.stats.sheets += 1
                try:
                    table = tables.get(sheet_name)
                    if table is None:
                        table = TableInfo(name=sheet_name)
                        tables[sheet_name] = table
                    table.source_files.add(path)
                    if sheet_name == "UnitData" and "0" not in table.id_set:
                        # UnitData 的 Id=0 是导出器内置占位行，引用 "0" 合法。
                        table.id_set.add("0")
                        table.first_seen["0"] = ("<UnitData 占位行>", 0)

                    max_column = worksheet.max_column or 0
                    if max_column <= 0:
                        max_column = 1
                    scan_columns = min(max_column, MAX_SCAN_COLUMNS)
                    if max_column > MAX_SCAN_COLUMNS:
                        report.add(
                            Issue(
                                category=CATEGORY_NOTE,
                                severity=SEVERITY_WARNING,
                                file=path,
                                sheet=sheet_name,
                                message=f"列数 {max_column} 超过安全上限 {MAX_SCAN_COLUMNS}，仅检查前 {MAX_SCAN_COLUMNS} 列。",
                            )
                        )

                    rows_iter = worksheet.iter_rows(
                        min_row=1, max_col=scan_columns, values_only=True
                    )
                    header_name = next(rows_iter, None)
                    header_type = next(rows_iter, None)
                    if header_name is None or header_type is None:
                        report.add(
                            Issue(
                                category=CATEGORY_NOTE,
                                severity=SEVERITY_WARNING,
                                file=path,
                                sheet=sheet_name,
                                message="Sheet 数据不足 3 行（第 2 行字段名、第 3 行类型），已跳过。",
                            )
                        )
                        continue

                    # 字段名行是第 2 行，类型行是第 3 行；前面 next 拿到的分别是第 1、2 行，这里重新取第 3 行。
                    type_row = next(rows_iter, None)
                    if type_row is None:
                        report.add(
                            Issue(
                                category=CATEGORY_NOTE,
                                severity=SEVERITY_WARNING,
                                file=path,
                                sheet=sheet_name,
                                message="Sheet 缺少第 3 行类型定义，已跳过。",
                            )
                        )
                        continue

                    # 注意：header_name 实际是第 1 行说明，header_type 实际是第 2 行字段名。
                    # 为保持可读性，这里用明确的变量名重新指代。
                    field_name_row = header_type
                    field_type_row = type_row
                    type_column: Optional[int] = None
                    if options.type_whitelist and sheet_name in TYPE_CHECK_TABLES:
                        for column in range(1, scan_columns):
                            if (
                                column < len(field_name_row)
                                and cell_to_text(field_name_row[column]).strip()
                                == "Type"
                            ):
                                type_column = column
                                break
                        if type_column is None:
                            report.add(
                                Issue(
                                    category=CATEGORY_NOTE,
                                    severity=SEVERITY_WARNING,
                                    file=path,
                                    sheet=sheet_name,
                                    message='未找到字段 "Type"（第 2 行），已跳过该 Sheet 的 Type 合法性检查。',
                                )
                            )
                    data_row_number = DATA_START_ROW
                    scanned_rows = 0

                    for row_values in rows_iter:
                        if scanned_rows >= MAX_SCAN_ROWS:
                            report.add(
                                Issue(
                                    category=CATEGORY_NOTE,
                                    severity=SEVERITY_WARNING,
                                    file=path,
                                    sheet=sheet_name,
                                    message=f"数据行超过安全上限 {MAX_SCAN_ROWS}，后续行未检查。",
                                )
                            )
                            break
                        scanned_rows += 1
                        row_number = data_row_number
                        data_row_number += 1
                        report.stats.rows_seen += 1

                        raw_id = row_values[0] if len(row_values) > 0 else None
                        id_text = cell_to_text(raw_id)
                        has_data = any(
                            not is_empty_cell(row_values[column])
                            for column in range(min(len(row_values), scan_columns))
                        )
                        if id_text == "":
                            if has_data:
                                report.stats.ignored_empty_id_rows += 1
                            continue
                        if id_text.startswith("#"):
                            report.stats.ignored_comment_rows += 1
                            continue
                        if sheet_name == "UnitData" and id_text == "0":
                            report.stats.ignored_unit_placeholder_rows += 1
                            continue

                        report.stats.valid_rows += 1
                        first = table.first_seen.get(id_text)
                        if first is None:
                            table.id_set.add(id_text)
                            table.first_seen[id_text] = (path, row_number)
                        elif options.duplicate_id:
                            first_file, first_row = first
                            if first_file == "<UnitData 占位行>":
                                first_location = "UnitData 内置占位行"
                            else:
                                first_location = f"{display_file(first_file)}:{first_row}"
                            report.add(
                                Issue(
                                    category=CATEGORY_DUP,
                                    severity=SEVERITY_ERROR,
                                    file=path,
                                    sheet=sheet_name,
                                    row=row_number,
                                    row_id=id_text,
                                    message=f"重复 ID（首次出现于 {first_location}）",
                                )
                            )

                        # Type 合法性：只检查 Unit/Skill/Buff/Modify/Bullet 五张表的 Type 列。
                        if (
                            options.type_whitelist
                            and type_column is not None
                            and type_whitelist.get(sheet_name)
                        ):
                            raw_type_value = (
                                row_values[type_column]
                                if type_column < len(row_values)
                                else None
                            )
                            type_text = cell_to_text(raw_type_value)
                            report.stats.type_fields_checked += 1
                            stripped_type = type_text.strip()
                            if stripped_type == "":
                                report.add(
                                    Issue(
                                        category=CATEGORY_TYPE_WHITELIST,
                                        severity=SEVERITY_ERROR,
                                        file=path,
                                        sheet=sheet_name,
                                        row=row_number,
                                        row_id=id_text,
                                        field_name="Type",
                                        message="Type 为空，运行时无法反射创建对应子类。",
                                    )
                                )
                            elif type_text != stripped_type:
                                report.add(
                                    Issue(
                                        category=CATEGORY_TYPE_WHITELIST,
                                        severity=SEVERITY_ERROR,
                                        file=path,
                                        sheet=sheet_name,
                                        row=row_number,
                                        row_id=id_text,
                                        field_name="Type",
                                        message=f'Type "{type_text}" 包含首尾空白，运行时将按原值查找而失败。',
                                    )
                                )
                            elif type_text not in type_whitelist[sheet_name]:
                                namespace = TYPE_TABLE_RUNTIME_NAMESPACE[sheet_name]
                                report.add(
                                    Issue(
                                        category=CATEGORY_TYPE_WHITELIST,
                                        severity=SEVERITY_ERROR,
                                        file=path,
                                        sheet=sheet_name,
                                        row=row_number,
                                        row_id=id_text,
                                        field_name="Type",
                                        message=(
                                            f'Type "{type_text}" 不在合法列表中'
                                            f"（{namespace}.{type_text} 不存在）；"
                                            f"如为新增子类，请更新 type_whitelist.txt。"
                                        ),
                                    )
                                )

                        # 第 1 列固定为 Id，不参与字段检查。
                        for column in range(1, min(len(row_values), scan_columns)):
                            field_name = (
                                cell_to_text(field_name_row[column]).strip()
                                if column < len(field_name_row)
                                else ""
                            )
                            field_type = (
                                cell_to_text(field_type_row[column]).strip()
                                if column < len(field_type_row)
                                else ""
                            )
                            if (
                                not field_name
                                or field_name.startswith("#")
                                or not field_type
                            ):
                                continue
                            raw_value = row_values[column]
                            if is_empty_cell(raw_value):
                                continue
                            text_value = cell_to_text(raw_value)
                            report.stats.field_values += 1

                            record = FieldRecord(
                                file=path,
                                sheet=sheet_name,
                                row=row_number,
                                row_id=id_text,
                                field_name=field_name,
                                field_type=field_type,
                                raw_value=raw_value,
                                text_value=text_value,
                            )

                            # JSON 检查与其它检查相互独立，可以在扫描阶段直接完成。
                            if options.dictionary_json:
                                base_type, _ = split_array_type(field_type)
                                if is_json_like_field(field_name, base_type):
                                    for issue in validate_json_field(record, report.stats):
                                        report.add(issue)

                            if need_field_records:
                                # 引用/基础类型检查需要先收集全部表的 Id，随后统一处理。
                                base_type, _ = split_array_type(field_type)
                                if not (
                                    options.dictionary_json
                                    and is_json_like_field(field_name, base_type)
                                ):
                                    field_records.append(record)
                except Exception as exc:
                    report.add(
                        Issue(
                            category=CATEGORY_READ,
                            severity=SEVERITY_ERROR,
                            file=path,
                            sheet=sheet_name,
                            message=f"读取 Sheet 失败：{exc}",
                        )
                    )
        finally:
            try:
                workbook.close()
            except Exception:
                pass

    # 统一处理引用和基础类型检查（此时所有表的 Id 已知）。
    if options.invalid_reference:
        for record in field_records:
            base_type, is_array = split_array_type(record.field_type)
            if is_json_like_field(record.field_name, base_type):
                continue
            if base_type in tables:
                for issue in validate_reference_field(
                    record, base_type, is_array, tables, report.stats
                ):
                    report.add(issue)
            elif looks_like_reference_type(base_type):
                report.stats.missing_target_tables[base_type] = (
                    report.stats.missing_target_tables.get(base_type, 0) + 1
                )

    if options.basic_type:
        for record in field_records:
            base_type, is_array = split_array_type(record.field_type)
            if is_json_like_field(record.field_name, base_type):
                continue
            if base_type in tables or looks_like_reference_type(base_type):
                continue
            for issue in validate_basic_field(record, base_type, is_array, report.stats):
                report.add(issue)

    # 目标表缺失与未知类型统一汇总为警告，避免逐字段刷屏。
    if options.invalid_reference and report.stats.missing_target_tables:
        details = "、".join(
            f"{name}({count} 个字段)"
            for name, count in sorted(report.stats.missing_target_tables.items())
        )
        report.add(
            Issue(
                category=CATEGORY_NOTE,
                severity=SEVERITY_WARNING,
                message="以下引用目标表未包含在所选 Excel 中，已跳过对应引用校验："
                + details
                + "。如需完整校验，请同时选择包含这些表的 Excel。",
            )
        )

    if options.basic_type and report.stats.unknown_types:
        details = "、".join(
            f"{name}({count})"
            for name, count in sorted(
                report.stats.unknown_types.items(), key=lambda kv: (-kv[1], kv[0])
            )[:12]
        )
        if len(report.stats.unknown_types) > 12:
            details += " ..."
        report.add(
            Issue(
                category=CATEGORY_NOTE,
                severity=SEVERITY_WARNING,
                message="以下字段类型未被工具识别，已跳过基础类型检查（可能是自定义类型/拼写错误）："
                + details,
            )
        )

    if progress_callback:
        progress_callback("检查完成，正在生成报告...")

    return report


# ---------------------------------------------------------------------------
# GUI
# ---------------------------------------------------------------------------


class ExcelCheckerApp:
    def __init__(
        self,
        root: Any,
        initial_files: Optional[Iterable[str]] = None,
        initial_options: Optional[CheckOptions] = None,
    ) -> None:
        self.root = root
        self.files: list[str] = []
        self.message_queue: "queue.Queue[tuple]" = queue.Queue()
        self.last_report_path: Optional[str] = None
        self.checking = False

        self.var_duplicate = None
        self.var_reference = None
        self.var_type = None
        self.var_type_whitelist = None
        self.var_json = None

        self._build_style()
        self._build_ui()

        if initial_options is None:
            initial_options = CheckOptions()
        self.var_duplicate.set(initial_options.duplicate_id)
        self.var_reference.set(initial_options.invalid_reference)
        self.var_type.set(initial_options.basic_type)
        self.var_type_whitelist.set(initial_options.type_whitelist)
        self.var_json.set(initial_options.dictionary_json)

        if initial_files:
            self._add_paths(initial_files)

        self.root.after(100, self._poll_queue)

    # ---- 样式与布局 -----------------------------------------------------
    def _build_style(self) -> None:
        from tkinter import ttk

        style = ttk.Style(self.root)
        available = style.theme_names()
        for theme in ("vista", "winnative", "clam", "default"):
            if theme in available:
                try:
                    style.theme_use(theme)
                    break
                except Exception:
                    continue
        base_font = ("Microsoft YaHei UI", 10)
        style.configure(".", font=base_font)
        style.configure("Title.TLabel", font=("Microsoft YaHei UI", 16, "bold"))
        style.configure("Subtitle.TLabel", foreground="#555555")
        style.configure("Section.TLabelframe.Label", font=("Microsoft YaHei UI", 10, "bold"))
        style.configure("Accent.TButton", font=("Microsoft YaHei UI", 10, "bold"))
        style.configure("Status.TLabel", foreground="#444444")
        style.configure("Error.TLabel", foreground="#b71c1c")
        style.configure("Success.TLabel", foreground="#1b5e20")

    def _build_ui(self) -> None:
        import tkinter as tk
        from tkinter import ttk

        self.root.title(f"{APP_NAME} v{APP_VERSION}")
        self.root.geometry("1160x850")
        self.root.minsize(960, 700)

        main = ttk.Frame(self.root, padding=12)
        main.pack(fill=tk.BOTH, expand=True)
        main.columnconfigure(0, weight=1)
        main.rowconfigure(5, weight=1)

        header = ttk.Frame(main)
        header.grid(row=0, column=0, sticky="ew", pady=(0, 8))
        ttk.Label(header, text=APP_NAME, style="Title.TLabel").pack(anchor="w")
        ttk.Label(
            header,
            text=(
                "选择 1 个或多个 Excel；多个文件会合并为一套配置，跨文件检查重复 Id 与引用。"
                "只选部分 Excel 时，指向未选表的 Id 会被视为无效引用。"
            ),
            style="Subtitle.TLabel",
            wraplength=1040,
            justify="left",
        ).pack(anchor="w", pady=(2, 0))

        file_frame = ttk.LabelFrame(main, text="1. 选择 Excel 文件（支持 .xlsx / .xlsm，可多选）", padding=8)
        file_frame.grid(row=1, column=0, sticky="nsew", pady=(0, 8))
        file_frame.columnconfigure(0, weight=1)

        list_container = ttk.Frame(file_frame)
        list_container.grid(row=0, column=0, sticky="nsew")
        list_container.columnconfigure(0, weight=1)
        list_container.rowconfigure(0, weight=1)

        self.file_listbox = tk.Listbox(
            list_container,
            selectmode=tk.EXTENDED,
            height=7,
            activestyle="none",
            font=("Microsoft YaHei UI", 9),
            relief=tk.SOLID,
            borderwidth=1,
        )
        self.file_listbox.grid(row=0, column=0, sticky="nsew")
        file_scroll = ttk.Scrollbar(list_container, orient=tk.VERTICAL, command=self.file_listbox.yview)
        file_scroll.grid(row=0, column=1, sticky="ns")
        self.file_listbox.configure(yscrollcommand=file_scroll.set)

        file_buttons = ttk.Frame(file_frame)
        file_buttons.grid(row=0, column=1, sticky="ns", padx=(8, 0))
        ttk.Button(file_buttons, text="添加文件...", command=self._add_files_dialog).pack(fill=tk.X, pady=(0, 4))
        ttk.Button(file_buttons, text="移除选中", command=self._remove_selected).pack(fill=tk.X, pady=4)
        ttk.Button(file_buttons, text="清空", command=self._clear_files).pack(fill=tk.X, pady=4)

        self.file_count_label = ttk.Label(file_frame, text="已选择 0 个文件", style="Status.TLabel")
        self.file_count_label.grid(row=1, column=0, columnspan=2, sticky="w", pady=(6, 0))

        option_frame = ttk.LabelFrame(main, text="2. 检查项开关（可独立控制）", padding=8)
        option_frame.grid(row=2, column=0, sticky="ew", pady=(0, 8))
        option_frame.columnconfigure(0, weight=1)
        option_frame.columnconfigure(1, weight=1)

        self.var_duplicate = tk.BooleanVar(value=True)
        self.var_reference = tk.BooleanVar(value=True)
        self.var_type = tk.BooleanVar(value=True)
        self.var_type_whitelist = tk.BooleanVar(value=True)
        self.var_json = tk.BooleanVar(value=True)

        ttk.Checkbutton(
            option_frame,
            text="重复 Id 检查（跨文件统一检测同一 Sheet 的重复 Id）",
            variable=self.var_duplicate,
        ).grid(row=0, column=0, sticky="w", padx=(0, 12), pady=2)
        ttk.Checkbutton(
            option_frame,
            text="无效引用检查（表名类型字段的 Id 必须存在）",
            variable=self.var_reference,
        ).grid(row=0, column=1, sticky="w", pady=2)
        ttk.Checkbutton(
            option_frame,
            text="基础类型检查（int/float/bool/数组/Vector/Rect）",
            variable=self.var_type,
        ).grid(row=1, column=0, sticky="w", padx=(0, 12), pady=2)
        ttk.Checkbutton(
            option_frame,
            text="Dictionary/Data JSON 检查（语法与顶层对象）",
            variable=self.var_json,
        ).grid(row=1, column=1, sticky="w", pady=2)
        ttk.Checkbutton(
            option_frame,
            text="Type 合法性检查（Unit/Skill/Buff/Modify/Bullet 的 Type 必须在白名单中）",
            variable=self.var_type_whitelist,
        ).grid(row=2, column=0, columnspan=2, sticky="w", pady=2)

        toggles = ttk.Frame(option_frame)
        toggles.grid(row=0, column=2, rowspan=3, sticky="e")
        ttk.Button(toggles, text="全部开启", command=lambda: self._set_all_options(True)).pack(fill=tk.X, pady=2)
        ttk.Button(toggles, text="全部关闭", command=lambda: self._set_all_options(False)).pack(fill=tk.X, pady=2)

        type_help = ttk.Frame(option_frame)
        type_help.grid(row=3, column=0, columnspan=3, sticky="ew", pady=(7, 0))
        type_help.columnconfigure(1, weight=1)
        ttk.Label(type_help, text="Type 白名单：", style="Status.TLabel").grid(
            row=0, column=0, sticky="w"
        )
        self.type_config_label = ttk.Label(
            type_help,
            text=get_type_whitelist_path(),
            style="Status.TLabel",
        )
        self.type_config_label.grid(row=0, column=1, sticky="w", padx=(2, 8))
        ttk.Button(
            type_help,
            text="扫描合法 Excel 更新配置...",
            command=self._scan_types_dialog,
        ).grid(row=0, column=2, padx=(0, 6))
        ttk.Button(
            type_help,
            text="打开配置文件",
            command=self._open_type_config,
        ).grid(row=0, column=3)
        ttk.Label(
            option_frame,
            text=(
                "帮助：选择一个确认可用的 Excel，工具会扫描其中 UnitData/SkillData/BuffData/"
                "ModifyData/BulletData 的 Type，并合并进右侧白名单文件；也可手动编辑该文件增删。"
            ),
            style="Subtitle.TLabel",
            wraplength=1080,
            justify="left",
        ).grid(row=4, column=0, columnspan=3, sticky="w", pady=(4, 0))

        action_frame = ttk.Frame(main)
        action_frame.grid(row=3, column=0, sticky="ew", pady=(0, 8))
        action_frame.columnconfigure(2, weight=1)

        self.start_button = ttk.Button(
            action_frame,
            text="开始检查",
            style="Accent.TButton",
            command=self._start_check,
        )
        self.start_button.grid(row=0, column=0, sticky="w")
        self.progress = ttk.Progressbar(action_frame, mode="indeterminate", length=180)
        self.progress.grid(row=0, column=1, sticky="w", padx=(10, 10))
        self.status_label = ttk.Label(
            action_frame,
            text="就绪。请选择 Excel 文件后点击“开始检查”。",
            style="Status.TLabel",
        )
        self.status_label.grid(row=0, column=2, sticky="w")

        result_frame = ttk.LabelFrame(main, text="3. 检查结果", padding=8)
        result_frame.grid(row=5, column=0, sticky="nsew")
        result_frame.columnconfigure(0, weight=1)
        result_frame.rowconfigure(0, weight=1)

        self.result_text = tk.Text(
            result_frame,
            wrap=tk.WORD,
            font=("Microsoft YaHei UI", 9),
            relief=tk.SOLID,
            borderwidth=1,
            padx=6,
            pady=6,
            state=tk.DISABLED,
        )
        self.result_text.grid(row=0, column=0, sticky="nsew")
        result_scroll = ttk.Scrollbar(result_frame, orient=tk.VERTICAL, command=self.result_text.yview)
        result_scroll.grid(row=0, column=1, sticky="ns")
        self.result_text.configure(yscrollcommand=result_scroll.set)
        self.result_text.tag_configure("heading", font=("Microsoft YaHei UI", 11, "bold"), foreground="#0d47a1")
        self.result_text.tag_configure("info", foreground="#333333")
        self.result_text.tag_configure("error", foreground="#b71c1c")
        self.result_text.tag_configure("warning", foreground="#e65100")
        self.result_text.tag_configure("success", foreground="#1b5e20", font=("Microsoft YaHei UI", 10, "bold"))
        self.result_text.tag_configure("muted", foreground="#666666")

        bottom = ttk.Frame(main)
        bottom.grid(row=6, column=0, sticky="ew", pady=(8, 0))
        bottom.columnconfigure(0, weight=1)
        self.report_path_label = ttk.Label(
            bottom,
            text="报告将保存到工具同级目录。",
            style="Status.TLabel",
        )
        self.report_path_label.grid(row=0, column=0, sticky="w")
        ttk.Button(bottom, text="复制报告路径", command=self._copy_report_path).grid(row=0, column=1, padx=(8, 0))
        ttk.Button(bottom, text="打开报告目录", command=self._open_report_dir).grid(row=0, column=2, padx=(8, 0))

        self._update_file_count()

    # ---- 文件列表 -------------------------------------------------------
    def _add_files_dialog(self) -> None:
        from tkinter import filedialog

        paths = filedialog.askopenfilenames(
            title="选择 Excel 文件（可多选）",
            filetypes=[
                ("Excel 文件", "*.xlsx *.xlsm"),
                ("Excel 97-2003（不支持）", "*.xls"),
                ("所有文件", "*.*"),
            ],
        )
        if paths:
            self._add_paths(paths)

    def _add_paths(self, paths: Iterable[str]) -> None:
        added = 0
        existing = {os.path.normcase(path) for path in self.files}
        for path in paths:
            absolute = os.path.abspath(path)
            key = os.path.normcase(absolute)
            if key in existing:
                continue
            self.files.append(absolute)
            self.file_listbox.insert("end", absolute)
            existing.add(key)
            added += 1
        self._update_file_count()
        if added:
            self.status_label.configure(text=f"已添加 {added} 个文件。")

    def _remove_selected(self) -> None:
        selected = list(self.file_listbox.curselection())
        if not selected:
            return
        for index in reversed(selected):
            self.file_listbox.delete(index)
            del self.files[index]
        self._update_file_count()

    def _clear_files(self) -> None:
        self.files.clear()
        self.file_listbox.delete(0, "end")
        self._update_file_count()

    def _update_file_count(self) -> None:
        self.file_count_label.configure(text=f"已选择 {len(self.files)} 个文件")

    # ---- Type 白名单帮助 ------------------------------------------------
    def _open_path(self, path: str) -> None:
        if os.name == "nt":
            os.startfile(path)  # type: ignore[attr-defined]
        elif sys.platform == "darwin":
            subprocess.Popen(["open", path])
        else:
            subprocess.Popen(["xdg-open", path])

    def _open_type_config(self) -> None:
        try:
            path = ensure_type_whitelist_file()
            self.type_config_label.configure(text=path)
            self._open_path(path)
        except Exception as exc:
            from tkinter import messagebox

            messagebox.showerror("打开配置文件失败", str(exc))

    def _scan_types_dialog(self) -> None:
        from tkinter import filedialog, messagebox

        if self.checking:
            return
        path = filedialog.askopenfilename(
            title="选择一个合法的 Excel 配置文件（用于扫描其中全部 Type）",
            filetypes=[
                ("Excel 文件", "*.xlsx *.xlsm"),
                ("Excel 97-2003（不支持）", "*.xls"),
                ("所有文件", "*.*"),
            ],
        )
        if not path:
            return

        try:
            scanned, warnings = scan_types_from_excel([path])
            total = sum(len(items) for items in scanned.values())
            if total == 0:
                messagebox.showwarning(
                    "未找到 Type",
                    "所选 Excel 的 UnitData/SkillData/BuffData/ModifyData/BulletData 中"
                    "没有找到非空 Type。请确认选择的是有效的配置 Excel。",
                )
                return

            result = merge_types_into_config(get_type_whitelist_path(), scanned)
            self.type_config_label.configure(text=result.path)

            lines = [f"扫描文件：{path}", ""]
            for table in TYPE_CHECK_TABLES:
                if scanned[table]:
                    lines.append(
                        f"{table}：扫描 {len(scanned[table])} 个，"
                        f"新增 {len(result.added.get(table, []))} 个"
                    )
            lines.append("")
            lines.append(f"新增 Type 总数：{result.total_added}")
            lines.append(f"配置文件：{result.path}")
            if result.created_sections:
                lines.append("新建分区：" + "、".join(result.created_sections))
            if warnings:
                lines.append("")
                lines.append("提示：")
                for warning in warnings[:5]:
                    lines.append("  " + warning)
                if len(warnings) > 5:
                    lines.append(f"  ... 其余 {len(warnings) - 5} 条见 Console")
                for warning in warnings:
                    print("[Type 配置扫描] " + warning)

            messagebox.showinfo("Type 配置已更新", "\n".join(lines))
            self.status_label.configure(
                text=f"Type 白名单已更新，新增 {result.total_added} 个 Type。"
            )
        except Exception as exc:
            messagebox.showerror("更新 Type 配置失败", str(exc))

    # ---- 选项与检查 -----------------------------------------------------
    def _set_all_options(self, value: bool) -> None:
        self.var_duplicate.set(value)
        self.var_reference.set(value)
        self.var_type.set(value)
        self.var_type_whitelist.set(value)
        self.var_json.set(value)

    def _gather_options(self) -> CheckOptions:
        return CheckOptions(
            duplicate_id=bool(self.var_duplicate.get()),
            invalid_reference=bool(self.var_reference.get()),
            basic_type=bool(self.var_type.get()),
            type_whitelist=bool(self.var_type_whitelist.get()),
            dictionary_json=bool(self.var_json.get()),
        )

    def _start_check(self) -> None:
        from tkinter import messagebox

        if self.checking:
            return
        if not self.files:
            messagebox.showwarning("未选择文件", "请先添加至少一个 Excel 文件。")
            return

        options = self._gather_options()
        if not options.any_enabled():
            if not messagebox.askyesno(
                "未启用检查项",
                "所有检查项都已关闭，仍然要扫描并生成统计报告吗？",
            ):
                return

        self.checking = True
        self.start_button.configure(state="disabled")
        self.progress.start(12)
        self.status_label.configure(text="正在检查，请稍候...")
        self._set_result_text("正在扫描 Excel，请稍候...\n", "muted")
        self.report_path_label.configure(text="正在生成报告...")

        files_snapshot = list(self.files)
        thread = threading.Thread(
            target=self._worker,
            args=(files_snapshot, options),
            daemon=True,
        )
        thread.start()

    def _worker(self, files: list[str], options: CheckOptions) -> None:
        try:
            report = run_checks(
                files,
                options,
                progress_callback=lambda message: self.message_queue.put(("progress", message)),
            )
            report_path = make_report_path()
            try:
                write_report(report, report_path)
                self.message_queue.put(("done", report, report_path, None))
            except Exception as exc:
                self.message_queue.put(
                    ("done", report, None, f"报告写入失败：{exc}")
                )
        except Exception:
            self.message_queue.put(("fatal", traceback.format_exc()))

    def _poll_queue(self) -> None:
        try:
            while True:
                message = self.message_queue.get_nowait()
                kind = message[0]
                if kind == "progress":
                    self.status_label.configure(text=message[1])
                elif kind == "done":
                    _, report, report_path, error = message
                    self._on_check_done(report, report_path, error)
                elif kind == "fatal":
                    self._on_fatal(message[1])
        except queue.Empty:
            pass
        self.root.after(100, self._poll_queue)

    def _on_check_done(self, report: CheckReport, report_path: Optional[str], error: Optional[str]) -> None:
        from tkinter import messagebox

        self.checking = False
        self.progress.stop()
        self.start_button.configure(state="normal")
        self.last_report_path = report_path
        self._render_report(report)

        if error:
            self.status_label.configure(text=error, style="Error.TLabel")
            self.report_path_label.configure(text=error)
            messagebox.showerror("报告保存失败", error)
            return

        if report.passed:
            self.status_label.configure(
                text=f"检查通过：0 个错误，{report.warning_count} 个警告。",
                style="Success.TLabel",
            )
        else:
            self.status_label.configure(
                text=f"检查完成：{report.error_count} 个错误，{report.warning_count} 个警告。",
                style="Error.TLabel",
            )
        if report_path:
            self.report_path_label.configure(text=f"报告已保存：{report_path}")

    def _on_fatal(self, trace: str) -> None:
        from tkinter import messagebox

        self.checking = False
        self.progress.stop()
        self.start_button.configure(state="normal")
        self._set_result_text("检查过程中发生未处理异常：\n\n" + trace, "error")
        self.status_label.configure(text="检查失败，请查看异常信息。", style="Error.TLabel")
        messagebox.showerror("检查失败", "检查过程中发生未处理异常，详情已显示在结果区。")

    # ---- 结果显示 -------------------------------------------------------
    def _set_result_text(self, text: str, tag: str = "info") -> None:
        self.result_text.configure(state="normal")
        self.result_text.delete("1.0", "end")
        self.result_text.insert("end", text, tag)
        self.result_text.configure(state="disabled")

    def _render_report(self, report: CheckReport) -> None:
        self.result_text.configure(state="normal")
        self.result_text.delete("1.0", "end")

        summary = "\n".join(report.summary_lines())
        self.result_text.insert("end", summary + "\n", "info")

        self.result_text.insert("end", "\n" + "-" * 78 + "\n", "muted")
        self.result_text.insert("end", "详细检查结果\n", "heading")
        self.result_text.insert("end", "-" * 78 + "\n", "muted")

        if not report.issues:
            self.result_text.insert("end", "未发现配置问题。\n", "success")
        else:
            for category in CATEGORY_ORDER:
                group = [issue for issue in report.sorted_issues() if issue.category == category]
                if not group:
                    continue
                self.result_text.insert("end", f"【{category}】{len(group)} 条\n", "heading")
                for issue in group:
                    tag = "error" if issue.severity == SEVERITY_ERROR else "warning"
                    self.result_text.insert("end", "  " + issue.to_line() + "\n", tag)
                self.result_text.insert("end", "\n")

        if self.last_report_path:
            self.result_text.insert("end", f"报告文件：{self.last_report_path}\n", "muted")

        self.result_text.configure(state="disabled")
        self.result_text.see("1.0")

    # ---- 底部按钮 -------------------------------------------------------
    def _copy_report_path(self) -> None:
        if not self.last_report_path:
            return
        try:
            self.root.clipboard_clear()
            self.root.clipboard_append(self.last_report_path)
            self.status_label.configure(text="报告路径已复制到剪贴板。")
        except Exception as exc:
            self.status_label.configure(text=f"复制失败：{exc}", style="Error.TLabel")

    def _open_report_dir(self) -> None:
        directory = (
            os.path.dirname(self.last_report_path)
            if self.last_report_path
            else get_app_dir()
        )
        try:
            if os.name == "nt":
                os.startfile(directory)  # type: ignore[attr-defined]
            elif sys.platform == "darwin":
                subprocess.Popen(["open", directory])
            else:
                subprocess.Popen(["xdg-open", directory])
        except Exception as exc:
            self.status_label.configure(text=f"打开目录失败：{exc}", style="Error.TLabel")


def enable_windows_dpi_awareness() -> None:
    if os.name != "nt":
        return
    try:
        import ctypes

        ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


def run_gui(initial_files: Optional[Iterable[str]] = None, options: Optional[CheckOptions] = None) -> int:
    import tkinter as tk

    enable_windows_dpi_awareness()
    root = tk.Tk()
    ExcelCheckerApp(root, initial_files=initial_files, initial_options=options)
    root.mainloop()
    return 0


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def safe_print(*args: Any) -> None:
    try:
        if sys.stdout is not None:
            print(*args)
    except Exception:
        pass


def run_cli(args: argparse.Namespace) -> int:
    options = CheckOptions(
        duplicate_id=not args.no_dup,
        invalid_reference=not args.no_ref,
        basic_type=not args.no_type,
        type_whitelist=not args.no_type_whitelist,
        dictionary_json=not args.no_json,
    )
    report = run_checks(
        args.files,
        options,
        progress_callback=lambda message: safe_print("[进度] " + message),
        type_whitelist_path=args.type_config,
    )
    report_path = None
    try:
        report_path = write_report(report, make_report_path(args.out))
    except Exception as exc:
        safe_print(f"[报告写入失败] {exc}")

    safe_print(report.to_text())
    if report_path:
        safe_print("")
        safe_print("报告已保存：" + report_path)

    return 0 if report.passed else 1


def run_import_types_cli(files: list[str], config_path: Optional[str] = None) -> int:
    scanned, warnings = scan_types_from_excel(files)
    total = sum(len(items) for items in scanned.values())
    for warning in warnings:
        safe_print("[警告] " + warning)
    if total == 0:
        safe_print("未扫描到任何非空 Type，未修改配置文件。")
        return 1

    result = merge_types_into_config(config_path or get_type_whitelist_path(), scanned)
    safe_print("扫描到的 Type：")
    for table in TYPE_CHECK_TABLES:
        if scanned[table]:
            safe_print(
                f"  {table}: {len(scanned[table])} 个，新增 {len(result.added.get(table, []))} 个"
            )
    safe_print(f"新增 Type 总数：{result.total_added}")
    safe_print(f"配置文件：{result.path}")
    if result.created_sections:
        safe_print("新建分区：" + "、".join(result.created_sections))
    return 0


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=APP_NAME + " v" + APP_VERSION,
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "示例：\n"
            "  ExcelConfigChecker.exe\n"
            "  ExcelConfigChecker.exe --cli a.xlsx b.xlsx --out report.txt\n"
            "  ExcelConfigChecker.exe --cli a.xlsx --no-json --no-type\n"
        ),
    )
    parser.add_argument("files", nargs="*", help="可选：启动 GUI 时预加载的 Excel 文件，或 --cli 模式要检查的文件。")
    parser.add_argument("--cli", action="store_true", help="命令行模式：不启动 GUI，直接检查并输出报告。")
    parser.add_argument("--out", help="命令行模式报告输出路径；默认保存到工具同级目录。")
    parser.add_argument("--no-dup", action="store_true", help="关闭重复 Id 检查。")
    parser.add_argument("--no-ref", action="store_true", help="关闭无效引用检查。")
    parser.add_argument("--no-type", action="store_true", help="关闭基础数据类型检查。")
    parser.add_argument(
        "--no-type-whitelist",
        action="store_true",
        help="关闭 Unit/Skill/Buff/Modify/Bullet 的 Type 合法性检查。",
    )
    parser.add_argument("--no-json", action="store_true", help="关闭 Dictionary/Data JSON 检查。")
    parser.add_argument(
        "--type-config",
        metavar="PATH",
        help="指定 Type 白名单配置文件；默认使用工具同级目录的 type_whitelist.txt。",
    )
    parser.add_argument(
        "--import-types",
        nargs="+",
        metavar="XLSX",
        help="扫描一个或多个合法 Excel，将其中的 Type 合并进白名单配置后退出。",
    )
    parser.add_argument("--version", action="version", version=f"{APP_NAME} v{APP_VERSION}")
    return parser


def main(argv: Optional[list[str]] = None) -> int:
    parser = build_arg_parser()
    args = parser.parse_args(argv)
    if args.import_types:
        return run_import_types_cli(args.import_types, args.type_config)
    if args.cli:
        return run_cli(args)

    options = CheckOptions(
        duplicate_id=not args.no_dup,
        invalid_reference=not args.no_ref,
        basic_type=not args.no_type,
        type_whitelist=not args.no_type_whitelist,
        dictionary_json=not args.no_json,
    )
    try:
        return run_gui(initial_files=args.files, options=options)
    except Exception:
        trace = traceback.format_exc()
        try:
            from tkinter import messagebox

            messagebox.showerror("启动失败", trace)
        except Exception:
            safe_print(trace)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

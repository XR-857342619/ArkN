#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
make_filelist.py —— 生成 Assets/StreamingAssets/filelist.txt

用途：filelist.txt 只被 **安卓首启拷贝** 使用
（Init.cs 里 CopyOnFirstLaunch 包在 #if UNITY_ANDROID 里；
 桌面端 StreamingAssetsCopyUtility 检测到 hotfix 路径 == StreamingAssets 会直接跳过，连清单都不读）。

因此清单固定为「安卓需要的内容」：
  - 列出 StreamingAssets 下所有文件
  - **排除 arkfx/**（Windows 专用：D3D11 shader + DXT 贴图，安卓用不上，多拷 311MB）
  - 排除 filelist.txt 自身与 .meta

用法：
    python Tools/make_filelist.py
"""
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SA = os.path.join(ROOT, "Assets", "StreamingAssets")

# 只在 Windows 构建中使用的目录，安卓首启不需要
EXCLUDE_TOP_DIRS = {"arkfx"}


def main():
    if not os.path.isdir(SA):
        print("找不到目录: " + SA)
        return 1

    out = []
    for dp, dns, fns in os.walk(SA):
        rel_dir = os.path.relpath(dp, SA).replace("\\", "/")
        if rel_dir.split("/")[0] in EXCLUDE_TOP_DIRS:
            dns[:] = []
            continue
        for f in fns:
            if f == "filelist.txt" or f.endswith(".meta"):
                continue
            rel = (rel_dir + "/" + f) if rel_dir != "." else f
            out.append(rel.replace("\\", "/"))
    out.sort()

    path = os.path.join(SA, "filelist.txt")
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out) + "\n")

    print(f"写出 {path}")
    print(f"  共 {len(out)} 条，其中 arkfx_android="
          f"{sum(1 for x in out if x.startswith('arkfx_android/'))}"
          f"（已排除 arkfx/ 的 Windows 专用包）")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

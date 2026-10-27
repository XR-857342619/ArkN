#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
make_filelist.py —— 按构建平台生成 Assets/StreamingAssets/filelist.txt

背景：ArkFx 有两套平台相关的特效包
    arkfx/          Windows（D3D11 shader + DXT 贴图）
    arkfx_android/  Android（GLES3/Vulkan shader + ETC2/ASTC 贴图）
两套 CAB 名相同、不能同时加载，打包时也只该带其中一套。
而 StreamingAssetsCopyUtility 会按 filelist.txt 把文件拷到 persistentDataPath，
若两份都列进去，安卓安装包和首启拷贝都会多出 300MB。

用法：
    python Tools/make_filelist.py --platform windows
    python Tools/make_filelist.py --platform android
    python Tools/make_filelist.py                 # 不筛平台，全部列出（等价于 Unity 菜单）
"""
import argparse
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SA = os.path.join(ROOT, "Assets", "StreamingAssets")

EXCLUDE_DIRS = {
    "windows": ["arkfx_android"],
    "android": ["arkfx"],
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--platform", choices=["windows", "android", "all"], default="all")
    args = ap.parse_args()

    if not os.path.isdir(SA):
        print("找不到目录: " + SA)
        return 1

    skip = EXCLUDE_DIRS.get(args.platform, [])
    out = []
    for dp, dns, fns in os.walk(SA):
        rel_dir = os.path.relpath(dp, SA).replace("\\", "/")
        top = rel_dir.split("/")[0]
        if top in skip:
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

    print(f"platform={args.platform}  排除={skip or '无'}")
    print(f"写出 {path}")
    print(f"  共 {len(out)} 条，其中 arkfx={sum(1 for x in out if x.startswith('arkfx/'))}，"
          f"arkfx_android={sum(1 for x in out if x.startswith('arkfx_android/'))}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

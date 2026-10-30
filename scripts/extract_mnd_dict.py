#!/usr/bin/env python3
"""從《美華軍語辭典.pdf》提取結構化英中軍語對照字典。

提取內容：
1. 全稱涉軍詞彙（英中對照）
2. 軍事縮寫詞彙（縮寫 -> 全稱 -> 中文）
3. 輸出結構化 JSON 檔至 dict/mnd_military_terms.json，供臺灣繁化轉換腳本與術語門禁使用。
"""

from __future__ import annotations

import json
import os
import re
import sys
import time
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PDF_PATH = ROOT / "美華軍語辭典.pdf"
OUT_PATH = ROOT / "dict" / "mnd_military_terms.json"


def extract_mnd_terms() -> dict:
    if not PDF_PATH.exists():
        print(f"ERROR: {PDF_PATH} 不存在", file=sys.stderr)
        sys.exit(1)

    t0 = time.time()
    with open(PDF_PATH, "rb") as f:
        data = f.read()

    # 1. 提取所有 /ToUnicode CMap
    cmaps: dict[int, dict[int, str]] = {}
    for m in re.finditer(rb"/ToUnicode\s+(\d+)\s+0\s+R", data):
        obj_num = int(m.group(1))
        if obj_num in cmaps:
            continue
        obj_tag = f"{obj_num} 0 obj".encode()
        pos = data.find(obj_tag)
        if pos == -1:
            continue
        s_start = data.find(b"stream", pos)
        s_end = data.find(b"endstream", s_start)
        stream = data[s_start + 6:s_end].strip()
        try:
            decomp = zlib.decompress(stream)
        except Exception:
            continue
        cmap: dict[int, str] = {}
        for block in re.finditer(rb"beginbfchar\s*(.*?)\s*endbfchar", decomp, re.S):
            for line in block.group(1).splitlines():
                line = line.strip()
                if not line:
                    continue
                parts = re.findall(rb"<([0-9a-fA-F]+)>", line)
                if len(parts) >= 2:
                    src = int(parts[0], 16)
                    dst = bytes.fromhex(parts[1].decode()).decode("utf-16-be", errors="ignore")
                    cmap[src] = dst
        for block in re.finditer(rb"beginbfrange\s*(.*?)\s*endbfrange", decomp, re.S):
            for line in block.group(1).splitlines():
                line = line.strip()
                if not line:
                    continue
                parts = re.findall(rb"<([0-9a-fA-F]+)>", line)
                if len(parts) == 3:
                    s = int(parts[0], 16)
                    e = int(parts[1], 16)
                    dst_start = int(parts[2], 16)
                    for i in range(e - s + 1):
                        cmap[s + i] = (dst_start + i).to_bytes(2, "big").decode("utf-16-be", errors="ignore")
        cmaps[obj_num] = cmap

    main_cmap = cmaps.get(1892, {})
    if not main_cmap:
        # Fallback to largest cmap
        main_cmap = max(cmaps.values(), key=len)

    def decode_hex_chars(h_bytes: bytes) -> str:
        s = h_bytes.decode()
        return "".join([main_cmap.get(int(s[i:i + 4], 16), "") for i in range(0, len(s), 4)])

    terms: dict[str, str] = {}
    abbrs: dict[str, dict[str, str]] = {}

    p = 0
    while True:
        p = data.find(b"stream", p)
        if p == -1:
            break
        e = data.find(b"endstream", p)
        s_data = data[p + 6:e].strip()
        p = e + 9
        try:
            decomp = zlib.decompress(s_data)
        except Exception:
            continue
        if b"Tj" not in decomp and b"TJ" not in decomp:
            continue

        line_blocks = re.split(rb"(?:-?[\d\.]+\s+-[\d\.]+\s+T[dD]|T\*|ET)", decomp)
        for block in line_blocks:
            parts = re.split(rb"/Span\s*<<\s*/ActualText\s*<FEFF00200020>\s*>>\s*BDC", block)
            if len(parts) >= 2:
                left_block = parts[0]
                right_block = parts[1]

                # 英文詞（左側）
                left_words = []
                for sp in re.split(rb"/Span\s*<<\s*/ActualText\s*<FEFF0020>\s*>>\s*BDC", left_block):
                    chars = []
                    for m in re.finditer(rb"<([0-9a-fA-F]+)>", sp):
                        chars.append(decode_hex_chars(m.group(1)))
                    w = "".join(chars).replace("\x01", "").strip()
                    if w:
                        left_words.append(w)

                # 中文譯名（右側）
                right_chars = []
                for m in re.finditer(rb"<([0-9a-fA-F]+)>", right_block):
                    right_chars.append(decode_hex_chars(m.group(1)))
                zh_text = "".join(right_chars).replace("\x01", "").strip()

                left_text = " ".join(left_words).strip()
                if left_text and zh_text:
                    if "─" in left_text:
                        aparts = left_text.split("─", 1)
                        abbr = aparts[0].strip()
                        full = aparts[1].strip()
                        if abbr:
                            abbrs[abbr] = {"full": full, "zh": zh_text}
                    else:
                        if left_text not in terms:
                            terms[left_text] = zh_text

    result = {
        "metadata": {
            "source": "美華軍語辭典.pdf",
            "extracted_terms": len(terms),
            "extracted_abbrs": len(abbrs),
            "generated_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        },
        "terms": terms,
        "abbrs": abbrs,
    }

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(OUT_PATH, "w", encoding="utf-8", newline="\n") as out:
        json.dump(result, out, ensure_ascii=False, indent=2)

    t1 = time.time()
    print(f"成功提取 {len(terms)} 條全稱軍語與 {len(abbrs)} 條軍事縮寫，儲存至 {OUT_PATH} (耗時 {t1 - t0:.2f} 秒)")
    return result


if __name__ == "__main__":
    extract_mnd_terms()

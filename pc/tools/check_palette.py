"""C15: nenhuma cor fora da paleta do AC 15 no código da UI do firmware.

Uso: python pc/tools/check_palette.py firmware/src
Procura literais 0xRRGGBB e #rrggbb (as fontes geradas em src/fonts ficam de fora).
"""
import pathlib
import re
import sys

PALETTE = {
    "141413", "2b2a27", "45423c", "faf9f5", "b0aea5", "7a786f",
    "e8e6dc", "d97757", "8fa874", "6a9bcc", "000000",
}
COLOR = re.compile(r"(?:0x|#)([0-9a-fA-F]{6})\b")


def main(root: str) -> int:
    bad = []
    files = [p for p in pathlib.Path(root).rglob("*") if p.suffix in {".c", ".cpp", ".h"} and "fonts" not in p.parts]
    for path in files:
        for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            for m in COLOR.finditer(line):
                if m.group(1).lower() not in PALETTE:
                    bad.append(f"{path}:{n}: {m.group(0)}")
    used = {m.group(1).lower() for p in files for m in COLOR.finditer(p.read_text(encoding="utf-8"))}
    if not files or not used:
        print("nenhuma cor encontrada: o caminho está certo?")
        return 1
    for b in bad:
        print("fora da paleta:", b)
    print(f"{len(files)} arquivos, {len(used)} cores distintas, {len(bad)} fora da paleta")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "firmware/src"))

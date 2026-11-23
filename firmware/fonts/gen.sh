#!/usr/bin/env bash
# Gera as fontes LVGL usadas pela UI (Poppins e Lora, licença OFL) em ../src/fonts.
# Uso: bash firmware/fonts/gen.sh   (precisa de node/npx e internet)
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p ttf ../src/fonts
G=https://raw.githubusercontent.com/google/fonts/main/ofl
for f in notosanssymbols2/NotoSansSymbols2-Regular.ttf poppins/Poppins-Regular.ttf poppins/Poppins-Medium.ttf poppins/Poppins-SemiBold.ttf "lora/Lora%5Bwght%5D.ttf"; do
  out="ttf/$(basename "${f//%5B/[}" | sed 's/%5D/]/')"
  [ -f "$out" ] || curl -fsSL "$G/$f" -o "$out"
done
LATIN=0x20-0x7E,0xA0-0xFF
CHECK=(--font ttf/NotoSansSymbols2-Regular.ttf -r 0x2713)  # o ✓ de "renovada ✓"
conv() { npx --yes lv_font_conv@1.5.3 --no-compress --bpp 4 --format lvgl --font "ttf/$1" -r "$2" "${@:5}" --size "$3" --lv-font-name "$4" -o "../src/fonts/$4.c"; }
conv "Lora[wght].ttf"        0x20-0x7E,0xA0-0xFF 22 font_lora_22
conv Poppins-SemiBold.ttf    0x25,0x2D,0x30-0x39 26 font_num_26
conv Poppins-Medium.ttf      $LATIN 12 font_label_12
conv Poppins-Regular.ttf     $LATIN 12 font_text_12 "${CHECK[@]}"
conv Poppins-Regular.ttf     $LATIN 11 font_text_11 "${CHECK[@]}"

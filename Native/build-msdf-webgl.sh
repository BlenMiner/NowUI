#!/usr/bin/env bash
# Builds the WebGL nowui-msdf plugin as a single relocatable wasm object.
#
# Symbol strategy (a naive build bundles FreeType + HarfBuzz + Brotli + bzip2
# as strong globals, which collide with Unity's player libraries at wasm-ld
# time and fail every WebGL player build):
#   - HarfBuzz: NOT bundled. Unity's TextRenderingModule exports the full hb
#     API; our hb_* imports resolve against it at player link time.
#   - FreeType: bundled (Unity localizes most of its copy, so it is not
#     linkable), built minimal — no brotli/bzip2/png — and every strong
#     symbol Unity's archives ALSO export is renamed with a nowui_ prefix
#     via the force-included nowui-ft-renames.h (committed, deterministic).
#   - Brotli/bzip2/png: not compiled at all.
#
# Runs in two environments:
#   - Locally on Windows against a Unity install: tools come from Unity's
#     bundled Emscripten, and a hygiene gate diffs the merged object's strong
#     globals against every strong export of Unity's release module archives
#     (weak/COMDAT symbols dedupe fine and are ignored). On new overlap —
#     e.g. after a Unity upgrade — the gate appends to nowui-ft-renames.h
#     and rebuilds once; it fails only if overlap remains.
#   - On CI with an activated emsdk (emcc on PATH): no Unity archives exist,
#     so the gate degrades to asserting that every symbol listed in
#     nowui-ft-renames.h really was renamed and that no hb/brotli/bz2/png
#     symbols are defined.
#
# Inputs (all optional, default to local layout):
#   MSDF_ATLAS_GEN_SOURCE, FREETYPE_SOURCE, HARFBUZZ_SOURCE — checkouts
#   NOWUI_MSDF_TARGET — where to install the merged object
#   UNITY_WEBGL_BUILDTOOLS — Unity's WebGLSupport/BuildTools (enables the gate)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
ATLAS="${MSDF_ATLAS_GEN_SOURCE:-$ROOT/msdf-atlas-gen}"
MSDFGEN="$ATLAS/msdfgen"
FT="${FREETYPE_SOURCE:-$ROOT/freetype}"
HB="${HARFBUZZ_SOURCE:-$ROOT/harfbuzz}"
PLUGIN="$ROOT/../Assets/NowUI/Plugins/Native/nowui-msdf"
OUT="$ROOT/build-msdf-webgl"
TARGET="${NOWUI_MSDF_TARGET:-$ROOT/../Assets/NowUI/Plugins/WebGL/nowui-msdf.bc}"
RENAMES="$ROOT/nowui-ft-renames.h"

# Tool resolution: an activated emsdk (CI) puts emcc on PATH; otherwise use
# Unity's bundled Emscripten. emcc is invoked via python there because the
# .bat wrapper routes arguments through cmd.exe, which mangles angle
# brackets (-DFT_CONFIG_MODULES_H=<...>).
UNITY_BT="${UNITY_WEBGL_BUILDTOOLS:-/c/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools}"
if command -v emcc >/dev/null 2>&1; then
    EMCC_CMD=(emcc)
    LLVM_BIN="$(dirname "$(command -v emcc)")/../bin"
    WASM_LD="$(command -v wasm-ld || echo "$LLVM_BIN/wasm-ld")"
    NM="$(command -v llvm-nm || echo "$LLVM_BIN/llvm-nm")"
elif [[ -d "$UNITY_BT/Emscripten" ]]; then
    EMCC_CMD=("$UNITY_BT/Emscripten/python/python.exe" "$UNITY_BT/Emscripten/emscripten/emcc.py")
    WASM_LD="$UNITY_BT/Emscripten/llvm/wasm-ld.exe"
    NM="$UNITY_BT/Emscripten/llvm/llvm-nm.exe"
    export EM_CONFIG="$(cygpath -w "$UNITY_BT/Emscripten/.emscripten" 2>/dev/null || echo "$UNITY_BT/Emscripten/.emscripten")"
else
    echo "No emcc on PATH and no Unity Emscripten at $UNITY_BT" >&2
    exit 1
fi

mkdir -p "$OUT"
touch "$RENAMES"

CXX_SOURCES=(
    "$PLUGIN/nowui_msdf.cpp"
    "$MSDFGEN"/core/*.cpp
    "$MSDFGEN/ext/import-font.cpp"
    "$ATLAS/msdf-atlas-gen/Charset.cpp"
    "$ATLAS/msdf-atlas-gen/FontGeometry.cpp"
    "$ATLAS/msdf-atlas-gen/GlyphGeometry.cpp"
    "$ATLAS/msdf-atlas-gen/GridAtlasPacker.cpp"
    "$ATLAS/msdf-atlas-gen/Padding.cpp"
    "$ATLAS/msdf-atlas-gen/RectanglePacker.cpp"
    "$ATLAS/msdf-atlas-gen/TightAtlasPacker.cpp"
    "$ATLAS/msdf-atlas-gen/Workload.cpp"
    "$ATLAS/msdf-atlas-gen/bitmap-blit.cpp"
    "$ATLAS/msdf-atlas-gen/charset-parser.cpp"
    "$ATLAS/msdf-atlas-gen/glyph-generators.cpp"
    "$ATLAS/msdf-atlas-gen/size-selectors.cpp"
    "$ATLAS/msdf-atlas-gen/utf8.cpp"
)

FT_SOURCES=(
    "$FT/src/base/ftsystem.c"
    "$FT/src/base/ftinit.c"
    "$FT/src/base/ftdebug.c"
    "$FT/src/base/ftbase.c"
    "$FT/src/base/ftbbox.c"
    "$FT/src/base/ftglyph.c"
    "$FT/src/base/ftbitmap.c"
    "$FT/src/base/ftmm.c"
    "$FT/src/truetype/truetype.c"
    "$FT/src/cff/cff.c"
    "$FT/src/sfnt/sfnt.c"
    "$FT/src/smooth/smooth.c"
    "$FT/src/autofit/autofit.c"
    "$FT/src/psaux/psaux.c"
    "$FT/src/psnames/psnames.c"
    "$FT/src/pshinter/pshinter.c"
    "$FT/src/gzip/ftgzip.c"
)

compile() {
    local src=$1; shift
    local obj="$OUT/$(basename "$src" | sed 's/\.[^.]*$//').o"
    if [[ -f "$obj" && "$obj" -nt "$src" && "$obj" -nt "$RENAMES" ]]; then
        return
    fi
    echo "  $(basename "$src")"
    "${EMCC_CMD[@]}" "$@" "$src" -o "$obj"
}

build_all() {
    local common=(
        -c -O3
        -fno-threadsafe-statics
        -ffunction-sections -fdata-sections
        -DNDEBUG
        -include "$RENAMES"
        -I"$FT/include"
    )
    local cxx=(
        "${common[@]}"
        -std=c++17 -fno-rtti
        # Native wasm exception handling: Unity 6 WebAssembly 2023 builds use
        # -fwasm-exceptions; objects compiled with the legacy emscripten JS EH
        # fail the player link with undefined __cxa_find_matching_catch_*.
        -fwasm-exceptions
        -DNOWUI_MSDF_DISABLE_FILE_API
        -DMSDFGEN_PUBLIC=
        -DMSDF_ATLAS_PUBLIC=
        -DMSDF_ATLAS_NO_ARTERY_FONT
        -I"$MSDFGEN"
        -I"$ATLAS"
        -I"$HB/src"
        -I"$PLUGIN"
    )
    local ft=(
        "${common[@]}"
        -fwasm-exceptions
        -DFT2_BUILD_LIBRARY
        "-DFT_CONFIG_MODULES_H=<nowui-ftmodule.h>"
        -I"$ROOT"
    )

    echo "compiling msdfgen/atlas/plugin (C++)..."
    for src in "${CXX_SOURCES[@]}"; do
        compile "$src" "${cxx[@]}"
    done

    echo "compiling freetype (C)..."
    for src in "${FT_SOURCES[@]}"; do
        compile "$src" "${ft[@]}"
    done

    echo "merging..."
    "$WASM_LD" -r "$OUT"/*.o -o "$OUT/nowui-msdf.bc"
    "$NM" "$OUT/nowui-msdf.bc" | awk '$2=="T" || $2=="D" {print $3}' | sort -u > "$OUT/plugin-strong-exports.txt"
}

unity_strong_exports() {
    local cache="$OUT/unity-strong-exports.txt"
    if [[ ! -f "$cache" ]]; then
        for a in "$UNITY_BT/lib/modules"/*.a; do
            "$NM" "$a" 2>/dev/null
        done | awk '$2=="T" || $2=="D" {print $3}' | sort -u > "$cache"
    fi
    echo "$cache"
}

build_all

if [[ -d "$UNITY_BT/lib/modules" ]]; then
    echo "hygiene gate: diffing against Unity's player archives..."
    OVERLAP=$(comm -12 "$OUT/plugin-strong-exports.txt" "$(unity_strong_exports)")
    if [[ -n "$OVERLAP" ]]; then
        COUNT=$(echo "$OVERLAP" | wc -l)
        echo "renaming $COUNT newly-colliding symbols and rebuilding..."
        if [[ ! -s "$RENAMES" ]]; then
            {
                echo "/* Generated by build-msdf-webgl.sh: every strong symbol this plugin's"
                echo " * bundled FreeType would export that Unity's WebGL player archives ALSO"
                echo " * export gets a nowui_ prefix, so both copies link side by side."
                echo " * Regenerated automatically when the hygiene gate finds new overlap. */"
            } > "$RENAMES"
        fi
        echo "$OVERLAP" | awk '{printf "#define %s nowui_%s\n", $1, $1}' >> "$RENAMES"
        build_all
        OVERLAP=$(comm -12 "$OUT/plugin-strong-exports.txt" "$(unity_strong_exports)")
    fi
    if [[ -n "$OVERLAP" ]]; then
        echo "FAILED: symbols still colliding with Unity's player archives:" >&2
        echo "$OVERLAP" | head -20 >&2
        exit 1
    fi
else
    echo "hygiene gate (CI mode): verifying committed renames were applied..."
    UNRENAMED=$(grep -o "^#define [A-Za-z0-9_]*" "$RENAMES" | awk '{print $2}' | sort -u |
        comm -12 - "$OUT/plugin-strong-exports.txt")
    if [[ -n "$UNRENAMED" ]]; then
        echo "FAILED: symbols listed in nowui-ft-renames.h are still exported:" >&2
        echo "$UNRENAMED" | head -20 >&2
        exit 1
    fi
fi

echo "verifying..."
API=$(grep -c "^nowui_msdf_\|^nowui_compile_" "$OUT/plugin-strong-exports.txt" || true)
BUNDLED=$(grep -c "^hb_\|^Brotli\|^BZ2\|^png_" "$OUT/plugin-strong-exports.txt" || true)
echo "  nowui plugin API exports: $API"
echo "  bundled hb/brotli/bz2/png exports: $BUNDLED (want 0)"
if [[ "$API" == "0" || "$BUNDLED" != "0" ]]; then
    echo "FAILED symbol check" >&2
    exit 1
fi

mkdir -p "$(dirname "$TARGET")"
cp "$OUT/nowui-msdf.bc" "$TARGET"
echo "installed -> $TARGET"

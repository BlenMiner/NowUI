#pragma once

#if defined(_WIN32) && !defined(__EMSCRIPTEN__)
#define NOWUI_MSDF_EXPORT __declspec(dllexport)
#elif defined(__EMSCRIPTEN__)
#define NOWUI_MSDF_EXPORT __attribute__((used, visibility("default")))
#else
#define NOWUI_MSDF_EXPORT __attribute__((visibility("default")))
#endif

#define NOWUI_MSDF_OK 0
#define NOWUI_MSDF_ERROR 1
#define NOWUI_MSDF_BUFFER_TOO_SMALL 2

extern "C" {

typedef struct NowUIMsdfMetrics {
    float em_size;
    float line_height;
    float ascender;
    float descender;
    float underline_y;
    float underline_thickness;
} NowUIMsdfMetrics;

typedef struct NowUIMsdfGlyph {
    unsigned int unicode;
    float advance;
    float plane_left;
    float plane_bottom;
    float plane_right;
    float plane_top;
    float atlas_left;
    float atlas_bottom;
    float atlas_right;
    float atlas_top;
} NowUIMsdfGlyph;

typedef struct NowUIMsdfAtlasInfo {
    int width;
    int height;
    int glyph_count;
    int atlas_byte_count;
    float size;
    float distance_range;
    NowUIMsdfMetrics metrics;
} NowUIMsdfAtlasInfo;

typedef struct NowUIColorGlyph {
    unsigned int unicode;
    unsigned int glyph_index;
    float advance;
    float plane_left;
    float plane_bottom;
    float plane_right;
    float plane_top;
    float atlas_left;
    float atlas_bottom;
    float atlas_right;
    float atlas_top;
} NowUIColorGlyph;

typedef struct NowUIColorAtlasInfo {
    int width;
    int height;
    int glyph_count;
    int atlas_byte_count;
    float size;
    float line_height;
    float ascender;
    float descender;
} NowUIColorAtlasInfo;

NOWUI_MSDF_EXPORT int nowui_compile_font(
    const char *font_path,
    const char *image_path,
    const char *json_path,
    int size,
    int pixel_range,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT int nowui_compile_font_from_memory(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowUIMsdfGlyph *glyphs,
    int glyph_capacity,
    NowUIMsdfAtlasInfo *info,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT int nowui_compile_font_from_memory_with_codepoints(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    const unsigned int *codepoints,
    int codepoint_count,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowUIMsdfGlyph *glyphs,
    int glyph_capacity,
    NowUIMsdfAtlasInfo *info,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT int nowui_compile_color_font_from_memory(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowUIColorGlyph *glyphs,
    int glyph_capacity,
    NowUIColorAtlasInfo *info,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT int nowui_compile_color_font_from_memory_with_codepoints(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    const unsigned int *codepoints,
    int codepoint_count,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowUIColorGlyph *glyphs,
    int glyph_capacity,
    NowUIColorAtlasInfo *info,
    char *error_buffer,
    int error_buffer_length);

/* Stateful incremental baking session.
 * Keeps the FreeType face, font geometry and a dynamic atlas alive across calls so
 * adding glyphs on demand does not re-parse the font or re-bake existing glyphs. */

typedef struct NowUIMsdfSessionInfo {
    float size;
    float distance_range;
    NowUIMsdfMetrics metrics;
} NowUIMsdfSessionInfo;

#define NOWUI_MSDF_SESSION_RESIZED 1

NOWUI_MSDF_EXPORT int nowui_msdf_session_create(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    NowUIMsdfSessionInfo *info,
    void **out_session,
    char *error_buffer,
    int error_buffer_length);

/* Bakes the requested codepoints that are not yet in the atlas into it and writes one
 * NowUIMsdfGlyph per successfully loaded glyph (codepoints missing from the font are
 * skipped; the caller diffs against its request to detect misses).
 * out_change_flags has NOWUI_MSDF_SESSION_RESIZED set when the atlas grew; previously
 * returned glyph atlas coordinates remain valid in the enlarged atlas. */
NOWUI_MSDF_EXPORT int nowui_msdf_session_add_glyphs(
    void *session,
    const unsigned int *codepoints,
    int codepoint_count,
    NowUIMsdfGlyph *glyphs,
    int glyph_capacity,
    int *out_glyph_count,
    int *out_atlas_side,
    int *out_change_flags,
    char *error_buffer,
    int error_buffer_length);

/* Copies the full atlas RGBA into the caller buffer (side * side * 4 bytes, bottom-up rows). */
NOWUI_MSDF_EXPORT int nowui_msdf_session_copy_atlas(
    void *session,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT void nowui_msdf_session_destroy(void *session);

NOWUI_MSDF_EXPORT const char *nowui_msdf_version();

}

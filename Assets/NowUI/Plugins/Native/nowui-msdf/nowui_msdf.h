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
#define NOWUI_MSDF_ATLAS_FULL 3

extern "C" {

typedef struct NowMsdfMetrics {
    float em_size;
    float line_height;
    float ascender;
    float descender;
    float underline_y;
    float underline_thickness;
} NowMsdfMetrics;

typedef struct NowMsdfGlyph {
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
} NowMsdfGlyph;

typedef struct NowMsdfAtlasInfo {
    int width;
    int height;
    int glyph_count;
    int atlas_byte_count;
    float size;
    float distance_range;
    NowMsdfMetrics metrics;
} NowMsdfAtlasInfo;

typedef struct NowColorGlyph {
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
} NowColorGlyph;

typedef struct NowColorAtlasInfo {
    int width;
    int height;
    int glyph_count;
    int atlas_byte_count;
    float size;
    float line_height;
    float ascender;
    float descender;
} NowColorAtlasInfo;

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
    NowMsdfGlyph *glyphs,
    int glyph_capacity,
    NowMsdfAtlasInfo *info,
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
    NowMsdfGlyph *glyphs,
    int glyph_capacity,
    NowMsdfAtlasInfo *info,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT int nowui_compile_color_font_from_memory(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowColorGlyph *glyphs,
    int glyph_capacity,
    NowColorAtlasInfo *info,
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
    NowColorGlyph *glyphs,
    int glyph_capacity,
    NowColorAtlasInfo *info,
    char *error_buffer,
    int error_buffer_length);

/* Stateful incremental baking session.
 * Keeps the FreeType face, font geometry and a dynamic atlas alive across calls so
 * adding glyphs on demand does not re-parse the font or re-bake existing glyphs. */

typedef struct NowMsdfSessionInfo {
    float size;
    float distance_range;
    NowMsdfMetrics metrics;
} NowMsdfSessionInfo;

#define NOWUI_MSDF_SESSION_RESIZED 1

NOWUI_MSDF_EXPORT int nowui_msdf_session_create(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    NowMsdfSessionInfo *info,
    void **out_session,
    char *error_buffer,
    int error_buffer_length);

/* Like nowui_msdf_session_create, but the atlas is allocated at atlas_side and never
 * grows; previously returned glyph coordinates and the atlas dimensions stay stable for
 * the whole session lifetime (retained meshes keep valid UVs). When a batch no longer
 * fits, nowui_msdf_session_add_glyphs returns NOWUI_MSDF_ATLAS_FULL without modifying
 * the session, and the caller is expected to start a new session for further glyphs. */
NOWUI_MSDF_EXPORT int nowui_msdf_session_create_fixed(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    int atlas_side,
    NowMsdfSessionInfo *info,
    void **out_session,
    char *error_buffer,
    int error_buffer_length);

/* Bakes the requested codepoints that are not yet in the atlas into it and writes one
 * NowMsdfGlyph per successfully loaded glyph (codepoints missing from the font are
 * skipped; the caller diffs against its request to detect misses).
 * out_change_flags has NOWUI_MSDF_SESSION_RESIZED set when the atlas grew (growable
 * sessions only); previously returned glyph atlas coordinates remain valid in the
 * enlarged atlas. Returns NOWUI_MSDF_ATLAS_FULL (leaving the session untouched) when
 * the batch cannot fit within the session's maximum atlas size. */
NOWUI_MSDF_EXPORT int nowui_msdf_session_add_glyphs(
    void *session,
    const unsigned int *codepoints,
    int codepoint_count,
    NowMsdfGlyph *glyphs,
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

/* HarfBuzz text shaping. Standalone handles, independent of baking sessions, so
 * shaping composes with both the native and the managed glyph bakers. Shaped
 * output is glyph indices with positions in em units and UTF-16 cluster mapping. */

typedef struct NowShapedGlyph {
    unsigned int glyph_index;
    unsigned int cluster;  /* UTF-16 code unit index into the input text */
    float x_advance;       /* em units (font units / unitsPerEm) */
    float y_advance;
    float x_offset;
    float y_offset;
} NowShapedGlyph;

NOWUI_MSDF_EXPORT int nowui_shaper_create(
    const unsigned char *font_data,
    int font_data_length,
    void **out_shaper,
    char *error_buffer,
    int error_buffer_length);

/* Shapes a UTF-16 run. Direction/script/language are inferred from the text.
 * Returns NOWUI_MSDF_BUFFER_TOO_SMALL with out_glyph_count set to the required
 * capacity when the output array cannot hold the shaped result. */
NOWUI_MSDF_EXPORT int nowui_shaper_shape_utf16(
    void *shaper,
    const unsigned short *text,
    int text_length,
    NowShapedGlyph *glyphs,
    int glyph_capacity,
    int *out_glyph_count,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT void nowui_shaper_destroy(void *shaper);

NOWUI_MSDF_EXPORT const char *nowui_msdf_version();

}

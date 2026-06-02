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

NOWUI_MSDF_EXPORT const char *nowui_msdf_version();

}

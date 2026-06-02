#pragma once

#ifdef _WIN32
#define NOWUI_MSDF_EXPORT __declspec(dllexport)
#else
#define NOWUI_MSDF_EXPORT __attribute__((visibility("default")))
#endif

extern "C" {

NOWUI_MSDF_EXPORT int nowui_compile_font(
    const char *font_path,
    const char *image_path,
    const char *json_path,
    int size,
    int pixel_range,
    char *error_buffer,
    int error_buffer_length);

NOWUI_MSDF_EXPORT const char *nowui_msdf_version();

}

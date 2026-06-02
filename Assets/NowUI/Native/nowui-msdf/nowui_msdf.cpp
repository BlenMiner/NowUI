#include "nowui_msdf.h"

#include <algorithm>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

#ifndef __EMSCRIPTEN__
#include <thread>
#endif

#include <msdfgen.h>
#include <msdfgen-ext.h>
#include <ft2build.h>
#include FT_FREETYPE_H
#include <msdf-atlas-gen/BitmapAtlasStorage.h>
#include <msdf-atlas-gen/Charset.h>
#include <msdf-atlas-gen/FontGeometry.h>
#include <msdf-atlas-gen/glyph-generators.h>
#include <msdf-atlas-gen/ImmediateAtlasGenerator.h>
#include <msdf-atlas-gen/TightAtlasPacker.h>

#ifndef NOWUI_MSDF_DISABLE_FILE_API
#include <msdf-atlas-gen/image-save.h>
#include <msdf-atlas-gen/json-export.h>
#endif

namespace {

const double DefaultAngleThreshold = 3.0;
const double DefaultMiterLimit = 1.0;

void set_error(char *error_buffer, int error_buffer_length, const std::string &message) {
    if (!error_buffer || error_buffer_length <= 0)
        return;

    const size_t length = std::min(static_cast<size_t>(error_buffer_length - 1), message.size());
    std::memcpy(error_buffer, message.c_str(), length);
    error_buffer[length] = '\0';
}

template <typename T>
std::string to_string(T value) {
    std::ostringstream stream;
    stream << value;
    return stream.str();
}

class FreetypeLibrary {
public:
    FreetypeLibrary() : handle(msdfgen::initializeFreetype()) {
    }

    ~FreetypeLibrary() {
        if (handle)
            msdfgen::deinitializeFreetype(handle);
    }

    msdfgen::FreetypeHandle *get() const {
        return handle;
    }

private:
    msdfgen::FreetypeHandle *handle;
};

class FontHandle {
public:
    FontHandle(msdfgen::FreetypeHandle *library, const char *font_path)
        : handle(msdfgen::loadFont(library, font_path)) {
    }

    FontHandle(msdfgen::FreetypeHandle *library, const unsigned char *font_data, int font_data_length)
        : handle(msdfgen::loadFontData(library, font_data, font_data_length)) {
    }

    ~FontHandle() {
        if (handle)
            msdfgen::destroyFont(handle);
    }

    msdfgen::FontHandle *get() const {
        return handle;
    }

private:
    msdfgen::FontHandle *handle;
};

typedef msdf_atlas::ImmediateAtlasGenerator<
    float,
    4,
    msdf_atlas::mtsdfGenerator,
    msdf_atlas::BitmapAtlasStorage<msdf_atlas::byte, 4> > AtlasGenerator;

struct ColorGlyphBitmap {
    unsigned int unicode;
    unsigned int glyph_index;
    int width;
    int height;
    int atlas_x;
    int atlas_y;
    float advance;
    float plane_left;
    float plane_bottom;
    float plane_right;
    float plane_top;
    std::vector<unsigned char> rgba;
};

msdf_atlas::Charset build_charset(const unsigned int *codepoints, int codepoint_count) {
    msdf_atlas::Charset charset = msdf_atlas::Charset::ASCII;

    if (!codepoints || codepoint_count <= 0)
        return charset;

    for (int i = 0; i < codepoint_count; ++i)
        charset.add(static_cast<msdf_atlas::unicode_t>(codepoints[i]));

    return charset;
}

void load_glyphs(
    msdfgen::FontHandle *font,
    msdf_atlas::FontGeometry &font_geometry,
    std::vector<msdf_atlas::GlyphGeometry> &glyphs,
    const unsigned int *codepoints = nullptr,
    int codepoint_count = 0) {

    const msdf_atlas::Charset charset = build_charset(codepoints, codepoint_count);
    const int loaded_glyphs = font_geometry.loadCharset(font, 1.0, charset);
    if (loaded_glyphs <= 0 || glyphs.empty())
        throw std::runtime_error("Font did not contain any requested glyphs.");

    for (msdf_atlas::GlyphGeometry &glyph : glyphs)
        glyph.edgeColoring(msdfgen::edgeColoringInkTrap, DefaultAngleThreshold, 0);
}

void pack_glyphs(
    std::vector<msdf_atlas::GlyphGeometry> &glyphs,
    int size,
    int pixel_range,
    msdf_atlas::TightAtlasPacker &packer,
    int &width,
    int &height) {

    packer.setDimensionsConstraint(msdf_atlas::DimensionsConstraint::MULTIPLE_OF_FOUR_SQUARE);
    packer.setScale(static_cast<double>(size));
    packer.setPixelRange(msdfgen::Range(static_cast<double>(pixel_range)));
    packer.setMiterLimit(DefaultMiterLimit);

    const int pack_result = packer.pack(glyphs.data(), static_cast<int>(glyphs.size()));
    if (pack_result != 0)
        throw std::runtime_error("Failed to pack glyph atlas, error code: " + to_string(pack_result));

    packer.getDimensions(width, height);

    if (width <= 0 || height <= 0)
        throw std::runtime_error("Generated atlas dimensions are invalid.");
}

void fill_info(
    NowUIMsdfAtlasInfo *info,
    const msdf_atlas::FontGeometry &font_geometry,
    int width,
    int height,
    int glyph_count,
    double size,
    int pixel_range) {

    if (!info)
        return;

    const msdfgen::FontMetrics &metrics = font_geometry.getMetrics();

    info->width = width;
    info->height = height;
    info->glyph_count = glyph_count;
    info->atlas_byte_count = width * height * 4;
    info->size = static_cast<float>(size);
    info->distance_range = static_cast<float>(pixel_range);
    info->metrics.em_size = static_cast<float>(metrics.emSize);
    info->metrics.line_height = static_cast<float>(metrics.lineHeight);
    info->metrics.ascender = static_cast<float>(metrics.ascenderY);
    info->metrics.descender = static_cast<float>(metrics.descenderY);
    info->metrics.underline_y = static_cast<float>(metrics.underlineY);
    info->metrics.underline_thickness = static_cast<float>(metrics.underlineThickness);
}

void fill_glyphs(NowUIMsdfGlyph *output, const msdf_atlas::FontGeometry &font_geometry) {
    if (!output)
        return;

    msdf_atlas::FontGeometry::GlyphRange glyph_range = font_geometry.getGlyphs();
    int index = 0;

    for (const msdf_atlas::GlyphGeometry *glyph = glyph_range.begin(); glyph != glyph_range.end(); ++glyph, ++index) {
        double plane_left = 0;
        double plane_bottom = 0;
        double plane_right = 0;
        double plane_top = 0;
        double atlas_left = 0;
        double atlas_bottom = 0;
        double atlas_right = 0;
        double atlas_top = 0;

        glyph->getQuadPlaneBounds(plane_left, plane_bottom, plane_right, plane_top);
        glyph->getQuadAtlasBounds(atlas_left, atlas_bottom, atlas_right, atlas_top);

        output[index].unicode = glyph->getCodepoint();
        output[index].advance = static_cast<float>(glyph->getAdvance());
        output[index].plane_left = static_cast<float>(plane_left);
        output[index].plane_bottom = static_cast<float>(plane_bottom);
        output[index].plane_right = static_cast<float>(plane_right);
        output[index].plane_top = static_cast<float>(plane_top);
        output[index].atlas_left = static_cast<float>(atlas_left);
        output[index].atlas_bottom = static_cast<float>(atlas_bottom);
        output[index].atlas_right = static_cast<float>(atlas_right);
        output[index].atlas_top = static_cast<float>(atlas_top);
    }
}

AtlasGenerator generate_atlas(const std::vector<msdf_atlas::GlyphGeometry> &glyphs, int width, int height) {
    AtlasGenerator generator(width, height);
    msdf_atlas::GeneratorAttributes attributes;
    generator.setAttributes(attributes);

#ifdef __EMSCRIPTEN__
    generator.setThreadCount(1);
#else
    const unsigned hardware_threads = std::thread::hardware_concurrency();
    generator.setThreadCount(static_cast<int>(hardware_threads == 0 ? 1 : hardware_threads));
#endif
    generator.generate(glyphs.data(), static_cast<int>(glyphs.size()));
    return generator;
}

void copy_bitmap_to_rgba(const FT_Bitmap &bitmap, std::vector<unsigned char> &rgba) {
    rgba.assign(static_cast<size_t>(bitmap.width * bitmap.rows * 4), 0);

    for (unsigned int y = 0; y < bitmap.rows; ++y) {
        const unsigned char *source = bitmap.buffer + y * bitmap.pitch;
        unsigned char *target = rgba.data() + y * bitmap.width * 4;

        for (unsigned int x = 0; x < bitmap.width; ++x) {
            if (bitmap.pixel_mode == FT_PIXEL_MODE_BGRA) {
                const unsigned char b = source[x * 4 + 0];
                const unsigned char g = source[x * 4 + 1];
                const unsigned char r = source[x * 4 + 2];
                const unsigned char a = source[x * 4 + 3];
                target[x * 4 + 0] = r;
                target[x * 4 + 1] = g;
                target[x * 4 + 2] = b;
                target[x * 4 + 3] = a;
            } else if (bitmap.pixel_mode == FT_PIXEL_MODE_GRAY) {
                const unsigned char a = source[x];
                target[x * 4 + 0] = 255;
                target[x * 4 + 1] = 255;
                target[x * 4 + 2] = 255;
                target[x * 4 + 3] = a;
            }
        }
    }
}

void collect_color_glyphs(FT_Face face, int size, std::vector<ColorGlyphBitmap> &glyphs) {
    const double scale = size > 0 ? static_cast<double>(size) : 1.0;

    const auto collect_glyph = [&](FT_ULong charcode, FT_UInt glyph_index) {
        if (FT_Load_Glyph(face, glyph_index, FT_LOAD_COLOR | FT_LOAD_RENDER) == 0) {
            FT_GlyphSlot slot = face->glyph;
            const FT_Bitmap &bitmap = slot->bitmap;

            if (bitmap.width > 0 && bitmap.rows > 0 &&
                (bitmap.pixel_mode == FT_PIXEL_MODE_BGRA || bitmap.pixel_mode == FT_PIXEL_MODE_GRAY)) {
                ColorGlyphBitmap glyph;
                glyph.unicode = static_cast<unsigned int>(charcode);
                glyph.glyph_index = static_cast<unsigned int>(glyph_index);
                glyph.width = static_cast<int>(bitmap.width);
                glyph.height = static_cast<int>(bitmap.rows);
                glyph.atlas_x = 0;
                glyph.atlas_y = 0;
                glyph.advance = static_cast<float>((slot->advance.x / 64.0) / scale);
                glyph.plane_left = static_cast<float>(slot->bitmap_left / scale);
                glyph.plane_right = static_cast<float>((slot->bitmap_left + glyph.width) / scale);
                glyph.plane_top = static_cast<float>(slot->bitmap_top / scale);
                glyph.plane_bottom = static_cast<float>((slot->bitmap_top - glyph.height) / scale);
                copy_bitmap_to_rgba(bitmap, glyph.rgba);
                glyphs.push_back(glyph);
            }
        }
    };

    FT_UInt glyph_index = 0;
    FT_ULong charcode = FT_Get_First_Char(face, &glyph_index);

    while (glyph_index != 0) {
        collect_glyph(charcode, glyph_index);

        charcode = FT_Get_Next_Char(face, charcode, &glyph_index);
    }
}

void collect_color_glyphs(
    FT_Face face,
    int size,
    const unsigned int *codepoints,
    int codepoint_count,
    std::vector<ColorGlyphBitmap> &glyphs) {

    if (!codepoints || codepoint_count <= 0) {
        collect_color_glyphs(face, size, glyphs);
        return;
    }

    const double scale = size > 0 ? static_cast<double>(size) : 1.0;

    for (int i = 0; i < codepoint_count; ++i) {
        const unsigned int unicode = codepoints[i];
        const FT_UInt glyph_index = FT_Get_Char_Index(face, unicode);

        if (glyph_index == 0)
            continue;

        if (FT_Load_Glyph(face, glyph_index, FT_LOAD_COLOR | FT_LOAD_RENDER) != 0)
            continue;

        FT_GlyphSlot slot = face->glyph;
        const FT_Bitmap &bitmap = slot->bitmap;

        if (bitmap.width <= 0 || bitmap.rows <= 0 ||
            (bitmap.pixel_mode != FT_PIXEL_MODE_BGRA && bitmap.pixel_mode != FT_PIXEL_MODE_GRAY))
            continue;

        ColorGlyphBitmap glyph;
        glyph.unicode = unicode;
        glyph.glyph_index = static_cast<unsigned int>(glyph_index);
        glyph.width = static_cast<int>(bitmap.width);
        glyph.height = static_cast<int>(bitmap.rows);
        glyph.atlas_x = 0;
        glyph.atlas_y = 0;
        glyph.advance = static_cast<float>((slot->advance.x / 64.0) / scale);
        glyph.plane_left = static_cast<float>(slot->bitmap_left / scale);
        glyph.plane_right = static_cast<float>((slot->bitmap_left + glyph.width) / scale);
        glyph.plane_top = static_cast<float>(slot->bitmap_top / scale);
        glyph.plane_bottom = static_cast<float>((slot->bitmap_top - glyph.height) / scale);
        copy_bitmap_to_rgba(bitmap, glyph.rgba);
        glyphs.push_back(glyph);
    }
}

void set_color_font_size(FT_Face face, int size) {
    if (FT_HAS_FIXED_SIZES(face) && face->num_fixed_sizes > 0) {
        int best_index = 0;
        long best_delta = std::labs(face->available_sizes[0].y_ppem / 64 - size);

        for (int i = 1; i < face->num_fixed_sizes; ++i) {
            const long delta = std::labs(face->available_sizes[i].y_ppem / 64 - size);

            if (delta < best_delta) {
                best_delta = delta;
                best_index = i;
            }
        }

        if (FT_Select_Size(face, best_index) == 0)
            return;
    }

    FT_Set_Pixel_Sizes(face, 0, static_cast<FT_UInt>(size));
}

void pack_color_glyphs(std::vector<ColorGlyphBitmap> &glyphs, int &width, int &height) {
    const int padding = 2;
    const int max_width = 4096;
    int x = padding;
    int y = padding;
    int row_height = 0;
    width = 0;

    for (ColorGlyphBitmap &glyph : glyphs) {
        if (x + glyph.width + padding > max_width && x > padding) {
            y += row_height + padding;
            x = padding;
            row_height = 0;
        }

        glyph.atlas_x = x;
        glyph.atlas_y = y;
        x += glyph.width + padding;
        row_height = std::max(row_height, glyph.height);
        width = std::max(width, x + padding);
    }

    height = y + row_height + padding;

    if (width <= 0 || height <= 0)
        throw std::runtime_error("Generated color glyph atlas dimensions are invalid.");
}

int compile_color_font_to_memory(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowUIColorGlyph *glyphs_output,
    int glyph_capacity,
    NowUIColorAtlasInfo *info,
    const unsigned int *codepoints = nullptr,
    int codepoint_count = 0) {

    if (!font_data || font_data_length <= 0)
        throw std::runtime_error("Font data is empty.");
    if (size <= 0)
        throw std::runtime_error("Color font atlas size must be greater than zero.");

    FT_Library library = nullptr;
    if (FT_Init_FreeType(&library) != 0)
        throw std::runtime_error("Failed to initialize FreeType.");

    FT_Face face = nullptr;
    if (FT_New_Memory_Face(library, font_data, font_data_length, 0, &face) != 0) {
        FT_Done_FreeType(library);
        throw std::runtime_error("Failed to load color font from memory.");
    }

    set_color_font_size(face, size);

    std::vector<ColorGlyphBitmap> glyphs;
    collect_color_glyphs(face, size, codepoints, codepoint_count, glyphs);

    if (glyphs.empty()) {
        FT_Done_Face(face);
        FT_Done_FreeType(library);
        throw std::runtime_error("Font did not contain requested color bitmap glyphs that NowUI can import.");
    }

    int width = 0;
    int height = 0;
    pack_color_glyphs(glyphs, width, height);

    const int atlas_byte_count = width * height * 4;

    if (info) {
        info->width = width;
        info->height = height;
        info->glyph_count = static_cast<int>(glyphs.size());
        info->atlas_byte_count = atlas_byte_count;
        info->size = static_cast<float>(size);
        info->line_height = face->size ? static_cast<float>((face->size->metrics.height / 64.0) / size) : 1.0f;
        info->ascender = face->size ? static_cast<float>((face->size->metrics.ascender / 64.0) / size) : 1.0f;
        info->descender = face->size ? static_cast<float>((face->size->metrics.descender / 64.0) / size) : 0.0f;
    }

    if (!atlas_rgba || atlas_rgba_length < atlas_byte_count || !glyphs_output || glyph_capacity < static_cast<int>(glyphs.size())) {
        FT_Done_Face(face);
        FT_Done_FreeType(library);
        return NOWUI_MSDF_BUFFER_TOO_SMALL;
    }

    std::memset(atlas_rgba, 0, static_cast<size_t>(atlas_byte_count));

    for (int i = 0; i < static_cast<int>(glyphs.size()); ++i) {
        const ColorGlyphBitmap &glyph = glyphs[i];

        for (int row = 0; row < glyph.height; ++row) {
            const unsigned char *source = glyph.rgba.data() + row * glyph.width * 4;
            unsigned char *target = atlas_rgba + ((glyph.atlas_y + row) * width + glyph.atlas_x) * 4;
            std::memcpy(target, source, static_cast<size_t>(glyph.width * 4));
        }

        glyphs_output[i].unicode = glyph.unicode;
        glyphs_output[i].glyph_index = glyph.glyph_index;
        glyphs_output[i].advance = glyph.advance;
        glyphs_output[i].plane_left = glyph.plane_left;
        glyphs_output[i].plane_bottom = glyph.plane_bottom;
        glyphs_output[i].plane_right = glyph.plane_right;
        glyphs_output[i].plane_top = glyph.plane_top;
        glyphs_output[i].atlas_left = static_cast<float>(glyph.atlas_x);
        glyphs_output[i].atlas_bottom = static_cast<float>(glyph.atlas_y);
        glyphs_output[i].atlas_right = static_cast<float>(glyph.atlas_x + glyph.width);
        glyphs_output[i].atlas_top = static_cast<float>(glyph.atlas_y + glyph.height);
    }

    FT_Done_Face(face);
    FT_Done_FreeType(library);
    return NOWUI_MSDF_OK;
}

#ifndef NOWUI_MSDF_DISABLE_FILE_API
int compile_font_to_files(
    const char *font_path,
    const char *image_path,
    const char *json_path,
    int size,
    int pixel_range) {

    if (!font_path || !font_path[0])
        throw std::runtime_error("Font path is empty.");
    if (!image_path || !image_path[0])
        throw std::runtime_error("Image output path is empty.");
    if (!json_path || !json_path[0])
        throw std::runtime_error("JSON output path is empty.");
    if (size <= 0)
        throw std::runtime_error("Font atlas size must be greater than zero.");
    if (pixel_range <= 0)
        throw std::runtime_error("Font atlas pixel range must be greater than zero.");

    FreetypeLibrary freetype;
    if (!freetype.get())
        throw std::runtime_error("Failed to initialize FreeType.");

    FontHandle font(freetype.get(), font_path);
    if (!font.get())
        throw std::runtime_error(std::string("Failed to load font: ") + font_path);

    std::vector<msdf_atlas::GlyphGeometry> glyphs;
    msdf_atlas::FontGeometry font_geometry(&glyphs);
    load_glyphs(font.get(), font_geometry, glyphs);

    msdf_atlas::TightAtlasPacker packer;
    int width = 0;
    int height = 0;
    pack_glyphs(glyphs, size, pixel_range, packer, width, height);

    AtlasGenerator generator = generate_atlas(glyphs, width, height);

    msdfgen::BitmapConstRef<msdf_atlas::byte, 4> atlas = generator.atlasStorage();
    if (!msdf_atlas::saveImage<msdf_atlas::byte, 4>(atlas, msdf_atlas::ImageFormat::PNG, image_path))
        throw std::runtime_error(std::string("Failed to write atlas image: ") + image_path);

    msdf_atlas::JsonAtlasMetrics metrics;
    metrics.distanceRange = packer.getPixelRange();
    metrics.size = packer.getScale();
    metrics.width = width;
    metrics.height = height;
    metrics.yDirection = msdfgen::Y_UPWARD;
    metrics.grid = nullptr;

    if (!msdf_atlas::exportJSON(&font_geometry, 1, msdf_atlas::ImageType::MTSDF, metrics, json_path, false))
        throw std::runtime_error(std::string("Failed to write atlas JSON: ") + json_path);

    return NOWUI_MSDF_OK;
}
#endif

int compile_font_to_memory(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowUIMsdfGlyph *glyphs_output,
    int glyph_capacity,
    NowUIMsdfAtlasInfo *info,
    const unsigned int *codepoints = nullptr,
    int codepoint_count = 0) {

    if (!font_data || font_data_length <= 0)
        throw std::runtime_error("Font data is empty.");
    if (size <= 0)
        throw std::runtime_error("Font atlas size must be greater than zero.");
    if (pixel_range <= 0)
        throw std::runtime_error("Font atlas pixel range must be greater than zero.");

    FreetypeLibrary freetype;
    if (!freetype.get())
        throw std::runtime_error("Failed to initialize FreeType.");

    FontHandle font(freetype.get(), font_data, font_data_length);
    if (!font.get())
        throw std::runtime_error("Failed to load font from memory.");

    std::vector<msdf_atlas::GlyphGeometry> glyphs;
    msdf_atlas::FontGeometry font_geometry(&glyphs);
    load_glyphs(font.get(), font_geometry, glyphs, codepoints, codepoint_count);

    msdf_atlas::TightAtlasPacker packer;
    int width = 0;
    int height = 0;
    pack_glyphs(glyphs, size, pixel_range, packer, width, height);

    const int glyph_count = static_cast<int>(glyphs.size());
    const int atlas_byte_count = width * height * 4;
    fill_info(info, font_geometry, width, height, glyph_count, packer.getScale(), pixel_range);

    if (!atlas_rgba || atlas_rgba_length < atlas_byte_count || !glyphs_output || glyph_capacity < glyph_count)
        return NOWUI_MSDF_BUFFER_TOO_SMALL;

    AtlasGenerator generator = generate_atlas(glyphs, width, height);
    msdfgen::BitmapConstRef<msdf_atlas::byte, 4> atlas = generator.atlasStorage();

    for (int y = 0; y < height; ++y)
        std::memcpy(atlas_rgba + y * width * 4, atlas(0, y), static_cast<size_t>(width * 4));

    fill_glyphs(glyphs_output, font_geometry);
    return NOWUI_MSDF_OK;
}

}

extern "C" {

int nowui_compile_font(
    const char *font_path,
    const char *image_path,
    const char *json_path,
    int size,
    int pixel_range,
    char *error_buffer,
    int error_buffer_length) {

    try {
#ifdef NOWUI_MSDF_DISABLE_FILE_API
        (void)font_path;
        (void)image_path;
        (void)json_path;
        (void)size;
        (void)pixel_range;

        set_error(error_buffer, error_buffer_length, "File-based font compilation is disabled in this build.");
        return NOWUI_MSDF_ERROR;
#else
        const int result = compile_font_to_files(font_path, image_path, json_path, size, pixel_range);
        set_error(error_buffer, error_buffer_length, std::string());
        return result;
#endif
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_compile_font_from_memory(
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
    int error_buffer_length) {

    try {
        const int result = compile_font_to_memory(
            font_data,
            font_data_length,
            size,
            pixel_range,
            atlas_rgba,
            atlas_rgba_length,
            glyphs,
            glyph_capacity,
            info);

        set_error(error_buffer, error_buffer_length, std::string());
        return result;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_compile_font_from_memory_with_codepoints(
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
    int error_buffer_length) {

    try {
        const int result = compile_font_to_memory(
            font_data,
            font_data_length,
            size,
            pixel_range,
            atlas_rgba,
            atlas_rgba_length,
            glyphs,
            glyph_capacity,
            info,
            codepoints,
            codepoint_count);

        set_error(error_buffer, error_buffer_length, std::string());
        return result;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_compile_color_font_from_memory(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    NowUIColorGlyph *glyphs,
    int glyph_capacity,
    NowUIColorAtlasInfo *info,
    char *error_buffer,
    int error_buffer_length) {

    try {
        const int result = compile_color_font_to_memory(
            font_data,
            font_data_length,
            size,
            atlas_rgba,
            atlas_rgba_length,
            glyphs,
            glyph_capacity,
            info);

        set_error(error_buffer, error_buffer_length, std::string());
        return result;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native color font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_compile_color_font_from_memory_with_codepoints(
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
    int error_buffer_length) {

    try {
        const int result = compile_color_font_to_memory(
            font_data,
            font_data_length,
            size,
            atlas_rgba,
            atlas_rgba_length,
            glyphs,
            glyph_capacity,
            info,
            codepoints,
            codepoint_count);

        set_error(error_buffer, error_buffer_length, std::string());
        return result;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native color font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

const char *nowui_msdf_version() {
    return "nowui-msdf/1";
}

}

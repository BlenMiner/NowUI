#include "nowui_msdf.h"

#include <algorithm>
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

void load_glyphs(msdfgen::FontHandle *font, msdf_atlas::FontGeometry &font_geometry, std::vector<msdf_atlas::GlyphGeometry> &glyphs) {
    const int loaded_glyphs = font_geometry.loadCharset(font, 1.0, msdf_atlas::Charset::ASCII);
    if (loaded_glyphs <= 0 || glyphs.empty())
        throw std::runtime_error("Font did not contain any printable ASCII glyphs.");

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
    NowUIMsdfAtlasInfo *info) {

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
    load_glyphs(font.get(), font_geometry, glyphs);

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

const char *nowui_msdf_version() {
    return "nowui-msdf/1";
}

}

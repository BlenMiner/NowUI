#include "nowui_msdf.h"

#include <algorithm>
#include <cstring>
#include <exception>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include <msdfgen.h>
#include <msdfgen-ext.h>
#include <msdf-atlas-gen/msdf-atlas-gen.h>

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

int compile_font(
    const char *font_path,
    const char *image_path,
    const char *json_path,
    int size,
    int pixel_range,
    char *error_buffer,
    int error_buffer_length) {

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

    const int loaded_glyphs = font_geometry.loadCharset(font.get(), 1.0, msdf_atlas::Charset::ASCII);
    if (loaded_glyphs <= 0 || glyphs.empty())
        throw std::runtime_error("Font did not contain any printable ASCII glyphs.");

    for (msdf_atlas::GlyphGeometry &glyph : glyphs)
        glyph.edgeColoring(msdfgen::edgeColoringInkTrap, DefaultAngleThreshold, 0);

    msdf_atlas::TightAtlasPacker packer;
    packer.setDimensionsConstraint(msdf_atlas::DimensionsConstraint::MULTIPLE_OF_FOUR_SQUARE);
    packer.setScale(static_cast<double>(size));
    packer.setPixelRange(msdfgen::Range(static_cast<double>(pixel_range)));
    packer.setMiterLimit(DefaultMiterLimit);

    const int pack_result = packer.pack(glyphs.data(), static_cast<int>(glyphs.size()));
    if (pack_result != 0)
        throw std::runtime_error("Failed to pack glyph atlas, error code: " + to_string(pack_result));

    int width = 0;
    int height = 0;
    packer.getDimensions(width, height);

    if (width <= 0 || height <= 0)
        throw std::runtime_error("Generated atlas dimensions are invalid.");

    typedef msdf_atlas::ImmediateAtlasGenerator<
        float,
        4,
        msdf_atlas::mtsdfGenerator,
        msdf_atlas::BitmapAtlasStorage<msdf_atlas::byte, 4> > Generator;

    Generator generator(width, height);
    msdf_atlas::GeneratorAttributes attributes;
    generator.setAttributes(attributes);

    const unsigned hardware_threads = std::thread::hardware_concurrency();
    generator.setThreadCount(static_cast<int>(hardware_threads == 0 ? 1 : hardware_threads));
    generator.generate(glyphs.data(), static_cast<int>(glyphs.size()));

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

    set_error(error_buffer, error_buffer_length, std::string());
    return 0;
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
        return compile_font(font_path, image_path, json_path, size, pixel_range, error_buffer, error_buffer_length);
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return 1;
}

const char *nowui_msdf_version() {
    return "nowui-msdf/1";
}

}

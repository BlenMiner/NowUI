#include "nowui_msdf.h"

#include <algorithm>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <memory>
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
#include <cstdint>

#ifndef NOWUI_MSDF_NO_SHAPING
#include <hb.h>
#endif
#include <msdf-atlas-gen/BitmapAtlasStorage.h>
#include <msdf-atlas-gen/Charset.h>
#include <msdf-atlas-gen/FontGeometry.h>
#include <msdf-atlas-gen/glyph-generators.h>
#include <msdf-atlas-gen/ImmediateAtlasGenerator.h>
#include <msdf-atlas-gen/RectanglePacker.h>
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

#ifndef NOWUI_MSDF_NO_SHAPING
/* Owns the HarfBuzz objects for one font; see nowui_shaper_create. */
struct NowUIShaperState {
    std::vector<unsigned char> data;
    hb_blob_t *blob = nullptr;
    hb_face_t *face = nullptr;
    hb_font_t *font = nullptr;
    hb_buffer_t *buffer = nullptr;
    unsigned int upem = 0;

    ~NowUIShaperState() {
        if (buffer)
            hb_buffer_destroy(buffer);
        if (font)
            hb_font_destroy(font);
        if (face)
            hb_face_destroy(face);
        if (blob)
            hb_blob_destroy(blob);
    }
};
#endif

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
    if (!codepoints || codepoint_count <= 0)
        return msdf_atlas::Charset::ASCII;

    msdf_atlas::Charset charset;
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

msdf_atlas::GeneratorAttributes make_generator_attributes() {
    msdf_atlas::GeneratorAttributes attributes;
    // Icon fonts (e.g. Material Design Icons) routinely contain overlapping contours and
    // inconsistent windings; without the scanline sign-correction pass such glyphs bake
    // with filled holes or inverted regions.
    attributes.scanlinePass = true;
    return attributes;
}

int default_thread_count() {
#ifdef __EMSCRIPTEN__
    return 1;
#else
    const unsigned hardware_threads = std::thread::hardware_concurrency();
    return static_cast<int>(hardware_threads == 0 ? 1 : hardware_threads);
#endif
}

AtlasGenerator generate_atlas(const std::vector<msdf_atlas::GlyphGeometry> &glyphs, int width, int height) {
    AtlasGenerator generator(width, height);
    generator.setAttributes(make_generator_attributes());
    generator.setThreadCount(default_thread_count());
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

double get_color_metric_scale(FT_Face face, int fallback_size) {
    if (face && face->size) {
        if (face->size->metrics.y_ppem > 0)
            return static_cast<double>(face->size->metrics.y_ppem);

        if (face->size->metrics.height > 0)
            return face->size->metrics.height / 64.0;
    }

    return fallback_size > 0 ? static_cast<double>(fallback_size) : 1.0;
}

void collect_color_glyphs(FT_Face face, int size, std::vector<ColorGlyphBitmap> &glyphs) {
    const double scale = get_color_metric_scale(face, size);

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

    const double scale = get_color_metric_scale(face, size);

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
        const double metric_scale = get_color_metric_scale(face, size);
        info->width = width;
        info->height = height;
        info->glyph_count = static_cast<int>(glyphs.size());
        info->atlas_byte_count = atlas_byte_count;
        info->size = static_cast<float>(size);
        info->line_height = face->size ? static_cast<float>((face->size->metrics.height / 64.0) / metric_scale) : 1.0f;
        info->ascender = face->size ? static_cast<float>((face->size->metrics.ascender / 64.0) / metric_scale) : 1.0f;
        info->descender = face->size ? static_cast<float>((face->size->metrics.descender / 64.0) / metric_scale) : 0.0f;
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

const int SESSION_MIN_SIDE = 256;
const int SESSION_MAX_SIDE = 1 << 14;

// Note: msdf_atlas::DynamicAtlas is deliberately not used here. Its add() assumes that
// RectanglePacker::pack leaves exactly the trailing rectangles unplaced when the atlas is
// full, but pack() places rectangles in best-fit order, so after an atlas resize arbitrary
// glyphs could remain unplaced at (0, 0) and overlap each other. The session drives the
// packer and generator directly and retries the whole batch from a packer snapshot instead.
struct NowUIMsdfSessionState {
    std::vector<unsigned char> font_data;
    FreetypeLibrary freetype;
    msdfgen::FontHandle *font = nullptr;
    msdf_atlas::FontGeometry font_geometry;
    double scale = 0;
    double range_em = 0;
    msdf_atlas::RectanglePacker packer;
    AtlasGenerator generator;
    int side = 0;
    int max_side = 0;

    ~NowUIMsdfSessionState() {
        if (font)
            msdfgen::destroyFont(font);
    }
};

NowUIMsdfSessionState *create_session(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    int min_side,
    int max_side,
    NowUIMsdfSessionInfo *info) {

    if (!font_data || font_data_length <= 0)
        throw std::runtime_error("Font data is empty.");
    if (size <= 0)
        throw std::runtime_error("Font atlas size must be greater than zero.");
    if (pixel_range <= 0)
        throw std::runtime_error("Font atlas pixel range must be greater than zero.");
    if (min_side <= 0 || max_side < min_side || max_side > SESSION_MAX_SIDE)
        throw std::runtime_error("Session atlas side constraints are invalid.");

    std::unique_ptr<NowUIMsdfSessionState> session(new NowUIMsdfSessionState());

    // FreeType keeps referencing the memory of FT_New_Memory_Face for the lifetime of
    // the face, so the session owns a copy of the font bytes.
    session->font_data.assign(font_data, font_data + font_data_length);

    if (!session->freetype.get())
        throw std::runtime_error("Failed to initialize FreeType.");

    session->font = msdfgen::loadFontData(
        session->freetype.get(),
        session->font_data.data(),
        static_cast<int>(session->font_data.size()));

    if (!session->font)
        throw std::runtime_error("Failed to load font from memory.");

    if (!session->font_geometry.loadMetrics(session->font, 1.0))
        throw std::runtime_error("Failed to load font metrics.");

    session->scale = static_cast<double>(size);
    session->range_em = static_cast<double>(pixel_range) / session->scale;
    session->side = min_side;
    session->max_side = max_side;
    session->packer = msdf_atlas::RectanglePacker(session->side, session->side);
    session->generator = AtlasGenerator(session->side, session->side);
    session->generator.setAttributes(make_generator_attributes());
    session->generator.setThreadCount(default_thread_count());

    if (info) {
        const msdfgen::FontMetrics &metrics = session->font_geometry.getMetrics();
        info->size = static_cast<float>(session->scale);
        info->distance_range = static_cast<float>(pixel_range);
        info->metrics.em_size = static_cast<float>(metrics.emSize);
        info->metrics.line_height = static_cast<float>(metrics.lineHeight);
        info->metrics.ascender = static_cast<float>(metrics.ascenderY);
        info->metrics.descender = static_cast<float>(metrics.descenderY);
        info->metrics.underline_y = static_cast<float>(metrics.underlineY);
        info->metrics.underline_thickness = static_cast<float>(metrics.underlineThickness);
    }

    return session.release();
}

int session_add_glyphs(
    NowUIMsdfSessionState *session,
    const unsigned int *codepoints,
    int codepoint_count,
    NowUIMsdfGlyph *glyphs_output,
    int glyph_capacity,
    int *out_glyph_count,
    int *out_atlas_side,
    int *out_change_flags) {

    if (!session)
        throw std::runtime_error("Session is null.");
    if (out_glyph_count)
        *out_glyph_count = 0;
    if (out_change_flags)
        *out_change_flags = 0;
    if (out_atlas_side)
        *out_atlas_side = session->side;
    if (!codepoints || codepoint_count <= 0)
        return NOWUI_MSDF_OK;

    std::vector<msdf_atlas::GlyphGeometry> loaded;
    loaded.reserve(static_cast<size_t>(codepoint_count));

    for (int i = 0; i < codepoint_count; ++i) {
        msdf_atlas::GlyphGeometry glyph;

        if (!glyph.load(session->font, session->font_geometry.getGeometryScale(), static_cast<msdf_atlas::unicode_t>(codepoints[i])))
            continue;

        if (!glyph.isWhitespace()) {
            glyph.edgeColoring(msdfgen::edgeColoringInkTrap, DefaultAngleThreshold, 0);
            glyph.wrapBox(session->scale, session->range_em, DefaultMiterLimit);
        }

        loaded.push_back(static_cast<msdf_atlas::GlyphGeometry &&>(glyph));
    }

    if (loaded.empty())
        return NOWUI_MSDF_OK;

    if (!glyphs_output || glyph_capacity < static_cast<int>(loaded.size()))
        return NOWUI_MSDF_BUFFER_TOO_SMALL;

    std::vector<msdf_atlas::Rectangle> batch_rects;
    std::vector<size_t> rect_glyph_indices;

    for (size_t i = 0; i < loaded.size(); ++i) {
        if (loaded[i].isWhitespace())
            continue;

        int w = 0, h = 0;
        loaded[i].getBoxSize(w, h);
        const msdf_atlas::Rectangle rect = { 0, 0, w, h };
        batch_rects.push_back(rect);
        rect_glyph_indices.push_back(i);
    }

    bool resized = false;

    if (!batch_rects.empty()) {
        // Pack the whole batch against snapshots of the packer; on failure grow a local
        // copy and retry the entire batch so no rectangle is ever left unplaced. Nothing
        // in the session is mutated until packing succeeds, so an over-full atlas can be
        // reported to the caller (NOWUI_MSDF_ATLAS_FULL) with the session intact.
        int attempt_side = session->side;
        msdf_atlas::RectanglePacker base_packer = session->packer;

        for (;;) {
            msdf_atlas::RectanglePacker attempt = base_packer;
            std::vector<msdf_atlas::Rectangle> attempt_rects = batch_rects;

            if (attempt.pack(attempt_rects.data(), static_cast<int>(attempt_rects.size())) == 0) {
                session->packer = attempt;
                batch_rects = attempt_rects;
                break;
            }

            if (attempt_side >= session->max_side)
                return NOWUI_MSDF_ATLAS_FULL;

            attempt_side <<= 1;
            if (attempt_side > session->max_side)
                attempt_side = session->max_side;

            // Already-placed glyphs keep their coordinates because expand() only adds
            // free space around the existing layout.
            base_packer.expand(attempt_side, attempt_side);
            resized = true;
        }

        if (resized) {
            session->side = attempt_side;
            session->generator.resize(attempt_side, attempt_side);
        }

        for (size_t r = 0; r < batch_rects.size(); ++r)
            loaded[rect_glyph_indices[r]].placeBox(batch_rects[r].x, batch_rects[r].y);
    }

    session->generator.generate(loaded.data(), static_cast<int>(loaded.size()));

    const int change_flags = resized ? NOWUI_MSDF_SESSION_RESIZED : 0;

    for (size_t i = 0; i < loaded.size(); ++i) {
        const msdf_atlas::GlyphGeometry &glyph = loaded[i];
        double plane_left = 0, plane_bottom = 0, plane_right = 0, plane_top = 0;
        double atlas_left = 0, atlas_bottom = 0, atlas_right = 0, atlas_top = 0;

        glyph.getQuadPlaneBounds(plane_left, plane_bottom, plane_right, plane_top);
        glyph.getQuadAtlasBounds(atlas_left, atlas_bottom, atlas_right, atlas_top);

        NowUIMsdfGlyph &output = glyphs_output[i];
        output.unicode = glyph.getCodepoint();
        output.advance = static_cast<float>(glyph.getAdvance());
        output.plane_left = static_cast<float>(plane_left);
        output.plane_bottom = static_cast<float>(plane_bottom);
        output.plane_right = static_cast<float>(plane_right);
        output.plane_top = static_cast<float>(plane_top);
        output.atlas_left = static_cast<float>(atlas_left);
        output.atlas_bottom = static_cast<float>(atlas_bottom);
        output.atlas_right = static_cast<float>(atlas_right);
        output.atlas_top = static_cast<float>(atlas_top);
    }

    if (out_glyph_count)
        *out_glyph_count = static_cast<int>(loaded.size());
    if (out_atlas_side)
        *out_atlas_side = session->side;
    if (out_change_flags)
        *out_change_flags = change_flags;

    return NOWUI_MSDF_OK;
}

int session_copy_atlas(NowUIMsdfSessionState *session, unsigned char *atlas_rgba, int atlas_rgba_length) {
    if (!session)
        throw std::runtime_error("Session is null.");

    msdfgen::BitmapConstRef<msdf_atlas::byte, 4> storage = session->generator.atlasStorage();
    const int byte_count = storage.width * storage.height * 4;

    if (!atlas_rgba || atlas_rgba_length < byte_count)
        return NOWUI_MSDF_BUFFER_TOO_SMALL;

    for (int y = 0; y < storage.height; ++y)
        std::memcpy(atlas_rgba + static_cast<size_t>(y) * storage.width * 4, storage(0, y), static_cast<size_t>(storage.width) * 4);

    return NOWUI_MSDF_OK;
}

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

int nowui_msdf_session_create(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    NowUIMsdfSessionInfo *info,
    void **out_session,
    char *error_buffer,
    int error_buffer_length) {

    if (out_session)
        *out_session = nullptr;

    try {
        if (!out_session)
            throw std::runtime_error("Session output pointer is null.");

        *out_session = create_session(font_data, font_data_length, size, pixel_range, SESSION_MIN_SIDE, SESSION_MAX_SIDE, info);
        set_error(error_buffer, error_buffer_length, std::string());
        return NOWUI_MSDF_OK;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_msdf_session_create_fixed(
    const unsigned char *font_data,
    int font_data_length,
    int size,
    int pixel_range,
    int atlas_side,
    NowUIMsdfSessionInfo *info,
    void **out_session,
    char *error_buffer,
    int error_buffer_length) {

    if (out_session)
        *out_session = nullptr;

    try {
        if (!out_session)
            throw std::runtime_error("Session output pointer is null.");

        *out_session = create_session(font_data, font_data_length, size, pixel_range, atlas_side, atlas_side, info);
        set_error(error_buffer, error_buffer_length, std::string());
        return NOWUI_MSDF_OK;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_msdf_session_add_glyphs(
    void *session,
    const unsigned int *codepoints,
    int codepoint_count,
    NowUIMsdfGlyph *glyphs,
    int glyph_capacity,
    int *out_glyph_count,
    int *out_atlas_side,
    int *out_change_flags,
    char *error_buffer,
    int error_buffer_length) {

    try {
        const int result = session_add_glyphs(
            static_cast<NowUIMsdfSessionState *>(session),
            codepoints,
            codepoint_count,
            glyphs,
            glyph_capacity,
            out_glyph_count,
            out_atlas_side,
            out_change_flags);

        set_error(error_buffer, error_buffer_length, std::string());
        return result;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_msdf_session_copy_atlas(
    void *session,
    unsigned char *atlas_rgba,
    int atlas_rgba_length,
    char *error_buffer,
    int error_buffer_length) {

    try {
        const int result = session_copy_atlas(
            static_cast<NowUIMsdfSessionState *>(session),
            atlas_rgba,
            atlas_rgba_length);

        set_error(error_buffer, error_buffer_length, std::string());
        return result;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native font compiler error.");
    }

    return NOWUI_MSDF_ERROR;
}

void nowui_msdf_session_destroy(void *session) {
    delete static_cast<NowUIMsdfSessionState *>(session);
}

#ifdef NOWUI_MSDF_NO_SHAPING

/* Shaping was not compiled into this build; the exports stay present so the
 * managed bindings get a clean error instead of EntryPointNotFoundException. */

int nowui_shaper_create(
    const unsigned char *font_data,
    int font_data_length,
    void **out_shaper,
    char *error_buffer,
    int error_buffer_length) {
    (void)font_data;
    (void)font_data_length;

    if (out_shaper)
        *out_shaper = nullptr;

    set_error(error_buffer, error_buffer_length, "Shaping support was not compiled into this nowui-msdf build.");
    return NOWUI_MSDF_ERROR;
}

int nowui_shaper_shape_utf16(
    void *shaper_handle,
    const unsigned short *text,
    int text_length,
    NowUIShapedGlyph *glyphs,
    int glyph_capacity,
    int *out_glyph_count,
    char *error_buffer,
    int error_buffer_length) {
    (void)shaper_handle;
    (void)text;
    (void)text_length;
    (void)glyphs;
    (void)glyph_capacity;

    if (out_glyph_count)
        *out_glyph_count = 0;

    set_error(error_buffer, error_buffer_length, "Shaping support was not compiled into this nowui-msdf build.");
    return NOWUI_MSDF_ERROR;
}

void nowui_shaper_destroy(void *shaper) {
    (void)shaper;
}

#else

int nowui_shaper_create(
    const unsigned char *font_data,
    int font_data_length,
    void **out_shaper,
    char *error_buffer,
    int error_buffer_length) {
    if (out_shaper)
        *out_shaper = nullptr;

    try {
        if (!font_data || font_data_length <= 0 || !out_shaper) {
            set_error(error_buffer, error_buffer_length, "Shaper arguments are invalid.");
            return NOWUI_MSDF_ERROR;
        }

        std::unique_ptr<NowUIShaperState> shaper(new NowUIShaperState());

        // Own a copy: the managed caller's array is only pinned for this call.
        shaper->data.assign(font_data, font_data + font_data_length);
        shaper->blob = hb_blob_create(
            reinterpret_cast<const char *>(shaper->data.data()),
            static_cast<unsigned int>(font_data_length),
            HB_MEMORY_MODE_READONLY,
            nullptr,
            nullptr);
        shaper->face = hb_face_create(shaper->blob, 0);

        if (!shaper->face || hb_face_get_glyph_count(shaper->face) == 0) {
            set_error(error_buffer, error_buffer_length, "HarfBuzz could not parse the font.");
            return NOWUI_MSDF_ERROR;
        }

        shaper->upem = hb_face_get_upem(shaper->face);

        if (shaper->upem == 0)
            shaper->upem = 1000;

        shaper->font = hb_font_create(shaper->face);
        hb_font_set_scale(
            shaper->font,
            static_cast<int>(shaper->upem),
            static_cast<int>(shaper->upem));
        shaper->buffer = hb_buffer_create();

        *out_shaper = shaper.release();
        set_error(error_buffer, error_buffer_length, std::string());
        return NOWUI_MSDF_OK;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native shaper error.");
    }

    return NOWUI_MSDF_ERROR;
}

int nowui_shaper_shape_utf16(
    void *shaper_handle,
    const unsigned short *text,
    int text_length,
    NowUIShapedGlyph *glyphs,
    int glyph_capacity,
    int *out_glyph_count,
    char *error_buffer,
    int error_buffer_length) {
    if (out_glyph_count)
        *out_glyph_count = 0;

    try {
        auto *shaper = static_cast<NowUIShaperState *>(shaper_handle);

        if (!shaper || !text || text_length <= 0 || !out_glyph_count) {
            set_error(error_buffer, error_buffer_length, "Shape arguments are invalid.");
            return NOWUI_MSDF_ERROR;
        }

        hb_buffer_t *buffer = shaper->buffer;
        hb_buffer_reset(buffer);
        hb_buffer_add_utf16(
            buffer,
            reinterpret_cast<const uint16_t *>(text),
            text_length,
            0,
            text_length);
        hb_buffer_guess_segment_properties(buffer);
        hb_shape(shaper->font, buffer, nullptr, 0);

        unsigned int count = hb_buffer_get_length(buffer);
        *out_glyph_count = static_cast<int>(count);

        if (!glyphs || static_cast<int>(count) > glyph_capacity) {
            set_error(error_buffer, error_buffer_length, std::string());
            return NOWUI_MSDF_BUFFER_TOO_SMALL;
        }

        const hb_glyph_info_t *infos = hb_buffer_get_glyph_infos(buffer, nullptr);
        const hb_glyph_position_t *positions = hb_buffer_get_glyph_positions(buffer, nullptr);
        const float inverse_upem = 1.0f / static_cast<float>(shaper->upem);

        for (unsigned int i = 0; i < count; ++i) {
            glyphs[i].glyph_index = infos[i].codepoint;
            glyphs[i].cluster = infos[i].cluster;
            glyphs[i].x_advance = positions[i].x_advance * inverse_upem;
            glyphs[i].y_advance = positions[i].y_advance * inverse_upem;
            glyphs[i].x_offset = positions[i].x_offset * inverse_upem;
            glyphs[i].y_offset = positions[i].y_offset * inverse_upem;
        }

        set_error(error_buffer, error_buffer_length, std::string());
        return NOWUI_MSDF_OK;
    } catch (const std::exception &ex) {
        set_error(error_buffer, error_buffer_length, ex.what());
    } catch (...) {
        set_error(error_buffer, error_buffer_length, "Unknown native shaper error.");
    }

    return NOWUI_MSDF_ERROR;
}

void nowui_shaper_destroy(void *shaper) {
    delete static_cast<NowUIShaperState *>(shaper);
}

#endif /* NOWUI_MSDF_NO_SHAPING */

const char *nowui_msdf_version() {
    return "nowui-msdf/4";
}

}

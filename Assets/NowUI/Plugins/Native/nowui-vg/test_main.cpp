// Standalone smoke test for nowui-vg (not part of the Unity plugin build).
// Exercises fills (solid/gradient/holes), strokes, trims and matte clipping, then
// rasterizes the produced triangles to a BMP for visual inspection.
//
//   cl /O2 /EHsc /std:c++17 test_main.cpp nowui_vg.cpp /Fe:nowui-vg-test.exe

#include "nowui_vg.h"

#include <cmath>
#include <cstdio>
#include <cstdint>
#include <cstring>
#include <vector>

namespace {

const float CIRCLE_TANGENT = 0.5519151f;

void packCircle(std::vector<float> &data, float cx, float cy, float rx, float ry)
{
    float tx = rx * CIRCLE_TANGENT;
    float ty = ry * CIRCLE_TANGENT;

    data.push_back(4.f); // point count
    data.push_back(1.f); // closed

    auto point = [&](float px, float py, float ix, float iy, float ox, float oy) {
        data.push_back(px); data.push_back(py);
        data.push_back(ix); data.push_back(iy);
        data.push_back(ox); data.push_back(oy);
    };

    point(cx, cy - ry, -tx, 0.f, tx, 0.f);
    point(cx + rx, cy, 0.f, -ty, 0.f, ty);
    point(cx, cy + ry, tx, 0.f, -tx, 0.f);
    point(cx - rx, cy, 0.f, ty, 0.f, -ty);
}

void packRect(std::vector<float> &data, float x0, float y0, float x1, float y1)
{
    data.push_back(4.f);
    data.push_back(1.f);

    auto point = [&](float px, float py) {
        data.push_back(px); data.push_back(py);
        data.push_back(0.f); data.push_back(0.f);
        data.push_back(0.f); data.push_back(0.f);
    };

    point(x0, y0);
    point(x1, y0);
    point(x1, y1);
    point(x0, y1);
}

void packLine(std::vector<float> &data, float x0, float y0, float x1, float y1)
{
    data.push_back(2.f);
    data.push_back(0.f); // open

    data.push_back(x0); data.push_back(y0);
    data.push_back(0.f); data.push_back(0.f);
    data.push_back(0.f); data.push_back(0.f);

    data.push_back(x1); data.push_back(y1);
    data.push_back(0.f); data.push_back(0.f);
    data.push_back(0.f); data.push_back(0.f);
}

std::vector<float> solidPaint(float r, float g, float b, float a)
{
    return {0.f, r, g, b, a, 1.f, 0.f, 0.f, 0.f, 0.f, 0.f, 0.f, 0.f};
}

std::vector<float> radialPaint(float cx, float cy, float ex, float ey)
{
    // Two stops: white center -> orange edge, plus alpha stops at full opacity.
    std::vector<float> paint = {2.f, 1.f, 1.f, 1.f, 1.f, 1.f, cx, cy, ex, ey, 2.f, 12.f, 12.f};
    float stops[] = {0.f, 1.f, 0.9f, 0.3f, 1.f, 1.f, 0.55f, 0.1f, 0.f, 1.f, 1.f, 1.f};
    paint.insert(paint.end(), stops, stops + 12);
    return paint;
}

struct Image {
    int width, height;
    std::vector<float> pixels; // rgb

    Image(int w, int h) : width(w), height(h), pixels(w * h * 3, 0.13f) {}

    void blend(int x, int y, float r, float g, float b, float a)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return;

        float *p = &pixels[(y * width + x) * 3];
        p[0] = p[0] * (1.f - a) + r * a;
        p[1] = p[1] * (1.f - a) + g * a;
        p[2] = p[2] * (1.f - a) + b * a;
    }

    bool writeBmp(const char *path) const
    {
        FILE *file = std::fopen(path, "wb");
        if (!file)
            return false;

        int rowSize = (width * 3 + 3) & ~3;
        int dataSize = rowSize * height;
        int fileSize = 54 + dataSize;

        unsigned char header[54] = {0};
        header[0] = 'B'; header[1] = 'M';
        std::memcpy(header + 2, &fileSize, 4);
        int dataOffset = 54;
        std::memcpy(header + 10, &dataOffset, 4);
        int infoSize = 40;
        std::memcpy(header + 14, &infoSize, 4);
        std::memcpy(header + 18, &width, 4);
        std::memcpy(header + 22, &height, 4);
        short planes = 1, bpp = 24;
        std::memcpy(header + 26, &planes, 2);
        std::memcpy(header + 28, &bpp, 2);
        std::memcpy(header + 34, &dataSize, 4);
        std::fwrite(header, 1, 54, file);

        std::vector<unsigned char> row(rowSize, 0);

        for (int y = height - 1; y >= 0; --y) { // BMP is bottom-up
            for (int x = 0; x < width; ++x) {
                const float *p = &pixels[(y * width + x) * 3];
                auto clampByte = [](float v) {
                    int i = static_cast<int>(v * 255.f + 0.5f);
                    return static_cast<unsigned char>(i < 0 ? 0 : (i > 255 ? 255 : i));
                };
                row[x * 3 + 0] = clampByte(p[2]);
                row[x * 3 + 1] = clampByte(p[1]);
                row[x * 3 + 2] = clampByte(p[0]);
            }
            std::fwrite(row.data(), 1, rowSize, file);
        }

        std::fclose(file);
        return true;
    }
};

void rasterize(Image &image, const std::vector<float> &positions, const std::vector<float> &colors, const std::vector<int> &indices)
{
    for (size_t t = 0; t + 2 < indices.size(); t += 3) {
        int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];

        float x0 = positions[i0 * 2], y0 = positions[i0 * 2 + 1];
        float x1 = positions[i1 * 2], y1 = positions[i1 * 2 + 1];
        float x2 = positions[i2 * 2], y2 = positions[i2 * 2 + 1];

        float minX = std::floor(std::fmin(x0, std::fmin(x1, x2)));
        float maxX = std::ceil(std::fmax(x0, std::fmax(x1, x2)));
        float minY = std::floor(std::fmin(y0, std::fmin(y1, y2)));
        float maxY = std::ceil(std::fmax(y0, std::fmax(y1, y2)));

        float area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);

        if (std::fabs(area) < 1e-9f)
            continue;

        for (int y = static_cast<int>(minY); y <= static_cast<int>(maxY); ++y) {
            for (int x = static_cast<int>(minX); x <= static_cast<int>(maxX); ++x) {
                float px = x + 0.5f, py = y + 0.5f;
                float w0 = ((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)) / area;
                float w1 = ((x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)) / area;
                float w2 = 1.f - w0 - w1;

                if (w0 < 0.f || w1 < 0.f || w2 < 0.f)
                    continue;

                float r = w0 * colors[i0 * 4] + w1 * colors[i1 * 4] + w2 * colors[i2 * 4];
                float g = w0 * colors[i0 * 4 + 1] + w1 * colors[i1 * 4 + 1] + w2 * colors[i2 * 4 + 1];
                float b = w0 * colors[i0 * 4 + 2] + w1 * colors[i1 * 4 + 2] + w2 * colors[i2 * 4 + 2];
                float a = w0 * colors[i0 * 4 + 3] + w1 * colors[i1 * 4 + 3] + w2 * colors[i2 * 4 + 3];
                image.blend(x, y, r, g, b, a);
            }
        }
    }
}

} // namespace

int main()
{
    nowui_vg_begin(0.2f, 0.75f);

    int failures = 0;
    auto check = [&](int result, const char *what) {
        if (result != NOWUI_VG_OK) {
            std::printf("FAIL: %s returned %d\n", what, result);
            ++failures;
        }
    };

    // 1. Radial gradient face circle.
    std::vector<float> face;
    packCircle(face, 128.f, 128.f, 100.f, 100.f);
    auto facePaint = radialPaint(128.f, 110.f, 228.f, 110.f);
    check(nowui_vg_fill(face.data(), (int)face.size(), 1, nullptr, 0, 0, 0,
        facePaint.data(), (int)facePaint.size(), 0, 0, 0.f, 0.f, 0.f, 0), "gradient fill");

    // 2. Donut (rect with a hole, nonzero winding via two contours).
    std::vector<float> donut;
    packRect(donut, 40.f, 40.f, 100.f, 100.f);
    packRect(donut, 60.f, 60.f, 80.f, 80.f);
    auto donutPaint = solidPaint(0.2f, 0.4f, 0.9f, 1.f);
    check(nowui_vg_fill(donut.data(), (int)donut.size(), 2, nullptr, 0, 0, 0,
        donutPaint.data(), (int)donutPaint.size(), 0, 0, 0.f, 0.f, 0.f, 0), "donut fill");

    // 3. Fill clipped by a matte (right half of a rect), plus inverted variant.
    std::vector<float> bar;
    packRect(bar, 40.f, 180.f, 216.f, 200.f);
    std::vector<float> matte;
    packRect(matte, 128.f, 170.f, 230.f, 230.f);
    auto barPaint = solidPaint(0.1f, 0.8f, 0.3f, 1.f);
    check(nowui_vg_fill(bar.data(), (int)bar.size(), 1, matte.data(), (int)matte.size(), 1, 0,
        barPaint.data(), (int)barPaint.size(), 0, 0, 0.f, 0.f, 0.f, 0), "clipped fill");

    std::vector<float> bar2;
    packRect(bar2, 40.f, 205.f, 216.f, 225.f);
    auto bar2Paint = solidPaint(0.9f, 0.2f, 0.5f, 1.f);
    check(nowui_vg_fill(bar2.data(), (int)bar2.size(), 1, matte.data(), (int)matte.size(), 1, 1,
        bar2Paint.data(), (int)bar2Paint.size(), 0, 0, 0.f, 0.f, 0.f, 0), "inverse-clipped fill");

    // 4. Trimmed stroke with round caps (60% of a diagonal line).
    std::vector<float> line;
    packLine(line, 50.f, 30.f, 206.f, 60.f);
    auto strokePaint = solidPaint(0.95f, 0.85f, 0.1f, 1.f);
    check(nowui_vg_stroke(line.data(), (int)line.size(), 1, nullptr, 0, 0, 0,
        strokePaint.data(), (int)strokePaint.size(), 8.f, 2, 2, 1, 0.f, 0.6f, 0.f, 0), "trimmed stroke");

    // 5. Stroked circle (closed stroke ring).
    auto ringPaint = solidPaint(0.1f, 0.1f, 0.1f, 1.f);
    check(nowui_vg_stroke(face.data(), (int)face.size(), 1, nullptr, 0, 0, 0,
        ringPaint.data(), (int)ringPaint.size(), 3.f, 2, 2, 0, 0.f, 0.f, 0.f, 0), "circle stroke");

    int vertexCount = 0, indexCount = 0;
    float bounds[4] = {0};
    check(nowui_vg_end(&vertexCount, &indexCount, bounds), "end");

    std::printf("vertices=%d indices=%d bounds=(%.1f, %.1f)-(%.1f, %.1f)\n",
        vertexCount, indexCount, bounds[0], bounds[1], bounds[2], bounds[3]);

    if (vertexCount <= 0 || indexCount <= 0) {
        std::printf("FAIL: empty output\n");
        return 1;
    }

    std::vector<float> positions(vertexCount * 2);
    std::vector<float> colors(vertexCount * 4);
    std::vector<int> indices(indexCount);
    check(nowui_vg_copy(positions.data(), colors.data(), indices.data(), vertexCount, indexCount), "copy");

    Image image(256, 256);
    rasterize(image, positions, colors, indices);

    if (!image.writeBmp("nowui-vg-test.bmp")) {
        std::printf("FAIL: could not write bmp\n");
        ++failures;
    }

    std::printf(failures == 0 ? "OK\n" : "FAILED (%d)\n", failures);
    return failures == 0 ? 0 : 1;
}

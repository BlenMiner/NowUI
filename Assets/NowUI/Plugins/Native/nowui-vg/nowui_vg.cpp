#include "nowui_vg.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

namespace {

constexpr float EPSILON = 0.0001f;
constexpr int VERSION = 5;

struct Vec2 {
    float x, y;
};

struct Vec4 {
    float x, y, z, w;
};

inline Vec2 operator+(Vec2 a, Vec2 b) { return {a.x + b.x, a.y + b.y}; }
inline Vec2 operator-(Vec2 a, Vec2 b) { return {a.x - b.x, a.y - b.y}; }
inline Vec2 operator*(Vec2 a, float s) { return {a.x * s, a.y * s}; }

inline float dot(Vec2 a, Vec2 b) { return a.x * b.x + a.y * b.y; }
inline float cross(Vec2 a, Vec2 b) { return a.x * b.y - a.y * b.x; }
inline float lengthSq(Vec2 a) { return a.x * a.x + a.y * a.y; }
inline float length(Vec2 a) { return std::sqrt(lengthSq(a)); }
inline float distance(Vec2 a, Vec2 b) { return length(a - b); }
inline float lerp(float a, float b, float t) { return a + (b - a) * t; }
inline Vec2 lerp(Vec2 a, Vec2 b, float t) { return {lerp(a.x, b.x, t), lerp(a.y, b.y, t)}; }
inline float clamp01(float v) { return v < 0.f ? 0.f : (v > 1.f ? 1.f : v); }

inline Vec2 normalize(Vec2 v)
{
    float len = length(v);
    return len > EPSILON ? Vec2{v.x / len, v.y / len} : Vec2{0.f, 0.f};
}

struct Polyline {
    std::vector<Vec2> points;
    bool closed = false;

    float pathLength() const
    {
        float result = 0.f;
        for (size_t i = 1; i < points.size(); ++i)
            result += distance(points[i - 1], points[i]);
        if (closed && points.size() > 1)
            result += distance(points.back(), points.front());
        return result;
    }
};

/* Polylines are pooled so the point buffers keep their capacity across calls and
 * frames — per-call vector reallocation was a measurable cost. Pointers stay stable
 * because the pool owns each polyline behind its own allocation. */
struct PolylinePool {
    std::vector<Polyline *> items;
    size_t used = 0;

    Polyline *alloc()
    {
        if (used == items.size())
            items.push_back(new Polyline());

        Polyline *result = items[used++];
        result->points.clear();
        result->closed = false;
        return result;
    }

    /* Returns the most recent allocation to the pool (used for rejected results). */
    void releaseLast() { if (used > 0) --used; }

    void reset() { used = 0; }
};

using PolylineRefs = std::vector<Polyline *>;

struct Bounds {
    float minX = 0.f, minY = 0.f, maxX = 0.f, maxY = 0.f;
    bool valid = false;

    void add(Vec2 p)
    {
        if (!valid) {
            minX = maxX = p.x;
            minY = maxY = p.y;
            valid = true;
        } else {
            minX = std::min(minX, p.x);
            maxX = std::max(maxX, p.x);
            minY = std::min(minY, p.y);
            maxY = std::max(maxY, p.y);
        }
    }

    bool contains(Vec2 p) const
    {
        return valid && p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;
    }
};

Bounds boundsOf(const PolylineRefs &polylines)
{
    Bounds bounds;
    for (const Polyline *polyline : polylines)
        for (const Vec2 &p : polyline->points)
            bounds.add(p);
    return bounds;
}

struct Paint {
    int kind = 0; // 0 solid, 1 linear, 2 radial
    Vec4 color{1.f, 1.f, 1.f, 1.f};
    float alphaMultiplier = 1.f;
    Vec2 start{0.f, 0.f};
    Vec2 end{0.f, 0.f};
    int colorStopCount = 0;
    const float *stops = nullptr;
    int stopFloatCount = 0;
    float gradientSpan = 0.f;

    bool isGradient() const { return kind != 0; }

    Vec4 evaluateColorStops(float t) const
    {
        int count = colorStopCount;
        if (!stops || count <= 0 || stopFloatCount < 4)
            return {1.f, 1.f, 1.f, 1.f};

        count = std::min(count, stopFloatCount / 4);

        if (t <= stops[0])
            return {stops[1], stops[2], stops[3], 1.f};

        for (int i = 1; i < count; ++i) {
            int offset = i * 4;
            float position = stops[offset];
            if (t <= position) {
                int previous = offset - 4;
                float previousPosition = stops[previous];
                float segment = position - previousPosition;
                float blend = segment > 0.00001f ? (t - previousPosition) / segment : 0.f;
                return {
                    lerp(stops[previous + 1], stops[offset + 1], blend),
                    lerp(stops[previous + 2], stops[offset + 2], blend),
                    lerp(stops[previous + 3], stops[offset + 3], blend),
                    1.f};
            }
        }

        int last = (count - 1) * 4;
        return {stops[last + 1], stops[last + 2], stops[last + 3], 1.f};
    }

    float evaluateAlphaStops(float t) const
    {
        int colorFloats = colorStopCount * 4;
        if (!stops || stopFloatCount <= colorFloats + 1)
            return 1.f;

        int alphaCount = (stopFloatCount - colorFloats) / 2;
        if (alphaCount <= 0)
            return 1.f;

        if (t <= stops[colorFloats])
            return stops[colorFloats + 1];

        for (int i = 1; i < alphaCount; ++i) {
            int offset = colorFloats + i * 2;
            float position = stops[offset];
            if (t <= position) {
                int previous = offset - 2;
                float previousPosition = stops[previous];
                float segment = position - previousPosition;
                float blend = segment > 0.00001f ? (t - previousPosition) / segment : 0.f;
                return lerp(stops[previous + 1], stops[offset + 1], blend);
            }
        }

        return stops[colorFloats + (alphaCount - 1) * 2 + 1];
    }

    Vec4 colorAt(Vec2 position) const
    {
        if (!isGradient())
            return color;

        float t;
        if (kind == 2) {
            float radius = distance(start, end);
            t = radius > EPSILON ? distance(position, start) / radius : 0.f;
        } else {
            Vec2 direction = end - start;
            float lenSq = lengthSq(direction);
            t = lenSq > EPSILON ? dot(position - start, direction) / lenSq : 0.f;
        }

        t = clamp01(t);

        Vec4 result = evaluateColorStops(t);
        result.w *= evaluateAlphaStops(t) * alphaMultiplier;
        result.x *= color.x;
        result.y *= color.y;
        result.z *= color.z;
        result.w *= color.w;
        return result;
    }
};

// ----------------------------------------------------------------------------
// Frame context (single threaded, buffers reused across frames)
// ----------------------------------------------------------------------------

struct Context {
    float tolerance = 0.2f;
    float aaWidth = 0.75f;

    std::vector<float> positions; // x, y per vertex
    std::vector<float> colors;    // r, g, b, a per vertex
    std::vector<int> indices;

    float minX = 0.f, minY = 0.f, maxX = 0.f, maxY = 0.f;
    bool hasBounds = false;

    // Scratch (persists to avoid reallocation).
    PolylinePool pool;
    PolylineRefs shape;
    PolylineRefs clip;
    PolylineRefs trimmed;
    PolylineRefs clippedStroke;

    int addVertex(Vec2 position, Vec4 color)
    {
        // Bounds are computed in one pass at frame end (see computeBounds).
        int index = static_cast<int>(positions.size() / 2);
        positions.push_back(position.x);
        positions.push_back(position.y);
        colors.push_back(color.x);
        colors.push_back(color.y);
        colors.push_back(color.z);
        colors.push_back(color.w);
        return index;
    }

    void computeBounds()
    {
        size_t count = positions.size();
        hasBounds = count >= 2;

        if (!hasBounds) {
            minX = minY = maxX = maxY = 0.f;
            return;
        }

        minX = maxX = positions[0];
        minY = maxY = positions[1];

        for (size_t i = 2; i + 1 < count; i += 2) {
            float x = positions[i];
            float y = positions[i + 1];
            minX = std::min(minX, x);
            maxX = std::max(maxX, x);
            minY = std::min(minY, y);
            maxY = std::max(maxY, y);
        }
    }

    void addTriangle(int a, int b, int c)
    {
        indices.push_back(a);
        indices.push_back(b);
        indices.push_back(c);
    }

    void addQuad(int a, int b, int c, int d)
    {
        addTriangle(a, b, c);
        addTriangle(a, c, d);
    }
};

Context g_context;

// ----------------------------------------------------------------------------
// Flattening
// ----------------------------------------------------------------------------

bool isFlat(Vec2 p0, Vec2 c1, Vec2 c2, Vec2 p1, float tolerance)
{
    Vec2 chord = p1 - p0;
    float chordLengthSq = lengthSq(chord);

    if (chordLengthSq < EPSILON * EPSILON)
        return length(c1 - p0) + length(c2 - p0) < tolerance;

    float d1 = std::fabs((c1.x - p0.x) * chord.y - (c1.y - p0.y) * chord.x);
    float d2 = std::fabs((c2.x - p0.x) * chord.y - (c2.y - p0.y) * chord.x);
    return (d1 + d2) / std::sqrt(chordLengthSq) <= tolerance;
}

void flattenCubic(Vec2 p0, Vec2 c1, Vec2 c2, Vec2 p1, float tolerance, std::vector<Vec2> &output, int depth)
{
    if (depth >= 18 || isFlat(p0, c1, c2, p1, tolerance)) {
        output.push_back(p1);
        return;
    }

    Vec2 p01 = (p0 + c1) * 0.5f;
    Vec2 p12 = (c1 + c2) * 0.5f;
    Vec2 p23 = (c2 + p1) * 0.5f;
    Vec2 p012 = (p01 + p12) * 0.5f;
    Vec2 p123 = (p12 + p23) * 0.5f;
    Vec2 mid = (p012 + p123) * 0.5f;

    flattenCubic(p0, p01, p012, mid, tolerance, output, depth + 1);
    flattenCubic(mid, p123, p23, p1, tolerance, output, depth + 1);
}

/* Parses the packed contour stream into pooled polylines. Returns false on malformed data. */
bool flattenPacked(const float *data, int floatCount, int contourCount, float tolerance, PolylineRefs &output)
{
    output.clear();

    if (!data || contourCount <= 0)
        return true;

    int cursor = 0;

    for (int contour = 0; contour < contourCount; ++contour) {
        if (cursor + 2 > floatCount)
            return false;

        int pointCount = static_cast<int>(data[cursor++]);
        bool closed = data[cursor++] != 0.f;

        if (pointCount < 0 || cursor + pointCount * 6 > floatCount)
            return false;

        if (pointCount == 0)
            continue;

        const float *points = data + cursor;
        cursor += pointCount * 6;

        Polyline *polyline = g_context.pool.alloc();
        polyline->closed = closed;

        auto pointAt = [points](int i) { return Vec2{points[i * 6 + 0], points[i * 6 + 1]}; };
        auto inAt = [points](int i) { return Vec2{points[i * 6 + 2], points[i * 6 + 3]}; };
        auto outAt = [points](int i) { return Vec2{points[i * 6 + 4], points[i * 6 + 5]}; };

        polyline->points.push_back(pointAt(0));

        int segmentCount = closed ? pointCount : pointCount - 1;

        for (int i = 0; i < segmentCount; ++i) {
            int next = (i + 1) % pointCount;
            Vec2 p0 = pointAt(i);
            Vec2 c1 = p0 + outAt(i);
            Vec2 p1 = pointAt(next);
            Vec2 c2 = p1 + inAt(next);
            flattenCubic(p0, c1, c2, p1, tolerance, polyline->points, 0);
        }

        if (closed && polyline->points.size() > 1 &&
            lengthSq(polyline->points.back() - polyline->points.front()) < EPSILON * EPSILON) {
            polyline->points.pop_back();
        }

        if (polyline->points.size() >= 2)
            output.push_back(polyline);
        else
            g_context.pool.releaseLast();
    }

    return true;
}

// ----------------------------------------------------------------------------
// Trim paths
// ----------------------------------------------------------------------------

void extractRange(const Polyline &polyline, float start, float end, PolylineRefs &output)
{
    const auto &points = polyline.points;
    int count = static_cast<int>(points.size());

    if (count < 2 || end - start < EPSILON)
        return;

    Polyline *result = g_context.pool.alloc();
    result->closed = false;

    float walked = 0.f;
    int segmentCount = polyline.closed ? count : count - 1;

    for (int i = 0; i < segmentCount; ++i) {
        Vec2 from = points[i];
        Vec2 to = points[(i + 1) % count];
        float segmentLength = distance(from, to);

        if (segmentLength < EPSILON)
            continue;

        float segmentStart = walked;
        float segmentEnd = walked + segmentLength;

        if (segmentEnd > start && segmentStart < end) {
            float fromT = clamp01((start - segmentStart) / segmentLength);
            float toT = clamp01((end - segmentStart) / segmentLength);

            Vec2 clippedFrom = lerp(from, to, fromT);
            Vec2 clippedTo = lerp(from, to, toT);

            if (result->points.empty())
                result->points.push_back(clippedFrom);

            result->points.push_back(clippedTo);
        }

        walked = segmentEnd;

        if (walked >= end)
            break;
    }

    if (result->points.size() >= 2)
        output.push_back(result);
    else
        g_context.pool.releaseLast();
}

void extractWrappedRange(const Polyline &polyline, float start, float end, float totalLength, PolylineRefs &output)
{
    if (totalLength <= EPSILON)
        return;

    float span = std::min(end - start, totalLength);

    if (span <= EPSILON)
        return;

    start = start - std::floor(start / totalLength) * totalLength; // repeat into [0, len)
    end = start + span;

    if (end <= totalLength) {
        extractRange(polyline, start, end, output);
        return;
    }

    extractRange(polyline, start, totalLength, output);
    extractRange(polyline, 0.f, end - totalLength, output);
}

void applyTrim(PolylineRefs &polylines, float start, float end, float offset, bool individually, PolylineRefs &scratch)
{
    start = clamp01(start);
    end = clamp01(end);

    if (end < start)
        std::swap(start, end);

    if (end - start >= 1.f)
        return;

    float trimStart = start + offset;
    float trimEnd = end + offset;

    scratch.clear();

    if (individually) {
        for (Polyline *polyline : polylines) {
            float len = polyline->pathLength();
            extractWrappedRange(*polyline, trimStart * len, trimEnd * len, len, scratch);
        }
    } else {
        float totalLength = 0.f;
        for (Polyline *polyline : polylines)
            totalLength += polyline->pathLength();

        float globalStart = trimStart * totalLength;
        float globalEnd = trimEnd * totalLength;

        for (int pass = 0; pass < 2; ++pass) {
            float rangeStart = pass == 0 ? globalStart : globalStart - totalLength;
            float rangeEnd = pass == 0 ? globalEnd : globalEnd - totalLength;

            if (pass == 1 && globalEnd <= totalLength)
                break;

            float walked = 0.f;

            for (Polyline *polyline : polylines) {
                float len = polyline->pathLength();
                float localStart = std::max(rangeStart - walked, 0.f);
                float localEnd = std::min(rangeEnd - walked, len);

                if (localEnd > localStart && localEnd > 0.f && localStart < len)
                    extractRange(*polyline, localStart, localEnd, scratch);

                walked += len;
            }
        }
    }

    polylines.swap(scratch);
}

// ----------------------------------------------------------------------------
// Winding helpers
// ----------------------------------------------------------------------------

int windingNumber(const std::vector<Vec2> &points, Vec2 probe)
{
    int winding = 0;
    int count = static_cast<int>(points.size());

    for (int i = 0; i < count; ++i) {
        Vec2 from = points[i];
        Vec2 to = points[(i + 1) % count];

        if (from.y <= probe.y) {
            if (to.y > probe.y && cross(to - from, probe - from) > 0.f)
                ++winding;
        } else if (to.y <= probe.y && cross(to - from, probe - from) < 0.f) {
            --winding;
        }
    }

    return winding;
}

int windingAt(const PolylineRefs &contours, const Bounds &bounds, Vec2 probe)
{
    if (!bounds.contains(probe))
        return 0;

    int winding = 0;
    for (const Polyline *contour : contours)
        winding += windingNumber(contour->points, probe);
    return winding;
}

float signedArea(const std::vector<Vec2> &points)
{
    float area = 0.f;
    int count = static_cast<int>(points.size());

    for (int i = 0; i < count; ++i) {
        Vec2 current = points[i];
        Vec2 next = points[(i + 1) % count];
        area += current.x * next.y - next.x * current.y;
    }

    return area * 0.5f;
}

// ----------------------------------------------------------------------------
// Scanline fill
// ----------------------------------------------------------------------------

struct ScanEdge {
    float yTop, yBottom, xTop, slope;
    int winding;
    bool isClip;

    float xAt(float y) const { return xTop + (y - yTop) * slope; }
};

struct SlabEdge {
    float xTop, xBottom, xMiddle;
    int winding;
    int id; // index into g_edges, used to match spans across slabs
    bool isClip;
};

/* A span emitted in the previous slab; its bottom vertices are reused as the next
 * slab's top vertices when the span continues (nearly halves fill vertices).
 * Continuity is matched by x position: consecutive polyline edges meet at shared
 * vertices, so the previous bottom and the new top coincide within float noise. */
struct SpanJoin {
    float xLeft, xRight;
    int bottomLeft, bottomRight;
};

constexpr float WELD_EPSILON = 0.05f;

std::vector<ScanEdge> g_edges;
std::vector<float> g_slabYs;
std::vector<int> g_active;
std::vector<SlabEdge> g_slabEdges;
std::vector<SpanJoin> g_previousSpans;
std::vector<SpanJoin> g_currentSpans;
float g_previousSlabBottom;

void collectEdges(const Polyline &polyline, bool isClip)
{
    const auto &points = polyline.points;
    int count = static_cast<int>(points.size());

    if (count < 2)
        return;

    // Fills always treat contours as closed.
    for (int i = 0; i < count; ++i) {
        Vec2 from = points[i];
        Vec2 to = points[(i + 1) % count];

        if (std::fabs(from.y - to.y) < EPSILON)
            continue;

        ScanEdge edge;

        if (from.y < to.y) {
            edge.yTop = from.y;
            edge.yBottom = to.y;
            edge.xTop = from.x;
            edge.winding = 1;
        } else {
            edge.yTop = to.y;
            edge.yBottom = from.y;
            edge.xTop = to.x;
            edge.winding = -1;
        }

        edge.slope = (to.x - from.x) / (to.y - from.y);
        edge.isClip = isClip;
        g_edges.push_back(edge);
        g_slabYs.push_back(edge.yTop);
        g_slabYs.push_back(edge.yBottom);
    }
}

bool isInside(int shapeWinding, int clipWinding, bool hasClip, bool clipInvert, bool evenOdd)
{
    bool shapeInside = evenOdd ? (shapeWinding & 1) != 0 : shapeWinding != 0;

    if (!shapeInside)
        return false;

    if (!hasClip)
        return true;

    bool clipInside = clipWinding != 0;
    return clipInvert ? !clipInside : clipInside;
}

void emitTrapezoid(
    float topLeft, float topRight, float bottomLeft, float bottomRight,
    float ya, float yb, const Paint &paint)
{
    float topWidth = topRight - topLeft;
    float bottomWidth = bottomRight - bottomLeft;

    if (topWidth < EPSILON && bottomWidth < EPSILON)
        return;

    int chunks = 1;

    if (paint.isGradient() && paint.gradientSpan > 0.f) {
        float width = std::max(topWidth, bottomWidth);
        chunks = std::max(1, std::min(256, static_cast<int>(std::ceil(width / paint.gradientSpan))));
    }

    // Adjacent chunks share their boundary vertices.
    int previousRight = -1;
    int previousBottomRight = -1;

    for (int chunk = 0; chunk < chunks; ++chunk) {
        float t0 = static_cast<float>(chunk) / chunks;
        float t1 = static_cast<float>(chunk + 1) / chunks;

        Vec2 a{lerp(topLeft, topRight, t0), ya};
        Vec2 b{lerp(topLeft, topRight, t1), ya};
        Vec2 c{lerp(bottomLeft, bottomRight, t1), yb};
        Vec2 d{lerp(bottomLeft, bottomRight, t0), yb};

        int ia = chunk == 0 ? g_context.addVertex(a, paint.colorAt(a)) : previousRight;
        int id = chunk == 0 ? g_context.addVertex(d, paint.colorAt(d)) : previousBottomRight;
        int ib = g_context.addVertex(b, paint.colorAt(b));
        int ic = g_context.addVertex(c, paint.colorAt(c));
        g_context.addQuad(ia, ib, ic, id);

        previousRight = ib;
        previousBottomRight = ic;
    }
}

void emitSlabSpans(
    float ya, float yb, float fractionTop, float fractionBottom,
    bool hasClip, bool clipInvert, bool evenOdd, const Paint &paint)
{
    int shapeWinding = 0;
    int clipWinding = 0;
    bool spanOpen = false;
    float spanTopX = 0.f;
    float spanBottomX = 0.f;

    for (const auto &edge : g_slabEdges) {
        bool insideBefore = isInside(shapeWinding, clipWinding, hasClip, clipInvert, evenOdd);

        if (edge.isClip)
            clipWinding += edge.winding;
        else
            shapeWinding += edge.winding;

        bool insideAfter = isInside(shapeWinding, clipWinding, hasClip, clipInvert, evenOdd);

        if (insideBefore == insideAfter)
            continue;

        float edgeTopX = lerp(edge.xTop, edge.xBottom, fractionTop);
        float edgeBottomX = lerp(edge.xTop, edge.xBottom, fractionBottom);

        if (insideAfter) {
            spanTopX = edgeTopX;
            spanBottomX = edgeBottomX;
            spanOpen = true;
        } else if (spanOpen) {
            emitTrapezoid(spanTopX, edgeTopX, spanBottomX, edgeBottomX, ya, yb, paint);
            spanOpen = false;
        }
    }
}

/* Solid-color span emission with vertex sharing: when a span is bounded by the same
 * edge pair as in the previous slab, the previous bottom vertices are reused as this
 * slab's top vertices (the coordinates are identical by construction). */
void emitSlabSpansShared(
    float ya, float yb,
    bool hasClip, bool clipInvert, bool evenOdd,
    Vec4 color)
{
    int shapeWinding = 0;
    int clipWinding = 0;
    bool spanOpen = false;
    const SlabEdge *openEdge = nullptr;

    bool joinable = ya == g_previousSlabBottom && !g_previousSpans.empty();
    g_currentSpans.clear();

    for (const auto &edge : g_slabEdges) {
        bool insideBefore = isInside(shapeWinding, clipWinding, hasClip, clipInvert, evenOdd);

        if (edge.isClip)
            clipWinding += edge.winding;
        else
            shapeWinding += edge.winding;

        bool insideAfter = isInside(shapeWinding, clipWinding, hasClip, clipInvert, evenOdd);

        if (insideBefore == insideAfter)
            continue;

        if (insideAfter) {
            openEdge = &edge;
            spanOpen = true;
            continue;
        }

        if (!spanOpen)
            continue;

        spanOpen = false;

        float topWidth = edge.xTop - openEdge->xTop;
        float bottomWidth = edge.xBottom - openEdge->xBottom;

        if (topWidth < EPSILON && bottomWidth < EPSILON)
            continue;

        int topLeft = -1, topRight = -1;

        if (joinable) {
            for (const SpanJoin &join : g_previousSpans) {
                if (std::fabs(join.xLeft - openEdge->xTop) <= WELD_EPSILON &&
                    std::fabs(join.xRight - edge.xTop) <= WELD_EPSILON) {
                    topLeft = join.bottomLeft;
                    topRight = join.bottomRight;
                    break;
                }
            }
        }

        if (topLeft < 0) {
            topLeft = g_context.addVertex({openEdge->xTop, ya}, color);
            topRight = g_context.addVertex({edge.xTop, ya}, color);
        }

        int bottomRight = g_context.addVertex({edge.xBottom, yb}, color);
        int bottomLeft = g_context.addVertex({openEdge->xBottom, yb}, color);

        g_context.addQuad(topLeft, topRight, bottomRight, bottomLeft);
        g_currentSpans.push_back({openEdge->xBottom, edge.xBottom, bottomLeft, bottomRight});
    }
}

void tessellateFill(
    const PolylineRefs &contours,
    const PolylineRefs &clipContours,
    bool hasClip, bool clipInvert, bool evenOdd,
    const Paint &paint)
{
    g_edges.clear();
    g_slabYs.clear();

    for (const Polyline *contour : contours)
        collectEdges(*contour, false);

    if (hasClip) {
        for (const Polyline *contour : clipContours)
            collectEdges(*contour, true);
    }

    if (g_edges.empty())
        return;

    std::sort(g_slabYs.begin(), g_slabYs.end());

    size_t uniqueYs = 0;
    for (size_t i = 0; i < g_slabYs.size(); ++i) {
        if (uniqueYs == 0 || g_slabYs[i] - g_slabYs[uniqueYs - 1] > EPSILON)
            g_slabYs[uniqueYs++] = g_slabYs[i];
    }
    g_slabYs.resize(uniqueYs);

    std::sort(g_edges.begin(), g_edges.end(), [](const ScanEdge &a, const ScanEdge &b) {
        return a.yTop < b.yTop;
    });

    g_active.clear();
    size_t nextEdge = 0;
    bool useSharing = !paint.isGradient();
    float maxSlabHeight = paint.isGradient() && paint.gradientSpan > 0.f ? paint.gradientSpan : 3.4e38f;

    g_previousSpans.clear();
    g_previousSlabBottom = -3.4e38f;

    for (size_t slab = 0; slab + 1 < g_slabYs.size(); ++slab) {
        float slabTop = g_slabYs[slab];
        float slabBottom = g_slabYs[slab + 1];

        if (slabBottom - slabTop < EPSILON)
            continue;

        while (nextEdge < g_edges.size() && g_edges[nextEdge].yTop <= slabTop + EPSILON) {
            g_active.push_back(static_cast<int>(nextEdge));
            ++nextEdge;
        }

        size_t activeCount = 0;
        for (size_t i = 0; i < g_active.size(); ++i) {
            if (g_edges[g_active[i]].yBottom > slabTop + EPSILON)
                g_active[activeCount++] = g_active[i];
        }
        g_active.resize(activeCount);

        if (activeCount == 0)
            continue;

        g_slabEdges.clear();
        g_slabEdges.reserve(activeCount);

        for (size_t i = 0; i < activeCount; ++i) {
            int edgeIndex = g_active[i];
            const ScanEdge &edge = g_edges[edgeIndex];
            SlabEdge slabEdge;
            slabEdge.xTop = edge.xAt(slabTop);
            slabEdge.xBottom = edge.xAt(slabBottom);
            slabEdge.xMiddle = (slabEdge.xTop + slabEdge.xBottom) * 0.5f;
            slabEdge.winding = edge.winding;
            slabEdge.id = edgeIndex;
            slabEdge.isClip = edge.isClip;
            g_slabEdges.push_back(slabEdge);
        }

        // Insertion sort: small and mostly sorted between slabs.
        for (size_t i = 1; i < g_slabEdges.size(); ++i) {
            SlabEdge current = g_slabEdges[i];
            size_t j = i;
            while (j > 0 && g_slabEdges[j - 1].xMiddle > current.xMiddle) {
                g_slabEdges[j] = g_slabEdges[j - 1];
                --j;
            }
            g_slabEdges[j] = current;
        }

        if (useSharing) {
            emitSlabSpansShared(slabTop, slabBottom, hasClip, clipInvert, evenOdd, paint.color);
            g_previousSpans.swap(g_currentSpans);
            g_previousSlabBottom = slabBottom;
            continue;
        }

        int verticalChunks = std::max(1, static_cast<int>(std::ceil((slabBottom - slabTop) / maxSlabHeight)));
        float inverseChunks = 1.f / verticalChunks;

        for (int chunk = 0; chunk < verticalChunks; ++chunk) {
            float fractionTop = chunk * inverseChunks;
            float fractionBottom = chunk == verticalChunks - 1 ? 1.f : fractionTop + inverseChunks;
            float ya = lerp(slabTop, slabBottom, fractionTop);
            float yb = lerp(slabTop, slabBottom, fractionBottom);
            emitSlabSpans(ya, yb, fractionTop, fractionBottom, hasClip, clipInvert, evenOdd, paint);
        }
    }
}

// ----------------------------------------------------------------------------
// Fill anti-alias fringe
// ----------------------------------------------------------------------------

std::vector<Vec2> g_fringeNormals;
std::vector<char> g_fringeInside;
std::vector<Bounds> g_contourBounds;

int containmentDepth(const PolylineRefs &contours, size_t contourIndex)
{
    Vec2 probe = contours[contourIndex]->points[0];
    int depth = 0;

    for (size_t i = 0; i < contours.size(); ++i) {
        if (i == contourIndex)
            continue;
        if (!g_contourBounds[i].contains(probe))
            continue;
        if (windingNumber(contours[i]->points, probe) != 0)
            ++depth;
    }

    return depth;
}

void emitFillFringe(
    const PolylineRefs &contours,
    const PolylineRefs &clipContours,
    const Bounds &clipBounds,
    bool hasClip, bool clipInvert,
    const Paint &paint, float aaWidth)
{
    if (aaWidth <= 0.f)
        return;

    g_contourBounds.clear();
    g_contourBounds.reserve(contours.size());

    for (const Polyline *contour : contours) {
        Bounds bounds;
        for (const Vec2 &point : contour->points)
            bounds.add(point);
        g_contourBounds.push_back(bounds);
    }

    bool solid = !paint.isGradient();
    Vec4 solidInner = paint.color;
    Vec4 solidOuter = solidInner;
    solidOuter.w = 0.f;

    for (size_t contourIndex = 0; contourIndex < contours.size(); ++contourIndex) {
        const auto &points = contours[contourIndex]->points;
        int count = static_cast<int>(points.size());

        if (count < 3)
            continue;

        float area = signedArea(points);

        if (std::fabs(area) < EPSILON)
            continue;

        int depth = containmentDepth(contours, contourIndex);
        float direction = (area > 0.f ? 1.f : -1.f) * ((depth & 1) == 0 ? 1.f : -1.f);

        g_fringeNormals.clear();

        for (int i = 0; i < count; ++i) {
            Vec2 previous = points[(i - 1 + count) % count];
            Vec2 current = points[i];
            Vec2 next = points[(i + 1) % count];

            Vec2 inNormal = normalize(Vec2{(current - previous).y, -(current - previous).x});
            Vec2 outNormal = normalize(Vec2{(next - current).y, -(next - current).x});
            Vec2 average = normalize(inNormal + outNormal);

            if (average.x == 0.f && average.y == 0.f)
                average = inNormal;

            float miter = dot(average, outNormal);
            float scale = 1.f / std::max(0.35f, miter);
            g_fringeNormals.push_back(average * (scale * direction * aaWidth));
        }

        g_fringeInside.clear();

        for (int i = 0; i < count; ++i) {
            bool inside = true;

            if (hasClip) {
                inside = windingAt(clipContours, clipBounds, points[i]) != 0;
                if (clipInvert)
                    inside = !inside;
            }

            g_fringeInside.push_back(inside ? 1 : 0);
        }

        int firstInner = -1;
        int previousInner = -1;
        int previousOuter = -1;

        for (int i = 0; i < count; ++i) {
            Vec2 position = points[i];
            Vec4 innerColor = solid ? solidInner : paint.colorAt(position);
            Vec4 outerColor = solid ? solidOuter : innerColor;

            if (!solid)
                outerColor.w = 0.f;

            int inner = g_context.addVertex(position, innerColor);
            int outer = g_context.addVertex(position + g_fringeNormals[i], outerColor);

            if (i == 0)
                firstInner = inner;
            else if (g_fringeInside[i - 1] && g_fringeInside[i])
                g_context.addQuad(previousInner, previousOuter, outer, inner);

            previousInner = inner;
            previousOuter = outer;
        }

        if (g_fringeInside[count - 1] && g_fringeInside[0])
            g_context.addQuad(previousInner, previousOuter, firstInner + 1, firstInner);
    }
}

// ----------------------------------------------------------------------------
// Strokes
// ----------------------------------------------------------------------------

std::vector<Vec2> g_strokePoints;
std::vector<Vec2> g_strokeNormals;

void connectStrokeRings(int ringA, int ringB)
{
    g_context.addQuad(ringA + 0, ringB + 0, ringB + 1, ringA + 1);
    g_context.addQuad(ringA + 1, ringB + 1, ringB + 2, ringA + 2);
    g_context.addQuad(ringA + 2, ringB + 2, ringB + 3, ringA + 3);
}

/* Strokes thinner than the AA width have a zero-width core, making the inner
 * vertex pair degenerate; a 3-vertex profile (edge+, center, edge-) covers the
 * same pixels with 25% fewer vertices and 33% fewer triangles. */
void connectThinStrokeRings(int ringA, int ringB)
{
    g_context.addQuad(ringA + 0, ringB + 0, ringB + 1, ringA + 1);
    g_context.addQuad(ringA + 1, ringB + 1, ringB + 2, ringA + 2);
}

void emitThinRoundCap(
    Vec2 center, Vec2 direction,
    float outerWidth, float coreAlpha,
    const Paint &paint)
{
    Vec2 normal{direction.y, -direction.x};

    Vec4 coreColor = paint.colorAt(center);
    coreColor.w *= coreAlpha;
    Vec4 edgeColor = coreColor;
    edgeColor.w = 0.f;

    int segments = std::max(4, std::min(24, static_cast<int>(std::ceil(outerWidth * 0.6f)) + 3));
    int centerIndex = g_context.addVertex(center, coreColor);
    int previousOuter = -1;

    for (int i = 0; i <= segments; ++i) {
        float angle = 3.14159265358979f * i / segments;
        Vec2 radial = normal * std::cos(angle) + direction * std::sin(angle);

        int outer = g_context.addVertex(center + radial * outerWidth, edgeColor);

        if (i > 0)
            g_context.addTriangle(centerIndex, previousOuter, outer);

        previousOuter = outer;
    }
}

void emitRoundCap(
    Vec2 center, Vec2 direction,
    float innerWidth, float outerWidth, float coreAlpha,
    const Paint &paint)
{
    Vec2 normal{direction.y, -direction.x};

    Vec4 coreColor = paint.colorAt(center);
    coreColor.w *= coreAlpha;
    Vec4 edgeColor = coreColor;
    edgeColor.w = 0.f;

    int segments = std::max(4, std::min(24, static_cast<int>(std::ceil(outerWidth * 0.6f)) + 3));
    int centerIndex = g_context.addVertex(center, coreColor);

    int previousInner = -1;
    int previousOuter = -1;

    for (int i = 0; i <= segments; ++i) {
        float angle = 3.14159265358979f * i / segments;
        Vec2 radial = normal * std::cos(angle) + direction * std::sin(angle);

        int inner = g_context.addVertex(center + radial * innerWidth, coreColor);
        int outer = g_context.addVertex(center + radial * outerWidth, edgeColor);

        if (i > 0) {
            g_context.addTriangle(centerIndex, previousInner, inner);
            g_context.addQuad(previousInner, previousOuter, outer, inner);
        }

        previousInner = inner;
        previousOuter = outer;
    }
}

void emitStrokePolyline(
    const Polyline &polyline,
    float halfWidth, int cap, int join,
    const Paint &paint, float aaWidth)
{
    (void)join; // joins use clamped miters, matching the managed implementation

    g_strokePoints.clear();

    for (const Vec2 &point : polyline.points) {
        if (g_strokePoints.empty() || lengthSq(point - g_strokePoints.back()) > EPSILON * EPSILON)
            g_strokePoints.push_back(point);
    }

    bool closed = polyline.closed;

    if (closed && g_strokePoints.size() > 1 &&
        lengthSq(g_strokePoints.back() - g_strokePoints.front()) < EPSILON * EPSILON) {
        g_strokePoints.pop_back();
    }

    int count = static_cast<int>(g_strokePoints.size());

    if (count < 2)
        return;

    if (!closed && cap == 3) {
        Vec2 startDirection = normalize(g_strokePoints[0] - g_strokePoints[1]);
        Vec2 endDirection = normalize(g_strokePoints[count - 1] - g_strokePoints[count - 2]);
        g_strokePoints[0] = g_strokePoints[0] + startDirection * halfWidth;
        g_strokePoints[count - 1] = g_strokePoints[count - 1] + endDirection * halfWidth;
    }

    float innerWidth = std::max(halfWidth - aaWidth, 0.f);
    float outerWidth = halfWidth + aaWidth;
    float coreAlpha = halfWidth < aaWidth ? halfWidth / aaWidth : 1.f;

    g_strokeNormals.clear();

    for (int i = 0; i < count; ++i) {
        Vec2 inDirection, outDirection;

        if (closed) {
            inDirection = normalize(g_strokePoints[i] - g_strokePoints[(i - 1 + count) % count]);
            outDirection = normalize(g_strokePoints[(i + 1) % count] - g_strokePoints[i]);
        } else {
            inDirection = i > 0 ? normalize(g_strokePoints[i] - g_strokePoints[i - 1]) : Vec2{0.f, 0.f};
            outDirection = i < count - 1 ? normalize(g_strokePoints[i + 1] - g_strokePoints[i]) : Vec2{0.f, 0.f};

            if (inDirection.x == 0.f && inDirection.y == 0.f) inDirection = outDirection;
            if (outDirection.x == 0.f && outDirection.y == 0.f) outDirection = inDirection;
        }

        Vec2 inNormal{inDirection.y, -inDirection.x};
        Vec2 outNormal{outDirection.y, -outDirection.x};
        Vec2 average = normalize(inNormal + outNormal);

        if (average.x == 0.f && average.y == 0.f)
            average = inNormal;

        float miter = dot(average, outNormal);
        float scale = 1.f / std::max(0.35f, std::fabs(miter));
        g_strokeNormals.push_back(average * scale);
    }

    int firstRing = -1;
    int previousRing = -1;

    bool solid = !paint.isGradient();
    bool thin = innerWidth <= 0.f;
    Vec4 solidCore = paint.color;
    solidCore.w *= coreAlpha;
    Vec4 solidEdge = solidCore;
    solidEdge.w = 0.f;

    for (int i = 0; i < count; ++i) {
        Vec2 position = g_strokePoints[i];
        Vec2 normal = g_strokeNormals[i];

        Vec4 coreColor = solidCore;
        Vec4 edgeColor = solidEdge;

        if (!solid) {
            coreColor = paint.colorAt(position);
            coreColor.w *= coreAlpha;
            edgeColor = coreColor;
            edgeColor.w = 0.f;
        }

        int ring = g_context.addVertex(position + normal * outerWidth, edgeColor);

        if (thin) {
            g_context.addVertex(position, coreColor);
            g_context.addVertex(position - normal * outerWidth, edgeColor);
        } else {
            g_context.addVertex(position + normal * innerWidth, coreColor);
            g_context.addVertex(position - normal * innerWidth, coreColor);
            g_context.addVertex(position - normal * outerWidth, edgeColor);
        }

        if (i == 0)
            firstRing = ring;

        if (i > 0) {
            if (thin)
                connectThinStrokeRings(previousRing, ring);
            else
                connectStrokeRings(previousRing, ring);
        }

        previousRing = ring;
    }

    if (closed) {
        if (thin)
            connectThinStrokeRings(previousRing, firstRing);
        else
            connectStrokeRings(previousRing, firstRing);
    }

    if (!closed && cap == 2) {
        Vec2 startDirection = normalize(g_strokePoints[0] - g_strokePoints[1]);
        Vec2 endDirection = normalize(g_strokePoints[count - 1] - g_strokePoints[count - 2]);

        if (thin) {
            emitThinRoundCap(g_strokePoints[0], startDirection, outerWidth, coreAlpha, paint);
            emitThinRoundCap(g_strokePoints[count - 1], endDirection, outerWidth, coreAlpha, paint);
        } else {
            emitRoundCap(g_strokePoints[0], startDirection, innerWidth, outerWidth, coreAlpha, paint);
            emitRoundCap(g_strokePoints[count - 1], endDirection, innerWidth, outerWidth, coreAlpha, paint);
        }
    }
}

// ----------------------------------------------------------------------------
// Polyline clipping against matte contours (strokes)
// ----------------------------------------------------------------------------

std::vector<float> g_crossings;

void collectCrossings(Vec2 from, Vec2 to, const PolylineRefs &clipContours)
{
    Vec2 direction = to - from;

    for (const Polyline *contour : clipContours) {
        const auto &points = contour->points;
        int count = static_cast<int>(points.size());

        for (int i = 0; i < count; ++i) {
            Vec2 edgeFrom = points[i];
            Vec2 edgeTo = points[(i + 1) % count];
            Vec2 edgeDirection = edgeTo - edgeFrom;

            float denominator = cross(direction, edgeDirection);

            if (std::fabs(denominator) < EPSILON)
                continue;

            Vec2 delta = edgeFrom - from;
            float t = cross(delta, edgeDirection) / denominator;
            float u = cross(delta, direction) / denominator;

            if (t > 0.f && t < 1.f && u >= 0.f && u <= 1.f)
                g_crossings.push_back(t);
        }
    }
}

void clipPolylines(
    const PolylineRefs &polylines,
    const PolylineRefs &clipContours,
    const Bounds &clipBounds,
    bool clipInvert,
    PolylineRefs &output)
{
    output.clear();

    for (const Polyline *polyline : polylines) {
        const auto &points = polyline->points;
        int count = static_cast<int>(points.size());
        int segmentCount = polyline->closed ? count : count - 1;

        Polyline *current = nullptr;

        auto flushCurrent = [&]() {
            if (!current)
                return;

            if (current->points.size() >= 2)
                output.push_back(current);
            else
                g_context.pool.releaseLast();

            current = nullptr;
        };

        for (int i = 0; i < segmentCount; ++i) {
            Vec2 from = points[i];
            Vec2 to = points[(i + 1) % count];

            // Segments entirely outside the clip bounds cannot cross the clip;
            // a single winding test decides them (cheap, common case for mattes).
            bool mayCross = clipBounds.valid &&
                !(std::max(from.x, to.x) < clipBounds.minX ||
                  std::min(from.x, to.x) > clipBounds.maxX ||
                  std::max(from.y, to.y) < clipBounds.minY ||
                  std::min(from.y, to.y) > clipBounds.maxY);

            g_crossings.clear();
            g_crossings.push_back(0.f);

            if (mayCross)
                collectCrossings(from, to, clipContours);

            g_crossings.push_back(1.f);
            std::sort(g_crossings.begin(), g_crossings.end());

            for (size_t piece = 0; piece + 1 < g_crossings.size(); ++piece) {
                float t0 = g_crossings[piece];
                float t1 = g_crossings[piece + 1];

                if (t1 - t0 < EPSILON)
                    continue;

                Vec2 middle = lerp(from, to, (t0 + t1) * 0.5f);
                bool inside = windingAt(clipContours, clipBounds, middle) != 0;

                if (clipInvert)
                    inside = !inside;

                if (!inside) {
                    flushCurrent();
                    continue;
                }

                Vec2 pieceFrom = lerp(from, to, t0);
                Vec2 pieceTo = lerp(from, to, t1);

                if (!current) {
                    current = g_context.pool.alloc();
                    current->closed = false;
                    current->points.push_back(pieceFrom);
                }

                current->points.push_back(pieceTo);
            }
        }

        flushCurrent();
    }
}

// ----------------------------------------------------------------------------
// Paint parsing
// ----------------------------------------------------------------------------

bool parsePaint(const float *data, int floatCount, Paint &paint)
{
    if (!data || floatCount < 13)
        return false;

    paint.kind = static_cast<int>(data[0]);
    paint.color = {data[1], data[2], data[3], data[4]};
    paint.alphaMultiplier = data[5];
    paint.start = {data[6], data[7]};
    paint.end = {data[8], data[9]};
    paint.colorStopCount = static_cast<int>(data[10]);
    paint.stopFloatCount = static_cast<int>(data[11]);
    paint.gradientSpan = data[12];
    paint.stops = floatCount > 13 ? data + 13 : nullptr;

    if (paint.stopFloatCount > floatCount - 13)
        paint.stopFloatCount = floatCount - 13;

    return true;
}

} // namespace

// ----------------------------------------------------------------------------
// Exported API
// ----------------------------------------------------------------------------

extern "C" {

NOWUI_VG_EXPORT void nowui_vg_begin(float flatten_tolerance, float aa_width)
{
    g_context.tolerance = flatten_tolerance > 0.f ? flatten_tolerance : 0.2f;
    g_context.aaWidth = aa_width >= 0.f ? aa_width : 0.75f;
    g_context.positions.clear();
    g_context.colors.clear();
    g_context.indices.clear();
    g_context.hasBounds = false;
    g_context.minX = g_context.minY = g_context.maxX = g_context.maxY = 0.f;
}

NOWUI_VG_EXPORT int nowui_vg_fill(
    const float *contours,
    int contour_float_count,
    int contour_count,
    const float *clip_contours,
    int clip_float_count,
    int clip_contour_count,
    int clip_invert,
    const float *paint_data,
    int paint_float_count,
    int even_odd,
    int has_trim,
    float trim_start,
    float trim_end,
    float trim_offset,
    int trim_individual)
{
    Paint paint;

    if (!parsePaint(paint_data, paint_float_count, paint))
        return NOWUI_VG_ERROR;

    g_context.pool.reset();

    if (!flattenPacked(contours, contour_float_count, contour_count, g_context.tolerance, g_context.shape))
        return NOWUI_VG_ERROR;

    bool hasClip = clip_contours != nullptr && clip_contour_count > 0;

    if (hasClip && !flattenPacked(clip_contours, clip_float_count, clip_contour_count, g_context.tolerance, g_context.clip))
        return NOWUI_VG_ERROR;

    if (has_trim)
        applyTrim(g_context.shape, trim_start, trim_end, trim_offset, trim_individual != 0, g_context.trimmed);

    if (g_context.shape.empty())
        return NOWUI_VG_OK;

    Bounds clipBounds = hasClip ? boundsOf(g_context.clip) : Bounds{};

    tessellateFill(g_context.shape, g_context.clip, hasClip, clip_invert != 0, even_odd != 0, paint);
    emitFillFringe(g_context.shape, g_context.clip, clipBounds, hasClip, clip_invert != 0, paint, g_context.aaWidth);
    return NOWUI_VG_OK;
}

NOWUI_VG_EXPORT int nowui_vg_stroke(
    const float *contours,
    int contour_float_count,
    int contour_count,
    const float *clip_contours,
    int clip_float_count,
    int clip_contour_count,
    int clip_invert,
    const float *paint_data,
    int paint_float_count,
    float width,
    int cap,
    int join,
    int has_trim,
    float trim_start,
    float trim_end,
    float trim_offset,
    int trim_individual)
{
    Paint paint;

    if (!parsePaint(paint_data, paint_float_count, paint))
        return NOWUI_VG_ERROR;

    float halfWidth = width * 0.5f;

    if (halfWidth <= 0.f)
        return NOWUI_VG_OK;

    g_context.pool.reset();

    if (!flattenPacked(contours, contour_float_count, contour_count, g_context.tolerance, g_context.shape))
        return NOWUI_VG_ERROR;

    bool hasClip = clip_contours != nullptr && clip_contour_count > 0;

    if (hasClip && !flattenPacked(clip_contours, clip_float_count, clip_contour_count, g_context.tolerance, g_context.clip))
        return NOWUI_VG_ERROR;

    if (has_trim)
        applyTrim(g_context.shape, trim_start, trim_end, trim_offset, trim_individual != 0, g_context.trimmed);

    const PolylineRefs *lines = &g_context.shape;

    if (hasClip) {
        Bounds clipBounds = boundsOf(g_context.clip);
        clipPolylines(g_context.shape, g_context.clip, clipBounds, clip_invert != 0, g_context.clippedStroke);
        lines = &g_context.clippedStroke;
    }

    for (const Polyline *polyline : *lines)
        emitStrokePolyline(*polyline, halfWidth, cap, join, paint, g_context.aaWidth);

    return NOWUI_VG_OK;
}

NOWUI_VG_EXPORT int nowui_vg_end(
    int *out_vertex_count,
    int *out_index_count,
    float *out_bounds)
{
    if (!out_vertex_count || !out_index_count)
        return NOWUI_VG_ERROR;

    *out_vertex_count = static_cast<int>(g_context.positions.size() / 2);
    *out_index_count = static_cast<int>(g_context.indices.size());

    g_context.computeBounds();

    if (out_bounds) {
        out_bounds[0] = g_context.minX;
        out_bounds[1] = g_context.minY;
        out_bounds[2] = g_context.maxX;
        out_bounds[3] = g_context.maxY;
    }

    return NOWUI_VG_OK;
}

NOWUI_VG_EXPORT int nowui_vg_copy(
    float *positions,
    float *colors,
    int *indices,
    int vertex_capacity,
    int index_capacity)
{
    int vertexCount = static_cast<int>(g_context.positions.size() / 2);
    int indexCount = static_cast<int>(g_context.indices.size());

    if (vertexCount > vertex_capacity || indexCount > index_capacity)
        return NOWUI_VG_ERROR;

    if (vertexCount > 0) {
        std::memcpy(positions, g_context.positions.data(), g_context.positions.size() * sizeof(float));
        std::memcpy(colors, g_context.colors.data(), g_context.colors.size() * sizeof(float));
    }

    if (indexCount > 0)
        std::memcpy(indices, g_context.indices.data(), g_context.indices.size() * sizeof(int));

    return NOWUI_VG_OK;
}

NOWUI_VG_EXPORT void nowui_vg_blit_mesh(
    const float *src_positions,
    const float *src_colors,
    int vertex_count,
    const int *src_indices,
    int index_count,
    float position_scale,
    float offset_x,
    float offset_y,
    const float *tint4,
    const float *mask4,
    const float *rect4,
    float *dst_verts,
    float *dst_uvs,
    float *dst_rawuv,
    float *dst_rect,
    float *dst_radius,
    float *dst_color,
    float *dst_outline,
    float *dst_extra,
    float *dst_mask,
    int dst_vertex_base,
    int *dst_indices,
    int dst_index_base,
    int index_offset)
{
    const float rectX = rect4[0], rectY = rect4[1];
    const float inverseWidth = rect4[2] > 0.f ? 1.f / rect4[2] : 0.f;
    const float inverseHeight = rect4[3] > 0.f ? 1.f / rect4[3] : 0.f;

    for (int i = 0; i < vertex_count; ++i) {
        float x = src_positions[i * 2 + 0] * position_scale + offset_x;
        float y = src_positions[i * 2 + 1] * position_scale + offset_y;
        float meshY = -y;

        float u = (x - rectX) * inverseWidth;
        float v = (meshY - rectY) * inverseHeight;

        int base3 = (dst_vertex_base + i) * 3;
        dst_verts[base3 + 0] = x;
        dst_verts[base3 + 1] = meshY;
        dst_verts[base3 + 2] = 0.f;

        int base2 = (dst_vertex_base + i) * 2;
        dst_uvs[base2 + 0] = u;
        dst_uvs[base2 + 1] = v;

        int base4 = (dst_vertex_base + i) * 4;
        dst_rawuv[base4 + 0] = u;
        dst_rawuv[base4 + 1] = v;
        dst_rawuv[base4 + 2] = 0.f;
        dst_rawuv[base4 + 3] = 0.f;

        dst_rect[base4 + 0] = rect4[0];
        dst_rect[base4 + 1] = rect4[1];
        dst_rect[base4 + 2] = rect4[2];
        dst_rect[base4 + 3] = rect4[3];

        dst_radius[base4 + 0] = 0.f;
        dst_radius[base4 + 1] = 0.f;
        dst_radius[base4 + 2] = 0.f;
        dst_radius[base4 + 3] = 0.f;

        dst_color[base4 + 0] = src_colors[i * 4 + 0] * tint4[0];
        dst_color[base4 + 1] = src_colors[i * 4 + 1] * tint4[1];
        dst_color[base4 + 2] = src_colors[i * 4 + 2] * tint4[2];
        dst_color[base4 + 3] = src_colors[i * 4 + 3] * tint4[3];

        dst_outline[base4 + 0] = 0.f;
        dst_outline[base4 + 1] = 0.f;
        dst_outline[base4 + 2] = 0.f;
        dst_outline[base4 + 3] = 0.f;

        dst_extra[base4 + 0] = 0.f;
        dst_extra[base4 + 1] = 0.f;
        dst_extra[base4 + 2] = 0.f;
        dst_extra[base4 + 3] = 0.f;

        dst_mask[base4 + 0] = mask4[0];
        dst_mask[base4 + 1] = mask4[1];
        dst_mask[base4 + 2] = mask4[2];
        dst_mask[base4 + 3] = mask4[3];
    }

    for (int i = 0; i < index_count; ++i)
        dst_indices[dst_index_base + i] = src_indices[i] + index_offset;
}

NOWUI_VG_EXPORT void nowui_vg_blit_text_run(
    const float *glyphs,
    int start,
    int end,
    float x,
    float y,
    float font_size,
    float baseline,
    const float *mask4,
    const float *color4,
    const float *outline4,
    float outline,
    float pixel_range,
    float *dst_verts,
    float *dst_uvs,
    float *dst_rawuv,
    float *dst_rect,
    float *dst_radius,
    float *dst_color,
    float *dst_outline,
    float *dst_extra,
    float *dst_mask,
    int dst_vertex_base,
    int *dst_indices,
    int dst_index_base,
    float *out_pen_x,
    int *out_counts,
    float *out_bounds)
{
    constexpr int GLYPH_STRIDE = 12;
    float penX = x;
    int emittedVertices = 0;
    int emittedIndices = 0;
    bool hasBounds = false;
    float boundsMinX = 0.f;
    float boundsMinY = 0.f;
    float boundsMaxX = 0.f;
    float boundsMaxY = 0.f;

    const float maskX = mask4[0];
    const float maskY = mask4[1];
    const float maskW = mask4[2];
    const float maskH = mask4[3];
    const float extra4[4] = {outline, pixel_range, 0.f, 0.f};

    for (int i = start; i < end; ++i) {
        const float *glyph = glyphs + static_cast<size_t>(i) * GLYPH_STRIDE;
        const float xAdvance = glyph[8] * font_size;

        if (glyph[11] <= 0.5f) {
            penX += xAdvance;
            continue;
        }

        const float left = glyph[0] * font_size;
        const float bottom = glyph[1] * font_size;
        const float right = glyph[2] * font_size;
        const float top = glyph[3] * font_size;
        const float width = right - left;
        const float height = top - bottom;

        if (width <= 0.f || height <= 0.f) {
            penX += xAdvance;
            continue;
        }

        const float px = penX + glyph[9] * font_size + left;
        const float py = y - glyph[10] * font_size - bottom + baseline - height;
        const float rectX = px;
        const float rectY = -(py + height);

        if (rectX + width < maskX ||
            rectX >= maskX + maskW ||
            -rectY < maskY ||
            -rectY - height >= maskY + maskH) {
            penX += xAdvance;
            continue;
        }

        const float minX = rectX;
        const float minY = rectY;
        const float maxX = rectX + width;
        const float maxY = rectY + height;

        if (!hasBounds) {
            hasBounds = true;
            boundsMinX = minX;
            boundsMinY = minY;
            boundsMaxX = maxX;
            boundsMaxY = maxY;
        } else {
            boundsMinX = std::min(boundsMinX, minX);
            boundsMinY = std::min(boundsMinY, minY);
            boundsMaxX = std::max(boundsMaxX, maxX);
            boundsMaxY = std::max(boundsMaxY, maxY);
        }

        const int vertexIndex = dst_vertex_base + emittedVertices;
        const int base3 = vertexIndex * 3;
        const int base2 = vertexIndex * 2;
        const int base4 = vertexIndex * 4;

        dst_verts[base3 + 0] = rectX;
        dst_verts[base3 + 1] = rectY;
        dst_verts[base3 + 2] = 0.f;
        dst_verts[base3 + 3] = rectX;
        dst_verts[base3 + 4] = rectY + height;
        dst_verts[base3 + 5] = 0.f;
        dst_verts[base3 + 6] = rectX + width;
        dst_verts[base3 + 7] = rectY + height;
        dst_verts[base3 + 8] = 0.f;
        dst_verts[base3 + 9] = rectX + width;
        dst_verts[base3 + 10] = rectY;
        dst_verts[base3 + 11] = 0.f;

        dst_uvs[base2 + 0] = glyph[4];
        dst_uvs[base2 + 1] = glyph[5];
        dst_uvs[base2 + 2] = glyph[4];
        dst_uvs[base2 + 3] = glyph[7];
        dst_uvs[base2 + 4] = glyph[6];
        dst_uvs[base2 + 5] = glyph[7];
        dst_uvs[base2 + 6] = glyph[6];
        dst_uvs[base2 + 7] = glyph[5];

        dst_rawuv[base4 + 0] = 0.f;
        dst_rawuv[base4 + 1] = 0.f;
        dst_rawuv[base4 + 2] = 0.f;
        dst_rawuv[base4 + 3] = 0.f;
        dst_rawuv[base4 + 4] = 0.f;
        dst_rawuv[base4 + 5] = 1.f;
        dst_rawuv[base4 + 6] = 0.f;
        dst_rawuv[base4 + 7] = 0.f;
        dst_rawuv[base4 + 8] = 1.f;
        dst_rawuv[base4 + 9] = 1.f;
        dst_rawuv[base4 + 10] = 0.f;
        dst_rawuv[base4 + 11] = 0.f;
        dst_rawuv[base4 + 12] = 1.f;
        dst_rawuv[base4 + 13] = 0.f;
        dst_rawuv[base4 + 14] = 0.f;
        dst_rawuv[base4 + 15] = 0.f;

        for (int v = 0; v < 4; ++v) {
            const int offset = base4 + v * 4;

            dst_rect[offset + 0] = rectX;
            dst_rect[offset + 1] = rectY;
            dst_rect[offset + 2] = width;
            dst_rect[offset + 3] = height;

            dst_radius[offset + 0] = 0.f;
            dst_radius[offset + 1] = 0.f;
            dst_radius[offset + 2] = 0.f;
            dst_radius[offset + 3] = 0.f;

            dst_color[offset + 0] = color4[0];
            dst_color[offset + 1] = color4[1];
            dst_color[offset + 2] = color4[2];
            dst_color[offset + 3] = color4[3];

            dst_outline[offset + 0] = outline4[0];
            dst_outline[offset + 1] = outline4[1];
            dst_outline[offset + 2] = outline4[2];
            dst_outline[offset + 3] = outline4[3];

            dst_extra[offset + 0] = extra4[0];
            dst_extra[offset + 1] = extra4[1];
            dst_extra[offset + 2] = extra4[2];
            dst_extra[offset + 3] = extra4[3];

            dst_mask[offset + 0] = mask4[0];
            dst_mask[offset + 1] = mask4[1];
            dst_mask[offset + 2] = mask4[2];
            dst_mask[offset + 3] = mask4[3];
        }

        const int indexOffset = dst_index_base + emittedIndices;
        dst_indices[indexOffset + 0] = vertexIndex;
        dst_indices[indexOffset + 1] = vertexIndex + 1;
        dst_indices[indexOffset + 2] = vertexIndex + 2;
        dst_indices[indexOffset + 3] = vertexIndex;
        dst_indices[indexOffset + 4] = vertexIndex + 2;
        dst_indices[indexOffset + 5] = vertexIndex + 3;

        emittedVertices += 4;
        emittedIndices += 6;
        penX += xAdvance;
    }

    out_pen_x[0] = penX;
    out_counts[0] = emittedVertices;
    out_counts[1] = emittedIndices;
    out_bounds[0] = hasBounds ? boundsMinX : 0.f;
    out_bounds[1] = hasBounds ? boundsMinY : 0.f;
    out_bounds[2] = hasBounds ? boundsMaxX : 0.f;
    out_bounds[3] = hasBounds ? boundsMaxY : 0.f;
}

NOWUI_VG_EXPORT void nowui_vg_pack_ugui(
    const float *src_verts,
    const float *src_uvs,
    const float *src_radius,
    const float *src_rawuv,
    const float *src_colors,
    int vertex_count,
    int is_text,
    float offset_x,
    float offset_y,
    float *dst_vertices,
    float *dst_uv0,
    float *dst_colors,
    float *dst_normals,
    int dst_vertex_base)
{
    for (int i = 0; i < vertex_count; ++i) {
        int base3 = (dst_vertex_base + i) * 3;
        dst_vertices[base3 + 0] = src_verts[i * 3 + 0] + offset_x;
        dst_vertices[base3 + 1] = src_verts[i * 3 + 1] + offset_y;
        dst_vertices[base3 + 2] = src_verts[i * 3 + 2];

        int base4 = (dst_vertex_base + i) * 4;

        if (is_text) {
            dst_uv0[base4 + 0] = src_uvs[i * 2 + 0];
            dst_uv0[base4 + 1] = src_uvs[i * 2 + 1];
            dst_uv0[base4 + 2] = src_rawuv[i * 4 + 0];
            dst_uv0[base4 + 3] = src_rawuv[i * 4 + 1];
        } else {
            dst_uv0[base4 + 0] = src_uvs[i * 2 + 0];
            dst_uv0[base4 + 1] = src_uvs[i * 2 + 1];
            dst_uv0[base4 + 2] = src_radius[i * 4 + 3];
            dst_uv0[base4 + 3] = 0.f;
        }

        dst_colors[base4 + 0] = src_colors[i * 4 + 0];
        dst_colors[base4 + 1] = src_colors[i * 4 + 1];
        dst_colors[base4 + 2] = src_colors[i * 4 + 2];
        dst_colors[base4 + 3] = src_colors[i * 4 + 3];

        dst_normals[base3 + 0] = src_radius[i * 4 + 0];
        dst_normals[base3 + 1] = src_radius[i * 4 + 1];
        dst_normals[base3 + 2] = src_radius[i * 4 + 2];
    }
}

NOWUI_VG_EXPORT void nowui_vg_pack_canvas(
    const float *src_verts,
    const float *src_uvs,
    const float *src_radius,
    const float *src_rawuv,
    const float *src_colors,
    const float *src_rect,
    const float *src_mask,
    const float *src_extra,
    const float *src_outline,
    int vertex_count,
    int is_text,
    float offset_x,
    float offset_y,
    float *dst,
    int dst_vertex_base)
{
    float *out = dst + static_cast<size_t>(dst_vertex_base) * 30;

    for (int i = 0; i < vertex_count; ++i) {
        const float *radius = src_radius + i * 4;
        const float *color = src_colors + i * 4;
        const float *rect = src_rect + i * 4;
        const float *mask = src_mask + i * 4;
        const float *extra = src_extra + i * 4;
        const float *outline = src_outline + i * 4;

        // position
        out[0] = src_verts[i * 3 + 0] + offset_x;
        out[1] = src_verts[i * 3 + 1] + offset_y;
        out[2] = src_verts[i * 3 + 2];

        // normal = radius.xyz
        out[3] = radius[0];
        out[4] = radius[1];
        out[5] = radius[2];

        // tangent = outline color
        out[6] = outline[0];
        out[7] = outline[1];
        out[8] = outline[2];
        out[9] = outline[3];

        // color
        out[10] = color[0];
        out[11] = color[1];
        out[12] = color[2];
        out[13] = color[3];

        // uv0: text packs raw uv, rectangles pack the fourth radius component
        out[14] = src_uvs[i * 2 + 0];
        out[15] = src_uvs[i * 2 + 1];

        if (is_text) {
            out[16] = src_rawuv[i * 4 + 0];
            out[17] = src_rawuv[i * 4 + 1];
        } else {
            out[16] = radius[3];
            out[17] = 0.f;
        }

        // uv1 = rect, uv2 = mask, uv3 = extras
        out[18] = rect[0];
        out[19] = rect[1];
        out[20] = rect[2];
        out[21] = rect[3];

        out[22] = mask[0];
        out[23] = mask[1];
        out[24] = mask[2];
        out[25] = mask[3];

        out[26] = extra[0];
        out[27] = extra[1];
        out[28] = extra[2];
        out[29] = extra[3];

        out += 30;
    }
}

NOWUI_VG_EXPORT void nowui_vg_pack_render(
    const float *src_verts,
    const float *src_uvs,
    const float *src_radius,
    const float *src_rawuv,
    const float *src_colors,
    const float *src_rect,
    const float *src_mask,
    const float *src_extra,
    const float *src_outline,
    int vertex_count,
    float offset_x,
    float offset_y,
    float *dst,
    int dst_vertex_base)
{
    float *out = dst + static_cast<size_t>(dst_vertex_base) * 33;

    for (int i = 0; i < vertex_count; ++i) {
        const float *radius = src_radius + i * 4;
        const float *color = src_colors + i * 4;
        const float *rect = src_rect + i * 4;
        const float *mask = src_mask + i * 4;
        const float *extra = src_extra + i * 4;
        const float *outline = src_outline + i * 4;
        const float *rawuv = src_rawuv + i * 4;

        out[0] = src_verts[i * 3 + 0] + offset_x;
        out[1] = src_verts[i * 3 + 1] + offset_y;
        out[2] = src_verts[i * 3 + 2];

        out[3] = src_uvs[i * 2 + 0];
        out[4] = src_uvs[i * 2 + 1];

        out[5] = rect[0];
        out[6] = rect[1];
        out[7] = rect[2];
        out[8] = rect[3];

        out[9] = radius[0];
        out[10] = radius[1];
        out[11] = radius[2];
        out[12] = radius[3];

        out[13] = color[0];
        out[14] = color[1];
        out[15] = color[2];
        out[16] = color[3];

        out[17] = outline[0];
        out[18] = outline[1];
        out[19] = outline[2];
        out[20] = outline[3];

        out[21] = extra[0];
        out[22] = extra[1];
        out[23] = extra[2];
        out[24] = extra[3];

        out[25] = mask[0];
        out[26] = mask[1];
        out[27] = mask[2];
        out[28] = mask[3];

        out[29] = rawuv[0];
        out[30] = rawuv[1];
        out[31] = rawuv[2];
        out[32] = rawuv[3];

        out += 33;
    }
}

NOWUI_VG_EXPORT int nowui_vg_version()
{
    return VERSION;
}

}

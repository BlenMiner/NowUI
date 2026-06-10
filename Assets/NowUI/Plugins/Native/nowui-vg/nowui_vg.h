#pragma once

/* NowUI vector graphics tessellator.
 *
 * Native port of the Lottie geometry pipeline: bezier flattening, trim paths,
 * scanline fill tessellation (nonzero/even-odd, compound paths, geometric matte
 * clipping), analytic anti-alias fringes and stroke expansion. The managed side
 * evaluates the animation (keyframes, transforms, paints) and sends transformed
 * cubic bezier contours here; triangles come back in one copy at the end of the
 * frame. Single threaded by design, matching the NowUI immediate mode contract.
 *
 * Packed contour stream (floats), repeated contour_count times:
 *   point_count, closed,
 *   { px, py, in_x, in_y, out_x, out_y } * point_count
 * Points are in final pixel space (y down); tangents are relative to the point.
 *
 * Packed paint stream (floats):
 *   kind (0 solid, 1 linear gradient, 2 radial gradient),
 *   r, g, b, a               (solid color, or tint multiplier for gradients)
 *   alpha_multiplier,
 *   start_x, start_y, end_x, end_y,
 *   color_stop_count, stop_float_count, gradient_span,
 *   stops...                  (color_stop_count * [pos, r, g, b], then [pos, a] pairs)
 */

#if defined(_WIN32) && !defined(__EMSCRIPTEN__)
#define NOWUI_VG_EXPORT __declspec(dllexport)
#elif defined(__EMSCRIPTEN__)
#define NOWUI_VG_EXPORT __attribute__((used, visibility("default")))
#else
#define NOWUI_VG_EXPORT __attribute__((visibility("default")))
#endif

#define NOWUI_VG_OK 0
#define NOWUI_VG_ERROR 1

extern "C" {

/* Starts a new frame, clearing the accumulated geometry. */
NOWUI_VG_EXPORT void nowui_vg_begin(float flatten_tolerance, float aa_width);

NOWUI_VG_EXPORT int nowui_vg_fill(
    const float *contours,
    int contour_float_count,
    int contour_count,
    const float *clip_contours,
    int clip_float_count,
    int clip_contour_count,
    int clip_invert,
    const float *paint,
    int paint_float_count,
    int even_odd,
    int has_trim,
    float trim_start,
    float trim_end,
    float trim_offset,
    int trim_individual);

/* cap/join: 1 butt/miter, 2 round, 3 square/bevel (After Effects encoding). */
NOWUI_VG_EXPORT int nowui_vg_stroke(
    const float *contours,
    int contour_float_count,
    int contour_count,
    const float *clip_contours,
    int clip_float_count,
    int clip_contour_count,
    int clip_invert,
    const float *paint,
    int paint_float_count,
    float width,
    int cap,
    int join,
    int has_trim,
    float trim_start,
    float trim_end,
    float trim_offset,
    int trim_individual);

/* bounds: min_x, min_y, max_x, max_y (zeroed when the frame is empty). */
NOWUI_VG_EXPORT int nowui_vg_end(
    int *out_vertex_count,
    int *out_index_count,
    float *out_bounds);

/* positions: 2 floats per vertex, colors: 4 floats per vertex. */
NOWUI_VG_EXPORT int nowui_vg_copy(
    float *positions,
    float *colors,
    int *indices,
    int vertex_capacity,
    int index_capacity);

NOWUI_VG_EXPORT int nowui_vg_version();

}

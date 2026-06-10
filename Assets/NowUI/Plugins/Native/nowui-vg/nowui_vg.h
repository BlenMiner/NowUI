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

/* Bulk-copies a tessellated buffer into the NowMesh vertex streams (scale + offset
 * the positions, tint the colors, splat the shared rect/mask, derive UVs from the
 * rect). Source buffers are tightly packed; destinations are written starting at
 * dst_vertex_base / dst_index_base. Strides (floats per vertex): src positions 2,
 * src colors 4; dst verts 3, uvs 2, rawuv/rect/radius/color/outline/extra/mask 4. */
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
    int index_offset);

/* Packs NowMesh streams into the UGUI canvas vertex layout (position offset, uv0
 * packing, color conversion, radius->normal). Strides: src verts 3, uvs 2,
 * radius/rawuv/colors 4; dst vertices/normals 3, uv0/colors 4. */
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
    int dst_vertex_base);

/* Packs NowMesh streams into the interleaved canvas vertex layout consumed by
 * Mesh.SetVertexBufferData. Output stride is 30 floats per vertex:
 * position(3), normal(3), tangent(4), color(4), uv0(4), uv1(4), uv2(4), uv3(4).
 * Writing starts at dst + dst_vertex_base * 30. */
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
    int dst_vertex_base);

NOWUI_VG_EXPORT int nowui_vg_version();

}

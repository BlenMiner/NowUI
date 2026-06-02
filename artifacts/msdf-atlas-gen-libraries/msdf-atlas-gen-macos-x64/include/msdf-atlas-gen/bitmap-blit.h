
#pragma once

#include <msdfgen.h>
#include "types.h"

namespace msdf_atlas {

/*
 * Copies a rectangular section from source bitmap to destination bitmap.
 * Makes sure not to access pixels outside either bitmap's bounds.
 * The two bitmaps must not overlap.
 */

void blit(const msdfgen::BitmapSection<byte, 1> &dst, const msdfgen::BitmapConstSection<byte, 1> &src);
void blit(const msdfgen::BitmapSection<byte, 3> &dst, const msdfgen::BitmapConstSection<byte, 3> &src);
void blit(const msdfgen::BitmapSection<byte, 4> &dst, const msdfgen::BitmapConstSection<byte, 4> &src);

void blit(const msdfgen::BitmapSection<float, 1> &dst, const msdfgen::BitmapConstSection<float, 1> &src);
void blit(const msdfgen::BitmapSection<float, 3> &dst, const msdfgen::BitmapConstSection<float, 3> &src);
void blit(const msdfgen::BitmapSection<float, 4> &dst, const msdfgen::BitmapConstSection<float, 4> &src);

void blit(const msdfgen::BitmapSection<byte, 1> &dst, const msdfgen::BitmapConstSection<float, 1> &src);
void blit(const msdfgen::BitmapSection<byte, 3> &dst, const msdfgen::BitmapConstSection<float, 3> &src);
void blit(const msdfgen::BitmapSection<byte, 4> &dst, const msdfgen::BitmapConstSection<float, 4> &src);

void blit(const msdfgen::BitmapSection<byte, 1> &dst, const msdfgen::BitmapConstSection<byte, 1> &src, int dx, int dy, int sx, int sy, int w, int h);
void blit(const msdfgen::BitmapSection<byte, 3> &dst, const msdfgen::BitmapConstSection<byte, 3> &src, int dx, int dy, int sx, int sy, int w, int h);
void blit(const msdfgen::BitmapSection<byte, 4> &dst, const msdfgen::BitmapConstSection<byte, 4> &src, int dx, int dy, int sx, int sy, int w, int h);

void blit(const msdfgen::BitmapSection<float, 1> &dst, const msdfgen::BitmapConstSection<float, 1> &src, int dx, int dy, int sx, int sy, int w, int h);
void blit(const msdfgen::BitmapSection<float, 3> &dst, const msdfgen::BitmapConstSection<float, 3> &src, int dx, int dy, int sx, int sy, int w, int h);
void blit(const msdfgen::BitmapSection<float, 4> &dst, const msdfgen::BitmapConstSection<float, 4> &src, int dx, int dy, int sx, int sy, int w, int h);

void blit(const msdfgen::BitmapSection<byte, 1> &dst, const msdfgen::BitmapConstSection<float, 1> &src, int dx, int dy, int sx, int sy, int w, int h);
void blit(const msdfgen::BitmapSection<byte, 3> &dst, const msdfgen::BitmapConstSection<float, 3> &src, int dx, int dy, int sx, int sy, int w, int h);
void blit(const msdfgen::BitmapSection<byte, 4> &dst, const msdfgen::BitmapConstSection<float, 4> &src, int dx, int dy, int sx, int sy, int w, int h);

}

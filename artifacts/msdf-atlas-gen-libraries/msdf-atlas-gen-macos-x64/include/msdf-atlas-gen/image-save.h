
#pragma once

#include <msdfgen.h>
#include "types.h"

namespace msdf_atlas {

/// Saves the bitmap as an image file with the specified format
template <typename T, int N>
bool saveImage(const msdfgen::BitmapConstSection<T, N> &bitmap, ImageFormat format, const char *filename);

}

#include "image-save.hpp"

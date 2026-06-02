
#pragma once

#include <vector>
#include <msdfgen.h>
#include "types.h"

#ifndef MSDFGEN_DISABLE_PNG

namespace msdf_atlas {

// Functions to encode an image as a sequence of bytes in memory
// Only PNG format available currently

bool encodePng(std::vector<byte> &output, msdfgen::BitmapConstSection<byte, 1> bitmap);
bool encodePng(std::vector<byte> &output, msdfgen::BitmapConstSection<byte, 3> bitmap);
bool encodePng(std::vector<byte> &output, msdfgen::BitmapConstSection<byte, 4> bitmap);
bool encodePng(std::vector<byte> &output, msdfgen::BitmapConstSection<float, 1> bitmap);
bool encodePng(std::vector<byte> &output, msdfgen::BitmapConstSection<float, 3> bitmap);
bool encodePng(std::vector<byte> &output, msdfgen::BitmapConstSection<float, 4> bitmap);

}

#endif

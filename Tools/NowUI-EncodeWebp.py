"""Encode a numbered PNG frame sequence into a looping animated WebP.

Invoked by Tools/NowUI-Harness.ps1 for every animation capture. Requires
Python 3 and Pillow (`pip install pillow`).

Why Pillow rather than ffmpeg's libwebp_anim encoder: fed the same frames,
the same quality, and the same libwebp method, Pillow's WebPAnimEncoder
path lands about 1.5 dB PSNR / 0.02 SSIM higher at the same file size
(measured on the README loops), which is worth roughly a 25 percent smaller
file at matched quality. ffmpeg still handles GIF and MP4.

Alpha is deliberately discarded. The harness captures carry partial alpha
inside the SDF shapes (a by-product of effect blending, never below 128),
so the GIFs that preceded WebP rendered every pixel opaque. Encoding the
alpha plane roughly triples the file and lets the page background bleed
through the artwork, so the frames are flattened to RGB before encoding.
"""

from __future__ import annotations

import argparse
import os
import sys


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--frames", required=True, help="Directory holding the PNG sequence.")
    parser.add_argument("--pattern", default="frame-%04d.png", help="printf-style frame file pattern.")
    parser.add_argument("--count", type=int, required=True, help="Number of frames to encode, starting at 0.")
    parser.add_argument("--fps", type=float, required=True, help="Playback rate of the source sequence.")
    parser.add_argument("--quality", type=int, default=60, help="Lossy quality 0-100 (default 60).")
    parser.add_argument("--method", type=int, default=6, help="libwebp effort 0-6 (default 6, slowest and smallest).")
    parser.add_argument("--output", required=True, help="Destination .webp path.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    try:
        from PIL import Image
    except ImportError:
        print("Pillow is required to encode animated WebP: pip install pillow", file=sys.stderr)
        return 2

    if args.count <= 0 or args.fps <= 0:
        print("--count and --fps must be positive.", file=sys.stderr)
        return 2
    if not 0 <= args.quality <= 100 or not 0 <= args.method <= 6:
        print("--quality must be 0-100 and --method 0-6.", file=sys.stderr)
        return 2

    frames = []
    for index in range(args.count):
        path = os.path.join(args.frames, args.pattern % index)
        if not os.path.isfile(path):
            print(f"Missing frame '{path}'.", file=sys.stderr)
            return 1
        with Image.open(path) as image:
            frames.append(image.convert("RGB"))

    # WebP stores integer millisecond durations. Deriving each frame's
    # duration from rounded cumulative timestamps keeps the loop length exact
    # (96 frames at 24 fps is 4000 ms, not 96 x 42 = 4032 ms).
    timestamps = [round(index * 1000.0 / args.fps) for index in range(args.count + 1)]
    durations = [timestamps[index + 1] - timestamps[index] for index in range(args.count)]

    frames[0].save(
        args.output,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        quality=args.quality,
        method=args.method,
        lossless=False,
        # minimize_size trades temporal drift for bytes (worst-frame PSNR
        # dropped 6 dB on the desktop loop), and allow_mixed gained nothing on
        # this content; both stay off.
        minimize_size=False,
        allow_mixed=False,
        exact=False,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

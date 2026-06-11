# Third-Party Notices

NowUI's native font compiler plugin (`nowui-msdf`) links the following
third-party libraries. They are compiled into the prebuilt binaries under
`Plugins/` but their sources are not part of this repository; CI fetches them
at build time (see `.github/workflows/build-native-libraries.yml`).

The `nowui-vg` vector tessellator is original NowUI code with no third-party
dependencies.

## msdf-atlas-gen and msdfgen

- Author: Viktor Chlumský
- Source: https://github.com/Chlumsky/msdf-atlas-gen
- License: MIT

> Copyright (c) 2020-2024 Viktor Chlumský
>
> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in
> all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
> FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
> IN THE SOFTWARE.

## FreeType

- Source: https://freetype.org
- License: FreeType License (FTL); FreeType is dual-licensed under the FTL and
  the GPLv2, and NowUI uses it under the FTL.

Portions of this software are copyright © The FreeType Project
(www.freetype.org). All rights reserved.

The full FTL text is available at
https://gitlab.freedesktop.org/freetype/freetype/-/blob/master/docs/FTL.TXT

## Transitive dependencies

FreeType is built through vcpkg with its default features, which may link
zlib (zlib license), libpng (libpng license), bzip2 (BSD-style), and Brotli
(MIT). All are permissive licenses compatible with this project's license.

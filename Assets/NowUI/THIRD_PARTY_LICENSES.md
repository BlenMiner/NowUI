# Third-Party Notices

NowUI includes or adapts the third-party software listed below.

The native font compiler plugin (`nowui-msdf`) links third-party libraries
compiled into the prebuilt binaries under `Plugins/`. Their sources are not
part of this repository; CI fetches them at build time (see
`.github/workflows/build-native-libraries.yml`).

The `nowui-vg` vector tessellator is original NowUI code with no third-party
dependencies.

## Inigo Quilez 2D signed-distance functions

NowUI's ChamferedBox, Triangle, Arc, and Pie shader distance functions are
adapted or reexpressed from Inigo Quilez's corresponding 2D signed-distance
functions, including `sdChamferBox`, `sdTriangle`, `sdArc`, and `sdPie`.

- Author: Inigo Quilez
- Source: https://iquilezles.org/articles/distfunctions2d/
- Arc demo/source: https://www.shadertoy.com/view/wl23RK
- Pie demo/source: https://www.shadertoy.com/view/3l23RK
- License: MIT

> Copyright © 2019 Inigo Quilez
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
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

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

## HarfBuzz

- Source: https://github.com/harfbuzz/harfbuzz
- License: Old MIT

HarfBuzz is licensed under the so-called "Old MIT" license. For parts of
HarfBuzz that are licensed under different licenses, see the individual files
named `COPYING` in the HarfBuzz source tree.

> Copyright © 2010-2022 Google, Inc.
> Copyright © 2015-2020 Ebrahim Byagowi
> Copyright © 2019,2020 Facebook, Inc.
> Copyright © 2012,2015 Mozilla Foundation
> Copyright © 2011 Codethink Limited
> Copyright © 2008,2010 Nokia Corporation and/or its subsidiary(-ies)
> Copyright © 2009 Keith Stribley
> Copyright © 2011 Martin Hosken and SIL International
> Copyright © 2007 Chris Wilson
> Copyright © 2005,2006,2020,2021,2022,2023 Behdad Esfahbod
> Copyright © 2004,2007,2008,2009,2010,2013,2021,2022,2023 Red Hat, Inc.
> Copyright © 1998-2005 David Turner and Werner Lemberg
> Copyright © 2016 Igalia S.L.
> Copyright © 2022 Matthias Clasen
> Copyright © 2018,2021 Khaled Hosny
> Copyright © 2018,2019,2020 Adobe, Inc
> Copyright © 2013-2015 Alexei Podtelezhnikov

For full copyright notices consult the individual files in the upstream
HarfBuzz source.

Permission is hereby granted, without written agreement and without
license or royalty fees, to use, copy, modify, and distribute this
software and its documentation for any purpose, provided that the
above copyright notice and the following two paragraphs appear in
all copies of this software.

IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE TO ANY PARTY FOR
DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES
ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION, EVEN
IF THE COPYRIGHT HOLDER HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH
DAMAGE.

THE COPYRIGHT HOLDER SPECIFICALLY DISCLAIMS ANY WARRANTIES, INCLUDING,
BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
ON AN "AS IS" BASIS, AND THE COPYRIGHT HOLDER HAS NO OBLIGATION TO
PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.

## Transitive dependencies

FreeType is built through vcpkg with its default features, which may link
zlib (zlib license), libpng (libpng license), bzip2 (BSD-style), and Brotli
(MIT). All are permissive licenses compatible with commercial and open-source
distribution alike.

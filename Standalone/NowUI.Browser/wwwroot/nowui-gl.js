// Low-level WebGL2 interop; all scenes are authored with the shared C# NowUI API.
const ATTR = [
    { name: 'aPosition', size: 3 },  // 0  POSITION   quad corner, mesh space (z always 0)
    { name: 'aUv', size: 2 },        // 1  TEXCOORD0  texture UV, Unity bottom-up
    { name: 'aRect', size: 4 },      // 2  TEXCOORD1  shape rect (x, y, w, h) in mesh space
    { name: 'aRadius', size: 4 },    // 3  TEXCOORD2  corner radii (TR, BR, TL, BL) / text gradient payload
    { name: 'aColor', size: 4 },     // 4  TEXCOORD3  fill RGBA, display encoded, not premultiplied
    { name: 'aOutline', size: 4 },   // 5  TEXCOORD4  outline RGBA
    { name: 'aExtras', size: 4 },    // 6  TEXCOORD5  per-shader scalars
    { name: 'aMask', size: 4 },      // 7  TEXCOORD6  legacy clip rect, UI coords (y down)
    { name: 'aRawUV', size: 4 },     // 8  TEXCOORD7  un-atlased quad coordinate
];

const VERTEX_FLOATS_PER_VERTEX = ATTR.reduce((n, a) => n + a.size, 0); // 33 floats = 132 bytes

const PROGRAM_SOURCES = [];

// Startup registration carries shader source only. It is not a scene authoring API.
export function registerShader(name, pass, vertex, fragment) {
    let source = PROGRAM_SOURCES.find(item => item.key === name);
    if (!source) {
        source = { key: name, passes: [] };
        PROGRAM_SOURCES.push(source);
    }
    source.passes[pass] = { vertex, fragment };
}
const BLEND_STATES = {
    'NowUI/UI Glass': { srcRGB: 'SRC_ALPHA', dstRGB: 'ONE_MINUS_SRC_ALPHA',
                        srcAlpha: 'ONE', dstAlpha: 'ONE_MINUS_SRC_ALPHA' },
    'Hidden/NowUI/GlassBlur': null,
    'NowUI/SDF Scene': { srcRGB: 'SRC_ALPHA', dstRGB: 'ONE_MINUS_SRC_ALPHA',
                         srcAlpha: 'SRC_ALPHA', dstAlpha: 'ONE_MINUS_SRC_ALPHA' },
    'Hidden/NowUI/SDF Image Field': null,
};
let appliedBlend = '';

function applyBlendState(shaderName) {
    const wanted = (shaderName in BLEND_STATES) ? shaderName : '';
    if (wanted === appliedBlend) return;
    appliedBlend = wanted;

    if (wanted === '') {
        gl.enable(gl.BLEND);
        gl.blendEquation(gl.FUNC_ADD);
        gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        return;
    }

    const mode = BLEND_STATES[wanted];

    if (mode === null) {
        gl.disable(gl.BLEND);
        return;
    }

    gl.enable(gl.BLEND);
    gl.blendEquation(gl.FUNC_ADD);
    gl.blendFuncSeparate(gl[mode.srcRGB], gl[mode.dstRGB], gl[mode.srcAlpha], gl[mode.dstAlpha]);
}
function invalidateBlendState() {
    appliedBlend = '\u0000';   // a name no shader can have, so the next applyBlendState always reprograms
}
const U = {
    MVP: 0,                       // mat4, column major
    MAIN_TEX_ST: 16,              // vec4
    PREMULTIPLIED_TEXTURE: 20,    // float
    TEXT_SDF_ENCODING: 21,        // float
    MASK_COUNT: 22,               // float
    TEXTURE_MASK_COUNT: 23,       // float
    MASK_RECTS: 24,               // vec4[8]
    MASK_DATA: 56,                // vec4[8]
    MASK_PARAMS: 88,              // vec4[8]
    MASK_TRANSFORMS: 120,         // vec4[8]
    TEXTURE_MASK_RECTS: 152,      // vec4[2]
    TEXTURE_MASK_PARAMS: 160,     // vec4[2]
    TEXTURE_MASK_TRANSFORMS: 168, // vec4[2]
    GRADIENT_RAMP_TEXEL_SIZE: 176, // vec4 -- NowUI/UI Gradient only
    COLOR_PICKER_MODE: 180,        // float -- NowUI/Color Picker only. GL's zero default is a VALID mode
    GLASS_USE_BACKDROP: 181,       // float -- 0 on every path the browser host reaches today
    GLASS_USE_STEREO_BACKDROP: 182,// float -- always 0; feeds the unported-branch marker
    GLASS_MATERIAL_MODE: 183,      // float -- always 0 (its only writer is excluded from the standalone build)
    BACKDROP_UV_TRANSFORM: 184,    // vec4  -- MUST fall back to (1,1,0,0); zero collapses the backdrop to
    BLUR_TEXEL_SIZE: 188,          // vec4  -- (1/w, 1/h, w, h) of the blur SOURCE; zero makes the blur a copy
    BLUR_SOURCE_SCALE_OFFSET: 192, // vec4  -- MUST fall back to (1,1,0,0)
    BLUR_DIRECTION: 196,           // vec2  -- (step,0) or (0,step); zero makes the blur a copy
    SOURCE_UV: 198,               // vec4
    FIELD_PARAMS: 202,            // vec4
    FIELD_TEXELS: 206,            // vec4
    STAMP_RECT: 210,              // vec4
    STEP: 214,                    // float
    COUNT: 215,
};
const UNIT_MAIN_TEX = 0;
const UNIT_TEXTURE_MASK0 = 1;
const UNIT_TEXTURE_MASK1 = 2;
const UNIT_GRADIENT_RAMP = 3;
const UNIT_BACKDROP_TEX = 4;
const UNIT_SDF_IMAGE_FIELD = 5;
const UNIT_SDF_IMAGE_COLOR = 6;
const UNIT_SOURCE_TEX = 7;
const SDF_MAX_SHAPES = 64;
const SDF_MAX_LAYERS = 16;

const S = {
    DATA0: 0,                         // vec4[64]
    DATA1: 256,                       // vec4[64]
    DATA2: 512,                       // vec4[64]
    SHAPE_META: 768,                  // vec4[64]
    COLORS: 1024,                     // vec4[64]
    UVS: 1280,                        // vec4[64]
    IMAGE_UVS: 1536,                  // vec4[64]
    LAYER_DATA0: 1792,                // vec4[16]
    LAYER_DATA1: 1856,                // vec4[16]
    IMAGE_ATLAS_SIZE: 1920,           // vec4  -- (fieldW, fieldH, colorW, colorH); Upload writes Vector4.one
    OUTLINE: 1924,                    // vec4
    OUTLINE_COLOR: 1928,              // vec4
    GLOW: 1932,                       // vec4
    GLOW_COLOR: 1936,                 // vec4
    SHADOW: 1940,                     // vec4
    SHADOW_COLOR: 1944,               // vec4
    INNER_SHADOW: 1948,               // vec4
    INNER_SHADOW_COLOR: 1952,         // vec4
    EMBOSS: 1956,                     // vec4
    CONTOUR: 1960,                    // vec4
    CONTOUR_COLOR: 1964,              // vec4
    CONTOUR_MASK: 1968,               // vec4
    WARP: 1972,                       // vec4
    SHAPE_COUNT: 1976,                // float
    LAYER_COUNT: 1977,                // float
    FEATHER: 1978,                    // float
    TEXT_EFFECT_LIMIT: 1979,          // float -- zero is WRONG, not merely unset; see the C# fallback
    MASK_OUTPUT: 1980,                // float
    CANVAS_LAYOUT: 1981,              // float -- vertex stage; always 0 in this build and still bridged
    TIME: 1982,                       // float -- Unity's _Time.y, read only by the domain warp
    COUNT: 1983,
};
const sdfBlock = new Float32Array(S.COUNT);
let sdfBlockPending = false;
const FILTER_POINT = 0;
const WRAP_REPEAT = 0, WRAP_CLAMP = 1, WRAP_MIRROR = 2, WRAP_MIRROR_ONCE = 3;

let gl = null;
let canvas = null;
let whiteTexture = null;   // 1x1 opaque white — the _MainTex fallback
let blackTexture = null;   // 1x1 opaque black — the mask sampler fallback, matching NowMaskShader.Apply
const programs = new Map();  // shader name -> { program, uniforms }
const textures = new Map();  // instance id -> { tex, width, height, filter, wrapS, wrapT, mipCount }
const meshes = new Map();    // instance id -> { vao, vbo, ibo, offsets }
const warned = new Set();
let frameCount = 0;
const renderTargets = new Map();
let contextGeneration = 1;
let boundTargetId = 0;
let colorBufferFloat = false;
let colorBufferHalfFloat = false;
let floatLinearFilter = false;

let blitQuad = null;      // VAO for the unit-square blit quad: attribute 0 position, attribute 1 uv
let emptyVao = null;      // VAO with nothing enabled, for DrawProcedural's gl_VertexID draws
let copyProgram = null;   // the internal texture-copy program used by a material-less blit
let scratchFbo = null;    // read framebuffer for CopyTexture

function warnOnce(key, message) {
    if (warned.has(key)) return;
    warned.add(key);
    console.warn('[NowUI.WebGL2] ' + message);
}

function fail(message) {
    const error = new Error('[NowUI.WebGL2] ' + message);
    console.error(error.message);
    throw error;
}

function requireGl() {
    if (gl === null) fail('init() has not run, or the WebGL2 context was lost.');
    return gl;
}
const COPY_MEMORY_VIEWS = (() => {
    try {
        return typeof location !== 'undefined' && new URLSearchParams(location.search).get('glcopy') === '1';
    }
    catch (error) {
        return false;
    }
})();
const TRACK_VIEW_BYTES = (() => {
    try {
        return typeof location !== 'undefined' && new URLSearchParams(location.search).get('glstats') === '1';
    }
    catch (error) {
        return false;
    }
})();

let viewBytes = 0;
let viewCalls = 0;

const EMPTY_BYTES = new Uint8Array(0);
const EMPTY_INTS = new Int32Array(0);

/**
 * A .NET MemoryView, unwrapped to a typed array this file can hand to WebGL.
 *
 * WHY THIS DOES NOT COPY, AND WHY THAT IS SAFE.
 *
 * A MemoryView's own slice() is literally `this._unsafe_create_view().slice(...)` - it builds a typed array over
 * the wasm heap and then copies it. Only the copy is optional, and it was costing real time: a CPU profile of
 * the documentation viewer put 8.4% of the frame in dotnet.runtime.js's `slice`, all of it arriving through
 * asBytes from uploadMesh and toBlock. That is the whole mesh, memcpy'd out of the heap and handed to
 * gl.bufferData, which immediately copies it AGAIN into GPU memory. The middle copy buys nothing.
 *
 * It is safe here for one reason, stated as a rule so it can be checked: EVERY CALLER IN THIS FILE CONSUMES ITS
 * BYTES SYNCHRONOUSLY AND RETAINS NOTHING. The view is a window onto WebAssembly.Memory, and two things
 * invalidate it - the memory growing (which detaches the ArrayBuffer) and the GC moving the managed array it
 * points at. Both can only happen while wasm is running, and nothing between the unwrap and the last use of the
 * result re-enters wasm: they are gl.* calls, arithmetic, and fail() throwing.
 *
 * SO THE RULE IS: if you ever retain one of these past its call - stash it in `meshes`, close over it in a
 * callback, hand it to anything with an `await` in it - copy it first with .slice(). setSdfUniforms is the one
 * place that keeps the data, and it already copies into the module's own sdfBlock.
 *
 * A plain typed array is passed straight through, so this file stays usable from a harness that calls it
 * directly; and if a future .NET drops _unsafe_create_view, the slice() fallback keeps everything correct and
 * merely slow.
 */
function unwrap(view, empty) {
    if (view === null || view === undefined) return empty;
    if (ArrayBuffer.isView(view)) return view;

    let out;

    if (!COPY_MEMORY_VIEWS && typeof view._unsafe_create_view === 'function') out = view._unsafe_create_view();
    else if (typeof view.slice === 'function') out = view.slice();
    else return empty;

    if (TRACK_VIEW_BYTES) {
        viewBytes += out.byteLength;
        viewCalls += 1;
        globalThis.__nowuiGlViewsTotal = { bytes: viewBytes, calls: viewCalls, copying: COPY_MEMORY_VIEWS };
    }

    return out;
}

function asBytes(view) {
    return unwrap(view, EMPTY_BYTES);
}

function asInts(view) {
    return unwrap(view, EMPTY_INTS);
}

/**
 * The uniform block, as floats over whatever asBytes returned.
 *
 * THE ALIGNMENT GUARD IS NOT DECORATION. A copy always starts at byteOffset 0; a heap view starts at an
 * arbitrary wasm pointer, and Float32Array throws RangeError on a byteOffset that is not a multiple of 4. The
 * C# side hands over float data, so it is aligned in practice - but "in practice" is not worth a hard crash
 * inside a draw call, so an unaligned block quietly takes the copy it used to take anyway.
 */
function asFloats(bytes) {
    if ((bytes.byteOffset & 3) === 0) return new Float32Array(bytes.buffer, bytes.byteOffset, bytes.byteLength >> 2);
    return new Float32Array(bytes.slice().buffer);
}
function wantsPreservedBuffer() {
    const search = new URLSearchParams(location.search);
    if (search.get('capture') === '1') return true;
    if (search.get('shot') === '1' || search.get('shot') === 'true') return true;
    const clip = search.get('clip');
    return clip !== null && Number.isFinite(Number(clip)) && Number(clip) > 0;
}

export function init(canvasSelector) {
    canvas = document.querySelector(canvasSelector);
    if (!canvas) fail(`no canvas matched the selector "${canvasSelector}".`);
    gl = canvas.getContext('webgl2', {
        alpha: false,
        depth: false,               // neither shader writes or tests depth (§2.2)
        stencil: false,
        antialias: false,           // NowUI does its own analytic AA
        premultipliedAlpha: false,
        preserveDrawingBuffer: wantsPreservedBuffer(),
        powerPreference: 'high-performance',
    });

    if (!gl) fail('the browser did not give us a WebGL2 context.');
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
    gl.pixelStorei(gl.UNPACK_COLORSPACE_CONVERSION_WEBGL, gl.NONE);
    gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
    gl.disable(gl.DEPTH_TEST);
    gl.depthMask(false);
    gl.disable(gl.CULL_FACE);       // the projection negates Y, reversing apparent winding
    gl.disable(gl.STENCIL_TEST);
    gl.disable(gl.SCISSOR_TEST);
    gl.disable(gl.DITHER);
    gl.colorMask(true, true, true, true);
    gl.enable(gl.BLEND);
    gl.blendEquation(gl.FUNC_ADD);
    gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);   // premultiplied source-over

    whiteTexture = createSolidTexture(255, 255, 255, 255);
    prewarmShaders();
    blackTexture = createSolidTexture(0, 0, 0, 255);
    gl.activeTexture(gl.TEXTURE0 + UNIT_BACKDROP_TEX);
    gl.bindTexture(gl.TEXTURE_2D, blackTexture);
    gl.activeTexture(gl.TEXTURE0 + UNIT_MAIN_TEX);

    appliedBlend = '';   // matches the blendFunc programmed just above

    canvas.addEventListener('webglcontextlost', (e) => {
        e.preventDefault();
        contextGeneration++;
        renderTargets.clear();
        textures.clear();
        meshes.clear();
        programs.clear();
        blitQuad = null;
        emptyVao = null;
        copyProgram = null;
        scratchFbo = null;
        whiteTexture = null;
        blackTexture = null;
        boundTargetId = 0;

        console.error('[NowUI.WebGL2] WebGL context lost. Every GPU object has been forgotten; render targets ' +
                      'now report themselves lost. Re-creating the context itself belongs to the host.');
    });

    const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
    const renderer = debugInfo
        ? gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL)
        : gl.getParameter(gl.RENDERER);
    colorBufferFloat = !!gl.getExtension('EXT_color_buffer_float');
    colorBufferHalfFloat = colorBufferFloat || !!gl.getExtension('EXT_color_buffer_half_float');
    floatLinearFilter = !!gl.getExtension('OES_texture_float_linear');
    return [
        gl.getParameter(gl.MAX_TEXTURE_SIZE),
        gl.getParameter(gl.MAX_SAMPLES),
        colorBufferFloat ? 1 : 0,
        String(renderer || 'WebGL2'),
        colorBufferHalfFloat ? 1 : 0,
        floatLinearFilter ? 1 : 0,
    ].join('|');
}

function createSolidTexture(r, g, b, a) {
    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([r, g, b, a]));
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_BASE_LEVEL, 0);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAX_LEVEL, 0);
    return tex;
}

function compile(type, source, label) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        fail(`${label} failed to compile:\n${log}`);
    }

    return shader;
}
export function resolveShader(name) {
    requireGl();

    if (programs.has(name)) return 1;

    const source = PROGRAM_SOURCES.find((p) => p.key === name);
    if (!source) return 0;

    const declared = source.passes || [{ vertex: source.vertex, fragment: source.fragment }];
    const started = pending.get(name) || declared.map((p, index) => startPass(name, index, p.vertex, p.fragment));
    pending.delete(name);

    const passes = started.map(finishPass);

    programs.set(name, { passes });
    return 1;
}
const pending = new Map();
export function prewarmShaders() {
    requireGl();

    for (const name of ['NowUI/UI Rectangle', 'NowUI/Text Renderer']) {
        if (programs.has(name) || pending.has(name)) continue;

        const source = PROGRAM_SOURCES.find((p) => p.key === name);
        if (!source) continue;

        const declared = source.passes || [{ vertex: source.vertex, fragment: source.fragment }];
        pending.set(name, declared.map((p, index) => startPass(name, index, p.vertex, p.fragment)));
    }
}
function startPass(name, index, vertexSource, fragmentSource) {
    const label = index === 0 ? name : `${name} pass ${index}`;
    const vs = compile(gl.VERTEX_SHADER, vertexSource, `${label} vertex shader`);
    const fs = compile(gl.FRAGMENT_SHADER, fragmentSource, `${label} fragment shader`);
    const program = gl.createProgram();
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    return { program, vs, fs, label };
}
function finishPass(started) {
    const { program, vs, fs, label } = started;

    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const log = gl.getProgramInfoLog(program);
        gl.deleteProgram(program);
        fail(`${label} failed to link:\n${log}`);
    }

    gl.deleteShader(vs);
    gl.deleteShader(fs);

    const loc = (n) => gl.getUniformLocation(program, n);
    const uniforms = {
        mvp: loc('nowui_MatrixMVP'),
        mainTexST: loc('_MainTex_ST'),
        premultipliedTexture: loc('_NowPremultipliedTexture'),
        textSdfEncoding: loc('_NowUITextSdfEncoding'),
        maskCount: loc('_NowUIMaskCount'),
        textureMaskCount: loc('_NowUITextureMaskCount'),
        maskRects: loc('_NowUIMaskRects[0]'),
        maskData: loc('_NowUIMaskData[0]'),
        maskParams: loc('_NowUIMaskParams[0]'),
        maskTransforms: loc('_NowUIMaskTransforms[0]'),
        textureMaskRects: loc('_NowUITextureMaskRects[0]'),
        textureMaskParams: loc('_NowUITextureMaskParams[0]'),
        textureMaskTransforms: loc('_NowUITextureMaskTransforms[0]'),
        gradientRampTexelSize: loc('_NowGradientRampTexelSize'),
        colorPickerMode: loc('_Mode'),
        glassUseBackdrop: loc('_NowGlassUseBackdrop'),
        glassUseStereoBackdrop: loc('_NowGlassUseStereoBackdrop'),
        glassMaterialMode: loc('_NowMaterialGlassMode'),
        backdropUvTransform: loc('_NowBackdropUVTransform'),
        blurTexelSize: loc('_NowBlurTexelSize'),
        blurSourceScaleOffset: loc('_NowBlurSourceScaleOffset'),
        blurDirection: loc('_NowBlurDirection'),
        mainTex: loc('_MainTex'),
        backdropTex: loc('_NowBackdropTex'),
        blurSourceTex: loc('_NowBlurSourceTex'),
        textureMask0: loc('_NowUITextureMask0'),
        textureMask1: loc('_NowUITextureMask1'),
        gradientRampTexture: loc('_NowGradientRampTexture'),
        sdfData0: loc('_SdfData0[0]'),
        sdfData1: loc('_SdfData1[0]'),
        sdfData2: loc('_SdfData2[0]'),
        sdfShapeMeta: loc('_SdfShapeMeta[0]'),
        sdfColors: loc('_SdfColors[0]'),
        sdfUvs: loc('_SdfUvs[0]'),
        sdfImageUvs: loc('_SdfImageUvs[0]'),
        sdfLayerData0: loc('_SdfLayerData0[0]'),
        sdfLayerData1: loc('_SdfLayerData1[0]'),
        sdfImageAtlasSize: loc('_SdfImageAtlasSize'),
        sdfOutline: loc('_SdfOutline'),
        sdfOutlineColor: loc('_SdfOutlineColor'),
        sdfGlow: loc('_SdfGlow'),
        sdfGlowColor: loc('_SdfGlowColor'),
        sdfShadow: loc('_SdfShadow'),
        sdfShadowColor: loc('_SdfShadowColor'),
        sdfInnerShadow: loc('_SdfInnerShadow'),
        sdfInnerShadowColor: loc('_SdfInnerShadowColor'),
        sdfEmboss: loc('_SdfEmboss'),
        sdfContour: loc('_SdfContour'),
        sdfContourColor: loc('_SdfContourColor'),
        sdfContourMask: loc('_SdfContourMask'),
        sdfWarp: loc('_SdfWarp'),
        sdfShapeCount: loc('_SdfShapeCount'),
        sdfLayerCount: loc('_SdfLayerCount'),
        sdfFeather: loc('_SdfFeather'),
        sdfTextEffectLimit: loc('_SdfTextEffectLimit'),
        sdfMaskOutput: loc('_SdfMaskOutput'),
        sdfCanvasLayout: loc('_NowCanvasLayout'),
        sdfTime: loc('nowui_Time'),
        sdfImageField: loc('_SdfImageField'),
        sdfImageColor: loc('_SdfImageColor'),
        sourceTex: loc('_SourceTex'),
        sourceUv: loc('_SourceUv'),
        fieldParams: loc('_FieldParams'),
        fieldTexels: loc('_FieldTexels'),
        stampRect: loc('_StampRect'),
        step: loc('_Step'),
    };
    gl.useProgram(program);
    if (uniforms.mainTex) gl.uniform1i(uniforms.mainTex, UNIT_MAIN_TEX);
    if (uniforms.blurSourceTex) gl.uniform1i(uniforms.blurSourceTex, UNIT_MAIN_TEX);
    if (uniforms.textureMask0) gl.uniform1i(uniforms.textureMask0, UNIT_TEXTURE_MASK0);
    if (uniforms.textureMask1) gl.uniform1i(uniforms.textureMask1, UNIT_TEXTURE_MASK1);
    if (uniforms.gradientRampTexture) gl.uniform1i(uniforms.gradientRampTexture, UNIT_GRADIENT_RAMP);
    if (uniforms.backdropTex) gl.uniform1i(uniforms.backdropTex, UNIT_BACKDROP_TEX);
    if (uniforms.sdfImageField) gl.uniform1i(uniforms.sdfImageField, UNIT_SDF_IMAGE_FIELD);
    if (uniforms.sdfImageColor) gl.uniform1i(uniforms.sdfImageColor, UNIT_SDF_IMAGE_COLOR);
    if (uniforms.sourceTex) gl.uniform1i(uniforms.sourceTex, UNIT_SOURCE_TEX);
    gl.useProgram(null);

    return { program, uniforms };
}
function selectPass(shaderName, pass) {
    const resolved = programs.get(shaderName);
    if (!resolved) fail(`shader "${shaderName}" was never resolved.`);

    if (pass < 0 || pass >= resolved.passes.length) {
        fail(`shader "${shaderName}" has ${resolved.passes.length} pass(es); pass ${pass} was asked for. A ` +
             'multi-pass program has to declare every pass in PROGRAM_SOURCES, in the SubShader order.');
    }

    return resolved.passes[pass];
}

export function beginFrame(frame) {
    requireGl();
    frameCount = frame;
    if (gl.isContextLost()) fail('the WebGL2 context is lost; nothing can be drawn.');
}

export function endFrame() {
    requireGl();
    flushPendingMips();
    gl.flush();
}

export function setViewport(x, y, width, height) {
    requireGl();
    gl.viewport(x, y, width, height);
}

export function clearTarget(clearColor, r, g, b, a) {
    requireGl();
    if (!clearColor) return;   // depth is never allocated (§2.2), so a depth-only clear has nothing to clear
    gl.clearColor(r, g, b, a);
    gl.clear(gl.COLOR_BUFFER_BIT);
}

function samplerFilters(filter, mipCount) {
    const magFilter = filter === FILTER_POINT ? gl.NEAREST : gl.LINEAR;
    let minFilter = magFilter;

    if (mipCount > 1) {
        if (filter === FILTER_POINT) minFilter = gl.NEAREST_MIPMAP_NEAREST;
        else if (filter === 2 /* Trilinear */) minFilter = gl.LINEAR_MIPMAP_LINEAR;
        else minFilter = gl.LINEAR_MIPMAP_NEAREST;
    }

    return { minFilter, magFilter };
}

function glWrap(mode) {
    switch (mode) {
        case WRAP_REPEAT: return gl.REPEAT;
        case WRAP_CLAMP: return gl.CLAMP_TO_EDGE;
        case WRAP_MIRROR: return gl.MIRRORED_REPEAT;
        case WRAP_MIRROR_ONCE: return gl.CLAMP_TO_EDGE;  // WebGL2 has no GL_MIRROR_CLAMP_TO_EDGE
        default: return gl.CLAMP_TO_EDGE;
    }
}

function applySampler(entry) {
    if (entry.filterable === false && entry.filter !== FILTER_POINT) {
        warnOnce('float-linear', 'a float render target asked for bilinear filtering, but ' +
                                 'OES_texture_float_linear is missing. Using point filtering instead; expect ' +
                                 'blockier results, not black ones.');
        entry = { ...entry, filter: FILTER_POINT };
    }

    const { minFilter, magFilter } = samplerFilters(entry.filter, entry.mipCount);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, minFilter);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, magFilter);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, glWrap(entry.wrapS));
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, glWrap(entry.wrapT));
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_BASE_LEVEL, 0);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAX_LEVEL, Math.max(0, entry.mipCount - 1));
}

function getOrCreateTexture(id) {
    let entry = textures.get(id);
    if (entry) return entry;
    entry = { tex: gl.createTexture(), width: 0, height: 0, filter: 1, wrapS: 1, wrapT: 1, mipCount: 1 };
    textures.set(id, entry);
    return entry;
}
export function uploadTexture(id, info, pixels) {
    requireGl();
    const i = asInts(info);
    const width = i[0], height = i[1];
    const dirtyX = i[2], dirtyY = i[3], dirtyW = i[4], dirtyH = i[5];
    const mipCount = i[9], generateMips = i[10] !== 0;
    const bytes = asBytes(pixels);

    if (width <= 0 || height <= 0) fail(`uploadTexture: texture ${id} is ${width}x${height}.`);
    if (bytes.length < width * height * 4)
        fail(`uploadTexture: texture ${id} is ${width}x${height} RGBA8 but only ${bytes.length} bytes arrived.`);

    const entry = getOrCreateTexture(id);
    entry.filter = i[6];
    entry.wrapS = i[7];
    entry.wrapT = i[8];
    entry.mipCount = Math.max(1, mipCount);

    gl.activeTexture(gl.TEXTURE0 + UNIT_MAIN_TEX);
    gl.bindTexture(gl.TEXTURE_2D, entry.tex);

    const reallocate = entry.width !== width || entry.height !== height;
    const fullRect = dirtyX === 0 && dirtyY === 0 && dirtyW === width && dirtyH === height;

    const partial = !fullRect && dirtyW > 0 && dirtyH > 0;

    if (reallocate && partial) {
        // A texture first seen with only a region written (a new font page) is
        // allocated empty (WebGL zero-fills it) and then gets just that region.
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        entry.width = width;
        entry.height = height;
    }

    if (!partial) {
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE,
                      bytes.subarray(0, width * height * 4));
        entry.width = width;
        entry.height = height;
    } else {
        gl.pixelStorei(gl.UNPACK_ROW_LENGTH, width);
        gl.pixelStorei(gl.UNPACK_SKIP_PIXELS, dirtyX);
        gl.pixelStorei(gl.UNPACK_SKIP_ROWS, dirtyY);
        gl.texSubImage2D(gl.TEXTURE_2D, 0, dirtyX, dirtyY, dirtyW, dirtyH, gl.RGBA, gl.UNSIGNED_BYTE,
                         bytes.subarray(0, width * height * 4));
        gl.pixelStorei(gl.UNPACK_ROW_LENGTH, 0);
        gl.pixelStorei(gl.UNPACK_SKIP_PIXELS, 0);
        gl.pixelStorei(gl.UNPACK_SKIP_ROWS, 0);
    }
    if (generateMips && entry.mipCount > 1) gl.generateMipmap(gl.TEXTURE_2D);

    applySampler(entry);
}
export function updateSampler(id, info) {
    requireGl();
    const i = asInts(info);
    const target = renderTargets.get(id);
    const entry = target || getOrCreateTexture(id);
    entry.filter = i[0];
    entry.wrapS = i[1];
    entry.wrapT = i[2];
    if (!target) entry.mipCount = Math.max(1, i[4]);

    gl.activeTexture(gl.TEXTURE0 + UNIT_MAIN_TEX);
    gl.bindTexture(gl.TEXTURE_2D, entry.tex);
    applySampler(entry);
}

export function releaseTexture(id) {
    if (gl === null) return;
    if (renderTargets.has(id)) return;

    const entry = textures.get(id);
    if (!entry) return;
    gl.deleteTexture(entry.tex);
    textures.delete(id);
}
function lookupSampleable(id) {
    const target = renderTargets.get(id);
    if (target && target.generation === contextGeneration) return target;

    return textures.get(id) || null;
}
function bindTextureUnit(unit, id, fallback, label) {
    gl.activeTexture(gl.TEXTURE0 + unit);

    if (id === 0) {
        gl.bindTexture(gl.TEXTURE_2D, fallback);
        return;
    }

    const entry = lookupSampleable(id);

    if (!entry || entry.width === 0) {
        warnOnce(`tex:${id}`, `${label} is bound to texture ${id}, which has never been uploaded. Using the ` +
                              `built-in fallback. Something upstream bound a texture without calling Apply().`);
        gl.bindTexture(gl.TEXTURE_2D, fallback);
        return;
    }
    if (id === boundTargetId) {
        warnOnce(`feedback:${id}`,
                 `${label} samples render target ${id}, which is also the current draw target. GL leaves that ` +
                 `undefined. Something upstream needs a ping-pong pair here.`);
    }

    gl.bindTexture(gl.TEXTURE_2D, entry.tex);
}
export function uploadMesh(id, vertexCount, header, payload) {
    requireGl();
    const h = asInts(header);
    const bytes = asBytes(payload);
    const vertexByteLength = h[0];
    const indexCount = h[1];

    let entry = meshes.get(id);
    if (!entry) {
        entry = { vao: gl.createVertexArray(), vbo: gl.createBuffer(), ibo: gl.createBuffer(), offsets: null };
        meshes.set(id, entry);
    }

    gl.bindVertexArray(entry.vao);

    gl.bindBuffer(gl.ARRAY_BUFFER, entry.vbo);
    gl.bufferData(gl.ARRAY_BUFFER, bytes.subarray(0, vertexByteLength), gl.DYNAMIC_DRAW);

    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, entry.ibo);
    gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, bytes.subarray(vertexByteLength), gl.DYNAMIC_DRAW);

    for (let a = 0; a < ATTR.length; ++a) {
        const offset = h[2 + a];

        if (offset < 0) {
            fail(`uploadMesh: mesh ${id} has no ${ATTR[a].name} stream. NowUI's render layout writes all nine.`);
        }

        gl.enableVertexAttribArray(a);
        gl.vertexAttribPointer(a, ATTR[a].size, gl.FLOAT, false, h[11 + a], offset);
    }

    gl.bindVertexArray(null);
    entry.indexCount = indexCount;
    entry.vertexCount = vertexCount;
}

export function releaseMesh(id) {
    if (gl === null) return;
    const entry = meshes.get(id);
    if (!entry) return;
    gl.deleteVertexArray(entry.vao);
    gl.deleteBuffer(entry.vbo);
    gl.deleteBuffer(entry.ibo);
    meshes.delete(id);
}
export function draw(meshId, shaderName, info, uniforms) {
    requireGl();

    const entry = meshes.get(meshId);
    if (!entry) fail(`draw: mesh ${meshId} was never uploaded.`);

    const i = asInts(info);
    const indexCount = i[0];
    if (indexCount <= 0) return;

    const resolved = selectPass(shaderName, i[4]);
    const block = toBlock(uniforms);
    const wantsSdfBlock = resolved.uniforms.sdfData0 !== null;

    if (wantsSdfBlock && !sdfBlockPending) {
        fail(`draw: "${shaderName}" reads the SDF uniform block, but setSdfUniforms was not called for this ` +
             'draw. The C# side must call it immediately before every SDF draw; see WebGL2Backend.DrawMesh.');
    }

    sdfBlockPending = false;
    applyBlendState(shaderName);

    gl.useProgram(resolved.program);
    applyUniformBlock(resolved.uniforms, block);

    if (wantsSdfBlock) applySdfUniformBlock(resolved.uniforms);

    bindTextureUnit(UNIT_MAIN_TEX, i[1], whiteTexture, '_MainTex');
    bindTextureUnit(UNIT_TEXTURE_MASK0, i[2], blackTexture, '_NowUITextureMask0');
    bindTextureUnit(UNIT_TEXTURE_MASK1, i[3], blackTexture, '_NowUITextureMask1');
    if (resolved.uniforms.gradientRampTexture) {
        bindTextureUnit(UNIT_GRADIENT_RAMP, i[7], whiteTexture, '_NowGradientRampTexture');
    }
    if (wantsSdfBlock) {
        bindTextureUnit(UNIT_SDF_IMAGE_FIELD, i[5], blackTexture, '_SdfImageField');
        bindTextureUnit(UNIT_SDF_IMAGE_COLOR, i[6], blackTexture, '_SdfImageColor');
    }

    gl.bindVertexArray(entry.vao);
    gl.drawElements(gl.TRIANGLES, indexCount, gl.UNSIGNED_INT, 0);
    gl.bindVertexArray(null);
}
function applyUniformBlock(u, block) {
    if (u.mvp) gl.uniformMatrix4fv(u.mvp, false, block.subarray(U.MVP, U.MVP + 16));
    if (u.mainTexST) gl.uniform4fv(u.mainTexST, block.subarray(U.MAIN_TEX_ST, U.MAIN_TEX_ST + 4));
    if (u.premultipliedTexture) gl.uniform1f(u.premultipliedTexture, block[U.PREMULTIPLIED_TEXTURE]);
    if (u.textSdfEncoding) gl.uniform1f(u.textSdfEncoding, block[U.TEXT_SDF_ENCODING]);
    if (u.maskCount) gl.uniform1f(u.maskCount, block[U.MASK_COUNT]);
    if (u.textureMaskCount) gl.uniform1f(u.textureMaskCount, block[U.TEXTURE_MASK_COUNT]);
    if (u.maskRects) gl.uniform4fv(u.maskRects, block.subarray(U.MASK_RECTS, U.MASK_RECTS + 32));
    if (u.maskData) gl.uniform4fv(u.maskData, block.subarray(U.MASK_DATA, U.MASK_DATA + 32));
    if (u.maskParams) gl.uniform4fv(u.maskParams, block.subarray(U.MASK_PARAMS, U.MASK_PARAMS + 32));
    if (u.maskTransforms) gl.uniform4fv(u.maskTransforms, block.subarray(U.MASK_TRANSFORMS, U.MASK_TRANSFORMS + 32));
    if (u.textureMaskRects)
        gl.uniform4fv(u.textureMaskRects, block.subarray(U.TEXTURE_MASK_RECTS, U.TEXTURE_MASK_RECTS + 8));
    if (u.textureMaskParams)
        gl.uniform4fv(u.textureMaskParams, block.subarray(U.TEXTURE_MASK_PARAMS, U.TEXTURE_MASK_PARAMS + 8));
    if (u.textureMaskTransforms)
        gl.uniform4fv(u.textureMaskTransforms,
                      block.subarray(U.TEXTURE_MASK_TRANSFORMS, U.TEXTURE_MASK_TRANSFORMS + 8));

    if (u.gradientRampTexelSize) {
        gl.uniform4fv(u.gradientRampTexelSize,
                      block.subarray(U.GRADIENT_RAMP_TEXEL_SIZE, U.GRADIENT_RAMP_TEXEL_SIZE + 4));
    }

    if (u.colorPickerMode) gl.uniform1f(u.colorPickerMode, block[U.COLOR_PICKER_MODE]);
    if (u.glassUseBackdrop) gl.uniform1f(u.glassUseBackdrop, block[U.GLASS_USE_BACKDROP]);
    if (u.glassUseStereoBackdrop) gl.uniform1f(u.glassUseStereoBackdrop, block[U.GLASS_USE_STEREO_BACKDROP]);
    if (u.glassMaterialMode) gl.uniform1f(u.glassMaterialMode, block[U.GLASS_MATERIAL_MODE]);

    if (u.backdropUvTransform) {
        gl.uniform4fv(u.backdropUvTransform,
                      block.subarray(U.BACKDROP_UV_TRANSFORM, U.BACKDROP_UV_TRANSFORM + 4));
    }

    if (u.blurTexelSize) gl.uniform4fv(u.blurTexelSize, block.subarray(U.BLUR_TEXEL_SIZE, U.BLUR_TEXEL_SIZE + 4));

    if (u.blurSourceScaleOffset) {
        gl.uniform4fv(u.blurSourceScaleOffset,
                      block.subarray(U.BLUR_SOURCE_SCALE_OFFSET, U.BLUR_SOURCE_SCALE_OFFSET + 4));
    }

    if (u.blurDirection) gl.uniform2fv(u.blurDirection, block.subarray(U.BLUR_DIRECTION, U.BLUR_DIRECTION + 2));

    if (u.sourceUv) gl.uniform4fv(u.sourceUv, block.subarray(U.SOURCE_UV, U.SOURCE_UV + 4));
    if (u.fieldParams) gl.uniform4fv(u.fieldParams, block.subarray(U.FIELD_PARAMS, U.FIELD_PARAMS + 4));
    if (u.fieldTexels) gl.uniform4fv(u.fieldTexels, block.subarray(U.FIELD_TEXELS, U.FIELD_TEXELS + 4));
    if (u.stampRect) gl.uniform4fv(u.stampRect, block.subarray(U.STAMP_RECT, U.STAMP_RECT + 4));
    if (u.step) gl.uniform1f(u.step, block[U.STEP]);
}

/**
 * Hands the SDF scene block over for the draw that follows. Called by the C# side immediately before a
 * NowUI/SDF Scene draw and at no other time; draw() refuses an SDF program that did not get one.
 *
 * The MemoryView is valid only for this call, so the payload is copied into the module's own Float32Array
 * rather than retained.
 */
export function setSdfUniforms(uniforms) {
    requireGl();

    const floats = asFloats(asBytes(uniforms));

    if (floats.length !== S.COUNT) {
        fail(`setSdfUniforms: the block is ${floats.length} floats; ${S.COUNT} were expected. The slot map ` +
             'here and WebGL2Backend.SdfUniformSlots have to move together.');
    }

    sdfBlock.set(floats);
    sdfBlockPending = true;
}

/**
 * Pushes the SDF block into the program. Every uniform is uploaded unconditionally rather than behind a
 * "did it change" test: the whole point of the separate block is that this runs only for SDF draws, of which
 * a frame has a handful, and a dirty-tracking scheme that got it wrong would show up as one scene wearing
 * another scene's shapes.
 *
 * The `[0]`-suffixed array locations take the WHOLE array in one call. Never slice them to the live shape
 * count -- see the capacity note above S.
 */
function applySdfUniformBlock(u) {
    const shapes = SDF_MAX_SHAPES * 4;
    const layers = SDF_MAX_LAYERS * 4;

    gl.uniform4fv(u.sdfData0, sdfBlock.subarray(S.DATA0, S.DATA0 + shapes));
    gl.uniform4fv(u.sdfData1, sdfBlock.subarray(S.DATA1, S.DATA1 + shapes));
    gl.uniform4fv(u.sdfData2, sdfBlock.subarray(S.DATA2, S.DATA2 + shapes));
    gl.uniform4fv(u.sdfShapeMeta, sdfBlock.subarray(S.SHAPE_META, S.SHAPE_META + shapes));
    gl.uniform4fv(u.sdfColors, sdfBlock.subarray(S.COLORS, S.COLORS + shapes));
    gl.uniform4fv(u.sdfUvs, sdfBlock.subarray(S.UVS, S.UVS + shapes));
    gl.uniform4fv(u.sdfImageUvs, sdfBlock.subarray(S.IMAGE_UVS, S.IMAGE_UVS + shapes));
    gl.uniform4fv(u.sdfLayerData0, sdfBlock.subarray(S.LAYER_DATA0, S.LAYER_DATA0 + layers));
    gl.uniform4fv(u.sdfLayerData1, sdfBlock.subarray(S.LAYER_DATA1, S.LAYER_DATA1 + layers));

    const vec4 = (loc, slot) => { if (loc) gl.uniform4fv(loc, sdfBlock.subarray(slot, slot + 4)); };
    vec4(u.sdfImageAtlasSize, S.IMAGE_ATLAS_SIZE);
    vec4(u.sdfOutline, S.OUTLINE);
    vec4(u.sdfOutlineColor, S.OUTLINE_COLOR);
    vec4(u.sdfGlow, S.GLOW);
    vec4(u.sdfGlowColor, S.GLOW_COLOR);
    vec4(u.sdfShadow, S.SHADOW);
    vec4(u.sdfShadowColor, S.SHADOW_COLOR);
    vec4(u.sdfInnerShadow, S.INNER_SHADOW);
    vec4(u.sdfInnerShadowColor, S.INNER_SHADOW_COLOR);
    vec4(u.sdfEmboss, S.EMBOSS);
    vec4(u.sdfContour, S.CONTOUR);
    vec4(u.sdfContourColor, S.CONTOUR_COLOR);
    vec4(u.sdfContourMask, S.CONTOUR_MASK);
    vec4(u.sdfWarp, S.WARP);

    if (u.sdfShapeCount) gl.uniform1f(u.sdfShapeCount, sdfBlock[S.SHAPE_COUNT]);
    if (u.sdfLayerCount) gl.uniform1f(u.sdfLayerCount, sdfBlock[S.LAYER_COUNT]);
    if (u.sdfFeather) gl.uniform1f(u.sdfFeather, sdfBlock[S.FEATHER]);
    if (u.sdfTextEffectLimit) gl.uniform1f(u.sdfTextEffectLimit, sdfBlock[S.TEXT_EFFECT_LIMIT]);
    if (u.sdfMaskOutput) gl.uniform1f(u.sdfMaskOutput, sdfBlock[S.MASK_OUTPUT]);
    if (u.sdfCanvasLayout) gl.uniform1f(u.sdfCanvasLayout, sdfBlock[S.CANVAS_LAYOUT]);
    if (u.sdfTime) gl.uniform1f(u.sdfTime, sdfBlock[S.TIME]);
}
function resolveTargetFormat(format) {
    switch (format) {
        case 0:   // ARGB32
        case 7:   // Default
            return { internal: gl.RGBA8, renderable: true, filterable: true, name: 'ARGB32' };

        case 20:  // BGRA32. GL has no renderable BGRA sized format; channel order is a CPU-side concern and
            return { internal: gl.RGBA8, renderable: true, filterable: true, name: 'BGRA32 (as RGBA8)' };

        case 16:  // R8
            return { internal: gl.R8, renderable: true, filterable: true, name: 'R8' };

        case 4:   // RGB565
            return { internal: gl.RGB565, renderable: true, filterable: true, name: 'RGB565' };

        case 8:   // ARGB2101010
            return { internal: gl.RGB10_A2, renderable: true, filterable: true, name: 'ARGB2101010' };

        case 2:   // ARGBHalf
        case 9:   // DefaultHDR
            return { internal: gl.RGBA16F, renderable: colorBufferHalfFloat, filterable: true, name: 'ARGBHalf' };

        case 13:  // RGHalf
            return { internal: gl.RG16F, renderable: colorBufferHalfFloat, filterable: true, name: 'RGHalf' };

        case 15:  // RHalf
            return { internal: gl.R16F, renderable: colorBufferHalfFloat, filterable: true, name: 'RHalf' };
        case 11:  // ARGBFloat
            return { internal: gl.RGBA32F, renderable: colorBufferFloat, filterable: floatLinearFilter, name: 'ARGBFloat' };

        case 12:  // RGFloat
            return { internal: gl.RG32F, renderable: colorBufferFloat, filterable: floatLinearFilter, name: 'RGFloat' };

        case 14:  // RFloat
            return { internal: gl.R32F, renderable: colorBufferFloat, filterable: floatLinearFilter, name: 'RFloat' };

        default:
            return null;
    }
}

function fullMipLevels(width, height) {
    let levels = 1;
    let size = Math.max(width, height);

    while (size > 1) {
        size >>= 1;
        ++levels;
    }

    return levels;
}
export function createRenderTexture(id, info) {
    requireGl();

    const i = asInts(info);
    const width = i[0], height = i[1], depthBits = i[2];
    const volumeDepth = i[3], msaaSamples = i[5], format = i[6], dimension = i[7];
    const filter = i[8], wrapS = i[9], wrapT = i[10];
    const useMipMap = i[11] !== 0, autoGenerateMips = i[12] !== 0;
    releaseRenderTexture(id);
    const stale = textures.get(id);

    if (stale) {
        gl.deleteTexture(stale.tex);
        textures.delete(id);
    }

    if (width <= 0 || height <= 0) {
        console.error(`[NowUI.WebGL2] createRenderTexture: target ${id} is ${width}x${height}.`);
        return 0;
    }
    if (dimension !== 2) {
        console.error(`[NowUI.WebGL2] createRenderTexture: target ${id} asks for TextureDimension ${dimension}. ` +
                      'Only Tex2D (2) is implemented; array, 3D and cube targets are not ported.');
        return 0;
    }

    if (volumeDepth > 1) {
        console.error(`[NowUI.WebGL2] createRenderTexture: target ${id} asks for ${volumeDepth} slices on a ` +
                      'Tex2D target.');
        return 0;
    }

    const resolved = resolveTargetFormat(format);

    if (!resolved) {
        console.error(`[NowUI.WebGL2] createRenderTexture: target ${id} asks for RenderTextureFormat ${format}, ` +
                      'which this backend has no WebGL2 internal format for.');
        return 0;
    }

    if (!resolved.renderable) {
        console.error(`[NowUI.WebGL2] createRenderTexture: target ${id} asks for ${resolved.name}, which this ` +
                      'context cannot render into (EXT_color_buffer_float / EXT_color_buffer_half_float is ' +
                      'missing). NowRenderCaps reports this, so a caller that checked ' +
                      'SystemInfo.SupportsRenderTextureFormat should never have got here.');
        return 0;
    }
    if (msaaSamples > 1) {
        warnOnce('msaa', `a render target asked for ${msaaSamples}x MSAA. WebGL2 has no sampleable multisampled ` +
                         'texture, so it is allocated single-sampled. Edges inside render-to-texture will be ' +
                         'aliased unless the shader anti-aliases them, which every NowUI shader does.');
    }

    const levels = useMipMap ? fullMipLevels(width, height) : 1;
    const tex = gl.createTexture();

    gl.activeTexture(gl.TEXTURE0 + UNIT_MAIN_TEX);
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texStorage2D(gl.TEXTURE_2D, levels, resolved.internal, width, height);

    const entry = {
        tex,
        fbo: gl.createFramebuffer(),
        depthBuffer: null,
        width,
        height,
        levels,
        mipCount: levels,
        attachedLevel: 0,
        autoGenerateMips: autoGenerateMips && levels > 1,
        dirtyMips: false,
        filter,
        wrapS,
        wrapT,
        filterable: resolved.filterable,
        generation: contextGeneration,
    };

    applySampler(entry);

    gl.bindFramebuffer(gl.FRAMEBUFFER, entry.fbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);

    if (depthBits > 0) {
        const depthFormat = depthBits >= 32 ? gl.DEPTH24_STENCIL8
            : depthBits > 16 ? gl.DEPTH_COMPONENT24
            : gl.DEPTH_COMPONENT16;
        const attachment = depthBits >= 32 ? gl.DEPTH_STENCIL_ATTACHMENT : gl.DEPTH_ATTACHMENT;

        entry.depthBuffer = gl.createRenderbuffer();
        gl.bindRenderbuffer(gl.RENDERBUFFER, entry.depthBuffer);
        gl.renderbufferStorage(gl.RENDERBUFFER, depthFormat, width, height);
        gl.framebufferRenderbuffer(gl.FRAMEBUFFER, attachment, gl.RENDERBUFFER, entry.depthBuffer);
        gl.bindRenderbuffer(gl.RENDERBUFFER, null);
    }

    const status = gl.checkFramebufferStatus(gl.FRAMEBUFFER);
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    boundTargetId = 0;

    if (status !== gl.FRAMEBUFFER_COMPLETE) {
        console.error(`[NowUI.WebGL2] createRenderTexture: target ${id} (${width}x${height} ${resolved.name}) ` +
                      `made an incomplete framebuffer (status 0x${status.toString(16)}).`);
        gl.deleteFramebuffer(entry.fbo);
        if (entry.depthBuffer) gl.deleteRenderbuffer(entry.depthBuffer);
        gl.deleteTexture(tex);
        return 0;
    }

    renderTargets.set(id, entry);
    return 1;
}
export function isRenderTextureLost(id) {
    if (gl === null || gl.isContextLost()) return 1;

    const entry = renderTargets.get(id);
    if (!entry) return 1;

    return entry.generation === contextGeneration ? 0 : 1;
}

export function releaseRenderTexture(id) {
    if (gl === null) return;

    const entry = renderTargets.get(id);
    if (!entry) return;

    if (boundTargetId === id) {
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        boundTargetId = 0;
    }

    gl.deleteFramebuffer(entry.fbo);
    if (entry.depthBuffer) gl.deleteRenderbuffer(entry.depthBuffer);
    gl.deleteTexture(entry.tex);
    renderTargets.delete(id);
}
export function setRenderTarget(id, mipLevel, depthSlice) {
    requireGl();

    if (depthSlice > 0) {
        fail(`setRenderTarget: target ${id} asks for depth slice ${depthSlice}. Array targets are not ported; ` +
             'createRenderTexture refuses them, so this should be unreachable.');
    }

    bindTarget(id, mipLevel);
}
function bindTarget(id, mipLevel) {
    if (boundTargetId !== id) flushPendingMips();

    if (id === 0) {
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        boundTargetId = 0;
        return null;
    }

    const entry = renderTargets.get(id);

    if (!entry || entry.generation !== contextGeneration) {
        fail(`setRenderTarget: render target ${id} was never created, or was lost with the context. ` +
             'RenderTexture.Create() has to succeed before the target can be bound.');
    }

    gl.bindFramebuffer(gl.FRAMEBUFFER, entry.fbo);

    const level = Math.max(0, mipLevel | 0);

    if (level !== entry.attachedLevel) {
        if (level >= entry.levels) {
            fail(`setRenderTarget: target ${id} has ${entry.levels} mip level(s); level ${level} was asked for.`);
        }

        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, entry.tex, level);
        entry.attachedLevel = level;
    }

    boundTargetId = id;
    if (entry.autoGenerateMips) entry.dirtyMips = true;
    return entry;
}

function flushPendingMips() {
    const entry = renderTargets.get(boundTargetId);
    if (!entry || !entry.dirtyMips) return;

    entry.dirtyMips = false;
    gl.activeTexture(gl.TEXTURE0 + UNIT_MAIN_TEX);
    gl.bindTexture(gl.TEXTURE_2D, entry.tex);
    gl.generateMipmap(gl.TEXTURE_2D);
}
const GLSL_VERTEX_BLIT = `#version 300 es
precision highp float;

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aUv;

uniform mat4 nowui_MatrixMVP;
uniform vec4 _MainTex_ST;

out vec2 vUv;

void main()
{
    gl_Position = nowui_MatrixMVP * vec4(aPosition, 1.0);
    vUv = aUv * _MainTex_ST.xy + _MainTex_ST.zw;
}
`;
const GLSL_FRAGMENT_BLIT = `#version 300 es
precision highp float;

uniform highp sampler2D _MainTex;
in vec2 vUv;
out vec4 fragColor;

void main()
{
    fragColor = texture(_MainTex, vUv);
}
`;

function ensureBlitResources() {
    if (blitQuad === null) {
        const quad = new Float32Array([
            0, 0, 0, 0, 0,
            1, 0, 0, 1, 0,
            0, 1, 0, 0, 1,
            1, 1, 0, 1, 1,
        ]);

        const vao = gl.createVertexArray();
        const vbo = gl.createBuffer();
        gl.bindVertexArray(vao);
        gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
        gl.bufferData(gl.ARRAY_BUFFER, quad, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 20, 0);
        gl.enableVertexAttribArray(1);
        gl.vertexAttribPointer(1, 2, gl.FLOAT, false, 20, 12);
        gl.bindVertexArray(null);
        blitQuad = { vao, vbo };
    }

    if (copyProgram === null) {
        const vs = compile(gl.VERTEX_SHADER, GLSL_VERTEX_BLIT, 'internal blit vertex shader');
        const fs = compile(gl.FRAGMENT_SHADER, GLSL_FRAGMENT_BLIT, 'internal blit fragment shader');
        const program = gl.createProgram();
        gl.attachShader(program, vs);
        gl.attachShader(program, fs);
        gl.linkProgram(program);

        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
            const log = gl.getProgramInfoLog(program);
            gl.deleteProgram(program);
            fail(`the internal blit program failed to link:\n${log}`);
        }

        gl.deleteShader(vs);
        gl.deleteShader(fs);

        const uniforms = {
            mvp: gl.getUniformLocation(program, 'nowui_MatrixMVP'),
            mainTexST: gl.getUniformLocation(program, '_MainTex_ST'),
            mainTex: gl.getUniformLocation(program, '_MainTex'),
        };

        gl.useProgram(program);
        gl.uniform1i(uniforms.mainTex, UNIT_MAIN_TEX);
        gl.useProgram(null);

        copyProgram = { program, uniforms };
    }
}

function ensureEmptyVao() {
    if (emptyVao === null) emptyVao = gl.createVertexArray();
    return emptyVao;
}

function toBlock(uniforms) {
    const block = asFloats(asBytes(uniforms));

    if (block.length < U.COUNT)
        fail(`the uniform block is ${block.length} floats; ${U.COUNT} were expected.`);

    return block;
}
export function blit(shaderName, info, uniforms) {
    requireGl();

    const i = asInts(info);
    const sourceId = i[0], destinationId = i[1], destinationMip = i[2];
    const destinationWidth = i[3], destinationHeight = i[4];

    ensureBlitResources();

    const resolved = shaderName ? selectPass(shaderName, i[7]) : copyProgram;

    bindTarget(destinationId, destinationMip);
    gl.viewport(0, 0, destinationWidth, destinationHeight);

    const block = toBlock(uniforms);
    gl.useProgram(resolved.program);

    if (resolved === copyProgram) {
        gl.uniformMatrix4fv(resolved.uniforms.mvp, false, block.subarray(U.MVP, U.MVP + 16));
        gl.uniform4fv(resolved.uniforms.mainTexST, block.subarray(U.MAIN_TEX_ST, U.MAIN_TEX_ST + 4));
    } else {
        applyUniformBlock(resolved.uniforms, block);
    }

    bindTextureUnit(UNIT_MAIN_TEX, sourceId, whiteTexture, '_MainTex');
    bindTextureUnit(UNIT_TEXTURE_MASK0, i[5], blackTexture, '_NowUITextureMask0');
    bindTextureUnit(UNIT_TEXTURE_MASK1, i[6], blackTexture, '_NowUITextureMask1');
    bindTextureUnit(UNIT_SOURCE_TEX, i[8], whiteTexture, '_SourceTex');

    gl.disable(gl.BLEND);
    gl.bindVertexArray(blitQuad.vao);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    gl.bindVertexArray(null);
    gl.enable(gl.BLEND);
    invalidateBlendState();
}

function glTopology(topology) {
    switch (topology) {
        case 0: return gl.TRIANGLES;
        case 3: return gl.LINES;
        case 4: return gl.LINE_STRIP;
        case 5: return gl.POINTS;
        default: return -1;
    }
}
export function drawProcedural(shaderName, info, uniforms) {
    requireGl();

    const i = asInts(info);
    const resolved = selectPass(shaderName, i[6]);
    const mode = glTopology(i[0]);
    const vertexCount = i[1];
    const instanceCount = i[2];

    if (mode < 0) fail(`drawProcedural: MeshTopology ${i[0]} has no WebGL2 equivalent.`);
    if (vertexCount <= 0 || instanceCount <= 0) return;

    const block = toBlock(uniforms);
    gl.useProgram(resolved.program);
    applyUniformBlock(resolved.uniforms, block);

    bindTextureUnit(UNIT_MAIN_TEX, i[3], whiteTexture, '_MainTex');
    bindTextureUnit(UNIT_TEXTURE_MASK0, i[4], blackTexture, '_NowUITextureMask0');
    bindTextureUnit(UNIT_TEXTURE_MASK1, i[5], blackTexture, '_NowUITextureMask1');

    gl.disable(gl.BLEND);
    gl.bindVertexArray(ensureEmptyVao());

    if (instanceCount > 1) gl.drawArraysInstanced(mode, 0, vertexCount, instanceCount);
    else gl.drawArrays(mode, 0, vertexCount);

    gl.bindVertexArray(null);
    gl.enable(gl.BLEND);
    invalidateBlendState();   // see the note in blit()
}
export function copyTexture(sourceId, destinationId) {
    requireGl();

    const source = lookupSampleable(sourceId);
    if (!source || source.width === 0) fail(`copyTexture: source texture ${sourceId} has no GPU storage.`);

    const destination = lookupSampleable(destinationId);
    if (!destination) fail(`copyTexture: destination texture ${destinationId} has no GPU object.`);

    if (destination.width !== 0 && (destination.width !== source.width || destination.height !== source.height)) {
        fail(`copyTexture: source ${sourceId} is ${source.width}x${source.height} and destination ` +
             `${destinationId} is ${destination.width}x${destination.height}. A copy does not rescale.`);
    }

    if (scratchFbo === null) scratchFbo = gl.createFramebuffer();
    const previousEntry = renderTargets.get(boundTargetId);
    const previousTarget = previousEntry ? boundTargetId : 0;
    const previousLevel = previousEntry ? previousEntry.attachedLevel : 0;

    gl.bindFramebuffer(gl.FRAMEBUFFER, scratchFbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, source.tex, 0);

    const status = gl.checkFramebufferStatus(gl.FRAMEBUFFER);

    if (status !== gl.FRAMEBUFFER_COMPLETE) {
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, null, 0);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        boundTargetId = 0;
        fail(`copyTexture: source ${sourceId} cannot be attached to a framebuffer (status ` +
             `0x${status.toString(16)}), so there is nothing to copy out of.`);
    }

    gl.activeTexture(gl.TEXTURE0 + UNIT_MAIN_TEX);
    gl.bindTexture(gl.TEXTURE_2D, destination.tex);

    if (destination.width === 0) {
        gl.copyTexImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, 0, 0, source.width, source.height, 0);
        destination.width = source.width;
        destination.height = source.height;
        applySampler(destination);
    } else {
        gl.copyTexSubImage2D(gl.TEXTURE_2D, 0, 0, 0, 0, 0, source.width, source.height);
    }

    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, null, 0);
    boundTargetId = -1;   // force bindTarget past its no-op check without firing a mip regeneration
    bindTarget(previousTarget, previousLevel);
}
export function getError() {
    if (gl === null) return 0;
    return gl.getError();
}

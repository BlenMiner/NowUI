using System;
using UnityEngine;

namespace NowUI.Internal
{
    /// <summary>
    /// GPU-ready description of one analytic mask. Four vectors intentionally
    /// mirror the fixed shader arrays so shaped masks do not consume another
    /// vertex stream (the UGUI layout has no universal spare channel).
    /// </summary>
    internal readonly struct NowMaskShaderDescriptor : IEquatable<NowMaskShaderDescriptor>
    {
        public readonly Vector4 rect;
        public readonly Vector4 data;
        public readonly Vector4 parameters;
        public readonly Vector4 transform;

        public NowMaskShaderDescriptor(Vector4 rect, Vector4 data, Vector4 parameters, Vector4 transform)
        {
            this.rect = rect;
            this.data = data;
            this.parameters = parameters;
            this.transform = transform;
        }

        public bool Equals(NowMaskShaderDescriptor other)
        {
            return rect.Equals(other.rect) &&
                data.Equals(other.data) &&
                parameters.Equals(other.parameters) &&
                transform.Equals(other.transform);
        }

        public override bool Equals(object obj)
        {
            return obj is NowMaskShaderDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = rect.GetHashCode();
                hash = hash * 397 ^ data.GetHashCode();
                hash = hash * 397 ^ parameters.GetHashCode();
                hash = hash * 397 ^ transform.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>GPU-ready description of one texture-coverage mask.</summary>
    internal readonly struct NowTextureMaskShaderDescriptor : IEquatable<NowTextureMaskShaderDescriptor>
    {
        public readonly Texture texture;
        public readonly Vector4 rect;
        public readonly Vector4 parameters;
        public readonly Vector4 transform;

        public NowTextureMaskShaderDescriptor(
            Texture texture,
            Vector4 rect,
            Vector4 parameters,
            Vector4 transform)
        {
            this.texture = texture;
            this.rect = rect;
            this.parameters = parameters;
            this.transform = transform;
        }

        public bool Equals(NowTextureMaskShaderDescriptor other)
        {
            return ReferenceEquals(texture, other.texture) &&
                rect.Equals(other.rect) &&
                parameters.Equals(other.parameters) &&
                transform.Equals(other.transform);
        }

        public override bool Equals(object obj)
        {
            return obj is NowTextureMaskShaderDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ReferenceEquals(texture, null) ? 0 : texture.GetHashCode();
                hash = hash * 397 ^ rect.GetHashCode();
                hash = hash * 397 ^ parameters.GetHashCode();
                hash = hash * 397 ^ transform.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Allocation-free snapshot carried by a material batch. Rect-only masks
    /// stay in the existing per-vertex stream and leave this value empty.
    /// </summary>
    internal struct NowMaskShaderState : IEquatable<NowMaskShaderState>
    {
        public const int Capacity = 8;

        public const int TextureCapacity = 2;

        int _count;

        int _textureCount;

        NowMaskShaderDescriptor _mask0;
        NowMaskShaderDescriptor _mask1;
        NowMaskShaderDescriptor _mask2;
        NowMaskShaderDescriptor _mask3;
        NowMaskShaderDescriptor _mask4;
        NowMaskShaderDescriptor _mask5;
        NowMaskShaderDescriptor _mask6;
        NowMaskShaderDescriptor _mask7;

        NowTextureMaskShaderDescriptor _textureMask0;
        NowTextureMaskShaderDescriptor _textureMask1;

        // Snapshots rebuilt from the same ambient stack share an identity. This
        // lets the overwhelmingly common same-scope batch comparison avoid
        // walking up to ten descriptors, while snapshots produced independently
        // still fall back to exact structural equality.
        ulong _identity;

        public readonly int count => _count;

        public readonly int textureCount => _textureCount;

        public readonly bool isEmpty => _count <= 0 && _textureCount <= 0;

        internal readonly ulong identity => _identity;

        public void Clear()
        {
            _count = 0;
            _textureCount = 0;
            _textureMask0 = default;
            _textureMask1 = default;
            _identity = 0;
        }

        public void Add(in NowMaskShaderDescriptor descriptor)
        {
            _identity = 0;

            switch (_count)
            {
                case 0: _mask0 = descriptor; break;
                case 1: _mask1 = descriptor; break;
                case 2: _mask2 = descriptor; break;
                case 3: _mask3 = descriptor; break;
                case 4: _mask4 = descriptor; break;
                case 5: _mask5 = descriptor; break;
                case 6: _mask6 = descriptor; break;
                case 7: _mask7 = descriptor; break;
                default:
                    throw new InvalidOperationException(
                        $"NowUI supports at most {Capacity} simultaneously nested analytic masks. " +
                        "Rectangular Now.Mask(NowRect) scopes do not count toward this limit.");
            }

            ++_count;
        }

        public void AddTexture(in NowTextureMaskShaderDescriptor descriptor)
        {
            _identity = 0;

            switch (_textureCount)
            {
                case 0: _textureMask0 = descriptor; break;
                case 1: _textureMask1 = descriptor; break;
                default:
                    throw new InvalidOperationException(
                        $"NowUI supports at most {TextureCapacity} simultaneously nested texture masks. " +
                        "Rectangular and analytic mask scopes do not count toward this limit.");
            }

            ++_textureCount;
        }

        internal void SetIdentity(ulong identity)
        {
            _identity = identity;
        }

        public readonly NowMaskShaderDescriptor Get(int index)
        {
            return index switch
            {
                0 => _mask0,
                1 => _mask1,
                2 => _mask2,
                3 => _mask3,
                4 => _mask4,
                5 => _mask5,
                6 => _mask6,
                7 => _mask7,
                _ => default
            };
        }

        public readonly NowTextureMaskShaderDescriptor GetTexture(int index)
        {
            return index switch
            {
                0 => _textureMask0,
                1 => _textureMask1,
                _ => default
            };
        }

        public readonly bool Equals(NowMaskShaderState other)
        {
            return Equals(in other);
        }

        /// <summary>
        /// Reference-taking equality. The state is over 600 bytes, and the batch
        /// selection in <c>Now.UseMaterial</c> compares it on every draw call, so
        /// callers pass it with <c>in</c> instead of copying it onto the stack.
        /// </summary>
        public readonly bool Equals(in NowMaskShaderState other)
        {
            if (_identity != 0 && _identity == other._identity)
                return true;

            // Two empty states are equal regardless of identity; this is the
            // common unmasked case and needs no descriptor walk.
            if (_count <= 0 && _textureCount <= 0)
                return other._count <= 0 && other._textureCount <= 0;

            int safeCount = Mathf.Clamp(_count, 0, Capacity);
            if (safeCount != Mathf.Clamp(other._count, 0, Capacity))
                return false;

            for (int i = 0; i < safeCount; ++i)
            {
                if (!Get(i).Equals(other.Get(i)))
                    return false;
            }

            int safeTextureCount = Mathf.Clamp(_textureCount, 0, TextureCapacity);
            if (safeTextureCount != Mathf.Clamp(other._textureCount, 0, TextureCapacity))
                return false;

            for (int i = 0; i < safeTextureCount; ++i)
            {
                if (!GetTexture(i).Equals(other.GetTexture(i)))
                    return false;
            }

            return true;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is NowMaskShaderState other && Equals(in other);
        }

        public override readonly int GetHashCode()
        {
            unchecked
            {
                int safeCount = Mathf.Clamp(_count, 0, Capacity);
                int hash = safeCount;

                for (int i = 0; i < safeCount; ++i)
                    hash = hash * 397 ^ Get(i).GetHashCode();

                int safeTextureCount = Mathf.Clamp(_textureCount, 0, TextureCapacity);
                hash = hash * 397 ^ safeTextureCount;

                for (int i = 0; i < safeTextureCount; ++i)
                    hash = hash * 397 ^ GetTexture(i).GetHashCode();

                return hash;
            }
        }
    }

    /// <summary>Shared material/property-block binding for shader-evaluated masks.</summary>
    internal static class NowMaskShader
    {
        static readonly int _countId = Shader.PropertyToID("_NowUIMaskCount");
        static readonly int _rectsId = Shader.PropertyToID("_NowUIMaskRects");
        static readonly int _dataId = Shader.PropertyToID("_NowUIMaskData");
        static readonly int _parametersId = Shader.PropertyToID("_NowUIMaskParams");
        static readonly int _transformsId = Shader.PropertyToID("_NowUIMaskTransforms");
        static readonly int _textureCountId = Shader.PropertyToID("_NowUITextureMaskCount");
        static readonly int _texture0Id = Shader.PropertyToID("_NowUITextureMask0");
        static readonly int _texture1Id = Shader.PropertyToID("_NowUITextureMask1");
        static readonly int _textureRectsId = Shader.PropertyToID("_NowUITextureMaskRects");
        static readonly int _textureParametersId = Shader.PropertyToID("_NowUITextureMaskParams");
        static readonly int _textureTransformsId = Shader.PropertyToID("_NowUITextureMaskTransforms");

        static readonly Vector4[] _rects = new Vector4[NowMaskShaderState.Capacity];
        static readonly Vector4[] _data = new Vector4[NowMaskShaderState.Capacity];
        static readonly Vector4[] _parameters = new Vector4[NowMaskShaderState.Capacity];
        static readonly Vector4[] _transforms = new Vector4[NowMaskShaderState.Capacity];
        static readonly Vector4[] _textureRects = new Vector4[NowMaskShaderState.TextureCapacity];
        static readonly Vector4[] _textureParameters = new Vector4[NowMaskShaderState.TextureCapacity];
        static readonly Vector4[] _textureTransforms = new Vector4[NowMaskShaderState.TextureCapacity];
        static readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
        static NowMaskShaderState _propertyBlockState;
        static int _propertyBlockTextureValidity;
        static bool _hasPropertyBlockState;

        internal static bool Supports(Material material)
        {
            return SupportsAnalytic(material) || SupportsTexture(material);
        }

        internal static bool Supports(Material material, in NowMaskShaderState state)
        {
            return (state.count > 0 && SupportsAnalytic(material)) ||
                (state.textureCount > 0 && SupportsTexture(material));
        }

        static bool SupportsAnalytic(Material material)
        {
            return material != null && material.HasProperty(_countId);
        }

        static bool SupportsTexture(Material material)
        {
            return material != null &&
                material.HasProperty(_textureCountId) &&
                material.HasProperty(_texture0Id) &&
                material.HasProperty(_texture1Id);
        }

        internal static void Apply(Material material, in NowMaskShaderState state)
        {
            if (material == null || state.isEmpty)
                return;

            if (SupportsAnalytic(material))
            {
                int count = Mathf.Clamp(state.count, 0, NowMaskShaderState.Capacity);
                material.SetFloat(_countId, count);

                if (count > 0)
                {
                    FillAnalyticArrays(state, count);
                    material.SetVectorArray(_rectsId, _rects);
                    material.SetVectorArray(_dataId, _data);
                    material.SetVectorArray(_parametersId, _parameters);
                    material.SetVectorArray(_transformsId, _transforms);
                }
            }

            if (SupportsTexture(material))
            {
                int count = Mathf.Clamp(state.textureCount, 0, NowMaskShaderState.TextureCapacity);
                material.SetFloat(_textureCountId, count);

                if (count > 0)
                {
                    FillTextureArrays(state, count);
                    material.SetVectorArray(_textureRectsId, _textureRects);
                    material.SetVectorArray(_textureParametersId, _textureParameters);
                    material.SetVectorArray(_textureTransformsId, _textureTransforms);
                }

                material.SetTexture(
                    _texture0Id,
                    count > 0 ? TextureOrBlack(state.GetTexture(0).texture) : Texture2D.blackTexture);
                material.SetTexture(
                    _texture1Id,
                    count > 1 ? TextureOrBlack(state.GetTexture(1).texture) : Texture2D.blackTexture);
            }
        }

        internal static void Clear(Material material)
        {
            if (SupportsAnalytic(material))
                material.SetFloat(_countId, 0f);

            if (SupportsTexture(material))
            {
                material.SetFloat(_textureCountId, 0f);
                material.SetTexture(_texture0Id, Texture2D.blackTexture);
                material.SetTexture(_texture1Id, Texture2D.blackTexture);
            }
        }

        /// <summary>
        /// Returns null for the legacy fast path, or a reusable block for a
        /// shader-mask state. Unity copies a MaterialPropertyBlock when it is submitted
        /// to a command buffer or renderer, so callers may use this for consecutive
        /// batches without allocating one block per submesh.
        /// </summary>
        internal static MaterialPropertyBlock GetPropertyBlock(in NowMaskShaderState state)
        {
            if (state.isEmpty)
                return null;

            int textureValidity = TextureValidityBits(state);

            if (_hasPropertyBlockState &&
                textureValidity == _propertyBlockTextureValidity &&
                _propertyBlockState.Equals(in state))
            {
                return _propertyBlock;
            }

            _propertyBlock.Clear();
            int analyticCount = Mathf.Clamp(state.count, 0, NowMaskShaderState.Capacity);
            int textureCount = Mathf.Clamp(state.textureCount, 0, NowMaskShaderState.TextureCapacity);
            _propertyBlock.SetFloat(_countId, analyticCount);
            _propertyBlock.SetFloat(_textureCountId, textureCount);

            if (analyticCount > 0)
            {
                FillAnalyticArrays(state, analyticCount);
                _propertyBlock.SetVectorArray(_rectsId, _rects);
                _propertyBlock.SetVectorArray(_dataId, _data);
                _propertyBlock.SetVectorArray(_parametersId, _parameters);
                _propertyBlock.SetVectorArray(_transformsId, _transforms);
            }

            if (textureCount > 0)
            {
                FillTextureArrays(state, textureCount);
                _propertyBlock.SetVectorArray(_textureRectsId, _textureRects);
                _propertyBlock.SetVectorArray(_textureParametersId, _textureParameters);
                _propertyBlock.SetVectorArray(_textureTransformsId, _textureTransforms);
                _propertyBlock.SetTexture(_texture0Id, TextureOrBlack(state.GetTexture(0).texture));

                if (textureCount > 1)
                    _propertyBlock.SetTexture(_texture1Id, TextureOrBlack(state.GetTexture(1).texture));
            }

            _propertyBlockState = state;
            _propertyBlockTextureValidity = textureValidity;
            _hasPropertyBlockState = true;

            return _propertyBlock;
        }

        internal static int TextureValidityBits(in NowMaskShaderState state)
        {
            int count = Mathf.Clamp(state.textureCount, 0, NowMaskShaderState.TextureCapacity);
            int bits = 0;

            for (int i = 0; i < count; ++i)
            {
                if (state.GetTexture(i).texture)
                    bits |= 1 << i;
            }

            return bits;
        }

        static void FillAnalyticArrays(in NowMaskShaderState state, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                var descriptor = state.Get(i);
                _rects[i] = descriptor.rect;
                _data[i] = descriptor.data;
                _parameters[i] = descriptor.parameters;
                _transforms[i] = descriptor.transform;
            }
        }

        static void FillTextureArrays(in NowMaskShaderState state, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                var descriptor = state.GetTexture(i);
                var parameters = descriptor.parameters;

                // A texture may be destroyed after the batch captured its mask
                // state but before Unity binds that batch. Preserve capture-time
                // invalidity (for example, invalid bounds), and re-check Unity's
                // current object truth so inversion can never turn the black
                // fallback into solid coverage.
                parameters.z = parameters.z > 0f && descriptor.texture ? 1f : 0f;
                _textureRects[i] = descriptor.rect;
                _textureParameters[i] = parameters;
                _textureTransforms[i] = descriptor.transform;
            }
        }

        static Texture TextureOrBlack(Texture texture)
        {
            return texture ? texture : Texture2D.blackTexture;
        }
    }
}

namespace NowUI
{
    public static partial class Now
    {
        static int _suppressShaderMaskCaptureDepth;

        static NowUI.Internal.NowMaskShaderState _cachedMaskShaderState;
        static readonly NowUI.Internal.NowMaskShaderState _emptyMaskShaderState;
        static bool _maskShaderStateDirty;
        static int _cachedMaskTextureValidity;
        static ulong _nextMaskShaderStateIdentity;

        internal readonly ref struct ShaderMaskCaptureSuppressionScope
        {
            public void Dispose()
            {
                if (_suppressShaderMaskCaptureDepth > 0)
                    --_suppressShaderMaskCaptureDepth;
            }
        }

        /// <summary>
        /// Keeps ambient mask culling and legacy per-vertex bounds active while
        /// preventing already-rasterized analytic and texture coverage from being
        /// attached to an internal replay batch a second time.
        /// </summary>
        internal static ShaderMaskCaptureSuppressionScope SuppressShaderMaskCapture()
        {
            ++_suppressShaderMaskCaptureDepth;
            return default;
        }

        internal static void InvalidateMaskShaderState()
        {
            _maskShaderStateDirty = true;
        }

        internal static void ResetMaskShaderState()
        {
            // Assigning default also releases any caller-owned textures retained
            // by the previous cached descriptors at a frame/context reset.
            _cachedMaskShaderState = default;
            _cachedMaskTextureValidity = 0;
            _maskShaderStateDirty = false;
        }

        static ulong NextMaskShaderStateIdentity()
        {
            do
            {
                _nextMaskShaderStateIdentity = unchecked(_nextMaskShaderStateIdentity + 1UL);
            }
            while (_nextMaskShaderStateIdentity == 0UL);

            return _nextMaskShaderStateIdentity;
        }

        static void EnsureMaskShaderState()
        {
            int textureValidity = NowUI.Internal.NowMaskShader.TextureValidityBits(_cachedMaskShaderState);
            if (!_maskShaderStateDirty && textureValidity == _cachedMaskTextureValidity)
                return;

            NowUI.Internal.NowMaskShaderState state = default;

            for (int i = 0; i < ambientMaskCount; ++i)
            {
                var ambient = GetAmbientMask(i);
                if (!ambient.analytic && !ambient.texture)
                    continue;

                Vector2 origin = Vector2.zero;
                Vector2 scale = Vector2.one;

                if (ambient.transform.active)
                {
                    origin = ambient.transform.transform.origin;
                    scale = ambient.transform.transform.scale;
                }

                var transform = new Vector4(origin.x, origin.y, scale.x, scale.y);

                if (ambient.analytic)
                {
                    var shape = ambient.shape;
                    state.Add(new NowUI.Internal.NowMaskShaderDescriptor(
                        shape.rect,
                        shape.shaderData,
                        new Vector4((float)shape.kind, shape.feather, shape.radius, 0f),
                        transform));
                    continue;
                }

                var textureMask = ambient.textureMask;
                state.AddTexture(new NowUI.Internal.NowTextureMaskShaderDescriptor(
                    textureMask.hasTexture ? textureMask.texture : null,
                    textureMask.bounds,
                    new Vector4(
                        (float)textureMask.channel,
                        textureMask.inverted ? 1f : 0f,
                        textureMask.isValid && textureMask.hasTexture ? 1f : 0f,
                        0f),
                    transform));
            }

            if (!state.isEmpty)
                state.SetIdentity(NextMaskShaderStateIdentity());

            _cachedMaskShaderState = state;
            _cachedMaskTextureValidity = NowUI.Internal.NowMaskShader.TextureValidityBits(state);
            _maskShaderStateDirty = false;
        }

        internal static ref readonly NowUI.Internal.NowMaskShaderState CurrentMaskShaderState()
        {
            if (_suppressShaderMaskCaptureDepth > 0)
                return ref _emptyMaskShaderState;

            EnsureMaskShaderState();
            return ref _cachedMaskShaderState;
        }

        internal static int currentAnalyticMaskCount
        {
            get => _ambientAnalyticMaskCount;
        }

        internal static int currentTextureMaskCount
        {
            get => _ambientTextureMaskCount;
        }

        /// <summary>
        /// Freezes shader-evaluated portions of the ambient mask stack into
        /// batch-owned data. Legacy rect entries already travel per vertex.
        /// </summary>
        internal static NowUI.Internal.NowMaskShaderState CaptureMaskShaderState()
        {
            if (_suppressShaderMaskCaptureDepth > 0)
                return default;

            EnsureMaskShaderState();
            return _cachedMaskShaderState;
        }
    }
}

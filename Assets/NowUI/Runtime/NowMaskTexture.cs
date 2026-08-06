using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>Selects the coverage channel sampled by <see cref="NowMaskTexture"/>.</summary>
    public enum NowMaskTextureChannel : byte
    {
        /// <summary>Use the texture alpha channel as mask coverage.</summary>
        Alpha = 0,

        /// <summary>Use the texture red channel as mask coverage.</summary>
        Red = 1
    }

    /// <summary>
    /// Immutable texture coverage for an ambient <see cref="Now.Mask(NowMaskTexture)"/>
    /// scope. The full texture maps to <see cref="bounds"/> in authored UI space;
    /// samples outside those bounds have zero coverage.
    /// </summary>
    public readonly struct NowMaskTexture : IEquatable<NowMaskTexture>
    {
        readonly Texture _texture;
        readonly NowRect _bounds;
        readonly NowMaskTextureChannel _channel;
        readonly bool _inverted;
        readonly bool _valid;

        /// <summary>
        /// Creates an alpha-coverage texture mask. A null texture represents zero
        /// coverage, which is useful for an asynchronously or lazily produced mask.
        /// </summary>
        public NowMaskTexture(Texture texture, NowRect bounds)
            : this(texture, bounds, NowMaskTextureChannel.Alpha, false)
        {
        }

        /// <summary>
        /// Creates a texture mask using the selected coverage channel. A null texture
        /// represents zero coverage and remains empty when inversion is requested.
        /// </summary>
        public NowMaskTexture(
            Texture texture,
            NowRect bounds,
            NowMaskTextureChannel channel,
            bool inverted = false)
        {
            bool valid = IsFinite(bounds.x) &&
                IsFinite(bounds.y) &&
                IsFinite(bounds.width) &&
                IsFinite(bounds.height) &&
                IsFinite(bounds.xMax) &&
                IsFinite(bounds.yMax) &&
                bounds.width >= 0f &&
                bounds.height >= 0f;

            _texture = texture;
            _bounds = valid ? bounds : default;
            _channel = channel == NowMaskTextureChannel.Red
                ? NowMaskTextureChannel.Red
                : NowMaskTextureChannel.Alpha;
            _inverted = inverted;
            _valid = valid;
        }

        /// <summary>The sampled texture, or null for constant zero coverage.</summary>
        public Texture texture => _texture;

        /// <summary>The authored UI rectangle to which the full texture is mapped.</summary>
        public NowRect bounds => _bounds;

        /// <summary>The texture channel used as coverage.</summary>
        public NowMaskTextureChannel channel => _channel;

        /// <summary>Whether coverage is inverted inside <see cref="bounds"/>.</summary>
        public bool inverted => _inverted;

        /// <summary>Whether this value has no possible covered interior.</summary>
        public bool isEmpty => !_valid || _bounds.isEmpty || !hasTexture;

        internal bool isValid => _valid;

        internal bool hasTexture => _texture != null;

        /// <summary>Creates an alpha-channel texture mask.</summary>
        public static NowMaskTexture Alpha(Texture texture, NowRect bounds)
        {
            return new NowMaskTexture(texture, bounds, NowMaskTextureChannel.Alpha);
        }

        /// <summary>Creates a red-channel texture mask, suitable for single-channel coverage textures.</summary>
        public static NowMaskTexture Red(Texture texture, NowRect bounds)
        {
            return new NowMaskTexture(texture, bounds, NowMaskTextureChannel.Red);
        }

        /// <summary>Creates a mask with zero coverage throughout <paramref name="bounds"/>.</summary>
        public static NowMaskTexture Empty(NowRect bounds)
        {
            return new NowMaskTexture(null, bounds, NowMaskTextureChannel.Red);
        }

        /// <summary>
        /// Returns this mask with coverage optionally inverted. Inversion applies only
        /// inside <see cref="bounds"/>; samples outside the rect and a missing or
        /// destroyed texture remain uncovered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NowMaskTexture SetInverted(bool inverted = true)
        {
            if (!_valid)
                return default;

            return new NowMaskTexture(_texture, _bounds, _channel, inverted);
        }

        internal bool Contains(Vector2 position)
        {
            if (!_valid || _bounds.isEmpty || !IsFinite(position.x) || !IsFinite(position.y))
                return false;

            if (!hasTexture)
                return false;

            return position.x >= _bounds.x && position.x <= _bounds.xMax &&
                   position.y >= _bounds.y && position.y <= _bounds.yMax;
        }

        public bool Equals(NowMaskTexture other)
        {
            return ReferenceEquals(_texture, other._texture) &&
                _bounds == other._bounds &&
                _channel == other._channel &&
                _inverted == other._inverted &&
                _valid == other._valid;
        }

        public override bool Equals(object obj)
        {
            return obj is NowMaskTexture other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ReferenceEquals(_texture, null) ? 0 : _texture.GetHashCode();
                hash = hash * 397 ^ _bounds.GetHashCode();
                hash = hash * 397 ^ (int)_channel;
                hash = hash * 397 ^ _inverted.GetHashCode();
                hash = hash * 397 ^ _valid.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NowMaskTexture left, NowMaskTexture right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NowMaskTexture left, NowMaskTexture right)
        {
            return !left.Equals(right);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI.Sdf
{
    /// <summary>
    /// Per-scene image atlases. Every image shape in a scene gets its baked
    /// distance field stamped into one field atlas and its sprite pixels into
    /// a color atlas with the same layout, so a scene can draw any number of
    /// images from unrelated textures through two samplers. The layout is
    /// rebuilt only when the set of images, their bakes, or their sources
    /// change; the per-image bakes themselves stay in the global field cache.
    /// </summary>
    sealed class NowSdfImageAtlas
    {
        public readonly struct Entry
        {
            public readonly NowSdfImageField field;
            /// <summary>Texel rect of the padded field inside the field atlas.</summary>
            public readonly Vector4 fieldRect;
            /// <summary>Texel rect of the sprite pixels inside the color atlas.</summary>
            public readonly Vector4 colorRect;

            public Entry(NowSdfImageField field, Vector4 fieldRect, Vector4 colorRect)
            {
                this.field = field;
                this.fieldRect = fieldRect;
                this.colorRect = colorRect;
            }
        }

        struct Placement
        {
            public NowSdfImageField field;
            public int x;
            public int y;
            public int width;
            public int height;
        }

        /// <summary>Transparent texels kept between entries so bilinear filtering never mixes neighbors.</summary>
        public const int Gutter = 2;

        readonly List<NowSdfImageField> _pending = new List<NowSdfImageField>(4);
        readonly List<Placement> _placements = new List<Placement>(4);
        readonly List<Entry> _entries = new List<Entry>(4);
        readonly Dictionary<NowSdfImageField, int> _lookup = new Dictionary<NowSdfImageField, int>(4);

        RenderTexture _fieldAtlas;
        RenderTexture _colorAtlas;
        ulong _contentHash;
        bool _hasContent;
        int _version;

        public RenderTexture fieldTexture => _fieldAtlas;

        public RenderTexture colorTexture => _colorAtlas;

        public int version => _version;

        public int entryCount => _entries.Count;

        public Vector4 atlasSize => new Vector4(
            _fieldAtlas != null ? _fieldAtlas.width : 1,
            _fieldAtlas != null ? _fieldAtlas.height : 1,
            _colorAtlas != null ? _colorAtlas.width : 1,
            _colorAtlas != null ? _colorAtlas.height : 1);

        public bool isValid =>
            !_hasContent ||
            _entries.Count == 0 ||
            (_fieldAtlas != null && _fieldAtlas.IsCreated() &&
                _colorAtlas != null && _colorAtlas.IsCreated());

        public bool TryGetEntry(NowSdfImageField field, out Entry entry)
        {
            if (field != null && _lookup.TryGetValue(field, out int index))
            {
                entry = _entries[index];
                return true;
            }

            entry = default;
            return false;
        }

        public void Begin()
        {
            _pending.Clear();
        }

        public void Request(NowSdfImageField field)
        {
            if (field == null || !field.isValid || field.key.texture == null)
                return;

            for (int i = 0; i < _pending.Count; ++i)
            {
                if (ReferenceEquals(_pending[i], field))
                    return;
            }

            _pending.Add(field);
        }

        /// <summary>
        /// Packs and stamps the requested fields unless the atlas already holds
        /// exactly that content. Returns true when the atlas was rebuilt.
        /// </summary>
        public bool Build()
        {
            ulong hash = ContentHash();

            if (_hasContent && hash == _contentHash && isValid)
                return false;

            _entries.Clear();
            _lookup.Clear();
            _placements.Clear();
            _contentHash = hash;
            _hasContent = true;
            unchecked
            {
                ++_version;
            }

            if (_pending.Count == 0)
            {
                Release();
                _hasContent = true;
                _contentHash = hash;
                return true;
            }

            int maximum = Mathf.Min(4096, Mathf.Max(1, SystemInfo.maxTextureSize));
            Pack(maximum, out int width, out int height);

            if (_placements.Count == 0)
            {
                Release();
                _hasContent = true;
                _contentHash = hash;
                return true;
            }

            EnsureTextures(width, height);

            if (_fieldAtlas == null || _colorAtlas == null)
            {
                _placements.Clear();
                return true;
            }

            NowSdfImageFields.ClearTarget(_fieldAtlas, new Color(NowSdfImageFields.MaxDistance, 0f, 0f, 1f));
            NowSdfImageFields.ClearTarget(_colorAtlas, Color.clear);

            for (int i = 0; i < _placements.Count; ++i)
            {
                Placement placement = _placements[i];
                NowSdfImageField field = placement.field;
                Texture source = field.key.texture;
                RectInt texelRect = field.key.texelRect;
                int padding = field.key.padding;
                var fieldRect = new Vector4(placement.x, placement.y, placement.width, placement.height);
                var colorRect = new Vector4(
                    placement.x + padding,
                    placement.y + padding,
                    texelRect.width,
                    texelRect.height);
                float sourceWidth = Mathf.Max(1, source.width);
                float sourceHeight = Mathf.Max(1, source.height);

                NowSdfImageFields.Stamp(
                    field.texture,
                    new Vector4(0f, 0f, 1f, 1f),
                    _fieldAtlas,
                    fieldRect);
                NowSdfImageFields.Stamp(
                    source,
                    new Vector4(
                        texelRect.x / sourceWidth,
                        texelRect.y / sourceHeight,
                        texelRect.width / sourceWidth,
                        texelRect.height / sourceHeight),
                    _colorAtlas,
                    colorRect);

                _lookup[field] = _entries.Count;
                _entries.Add(new Entry(field, fieldRect, colorRect));
            }

            _placements.Clear();
            return true;
        }

        public void Release()
        {
            ReleaseTexture(ref _fieldAtlas);
            ReleaseTexture(ref _colorAtlas);
            _entries.Clear();
            _lookup.Clear();
            _placements.Clear();
            _hasContent = false;
        }

        ulong ContentHash()
        {
            ulong hash = 1469598103934665603UL;

            for (int i = 0; i < _pending.Count; ++i)
            {
                NowSdfImageField field = _pending[i];
                hash = Hash(hash, RuntimeHelpers.GetHashCode(field));
                hash = Hash(hash, field.version);
                hash = Hash(hash, field.key.texelRect.width);
                hash = Hash(hash, field.key.texelRect.height);
                hash = Hash(hash, field.key.padding);
            }

            return hash;
        }

        static ulong Hash(ulong hash, int value)
        {
            unchecked
            {
                return (hash ^ (uint)value) * 0x100000001B3UL;
            }
        }

        /// <summary>
        /// Shelf packing, tallest first, into the narrowest power-of-two width
        /// whose resulting height is no taller than the width (or the maximum).
        /// Entries that cannot fit even the maximum atlas are left out.
        /// </summary>
        void Pack(int maximum, out int width, out int height)
        {
            _pending.Sort(CompareHeightDescending);

            int widest = 1;
            for (int i = 0; i < _pending.Count; ++i)
                widest = Mathf.Max(widest, _pending[i].texture.width + Gutter * 2);

            width = Mathf.Min(maximum, Mathf.NextPowerOfTwo(widest));
            height = 0;

            while (true)
            {
                height = Shelve(width, maximum);

                if (height <= width || width >= maximum)
                    break;

                width = Mathf.Min(maximum, width * 2);
            }

            height = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(height, 1)), 1, maximum);
        }

        int Shelve(int width, int maximum)
        {
            _placements.Clear();
            int shelfY = Gutter;
            int shelfHeight = 0;
            int cursorX = Gutter;

            for (int i = 0; i < _pending.Count; ++i)
            {
                NowSdfImageField field = _pending[i];
                int entryWidth = field.texture.width;
                int entryHeight = field.texture.height;

                if (cursorX + entryWidth + Gutter > width)
                {
                    shelfY += shelfHeight + Gutter;
                    shelfHeight = 0;
                    cursorX = Gutter;
                }

                if (entryWidth + Gutter * 2 > width || shelfY + entryHeight + Gutter > maximum)
                    continue;

                _placements.Add(new Placement
                {
                    field = field,
                    x = cursorX,
                    y = shelfY,
                    width = entryWidth,
                    height = entryHeight
                });
                cursorX += entryWidth + Gutter;
                shelfHeight = Mathf.Max(shelfHeight, entryHeight);
            }

            return shelfY + shelfHeight + Gutter;
        }

        static int CompareHeightDescending(NowSdfImageField a, NowSdfImageField b)
        {
            int result = b.texture.height.CompareTo(a.texture.height);
            return result != 0 ? result : b.texture.width.CompareTo(a.texture.width);
        }

        void EnsureTextures(int width, int height)
        {
            if (_fieldAtlas != null && (_fieldAtlas.width != width || _fieldAtlas.height != height))
                ReleaseTexture(ref _fieldAtlas);

            if (_colorAtlas != null && (_colorAtlas.width != width || _colorAtlas.height != height))
                ReleaseTexture(ref _colorAtlas);

            _fieldAtlas ??= NowSdfImageFields.CreateTarget(
                width,
                height,
                NowSdfImageFields.FieldFormat(),
                RenderTextureReadWrite.Linear,
                "Now SDF Image Field Atlas");
            _colorAtlas ??= NowSdfImageFields.CreateTarget(
                width,
                height,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB,
                "Now SDF Image Color Atlas");

            if (_fieldAtlas != null && !_fieldAtlas.IsCreated() && !_fieldAtlas.Create())
                ReleaseTexture(ref _fieldAtlas);

            if (_colorAtlas != null && !_colorAtlas.IsCreated() && !_colorAtlas.Create())
                ReleaseTexture(ref _colorAtlas);
        }

        static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            NowSdfImageFields.DestroyTarget(texture);
            texture = null;
        }
    }
}

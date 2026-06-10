using UnityEngine;

namespace NowUIInternal
{
    public enum NowMeshKind
    {
        Rectangle,
        Text
    }

    internal struct NowUIMeshBatch
    {
        public readonly Material material;

        public readonly NowMeshKind kind;

        public NowUIMeshBatch(Material material, NowMeshKind kind)
        {
            this.material = material;
            this.kind = kind;
        }
    }

    internal enum NowUIMeshLayout
    {
        Render,
        Canvas
    }

    public struct StaticList<T>
    {
        public int count;

        public T[] array;

        public StaticList(int count)
        {
            this.count = 0;
            array = new T[count];
        }

        public void Clear()
        {
            count = 0;
        }

        public void EnsureCapacity(int additionalCount)
        {
            int requiredCapacity = count + additionalCount;
            int currentCapacity = array?.Length ?? 0;

            if (requiredCapacity <= currentCapacity)
                return;

            int newCapacity = Mathf.Max(requiredCapacity, currentCapacity > 0 ? currentCapacity * 2 : 4);
            System.Array.Resize(ref array, newCapacity);
        }
    }

    public struct NowRectVertex
    {
        public Vector4 mask;

        public Vector4 position;

        public Vector4 radius;

        public Vector4 color;

        public Vector4 outlineColor;

        public Vector4 uvwh;

        public bool IsOutsideMask(Vector4 rect)
        {
            return rect.x + rect.z < mask.x ||
                rect.x >= mask.x + mask.z ||
                -rect.y < mask.y ||
                -rect.y - rect.w >= mask.y + mask.w;
        }
    }

    public class NowMesh
    {
        const int INITIAL_RECT_CAPACITY = 64;

        const int VERTICES_PER_RECT = 4;

        const int INDICES_PER_RECT = 6;

        public const int INITIAL_VERTEX_CAPACITY = INITIAL_RECT_CAPACITY * VERTICES_PER_RECT;

        public const int INITIAL_INDEX_CAPACITY = INITIAL_RECT_CAPACITY * INDICES_PER_RECT;

        public Mesh unityMesh {get; private set;}

        public bool hasVertices => _verts.count > 0;

        public int vertexCount => _verts.count;

        StaticList<Vector4> _rawuv;

        StaticList<Vector4> _mask;

        StaticList<Vector4> _extra;

        StaticList<Vector4> _outlineColor;

        StaticList<Vector4> _color;

        StaticList<Vector4> _rect;

        StaticList<Vector4> _radius;

        StaticList<Vector3> _verts;

        StaticList<Vector2> _uvs;

        StaticList<int> _tris;

        public Material material;

        public NowMeshKind kind;

        public NowMesh(Material mat, NowMeshKind kind)
        {
            material = mat;
            this.kind = kind;

            _radius = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _rect = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _verts = new StaticList<Vector3>(INITIAL_VERTEX_CAPACITY);
            _uvs = new StaticList<Vector2>(INITIAL_VERTEX_CAPACITY);
            _color = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _outlineColor = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _extra = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _tris = new StaticList<int>(INITIAL_INDEX_CAPACITY);
            _mask = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _rawuv = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
        }

        public void SetMaterial(Material material, NowMeshKind kind)
        {
            this.material = material;
            this.kind = kind;
            ClearVertices();
        }

        static readonly Vector2 _uv0 = new Vector2(0, 0);

        static readonly Vector2 _uv1 = new Vector2(0, 1);

        static readonly Vector2 _uv2 = new Vector2(1, 1);

        static readonly Vector2 _uv3 = new Vector2(1, 0);

        Vector3 _a, _b, _c, _d;

        void EnsureRectCapacity()
        {
            _mask.EnsureCapacity(VERTICES_PER_RECT);
            _rect.EnsureCapacity(VERTICES_PER_RECT);
            _radius.EnsureCapacity(VERTICES_PER_RECT);
            _color.EnsureCapacity(VERTICES_PER_RECT);
            _outlineColor.EnsureCapacity(VERTICES_PER_RECT);
            _extra.EnsureCapacity(VERTICES_PER_RECT);
            _verts.EnsureCapacity(VERTICES_PER_RECT);
            _uvs.EnsureCapacity(VERTICES_PER_RECT);
            _rawuv.EnsureCapacity(VERTICES_PER_RECT);
            _tris.EnsureCapacity(INDICES_PER_RECT);
        }

        public void AddRect(NowRectVertex vertexData, float extraX, float extraY)
        {
            if (vertexData.IsOutsideMask(vertexData.position)) return;

            EnsureRectCapacity();

            int indexOffset = _verts.count;

            Vector4 extra = default;
            extra.x = extraX;
            extra.y = extraY;

            var maskarr = _mask.array;
            var maskcount = _mask.count;

            var mask = vertexData.mask;

            maskarr[maskcount] = mask;
            maskarr[maskcount + 1] = mask;
            maskarr[maskcount + 2] = mask;
            maskarr[maskcount + 3] = mask;

            _mask.count += 4;

            var rarray = _rect.array;
            var rcount = _rect.count;

            var position = vertexData.position;

            rarray[rcount] = position;
            rarray[rcount + 1] = position;
            rarray[rcount + 2] = position;
            rarray[rcount + 3] = position;

            _rect.count += 4;

            var rrad= _radius.array;
            var rrcount = _radius.count;

            rrad[rrcount] = vertexData.radius;
            rrad[rrcount + 1] = vertexData.radius;
            rrad[rrcount + 2] = vertexData.radius;
            rrad[rrcount + 3] = vertexData.radius;

            _radius.count += 4;

            var rcol = _color.array;
            var ccount = _color.count;

            rcol[ccount] = vertexData.color;
            rcol[ccount + 1] = vertexData.color;
            rcol[ccount + 2] = vertexData.color;
            rcol[ccount + 3] = vertexData.color;

            _color.count += 4;

            var rout = _outlineColor.array;
            var ocount = _outlineColor.count;

            rout[ocount] = vertexData.outlineColor;
            rout[ocount + 1] = vertexData.outlineColor;
            rout[ocount + 2] = vertexData.outlineColor;
            rout[ocount + 3] = vertexData.outlineColor;

            _outlineColor.count += 4;

            var rextra = _extra.array;
            var extraC = _extra.count;

            rextra[extraC] = extra;
            rextra[extraC + 1] = extra;
            rextra[extraC + 2] = extra;
            rextra[extraC + 3] = extra;

            _extra.count += 4;

            _a.x = position.x;
            _a.y = position.y;
            _a.z = 0;

            _b.x = _a.x;
            _b.y = _a.y + position.w;
            _b.z = 0;

            _c.x = _a.x + position.z;
            _c.y = _b.y;
            _c.z = 0;

            _d.x = _c.x;
            _d.y = _a.y;
            _d.z = 0;

            var varr = _verts.array;
            var vcount = _verts.count;

            varr[vcount++] = _a;
            varr[vcount++] = _b;
            varr[vcount++] = _c;
            varr[vcount++] = _d;

            _verts.count = vcount;

            var ruvs = _uvs.array;
            var ruvsCount = _uvs.count;

            var uv0 = _uv0;
            var uv1 = _uv1;
            var uv2 = _uv2;
            var uv3 = _uv3;

            var uvwh = vertexData.uvwh;

            uv0.x = uvwh.x + uv0.x * uvwh.z;
            uv0.y = uvwh.y + uv0.y * uvwh.w;

            uv1.x = uvwh.x + uv1.x * uvwh.z;
            uv1.y = uvwh.y + uv1.y * uvwh.w;

            uv2.x = uvwh.x + uv2.x * uvwh.z;
            uv2.y = uvwh.y + uv2.y * uvwh.w;

            uv3.x = uvwh.x + uv3.x * uvwh.z;
            uv3.y = uvwh.y + uv3.y * uvwh.w;

            ruvs[ruvsCount] = uv0;
            ruvs[ruvsCount + 1] = uv1;
            ruvs[ruvsCount + 2] = uv2;
            ruvs[ruvsCount + 3] = uv3;

            var rawuvs = _rawuv.array;
            var rawuvsCount = _rawuv.count;

            rawuvs[rawuvsCount] = _uv0;
            rawuvs[rawuvsCount + 1] = _uv1;
            rawuvs[rawuvsCount + 2] = _uv2;
            rawuvs[rawuvsCount + 3] = _uv3;

            _rawuv.count += 4;
            _uvs.count += 4;

            int triCount = _tris.count;
            var triArr = _tris.array;

            triArr[triCount + 0] = indexOffset + 0;
            triArr[triCount + 1] = indexOffset + 1;
            triArr[triCount + 2] = indexOffset + 2;
            triArr[triCount + 3] = indexOffset + 0;
            triArr[triCount + 4] = indexOffset + 2;
            triArr[triCount + 5] = indexOffset + 3;

            _tris.count += 6;
        }

        public void ClearVertices()
        {
            _radius.Clear();
            _rect.Clear();
            _verts.Clear();
            _uvs.Clear();
            _tris.Clear();
            _color.Clear();
            _outlineColor.Clear();
            _extra.Clear();
            _mask.Clear();
            _rawuv.Clear();
        }

        public void AppendVertices(
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Vector2> uvs,
            System.Collections.Generic.List<Vector4> rects,
            System.Collections.Generic.List<Vector4> radii,
            System.Collections.Generic.List<Vector4> colors,
            System.Collections.Generic.List<Vector4> outlineColors,
            System.Collections.Generic.List<Vector4> extras,
            System.Collections.Generic.List<Vector4> masks,
            System.Collections.Generic.List<Vector4> rawUvs,
            Vector2 positionOffset)
        {
            for (int i = 0; i < _verts.count; ++i)
            {
                var vertex = _verts.array[i];
                vertex.x += positionOffset.x;
                vertex.y += positionOffset.y;
                vertices.Add(vertex);
            }

            for (int i = 0; i < _uvs.count; ++i)
                uvs.Add(_uvs.array[i]);

            for (int i = 0; i < _rect.count; ++i)
                rects.Add(_rect.array[i]);

            for (int i = 0; i < _radius.count; ++i)
                radii.Add(_radius.array[i]);

            for (int i = 0; i < _color.count; ++i)
                colors.Add(_color.array[i]);

            for (int i = 0; i < _outlineColor.count; ++i)
                outlineColors.Add(_outlineColor.array[i]);

            for (int i = 0; i < _extra.count; ++i)
                extras.Add(_extra.array[i]);

            for (int i = 0; i < _mask.count; ++i)
                masks.Add(_mask.array[i]);

            for (int i = 0; i < _rawuv.count; ++i)
                rawUvs.Add(_rawuv.array[i]);
        }

        public void AppendUGUIVertices(
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Vector4> uv0,
            System.Collections.Generic.List<Vector4> rects,
            System.Collections.Generic.List<Vector4> masks,
            System.Collections.Generic.List<Vector4> extras,
            System.Collections.Generic.List<Color> colors,
            System.Collections.Generic.List<Vector3> normals,
            System.Collections.Generic.List<Vector4> tangents,
            Vector2 positionOffset)
        {
            bool isText = kind == NowMeshKind.Text;

            for (int i = 0; i < _verts.count; ++i)
            {
                var vertex = _verts.array[i];
                var radius = _radius.array[i];
                var rawUv = _rawuv.array[i];
                var uv = _uvs.array[i];

                vertex.x += positionOffset.x;
                vertex.y += positionOffset.y;

                vertices.Add(vertex);
                uv0.Add(isText
                    ? new Vector4(uv.x, uv.y, rawUv.x, rawUv.y)
                    : new Vector4(uv.x, uv.y, radius.w, 0));
                rects.Add(_rect.array[i]);
                masks.Add(_mask.array[i]);
                extras.Add(_extra.array[i]);
                var color = _color.array[i];
                colors.Add(new Color(color.x, color.y, color.z, color.w));
                normals.Add(new Vector3(radius.x, radius.y, radius.z));
                tangents.Add(_outlineColor.array[i]);
            }
        }

        public void AppendTriangles(System.Collections.Generic.List<int> triangles, int vertexOffset)
        {
            for (int i = 0; i < _tris.count; ++i)
                triangles.Add(_tris.array[i] + vertexOffset);
        }

        public void UploadMesh()
        {
            if (!unityMesh)
            {
                unityMesh = new Mesh
                {
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };
                unityMesh.MarkDynamic();
            }

            unityMesh.Clear(true);

            unityMesh.SetVertices(_verts.array, 0, _verts.count);
            unityMesh.SetUVs(0, _uvs.array, 0, _uvs.count);
            unityMesh.SetUVs(1, _rect.array, 0, _rect.count);
            unityMesh.SetUVs(2, _radius.array, 0, _radius.count);
            unityMesh.SetUVs(3, _color.array, 0, _color.count);
            unityMesh.SetUVs(4, _outlineColor.array, 0, _outlineColor.count);
            unityMesh.SetUVs(5, _extra.array, 0, _extra.count);
            unityMesh.SetUVs(6, _mask.array, 0, _mask.count);
            unityMesh.SetUVs(7, _rawuv.array, 0, _rawuv.count);
            unityMesh.SetTriangles(_tris.array, 0, _tris.count, 0);
        }
    }
}

using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    internal static class NowEffectsMesh
    {
        static readonly List<Vector3> _vertices = new List<Vector3>(256);
        static readonly List<Vector2> _uv0 = new List<Vector2>(256);
        static readonly List<Vector4> _rect = new List<Vector4>(256);
        static readonly List<Vector4> _radius = new List<Vector4>(256);
        static readonly List<Vector4> _color = new List<Vector4>(256);
        static readonly List<Vector4> _outline = new List<Vector4>(256);
        static readonly List<Vector4> _extra = new List<Vector4>(256);
        static readonly List<Vector4> _mask = new List<Vector4>(256);
        static readonly List<Vector4> _rawUv = new List<Vector4>(256);
        static readonly List<int> _triangles = new List<int>(512);
        static int[] _indexMap = new int[256];
        static int[] _grid = new int[256];

        /// <summary>
        /// Union of the per-batch bounds recorded at capture time, avoiding a full
        /// CPU readback of the captured mesh. Every NowMesh append path encapsulates
        /// its emitted vertices, so the union always contains the geometry; for
        /// tessellated fills (AddGeometry) it is a slightly padded superset.
        /// </summary>
        internal static bool TryGetDrawListBounds(NowDrawList drawList, out NowRect bounds)
        {
            bounds = default;

            if (drawList == null || !drawList.hasGeometry)
                return false;

            var batches = drawList.batches;
            bool hasBounds = false;

            for (int i = 0; i < batches.Count; ++i)
            {
                var batchBounds = batches[i].bounds;

                if (batchBounds.isEmpty)
                    continue;

                bounds = hasBounds ? bounds.Union(batchBounds) : batchBounds;
                hasBounds = true;
            }

            return hasBounds;
        }

        internal static void RenderDrawListToTexture(
            NowDrawList drawList,
            NowRect sourceRect,
            RenderTexture target,
            CommandBuffer commandBuffer)
        {
            if (drawList == null || target == null || commandBuffer == null || !drawList.hasGeometry)
                return;

            using var profile = NowProfiler.EffectsRenderTexture.Auto();
            commandBuffer.Clear();
            commandBuffer.SetRenderTarget(target);
            commandBuffer.ClearRenderTarget(true, true, Color.clear);
            commandBuffer.SetViewProjectionMatrices(
                Matrix4x4.identity,
                Matrix4x4.Ortho(0f, sourceRect.width, -sourceRect.height, 0f, -1f, 100f));

            var matrix = Matrix4x4.Translate(new Vector3(-sourceRect.x, sourceRect.y, 0f));
            var batches = drawList.batches;
            int subMeshCount = Mathf.Min(batches.Count, drawList.mesh.subMeshCount);

            for (int i = 0; i < subMeshCount; ++i)
            {
                var batch = batches[i];
                if (batch.material != null)
                    commandBuffer.DrawMesh(drawList.mesh, matrix, batch.material, i, 0);
            }

            Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        internal static void DrawCapturedDrawList<TDeformer>(
            NowDrawList drawList,
            TDeformer deformer,
            NowSubdivision subdivision,
            bool subdivideText,
            bool hasSourceRect,
            NowRect sourceRect,
            int effectId,
            float time)
            where TDeformer : struct, INowVertexDeformer
        {
            if (drawList == null || !drawList.hasGeometry)
                return;

            using var profile = NowProfiler.EffectsDeform.Auto();
            var mesh = drawList.mesh;
            ReadMesh(mesh);

            if (!hasSourceRect && !TryGetBounds(_vertices, out sourceRect))
                return;

            if (sourceRect.isEmpty)
                return;

            var context = new NowEffectContext(effectId, sourceRect, time);
            var batches = drawList.batches;
            int subMeshCount = Mathf.Min(batches.Count, mesh.subMeshCount);

            for (int subMesh = 0; subMesh < subMeshCount; ++subMesh)
            {
                var batch = batches[subMesh];
                if (batch.material == null)
                    continue;

                _triangles.Clear();
                mesh.GetTriangles(_triangles, subMesh);
                if (_triangles.Count == 0)
                    continue;

                var targetMesh = UseEffectMaterial(batch.material, batch.kind);
                if (targetMesh == null)
                    continue;

                if (subdivision.mode == NowSubdivision.SubdivisionMode.None ||
                    !ShouldSubdivideBatch(batch.kind, subdivideText))
                {
                    CopyTriangles(targetMesh, deformer, context);
                    continue;
                }

                CopyTrianglesWithSubdivision(targetMesh, deformer, context, subdivision);
            }
        }

        internal static NowRect PixelSnapOutward(NowRect rect)
        {
            float scale = Mathf.Max(Now.uiScale, 0.0001f);
            float xMin = Mathf.Floor(rect.x * scale) / scale;
            float yMin = Mathf.Floor(rect.y * scale) / scale;
            float xMax = Mathf.Ceil(rect.xMax * scale) / scale;
            float yMax = Mathf.Ceil(rect.yMax * scale) / scale;
            float minSize = 1f / scale;

            return new NowRect(
                xMin,
                yMin,
                Mathf.Max(minSize, xMax - xMin),
                Mathf.Max(minSize, yMax - yMin));
        }

        static void ReadMesh(Mesh mesh)
        {
            _vertices.Clear();
            _uv0.Clear();
            _rect.Clear();
            _radius.Clear();
            _color.Clear();
            _outline.Clear();
            _extra.Clear();
            _mask.Clear();
            _rawUv.Clear();

            if (mesh == null)
                return;

            mesh.GetVertices(_vertices);
            mesh.GetUVs(0, _uv0);
            mesh.GetUVs(1, _rect);
            mesh.GetUVs(2, _radius);
            mesh.GetUVs(3, _color);
            mesh.GetUVs(4, _outline);
            mesh.GetUVs(5, _extra);
            mesh.GetUVs(6, _mask);
            mesh.GetUVs(7, _rawUv);
        }

        static NowMesh UseEffectMaterial(Material material, NowMeshKind kind)
        {
            if (material == null)
                return null;

            return Now.UseEffectMaterial(material, kind);
        }

        static bool ShouldSubdivideBatch(NowMeshKind kind, bool subdivideText)
        {
            return kind == NowMeshKind.Rectangle ||
                kind == NowMeshKind.TexturedRectangle ||
                kind == NowMeshKind.CustomRectangle ||
                kind == NowMeshKind.Ripple ||
                kind == NowMeshKind.Text && subdivideText;
        }

        static bool TryGetBounds(List<Vector3> vertices, out NowRect bounds)
        {
            bounds = default;

            if (vertices == null || vertices.Count == 0)
                return false;

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < vertices.Count; ++i)
            {
                var vertex = vertices[i];
                float x = vertex.x;
                float y = -vertex.y;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            if (float.IsInfinity(minX) || maxX <= minX || maxY <= minY)
                return false;

            bounds = new NowRect(minX, minY, maxX - minX, maxY - minY);
            return true;
        }

        static void CopyTriangles<TDeformer>(
            NowMesh targetMesh,
            TDeformer deformer,
            NowEffectContext context)
            where TDeformer : struct, INowVertexDeformer
        {
            targetMesh.EnsureRawCapacity(_vertices.Count, _triangles.Count);
            PrepareIndexMap(_vertices.Count);

            for (int i = 0; i + 2 < _triangles.Count; i += 3)
            {
                int a = CopyMappedVertex(targetMesh, _triangles[i], deformer, context);
                int b = CopyMappedVertex(targetMesh, _triangles[i + 1], deformer, context);
                int c = CopyMappedVertex(targetMesh, _triangles[i + 2], deformer, context);
                targetMesh.AddRawTriangleUnchecked(a, b, c);
            }
        }

        static void CopyTrianglesWithSubdivision<TDeformer>(
            NowMesh targetMesh,
            TDeformer deformer,
            NowEffectContext context,
            NowSubdivision subdivision)
            where TDeformer : struct, INowVertexDeformer
        {
            EstimateSubdivisionCapacity(subdivision, out int vertexCount, out int indexCount);
            targetMesh.EnsureRawCapacity(vertexCount, indexCount);
            PrepareIndexMap(_vertices.Count);

            for (int i = 0; i < _triangles.Count;)
            {
                if (IsQuadPattern(i, out int first))
                {
                    ResolveSubdivision(first, subdivision, out int divisionsX, out int divisionsY);

                    if (divisionsX > 1 || divisionsY > 1)
                    {
                        AddSubdividedQuad(targetMesh, first, divisionsX, divisionsY, deformer, context);
                        i += 6;
                        continue;
                    }
                }

                if (i + 2 >= _triangles.Count)
                    break;

                int a = CopyMappedVertex(targetMesh, _triangles[i], deformer, context);
                int b = CopyMappedVertex(targetMesh, _triangles[i + 1], deformer, context);
                int c = CopyMappedVertex(targetMesh, _triangles[i + 2], deformer, context);
                targetMesh.AddRawTriangleUnchecked(a, b, c);
                i += 3;
            }
        }

        static void EstimateSubdivisionCapacity(
            NowSubdivision subdivision,
            out int vertexCount,
            out int indexCount)
        {
            vertexCount = 0;
            indexCount = 0;

            for (int i = 0; i < _triangles.Count;)
            {
                if (IsQuadPattern(i, out int first))
                {
                    ResolveSubdivision(first, subdivision, out int divisionsX, out int divisionsY);

                    if (divisionsX > 1 || divisionsY > 1)
                    {
                        vertexCount += (divisionsX + 1) * (divisionsY + 1);
                        indexCount += divisionsX * divisionsY * 6;
                        i += 6;
                        continue;
                    }
                }

                if (i + 2 >= _triangles.Count)
                    break;

                vertexCount += 3;
                indexCount += 3;
                i += 3;
            }
        }

        static bool IsQuadPattern(int triangleIndex, out int first)
        {
            first = 0;

            if (triangleIndex + 5 >= _triangles.Count)
                return false;

            int a = _triangles[triangleIndex];
            if (a < 0 || a + 3 >= _vertices.Count)
                return false;

            bool match =
                _triangles[triangleIndex + 1] == a + 1 &&
                _triangles[triangleIndex + 2] == a + 2 &&
                _triangles[triangleIndex + 3] == a &&
                _triangles[triangleIndex + 4] == a + 2 &&
                _triangles[triangleIndex + 5] == a + 3;

            if (!match)
                return false;

            first = a;
            return true;
        }

        static void ResolveSubdivision(int first, NowSubdivision subdivision, out int divisionsX, out int divisionsY)
        {
            divisionsX = 1;
            divisionsY = 1;

            if (subdivision.mode == NowSubdivision.SubdivisionMode.Fixed)
            {
                divisionsX = divisionsY = Mathf.Max(1, subdivision.divisions);
                return;
            }

            if (subdivision.mode != NowSubdivision.SubdivisionMode.MaxCellSize)
                return;

            var bounds = QuadBounds(first);
            float size = Mathf.Max(1f, subdivision.maxCellSize);
            divisionsX = Mathf.Max(1, Mathf.CeilToInt(bounds.width / size));
            divisionsY = Mathf.Max(1, Mathf.CeilToInt(bounds.height / size));
        }

        static NowRect QuadBounds(int first)
        {
            var a = UiPosition(first);
            var b = UiPosition(first + 1);
            var c = UiPosition(first + 2);
            var d = UiPosition(first + 3);
            float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
            return new NowRect(minX, minY, maxX - minX, maxY - minY);
        }

        static void AddSubdividedQuad<TDeformer>(
            NowMesh targetMesh,
            int first,
            int divisionsX,
            int divisionsY,
            TDeformer deformer,
            NowEffectContext context)
            where TDeformer : struct, INowVertexDeformer
        {
            int gridCount = (divisionsX + 1) * (divisionsY + 1);
            EnsureGridCapacity(gridCount);

            for (int y = 0; y <= divisionsY; ++y)
            {
                float v = y / (float)divisionsY;

                for (int x = 0; x <= divisionsX; ++x)
                {
                    float u = x / (float)divisionsX;
                    int gridIndex = y * (divisionsX + 1) + x;
                    _grid[gridIndex] = AddInterpolatedQuadVertex(
                        targetMesh,
                        first,
                        u,
                        v,
                        deformer,
                        context);
                }
            }

            for (int y = 0; y < divisionsY; ++y)
            {
                for (int x = 0; x < divisionsX; ++x)
                {
                    int topLeft = _grid[y * (divisionsX + 1) + x];
                    int topRight = _grid[y * (divisionsX + 1) + x + 1];
                    int bottomLeft = _grid[(y + 1) * (divisionsX + 1) + x];
                    int bottomRight = _grid[(y + 1) * (divisionsX + 1) + x + 1];
                    targetMesh.AddRawTriangleUnchecked(bottomLeft, topLeft, topRight);
                    targetMesh.AddRawTriangleUnchecked(bottomLeft, topRight, bottomRight);
                }
            }
        }

        static int AddInterpolatedQuadVertex<TDeformer>(
            NowMesh targetMesh,
            int first,
            float u,
            float v,
            TDeformer deformer,
            NowEffectContext context)
            where TDeformer : struct, INowVertexDeformer
        {
            Vector2 position = Bilinear2(
                UiPosition(first + 1),
                UiPosition(first + 2),
                UiPosition(first),
                UiPosition(first + 3),
                u,
                v);

            Vector2 uv = Bilinear2(
                GetUv0(first + 1),
                GetUv0(first + 2),
                GetUv0(first),
                GetUv0(first + 3),
                u,
                v);

            Vector4 rect = Bilinear4(GetRect(first + 1), GetRect(first + 2), GetRect(first), GetRect(first + 3), u, v);
            Vector4 radius = Bilinear4(GetRadius(first + 1), GetRadius(first + 2), GetRadius(first), GetRadius(first + 3), u, v);
            Vector4 color = Bilinear4(GetColor(first + 1), GetColor(first + 2), GetColor(first), GetColor(first + 3), u, v);
            Vector4 outline = Bilinear4(GetOutline(first + 1), GetOutline(first + 2), GetOutline(first), GetOutline(first + 3), u, v);
            Vector4 extra = Bilinear4(GetExtra(first + 1), GetExtra(first + 2), GetExtra(first), GetExtra(first + 3), u, v);
            Vector4 mask = Bilinear4(GetMask(first + 1), GetMask(first + 2), GetMask(first), GetMask(first + 3), u, v);
            Vector4 rawUv = Bilinear4(GetRawUv(first + 1), GetRawUv(first + 2), GetRawUv(first), GetRawUv(first + 3), u, v);

            return AddVertex(
                targetMesh,
                position,
                uv,
                rect,
                radius,
                color,
                outline,
                extra,
                mask,
                rawUv,
                first,
                deformer,
                context);
        }

        static int CopyMappedVertex<TDeformer>(
            NowMesh targetMesh,
            int source,
            TDeformer deformer,
            NowEffectContext context)
            where TDeformer : struct, INowVertexDeformer
        {
            if (source < 0 || source >= _vertices.Count)
                return 0;

            int mapped = _indexMap[source];
            if (mapped >= 0)
                return mapped;

            mapped = AddVertex(
                targetMesh,
                UiPosition(source),
                GetUv0(source),
                GetRect(source),
                GetRadius(source),
                GetColor(source),
                GetOutline(source),
                GetExtra(source),
                GetMask(source),
                GetRawUv(source),
                source,
                deformer,
                context);
            _indexMap[source] = mapped;
            return mapped;
        }

        static int AddVertex<TDeformer>(
            NowMesh targetMesh,
            Vector2 position,
            Vector2 uv,
            Vector4 rect,
            Vector4 radius,
            Vector4 color,
            Vector4 outline,
            Vector4 extra,
            Vector4 mask,
            Vector4 rawUv,
            int index,
            TDeformer deformer,
            NowEffectContext context)
            where TDeformer : struct, INowVertexDeformer
        {
            Vector2 normalized = Normalized(position, context.sourceRect);
            Vector2 deformed = deformer.Deform(new NowEffectVertex(position, normalized, uv, index), context);
            return targetMesh.AddRawVertexUnchecked(
                new Vector3(deformed.x, -deformed.y, 0f),
                uv,
                rect,
                radius,
                color,
                outline,
                extra,
                mask,
                rawUv);
        }

        static Vector2 Normalized(Vector2 position, NowRect rect)
        {
            return new Vector2(
                rect.width > 0f ? Mathf.Clamp01((position.x - rect.x) / rect.width) : 0f,
                rect.height > 0f ? Mathf.Clamp01((position.y - rect.y) / rect.height) : 0f);
        }

        static Vector2 UiPosition(int index)
        {
            var vertex = _vertices[index];
            return new Vector2(vertex.x, -vertex.y);
        }

        static Vector2 GetUv0(int index) => index >= 0 && index < _uv0.Count ? _uv0[index] : default;
        static Vector4 GetRect(int index) => index >= 0 && index < _rect.Count ? _rect[index] : default;
        static Vector4 GetRadius(int index) => index >= 0 && index < _radius.Count ? _radius[index] : default;
        static Vector4 GetColor(int index) => index >= 0 && index < _color.Count ? _color[index] : Vector4.one;
        static Vector4 GetOutline(int index) => index >= 0 && index < _outline.Count ? _outline[index] : default;
        static Vector4 GetExtra(int index) => index >= 0 && index < _extra.Count ? _extra[index] : default;
        static Vector4 GetMask(int index) => index >= 0 && index < _mask.Count ? _mask[index] : default;
        static Vector4 GetRawUv(int index) => index >= 0 && index < _rawUv.Count ? _rawUv[index] : default;

        static Vector2 Bilinear2(Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight, float u, float v)
        {
            return Vector2.Lerp(Vector2.Lerp(topLeft, topRight, u), Vector2.Lerp(bottomLeft, bottomRight, u), v);
        }

        static Vector4 Bilinear4(Vector4 topLeft, Vector4 topRight, Vector4 bottomLeft, Vector4 bottomRight, float u, float v)
        {
            return Vector4.Lerp(Vector4.Lerp(topLeft, topRight, u), Vector4.Lerp(bottomLeft, bottomRight, u), v);
        }

        static void PrepareIndexMap(int count)
        {
            if (_indexMap.Length < count)
                Array.Resize(ref _indexMap, Mathf.NextPowerOfTwo(count));

            for (int i = 0; i < count; ++i)
                _indexMap[i] = -1;
        }

        static void EnsureGridCapacity(int count)
        {
            if (_grid.Length < count)
                Array.Resize(ref _grid, Mathf.NextPowerOfTwo(count));
        }
    }
}

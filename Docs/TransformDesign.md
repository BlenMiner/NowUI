# NowUI Transform System Design

## Problem Statement

The current `NowControls.Scale(float)` approach only scales "internals" (text, padding, outlines) but requires manual pre-scaling of rects and spatial values. This creates:
- Inconsistent scaling behavior
- Confusion about what gets scaled automatically
- Complex zoom implementations (like node graph)
- Easy to miss scaling some elements

## Proposed Solution: Proper Transform System

### Core Concept
Add a **transform stack** to NowUI's core that automatically transforms all drawing operations, similar to how Unity's `GUI.matrix` or Immediate Mode GUI transforms work.

### Architecture

#### 1. Transform Structure
```csharp
struct NowTransform
{
    public Vector2 origin;      // Translation offset
    public Vector2 scale;       // Scale factors (x, y)
    public float rotation;      // Rotation in radians
    public Matrix4x4 matrix;    // Combined 4x4 transform matrix
    
    public NowTransform(Vector2 scale, Vector2 origin, float rotation = 0f)
    {
        this.scale = scale;
        this.origin = origin;
        this.rotation = rotation;
        this.matrix = CalculateMatrix(scale, origin, rotation);
    }
    
    static Matrix4x4 CalculateMatrix(Vector2 scale, Vector2 origin, float rotation)
    {
        // Build transform matrix: T * R * S
        // Translation * Rotation * Scale
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        
        return new Matrix4x4(
            scale.x * cos,  scale.x * sin,  0, origin.x,
            -scale.y * sin, scale.y * cos,  0, origin.y,
            0,              0,              1, 0,
            0,              0,              0, 1
        );
    }
}
```

#### 2. Transform Stack (in Now.cs)
```csharp
static readonly List<NowTransform> _transformStack = new List<NowTransform>(4);

/// <summary>
/// The active transform from the transform stack, or identity if none.
/// </summary>
public static NowTransform currentTransform =>
    _transformStack.Count > 0 ? _transformStack[_transformStack.Count - 1] : NowTransform.identity;
```

#### 3. Public API
```csharp
/// <summary>
/// Pushes a transform scope. All drawing inside the scope is transformed
/// by the given scale, origin (translation), and optional rotation.
/// </summary>
public static NowTransformScope Transform(Vector2 scale, Vector2 origin = default, float rotation = 0f)
{
    _transformStack.Add(new NowTransform(scale, origin, rotation));
    return new NowTransformScope(true);
}

/// <summary>
/// Pushes a transform scope with uniform scale.
/// </summary>
public static NowTransformScope Transform(float scale, Vector2 origin = default, float rotation = 0f)
{
    return Transform(new Vector2(scale, scale), origin, rotation);
}

internal static void PopTransform()
{
    if (_transformStack.Count > 0)
        _transformStack.RemoveAt(_transformStack.Count - 1);
}
```

#### 4. Transform Scope (IDisposable)
```csharp
public struct NowTransformScope : IDisposable
{
    readonly bool _active;
    
    internal NowTransformScope(bool active) => _active = active;
    
    public void Dispose()
    {
        if (_active)
            Now.PopTransform();
    }
}
```

#### 5. Transform Application Helpers
```csharp
/// <summary>
/// Applies current transform to a position. Returns the transformed position.
/// </csharp
static Vector2 ApplyTransform(Vector2 position)
{
    if (_transformStack.Count == 0)
        return position;
        
    var transform = _transformStack[_transformStack.Count - 1];
    
    // For scale + origin only (most common case), optimize:
    if (Mathf.Abs(transform.rotation) < 0.001f)
    {
        return new Vector2(
            position.x * transform.scale.x + transform.origin.x,
            position.y * transform.scale.y + transform.origin.y
        );
    }
    
    // Full matrix transform for rotation cases
    return transform.matrix.MultiplyPoint(position);
}

/// <summary>
/// Applies current transform to a size. Returns the scaled size (no translation).
/// </csharp
static Vector2 ApplyTransformSize(Vector2 size)
{
    if (_transformStack.Count == 0)
        return size;
        
    var transform = _transformStack[_transformStack.Count - 1];
    return new Vector2(size.x * transform.scale.x, size.y * transform.scale.y);
}

/// <summary>
/// Applies current transform to a scalar value (like radius, outline width).
/// Returns the scaled value using average scale.
/// </csharp
static float ApplyTransformScalar(float value)
{
    if (_transformStack.Count == 0)
        return value;
        
    var transform = _transformStack[_transformStack.Count - 1];
    return value * Mathf.Max(transform.scale.x, transform.scale.y);
}
```

### Integration Points

#### 1. Rectangle Drawing (DrawRect)
Before setting `_tmpVertex.position`, apply transform:
```csharp
// Before:
_tmpVertex.position.x = x0;
_tmpVertex.position.y = -y0 - rectHeight;

// After:
Vector2 transformedPos = ApplyTransform(new Vector2(x0, -y0 - rectHeight));
_tmpVertex.position.x = transformedPos.x;
_tmpVertex.position.y = transformedPos.y;

// Size components also get scaled
_tmpVertex.position.z = rectWidth * currentTransform.scale.x;
_tmpVertex.position.w = rectHeight * currentTransform.scale.y;
```

#### 2. Text Drawing
Text positions are transformed, but font size scaling needs special handling:
```csharp
// Font size should be scaled by the transform
float scaledFontSize = fontSize * currentTransform.scale.x;
var text = NowTheme.themeAsset.Text(rect, style).SetFontSize(scaledFontSize);
```

#### 3. Lines, Beziers, etc.
All position/size values go through transform helpers before being added to mesh.

### Usage Examples

#### Before (Manual Scaling):
```csharp
void DrawNode(NowNode node, float zoom, Vector2 pan)
{
    var rect = node.position * zoom + pan;
    var size = node.size * zoom;
    
    Now.Rectangle(new NowRect(rect, size)).Draw();
    Now.Label(node.title, rect, 13f * zoom);
    // Everything needs manual zoom multiplication
}
```

#### After (Automatic Transforms):
```csharp
void DrawNode(NowNode node)
{
    using (Now.Transform(zoom: 0.5f, origin: new Vector2(100, 100)))
    {
        // Everything inside is automatically transformed
        Now.Rectangle(node.rect).Draw();
        Now.Label(node.title, node.rect, 13f);
        // No manual scaling needed!
    }
}
```

### Benefits

1. **Consistency**: Everything scales uniformly
2. **Simplicity**: No manual pre-scaling of rects
3. **Composability**: Nested transforms work naturally
4. **Correctness**: Hard to miss scaling something
5. **Performance**: Matrix multiplication is efficient
6. **Flexibility**: Supports scale, translation, and rotation

### Migration Path

1. **Phase 1**: Add transform infrastructure to Now.cs
2. **Phase 2**: Update drawing primitives to apply transforms
3. **Phase 3**: Update node graph to use new system
4. **Phase 4**: Deprecate/remove `NowControls.Scale()`

### Implementation Priority

1. **High**: Scale + translation (covers 99% of use cases)
2. **Low**: Rotation (rarely needed for UI)

### Notes

- Transform applies to **all** drawing inside the scope
- Mask intersection should happen **before** transform (masks are in screen space)
- Text sizing needs special handling (scale font size, don't transform glyph positions)
- Existing mask stack is unaffected and works independently

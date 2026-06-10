using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUIInternal
{
    /// <summary>
    /// Parsed Lottie (bodymovin) document. This is a pure data model evaluated every
    /// frame by <see cref="NowLottieRenderer"/> — nothing is ever baked to textures.
    /// </summary>
    public sealed class NowLottieComposition
    {
        public float frameRate = 30f;

        public float inPoint;

        public float outPoint;

        public float width = 512f;

        public float height = 512f;

        public string version;

        public readonly List<NowLottieLayer> layers = new List<NowLottieLayer>();

        public readonly Dictionary<string, List<NowLottieLayer>> precomps = new Dictionary<string, List<NowLottieLayer>>();

        public float durationFrames => Mathf.Max(0f, outPoint - inPoint);

        public float duration => frameRate > 0f ? durationFrames / frameRate : 0f;

        public static NowLottieComposition Parse(string json)
        {
            var root = NowJsonValue.Parse(json);

            if (root.kind != NowJsonKind.Object)
                throw new FormatException("Lottie document root must be a JSON object.");

            if (!root.Has("layers"))
                throw new FormatException("Lottie document has no 'layers' array.");

            var composition = new NowLottieComposition
            {
                version = root["v"].AsString(),
                frameRate = root["fr"].AsFloat(30f),
                inPoint = root["ip"].AsFloat(0f),
                outPoint = root["op"].AsFloat(0f),
                width = root["w"].AsFloat(512f),
                height = root["h"].AsFloat(512f)
            };

            var assets = root["assets"];

            for (int i = 0; i < assets.count; ++i)
            {
                var asset = assets[i];
                var assetLayers = asset["layers"];

                if (assetLayers.isNull)
                    continue;

                string id = asset["id"].AsString();

                if (string.IsNullOrEmpty(id))
                    continue;

                var list = new List<NowLottieLayer>(assetLayers.count);

                for (int l = 0; l < assetLayers.count; ++l)
                    list.Add(NowLottieLayer.Parse(assetLayers[l]));

                composition.precomps[id] = list;
            }

            var layers = root["layers"];

            for (int i = 0; i < layers.count; ++i)
                composition.layers.Add(NowLottieLayer.Parse(layers[i]));

            return composition;
        }
    }

    public enum NowLottieLayerType
    {
        Precomp = 0,
        Solid = 1,
        Image = 2,
        Null = 3,
        Shape = 4,
        Text = 5
    }

    public sealed class NowLottieLayer
    {
        public NowLottieLayerType type;

        public string name;

        public int index = -1;

        public int parent = -1;

        public float inPoint;

        public float outPoint;

        public float startTime;

        public float stretch = 1f;

        public bool hidden;

        /// <summary>True for layers that only exist to matte the layer below them (td:1).</summary>
        public bool isMatteSource;

        /// <summary>0 none, 1 alpha, 2 alpha inverted, 3 luma, 4 luma inverted.</summary>
        public int matteMode;

        public NowLottieTransform transform = new NowLottieTransform();

        public List<NowLottieShapeItem> shapes;

        public string refId;

        public Vector4 solidColor = Vector4.one;

        public float solidWidth;

        public float solidHeight;

        public NowLottieAnimatable timeRemap;

        public float ToLocalFrame(float compositionFrame)
        {
            float stretchValue = Mathf.Approximately(stretch, 0f) ? 1f : stretch;
            return (compositionFrame - startTime) / stretchValue;
        }

        public bool IsActive(float compositionFrame)
        {
            return !hidden && compositionFrame >= inPoint && compositionFrame < outPoint;
        }

        public static NowLottieLayer Parse(NowJsonValue json)
        {
            var layer = new NowLottieLayer
            {
                type = (NowLottieLayerType)json["ty"].AsInt(3),
                name = json["nm"].AsString(),
                index = json["ind"].AsInt(-1),
                parent = json.Has("parent") ? json["parent"].AsInt(-1) : -1,
                inPoint = json["ip"].AsFloat(float.MinValue),
                outPoint = json["op"].AsFloat(float.MaxValue),
                startTime = json["st"].AsFloat(0f),
                stretch = json["sr"].AsFloat(1f),
                hidden = json["hd"].AsBool(),
                isMatteSource = json["td"].AsInt() != 0,
                matteMode = json["tt"].AsInt(),
                refId = json["refId"].AsString()
            };

            layer.transform = NowLottieTransform.Parse(json["ks"]);

            var shapes = json["shapes"];

            if (shapes.count > 0)
            {
                layer.shapes = new List<NowLottieShapeItem>(shapes.count);
                NowLottieShapeItem.ParseList(shapes, layer.shapes);
            }

            if (layer.type == NowLottieLayerType.Solid)
            {
                layer.solidWidth = json["sw"].AsFloat();
                layer.solidHeight = json["sh"].AsFloat();
                layer.solidColor = ParseHexColor(json["sc"].AsString());
            }

            if (json.Has("tm"))
                layer.timeRemap = NowLottieAnimatable.Parse(json["tm"], 1);

            return layer;
        }

        static Vector4 ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Vector4.one;

            if (hex[0] == '#')
                hex = hex.Substring(1);

            if (hex.Length < 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint value))
                return Vector4.one;

            float r = ((value >> 16) & 0xFF) / 255f;
            float g = ((value >> 8) & 0xFF) / 255f;
            float b = (value & 0xFF) / 255f;
            return new Vector4(r, g, b, 1f);
        }
    }

    public sealed class NowLottieTransform
    {
        public NowLottieAnimatable anchor;

        public NowLottieAnimatable position;

        public NowLottieAnimatable positionX;

        public NowLottieAnimatable positionY;

        public NowLottieAnimatable scale;

        public NowLottieAnimatable rotation;

        public NowLottieAnimatable opacity;

        public NowLottieAnimatable skew;

        public NowLottieAnimatable skewAxis;

        public NowMatrix2D EvaluateMatrix(float frame)
        {
            Vector2 anchorPoint = anchor?.EvaluateVector2(frame) ?? Vector2.zero;

            Vector2 translation;

            if (positionX != null || positionY != null)
            {
                translation = new Vector2(
                    positionX?.EvaluateFloat(frame) ?? 0f,
                    positionY?.EvaluateFloat(frame) ?? 0f);
            }
            else
            {
                translation = position?.EvaluateVector2(frame) ?? Vector2.zero;
            }

            Vector2 scaleValue = scale != null
                ? scale.EvaluateVector2(frame) * 0.01f
                : Vector2.one;

            float rotationValue = rotation?.EvaluateFloat(frame) ?? 0f;
            float skewValue = skew?.EvaluateFloat(frame) ?? 0f;

            var matrix = NowMatrix2D.Translate(-anchorPoint.x, -anchorPoint.y);
            matrix = NowMatrix2D.Mul(NowMatrix2D.Scale(scaleValue.x, scaleValue.y), matrix);

            if (skewValue != 0f)
            {
                float axis = skewAxis?.EvaluateFloat(frame) ?? 0f;
                matrix = NowMatrix2D.Mul(NowMatrix2D.SkewFromAxis(-skewValue, axis), matrix);
            }

            if (rotationValue != 0f)
                matrix = NowMatrix2D.Mul(NowMatrix2D.Rotate(rotationValue), matrix);

            matrix = NowMatrix2D.Mul(NowMatrix2D.Translate(translation.x, translation.y), matrix);
            return matrix;
        }

        public float EvaluateOpacity(float frame)
        {
            return opacity != null ? Mathf.Clamp01(opacity.EvaluateFloat(frame) * 0.01f) : 1f;
        }

        public static NowLottieTransform Parse(NowJsonValue json)
        {
            var transform = new NowLottieTransform();

            if (json.isNull)
                return transform;

            if (json.Has("a"))
                transform.anchor = NowLottieAnimatable.Parse(json["a"], 2);

            var positionJson = json["p"];

            if (!positionJson.isNull)
            {
                if (positionJson["s"].AsBool())
                {
                    transform.positionX = NowLottieAnimatable.Parse(positionJson["x"], 1);
                    transform.positionY = NowLottieAnimatable.Parse(positionJson["y"], 1);
                }
                else
                {
                    transform.position = NowLottieAnimatable.Parse(positionJson, 2);
                }
            }

            if (json.Has("s"))
                transform.scale = NowLottieAnimatable.Parse(json["s"], 2);

            if (json.Has("r"))
                transform.rotation = NowLottieAnimatable.Parse(json["r"], 1);

            if (json.Has("o"))
                transform.opacity = NowLottieAnimatable.Parse(json["o"], 1);

            if (json.Has("sk"))
                transform.skew = NowLottieAnimatable.Parse(json["sk"], 1);

            if (json.Has("sa"))
                transform.skewAxis = NowLottieAnimatable.Parse(json["sa"], 1);

            return transform;
        }
    }

    // ----------------------------------------------------------------------------
    // Shape items
    // ----------------------------------------------------------------------------

    public abstract class NowLottieShapeItem
    {
        public bool hidden;

        public string name;

        public static void ParseList(NowJsonValue array, List<NowLottieShapeItem> output)
        {
            for (int i = 0; i < array.count; ++i)
            {
                var item = Parse(array[i]);

                if (item != null)
                    output.Add(item);
            }
        }

        static NowLottieShapeItem Parse(NowJsonValue json)
        {
            string type = json["ty"].AsString();
            NowLottieShapeItem item = null;

            switch (type)
            {
                case "gr": item = NowLottieGroup.ParseGroup(json); break;
                case "sh": item = NowLottiePathShape.ParsePath(json); break;
                case "el": item = NowLottieEllipse.ParseEllipse(json); break;
                case "rc": item = NowLottieRectShape.ParseRect(json); break;
                case "sr": item = NowLottiePolystar.ParsePolystar(json); break;
                case "fl": item = NowLottieFill.ParseFill(json); break;
                case "gf": item = NowLottieGradientFill.ParseGradientFill(json); break;
                case "st": item = NowLottieStroke.ParseStroke(json); break;
                case "gs": item = NowLottieGradientStroke.ParseGradientStroke(json); break;
                case "tm": item = NowLottieTrim.ParseTrim(json); break;
                case "tr": item = NowLottieGroupTransform.ParseGroupTransform(json); break;
                // 'mm' (merge paths): additive merge is approximated by the nonzero
                // compound fill already used for multi-path groups, so the item is skipped.
                // 'rp' (repeater) and 'rd' (round corners) are not supported yet.
                default: return null;
            }

            item.hidden = json["hd"].AsBool();
            item.name = json["nm"].AsString();
            return item;
        }
    }

    public sealed class NowLottieGroupTransform : NowLottieShapeItem
    {
        public NowLottieTransform transform;

        public static NowLottieGroupTransform ParseGroupTransform(NowJsonValue json)
        {
            return new NowLottieGroupTransform { transform = NowLottieTransform.Parse(json) };
        }
    }

    public sealed class NowLottieGroup : NowLottieShapeItem
    {
        public readonly List<NowLottieShapeItem> items = new List<NowLottieShapeItem>();

        public NowLottieTransform transform;

        public static NowLottieGroup ParseGroup(NowJsonValue json)
        {
            var group = new NowLottieGroup();
            ParseList(json["it"], group.items);

            // The group's transform lives in its item list as a 'tr' entry.
            for (int i = 0; i < group.items.Count; ++i)
            {
                if (group.items[i] is NowLottieGroupTransform groupTransform)
                {
                    group.transform = groupTransform.transform;
                    break;
                }
            }

            return group;
        }
    }

    public sealed class NowLottiePathShape : NowLottieShapeItem
    {
        public NowLottieShapeAnimatable shape;

        public static NowLottiePathShape ParsePath(NowJsonValue json)
        {
            return new NowLottiePathShape { shape = NowLottieShapeAnimatable.Parse(json["ks"]) };
        }
    }

    public sealed class NowLottieEllipse : NowLottieShapeItem
    {
        public NowLottieAnimatable position;

        public NowLottieAnimatable size;

        public static NowLottieEllipse ParseEllipse(NowJsonValue json)
        {
            return new NowLottieEllipse
            {
                position = NowLottieAnimatable.Parse(json["p"], 2),
                size = NowLottieAnimatable.Parse(json["s"], 2)
            };
        }
    }

    public sealed class NowLottieRectShape : NowLottieShapeItem
    {
        public NowLottieAnimatable position;

        public NowLottieAnimatable size;

        public NowLottieAnimatable roundness;

        public static NowLottieRectShape ParseRect(NowJsonValue json)
        {
            return new NowLottieRectShape
            {
                position = NowLottieAnimatable.Parse(json["p"], 2),
                size = NowLottieAnimatable.Parse(json["s"], 2),
                roundness = json.Has("r") ? NowLottieAnimatable.Parse(json["r"], 1) : null
            };
        }
    }

    public sealed class NowLottiePolystar : NowLottieShapeItem
    {
        public int starType = 1; // 1 star, 2 polygon

        public NowLottieAnimatable points;

        public NowLottieAnimatable position;

        public NowLottieAnimatable rotation;

        public NowLottieAnimatable innerRadius;

        public NowLottieAnimatable outerRadius;

        public NowLottieAnimatable innerRoundness;

        public NowLottieAnimatable outerRoundness;

        public static NowLottiePolystar ParsePolystar(NowJsonValue json)
        {
            return new NowLottiePolystar
            {
                starType = json["sy"].AsInt(1),
                points = NowLottieAnimatable.Parse(json["pt"], 1),
                position = NowLottieAnimatable.Parse(json["p"], 2),
                rotation = json.Has("r") ? NowLottieAnimatable.Parse(json["r"], 1) : null,
                innerRadius = json.Has("ir") ? NowLottieAnimatable.Parse(json["ir"], 1) : null,
                outerRadius = NowLottieAnimatable.Parse(json["or"], 1),
                innerRoundness = json.Has("is") ? NowLottieAnimatable.Parse(json["is"], 1) : null,
                outerRoundness = json.Has("os") ? NowLottieAnimatable.Parse(json["os"], 1) : null
            };
        }
    }

    public sealed class NowLottieFill : NowLottieShapeItem
    {
        public NowLottieAnimatable color;

        public NowLottieAnimatable opacity;

        /// <summary>1 nonzero, 2 even-odd.</summary>
        public int fillRule = 1;

        public static NowLottieFill ParseFill(NowJsonValue json)
        {
            return new NowLottieFill
            {
                color = NowLottieAnimatable.Parse(json["c"], 4),
                opacity = json.Has("o") ? NowLottieAnimatable.Parse(json["o"], 1) : null,
                fillRule = json["r"].AsInt(1)
            };
        }
    }

    public sealed class NowLottieGradient
    {
        /// <summary>1 linear, 2 radial.</summary>
        public int type = 1;

        public NowLottieAnimatable start;

        public NowLottieAnimatable end;

        public NowLottieAnimatable stops;

        public int colorStopCount;

        public static NowLottieGradient Parse(NowJsonValue json)
        {
            var gradientStops = json["g"];

            return new NowLottieGradient
            {
                type = json["t"].AsInt(1),
                start = NowLottieAnimatable.Parse(json["s"], 2),
                end = NowLottieAnimatable.Parse(json["e"], 2),
                colorStopCount = gradientStops["p"].AsInt(0),
                stops = NowLottieAnimatable.Parse(gradientStops["k"], 0)
            };
        }
    }

    public sealed class NowLottieGradientFill : NowLottieShapeItem
    {
        public NowLottieGradient gradient;

        public NowLottieAnimatable opacity;

        public int fillRule = 1;

        public static NowLottieGradientFill ParseGradientFill(NowJsonValue json)
        {
            return new NowLottieGradientFill
            {
                gradient = NowLottieGradient.Parse(json),
                opacity = json.Has("o") ? NowLottieAnimatable.Parse(json["o"], 1) : null,
                fillRule = json["r"].AsInt(1)
            };
        }
    }

    public sealed class NowLottieStroke : NowLottieShapeItem
    {
        public NowLottieAnimatable color;

        public NowLottieAnimatable opacity;

        public NowLottieAnimatable width;

        /// <summary>1 butt, 2 round, 3 square.</summary>
        public int cap = 2;

        /// <summary>1 miter, 2 round, 3 bevel.</summary>
        public int join = 2;

        public float miterLimit = 4f;

        public static NowLottieStroke ParseStroke(NowJsonValue json)
        {
            return new NowLottieStroke
            {
                color = NowLottieAnimatable.Parse(json["c"], 4),
                opacity = json.Has("o") ? NowLottieAnimatable.Parse(json["o"], 1) : null,
                width = NowLottieAnimatable.Parse(json["w"], 1),
                cap = json["lc"].AsInt(2),
                join = json["lj"].AsInt(2),
                miterLimit = json["ml"].AsFloat(4f)
            };
        }
    }

    public sealed class NowLottieGradientStroke : NowLottieShapeItem
    {
        public NowLottieGradient gradient;

        public NowLottieAnimatable opacity;

        public NowLottieAnimatable width;

        public int cap = 2;

        public int join = 2;

        public static NowLottieGradientStroke ParseGradientStroke(NowJsonValue json)
        {
            return new NowLottieGradientStroke
            {
                gradient = NowLottieGradient.Parse(json),
                opacity = json.Has("o") ? NowLottieAnimatable.Parse(json["o"], 1) : null,
                width = NowLottieAnimatable.Parse(json["w"], 1),
                cap = json["lc"].AsInt(2),
                join = json["lj"].AsInt(2)
            };
        }
    }

    public sealed class NowLottieTrim : NowLottieShapeItem
    {
        public NowLottieAnimatable start;

        public NowLottieAnimatable end;

        public NowLottieAnimatable offset;

        /// <summary>1 simultaneous (paths trimmed as one), 2 individual.</summary>
        public int mode = 1;

        public static NowLottieTrim ParseTrim(NowJsonValue json)
        {
            return new NowLottieTrim
            {
                start = NowLottieAnimatable.Parse(json["s"], 1),
                end = NowLottieAnimatable.Parse(json["e"], 1),
                offset = json.Has("o") ? NowLottieAnimatable.Parse(json["o"], 1) : null,
                mode = json["m"].AsInt(1)
            };
        }
    }

    // ----------------------------------------------------------------------------
    // Animatable values
    // ----------------------------------------------------------------------------

    /// <summary>
    /// A Lottie animated property holding float vectors of arbitrary dimension
    /// (opacity, position, scale, color, gradient stop arrays, ...).
    /// </summary>
    public sealed class NowLottieAnimatable
    {
        sealed class Keyframe
        {
            public float time;

            public float[] startValue;

            public float[] endValue;

            public float[] tangentOut; // spatial

            public float[] tangentIn;  // spatial

            public float easeOutX, easeOutY, easeInX, easeInY;

            public bool hasEase;

            public bool hold;
        }

        float[] _staticValue;

        Keyframe[] _keyframes;

        int _cursor;

        public int dimensions { get; private set; }

        public bool isAnimated => _keyframes != null && _keyframes.Length > 0;

        public float EvaluateFloat(float frame)
        {
            EvaluateInto(frame, _scalarScratch);
            return _scalarScratch[0];
        }

        public Vector2 EvaluateVector2(float frame)
        {
            EvaluateInto(frame, _vectorScratch);
            return new Vector2(_vectorScratch[0], _vectorScratch[1]);
        }

        public Vector4 EvaluateVector4(float frame)
        {
            EvaluateInto(frame, _vectorScratch);
            return new Vector4(_vectorScratch[0], _vectorScratch[1], _vectorScratch[2], _vectorScratch[3]);
        }

        [ThreadStatic] static float[] _scalarScratchStorage;

        [ThreadStatic] static float[] _vectorScratchStorage;

        static float[] _scalarScratch => _scalarScratchStorage ??= new float[1];

        static float[] _vectorScratch => _vectorScratchStorage ??= new float[4];

        /// <summary>
        /// Writes min(dimensions, destination.Length) components into destination.
        /// </summary>
        public void EvaluateInto(float frame, float[] destination)
        {
            int copyCount = Mathf.Min(dimensions, destination.Length);

            if (_keyframes == null || _keyframes.Length == 0)
            {
                CopyValue(_staticValue, destination, copyCount);
                return;
            }

            var keys = _keyframes;

            if (frame <= keys[0].time)
            {
                CopyValue(keys[0].startValue, destination, copyCount);
                return;
            }

            var lastKey = keys[keys.Length - 1];

            if (frame >= lastKey.time)
            {
                CopyValue(lastKey.startValue ?? keys[keys.Length - 2].endValue, destination, copyCount);
                return;
            }

            // Sequential playback friendly segment search starting from the cached cursor.
            int segment = Mathf.Clamp(_cursor, 0, keys.Length - 2);

            while (segment > 0 && frame < keys[segment].time)
                --segment;

            while (segment < keys.Length - 2 && frame >= keys[segment + 1].time)
                ++segment;

            _cursor = segment;

            var key = keys[segment];
            var next = keys[segment + 1];

            if (key.hold)
            {
                CopyValue(key.startValue, destination, copyCount);
                return;
            }

            float duration = next.time - key.time;
            float linearT = duration > 0f ? (frame - key.time) / duration : 0f;
            float easedT = key.hasEase
                ? NowLottieEasing.Evaluate(key.easeOutX, key.easeOutY, key.easeInX, key.easeInY, linearT)
                : linearT;

            var from = key.startValue;
            var to = key.endValue ?? next.startValue;

            if (to == null)
            {
                CopyValue(from, destination, copyCount);
                return;
            }

            bool spatial = key.tangentOut != null && key.tangentIn != null && copyCount >= 2;

            if (spatial)
            {
                // Animated position following a spatial bezier through the keyframe tangents.
                float oneMinusT = 1f - easedT;
                float b0 = oneMinusT * oneMinusT * oneMinusT;
                float b1 = 3f * oneMinusT * oneMinusT * easedT;
                float b2 = 3f * oneMinusT * easedT * easedT;
                float b3 = easedT * easedT * easedT;

                for (int i = 0; i < 2; ++i)
                {
                    float p0 = Get(from, i);
                    float p3 = Get(to, i);
                    float p1 = p0 + Get(key.tangentOut, i);
                    float p2 = p3 + Get(key.tangentIn, i);
                    destination[i] = b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
                }

                for (int i = 2; i < copyCount; ++i)
                    destination[i] = Mathf.LerpUnclamped(Get(from, i), Get(to, i), easedT);

                return;
            }

            for (int i = 0; i < copyCount; ++i)
                destination[i] = Mathf.LerpUnclamped(Get(from, i), Get(to, i), easedT);
        }

        static float Get(float[] values, int index)
        {
            if (values == null || values.Length == 0)
                return 0f;

            return index < values.Length ? values[index] : values[values.Length - 1];
        }

        static void CopyValue(float[] source, float[] destination, int count)
        {
            for (int i = 0; i < count; ++i)
                destination[i] = Get(source, i);
        }

        /// <summary>
        /// Parses either a raw value, a {a,k} property wrapper or a keyframe list.
        /// expectedDimensions 0 means "whatever length the data has" (gradient stops).
        /// </summary>
        public static NowLottieAnimatable Parse(NowJsonValue json, int expectedDimensions)
        {
            var result = new NowLottieAnimatable();

            NowJsonValue valueJson = json;
            bool animatedFlag = false;

            if (json.kind == NowJsonKind.Object)
            {
                animatedFlag = json["a"].AsInt() != 0;
                valueJson = json["k"];
            }

            bool looksLikeKeyframes = valueJson.kind == NowJsonKind.Array &&
                valueJson.count > 0 &&
                valueJson[0].kind == NowJsonKind.Object &&
                valueJson[0].Has("t");

            if (animatedFlag || looksLikeKeyframes)
            {
                result.ParseKeyframes(valueJson, expectedDimensions);
                return result;
            }

            result._staticValue = PadColor(ParseFloatArray(valueJson), expectedDimensions);
            result.dimensions = expectedDimensions > 0 ? expectedDimensions : result._staticValue.Length;
            return result;
        }

        /// <summary>RGB colors without an alpha component get an opaque alpha appended.</summary>
        static float[] PadColor(float[] values, int expectedDimensions)
        {
            if (expectedDimensions != 4 || values == null || values.Length != 3)
                return values;

            return new[] { values[0], values[1], values[2], 1f };
        }

        void ParseKeyframes(NowJsonValue keyframesJson, int expectedDimensions)
        {
            var keys = new List<Keyframe>(keyframesJson.count);
            int maxDimensions = expectedDimensions;

            for (int i = 0; i < keyframesJson.count; ++i)
            {
                var keyJson = keyframesJson[i];

                var key = new Keyframe
                {
                    time = keyJson["t"].AsFloat(),
                    hold = keyJson["h"].AsInt() != 0
                };

                if (keyJson.Has("s"))
                    key.startValue = PadColor(ParseFloatArray(keyJson["s"]), expectedDimensions);

                if (keyJson.Has("e"))
                    key.endValue = PadColor(ParseFloatArray(keyJson["e"]), expectedDimensions);

                if (keyJson.Has("to"))
                    key.tangentOut = ParseFloatArray(keyJson["to"]);

                if (keyJson.Has("ti"))
                    key.tangentIn = ParseFloatArray(keyJson["ti"]);

                if (keyJson.Has("o") && keyJson.Has("i"))
                {
                    key.hasEase = true;
                    key.easeOutX = Mathf.Clamp01(keyJson["o"]["x"].AsFloat(0.167f));
                    key.easeOutY = keyJson["o"]["y"].AsFloat(0.167f);
                    key.easeInX = Mathf.Clamp01(keyJson["i"]["x"].AsFloat(0.833f));
                    key.easeInY = keyJson["i"]["y"].AsFloat(0.833f);
                }

                if (key.startValue != null)
                    maxDimensions = Mathf.Max(maxDimensions, key.startValue.Length);

                keys.Add(key);
            }

            // Legacy exports leave a trailing time-only keyframe; older files also rely
            // on 'e'. Fill missing start values from the previous segment's end value.
            for (int i = 0; i < keys.Count; ++i)
            {
                if (keys[i].startValue == null && i > 0)
                    keys[i].startValue = keys[i - 1].endValue ?? keys[i - 1].startValue;
            }

            _keyframes = keys.ToArray();
            dimensions = maxDimensions;
        }

        static float[] ParseFloatArray(NowJsonValue json)
        {
            if (json.kind == NowJsonKind.Number || json.kind == NowJsonKind.Bool)
                return new[] { json.AsFloat() };

            if (json.kind != NowJsonKind.Array)
                return new[] { 0f };

            var result = new float[Mathf.Max(1, json.count)];

            for (int i = 0; i < json.count; ++i)
                result[i] = json[i].AsFloat();

            return result;
        }
    }

    /// <summary>Cubic-bezier easing identical to CSS cubic-bezier / AE keyframe influence.</summary>
    public static class NowLottieEasing
    {
        public static float Evaluate(float outX, float outY, float inX, float inY, float t)
        {
            if (t <= 0f)
                return 0f;

            if (t >= 1f)
                return 1f;

            if (Mathf.Approximately(outX, outY) && Mathf.Approximately(inX, inY))
                return t; // linear

            float u = SolveCurveX(outX, inX, t);
            return SampleCurve(outY, inY, u);
        }

        static float SampleCurve(float p1, float p2, float t)
        {
            float oneMinusT = 1f - t;
            return 3f * oneMinusT * oneMinusT * t * p1 + 3f * oneMinusT * t * t * p2 + t * t * t;
        }

        static float SampleCurveDerivative(float p1, float p2, float t)
        {
            float oneMinusT = 1f - t;
            return 3f * oneMinusT * oneMinusT * p1 +
                6f * oneMinusT * t * (p2 - p1) +
                3f * t * t * (1f - p2);
        }

        static float SolveCurveX(float p1, float p2, float x)
        {
            float t = x;

            // Newton-Raphson.
            for (int i = 0; i < 6; ++i)
            {
                float currentX = SampleCurve(p1, p2, t) - x;

                if (Mathf.Abs(currentX) < 0.0001f)
                    return t;

                float derivative = SampleCurveDerivative(p1, p2, t);

                if (Mathf.Abs(derivative) < 0.000001f)
                    break;

                t -= currentX / derivative;
            }

            // Bisection fallback.
            float low = 0f;
            float high = 1f;
            t = x;

            for (int i = 0; i < 24; ++i)
            {
                float currentX = SampleCurve(p1, p2, t);

                if (Mathf.Abs(currentX - x) < 0.0001f)
                    return t;

                if (currentX < x)
                    low = t;
                else
                    high = t;

                t = (low + high) * 0.5f;
            }

            return t;
        }
    }

    // ----------------------------------------------------------------------------
    // Animated bezier shapes
    // ----------------------------------------------------------------------------

    /// <summary>
    /// One bezier contour: anchor points with in/out tangents relative to the anchors,
    /// exactly as stored in the Lottie document.
    /// </summary>
    public sealed class NowLottieBezierData
    {
        public bool closed;

        public int count;

        public Vector2[] vertices = System.Array.Empty<Vector2>();

        public Vector2[] tangentsIn = System.Array.Empty<Vector2>();

        public Vector2[] tangentsOut = System.Array.Empty<Vector2>();

        public void EnsureCapacity(int capacity)
        {
            if (vertices.Length >= capacity)
                return;

            System.Array.Resize(ref vertices, capacity);
            System.Array.Resize(ref tangentsIn, capacity);
            System.Array.Resize(ref tangentsOut, capacity);
        }

        public void CopyFrom(NowLottieBezierData other)
        {
            EnsureCapacity(other.count);
            closed = other.closed;
            count = other.count;
            System.Array.Copy(other.vertices, vertices, other.count);
            System.Array.Copy(other.tangentsIn, tangentsIn, other.count);
            System.Array.Copy(other.tangentsOut, tangentsOut, other.count);
        }

        public static NowLottieBezierData Parse(NowJsonValue json)
        {
            // Shape values are sometimes wrapped in a single-element array.
            if (json.kind == NowJsonKind.Array)
                json = json[0];

            var vertices = json["v"];
            var data = new NowLottieBezierData
            {
                closed = json["c"].AsBool(),
                count = vertices.count
            };

            data.EnsureCapacity(Mathf.Max(1, data.count));

            var tangentsIn = json["i"];
            var tangentsOut = json["o"];

            for (int i = 0; i < data.count; ++i)
            {
                data.vertices[i] = ReadPoint(vertices[i]);
                data.tangentsIn[i] = ReadPoint(tangentsIn[i]);
                data.tangentsOut[i] = ReadPoint(tangentsOut[i]);
            }

            return data;
        }

        static Vector2 ReadPoint(NowJsonValue json)
        {
            return new Vector2(json[0].AsFloat(), json[1].AsFloat());
        }
    }

    public sealed class NowLottieShapeAnimatable
    {
        sealed class Keyframe
        {
            public float time;

            public NowLottieBezierData startValue;

            public NowLottieBezierData endValue;

            public float easeOutX, easeOutY, easeInX, easeInY;

            public bool hasEase;

            public bool hold;
        }

        NowLottieBezierData _staticValue;

        Keyframe[] _keyframes;

        int _cursor;

        /// <summary>
        /// Returns the contour at <paramref name="frame"/>. Static shapes (and exact
        /// keyframe boundaries) return their parsed data directly without copying —
        /// callers must treat the result as read-only. Interpolated results are
        /// written into <paramref name="scratch"/>.
        /// </summary>
        public NowLottieBezierData Evaluate(float frame, NowLottieBezierData scratch)
        {
            if (_keyframes == null || _keyframes.Length == 0)
                return _staticValue;

            var keys = _keyframes;

            if (frame <= keys[0].time)
                return keys[0].startValue;

            var lastKey = keys[keys.Length - 1];

            if (frame >= lastKey.time)
                return lastKey.startValue ?? keys[keys.Length - 2].endValue ?? keys[0].startValue;

            int segment = Mathf.Clamp(_cursor, 0, keys.Length - 2);

            while (segment > 0 && frame < keys[segment].time)
                --segment;

            while (segment < keys.Length - 2 && frame >= keys[segment + 1].time)
                ++segment;

            _cursor = segment;

            var key = keys[segment];
            var next = keys[segment + 1];
            var from = key.startValue;
            var to = key.endValue ?? next.startValue;

            if (key.hold || to == null || from == null)
                return from ?? to;

            var destination = scratch;

            float duration = next.time - key.time;
            float linearT = duration > 0f ? (frame - key.time) / duration : 0f;
            float easedT = key.hasEase
                ? NowLottieEasing.Evaluate(key.easeOutX, key.easeOutY, key.easeInX, key.easeInY, linearT)
                : linearT;

            int pointCount = Mathf.Min(from.count, to.count);
            destination.EnsureCapacity(Mathf.Max(1, pointCount));
            destination.count = pointCount;
            destination.closed = from.closed || to.closed;

            for (int i = 0; i < pointCount; ++i)
            {
                destination.vertices[i] = Vector2.LerpUnclamped(from.vertices[i], to.vertices[i], easedT);
                destination.tangentsIn[i] = Vector2.LerpUnclamped(from.tangentsIn[i], to.tangentsIn[i], easedT);
                destination.tangentsOut[i] = Vector2.LerpUnclamped(from.tangentsOut[i], to.tangentsOut[i], easedT);
            }

            return destination;
        }

        public static NowLottieShapeAnimatable Parse(NowJsonValue json)
        {
            var result = new NowLottieShapeAnimatable();

            NowJsonValue valueJson = json;
            bool animatedFlag = false;

            if (json.kind == NowJsonKind.Object)
            {
                animatedFlag = json["a"].AsInt() != 0;
                valueJson = json["k"];
            }

            bool looksLikeKeyframes = valueJson.kind == NowJsonKind.Array &&
                valueJson.count > 0 &&
                valueJson[0].kind == NowJsonKind.Object &&
                valueJson[0].Has("t");

            if (!animatedFlag && !looksLikeKeyframes)
            {
                result._staticValue = NowLottieBezierData.Parse(valueJson);
                return result;
            }

            var keys = new List<Keyframe>(valueJson.count);

            for (int i = 0; i < valueJson.count; ++i)
            {
                var keyJson = valueJson[i];

                var key = new Keyframe
                {
                    time = keyJson["t"].AsFloat(),
                    hold = keyJson["h"].AsInt() != 0
                };

                if (keyJson.Has("s"))
                    key.startValue = NowLottieBezierData.Parse(keyJson["s"]);

                if (keyJson.Has("e"))
                    key.endValue = NowLottieBezierData.Parse(keyJson["e"]);

                if (keyJson.Has("o") && keyJson.Has("i"))
                {
                    key.hasEase = true;
                    key.easeOutX = Mathf.Clamp01(keyJson["o"]["x"].AsFloat(0.167f));
                    key.easeOutY = keyJson["o"]["y"].AsFloat(0.167f);
                    key.easeInX = Mathf.Clamp01(keyJson["i"]["x"].AsFloat(0.833f));
                    key.easeInY = keyJson["i"]["y"].AsFloat(0.833f);
                }

                keys.Add(key);
            }

            for (int i = 0; i < keys.Count; ++i)
            {
                if (keys[i].startValue == null && i > 0)
                    keys[i].startValue = keys[i - 1].endValue ?? keys[i - 1].startValue;
            }

            result._keyframes = keys.ToArray();
            return result;
        }
    }
}

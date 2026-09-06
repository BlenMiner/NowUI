using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class NowShaderStereoTests
{
    const string PackageAssetRoot = "Assets/NowUI";
    const string PackageIncludePrefix = "Packages/com.blenminer.nowui/";

    static readonly Regex ProgramPattern = new Regex(
        @"\b(?<language>CG|HLSL)PROGRAM\b(?<body>.*?)\b(?:ENDCG|ENDHLSL)\b",
        RegexOptions.Singleline);

    static readonly Regex SharedIncludePattern = new Regex(
        @"\b(?<language>CG|HLSL)INCLUDE\b(?<body>.*?)\b(?:ENDCG|ENDHLSL)\b",
        RegexOptions.Singleline);

    static readonly Regex VertexPragmaPattern = new Regex(
        @"(?m)^[ \t]*#pragma[ \t]+vertex[ \t]+(?<entry>[A-Za-z_][A-Za-z0-9_]*)[ \t]*\r?$",
        RegexOptions.None);

    static readonly Regex IncludePattern = new Regex(
        @"(?m)^[ \t]*#include[ \t]+""(?<path>[^""\r\n]+)""[ \t]*\r?$",
        RegexOptions.None);

    static readonly Regex OrdinaryInstancingPragmaPattern = new Regex(
        @"(?m)^[ \t]*#pragma[ \t]+multi_compile_instancing(?:[ \t].*)?\r?$",
        RegexOptions.None);

    static readonly Dictionary<string, string> InstancingPragmaExemptShaders =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {
                "Assets/NowUI/Assets/Shaders/UIGlassBlur.shader",
                "Its fullscreen blur and resolve passes are dispatched as flat blits or one explicitly bound array slice at a time."
            },
            {
                "Assets/NowUI/Assets/Shaders/LottiePreview.shader",
                "It is an Editor-only offscreen preview drawn with Graphics.DrawMeshNow, never XR camera geometry."
            },
            {
                "Assets/NowUI/Extensions/Sdf/NowSdfImageField.shader",
                "Its five image-field bake and atlas-stamp passes use Graphics.Blit into flat texture caches, never XR camera geometry."
            }
        };

    static readonly HashSet<string> EditorOnlyFlatShaders =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Assets/NowUI/Assets/Shaders/LottiePreview.shader"
        };

    static readonly Dictionary<string, string> ExternalVertexProviders =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // UnityCG's vert_img owns the instance input, stereo output, setup,
            // and initialization macros. NowUI should not duplicate that helper.
            { "Assets/NowUI/Assets/Shaders/UIGlassBlur.shader::vert_img", "UnityCG.cginc" },
            { "Assets/NowUI/Extensions/Sdf/NowSdfImageField.shader::vert_img", "UnityCG.cginc" }
        };

    static readonly HashSet<string> SerializedResolveVertexEntries =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // This fullscreen pass binds exactly one array destination slice per
            // draw. Emitting a stereo render-target index would address the
            // wrong slice instead of improving stereo coverage.
            "Assets/NowUI/Assets/Shaders/UIGlassBlur.shader::NowGlassBlurArrayVertex",
            // MultiPass invokes this fullscreen resolve separately for each flat
            // eye target, so stereo instancing is deliberately not involved.
            "Assets/NowUI/Assets/Shaders/UIGlassBlur.shader::NowGlassBlurMSAAVertex"
        };

    [Test]
    public void EveryPackageShaderVertexProgramSupportsStereoInstancing()
    {
        string packageRoot = GetPackageRoot();
        string[] shaderPaths = Directory
            .EnumerateFiles(packageRoot, "*.shader", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.IsNotEmpty(shaderPaths, $"No package shaders were found below '{packageRoot}'.");

        var consumedExternalProviders = new HashSet<string>(StringComparer.Ordinal);
        var consumedSerializedResolveEntries = new HashSet<string>(StringComparer.Ordinal);
        var consumedEditorOnlyShaders = new HashSet<string>(StringComparer.Ordinal);

        for (int shaderIndex = 0; shaderIndex < shaderPaths.Length; ++shaderIndex)
        {
            string shaderPath = shaderPaths[shaderIndex];
            string displayPath = ToAssetPath(shaderPath);
            string source = StripComments(File.ReadAllText(shaderPath));
            MatchCollection programs = ProgramPattern.Matches(source);

            Assert.Greater(
                programs.Count,
                0,
                $"Shader '{displayPath}' has no CGPROGRAM or HLSLPROGRAM block.");

            for (int programIndex = 0; programIndex < programs.Count; ++programIndex)
            {
                string body = ProgramSourceWithSharedIncludes(source, programs[programIndex]);
                MatchCollection vertexPragmas = VertexPragmaPattern.Matches(body);

                Assert.AreEqual(
                    1,
                    vertexPragmas.Count,
                    $"Shader '{displayPath}' program {programIndex} must declare exactly one #pragma vertex entry point.");

                string entryPoint = vertexPragmas[0].Groups["entry"].Value;
                string entryKey = $"{displayPath}::{entryPoint}";

                if (EditorOnlyFlatShaders.Contains(displayPath))
                {
                    Assert.IsFalse(
                        Regex.IsMatch(
                            body,
                            @"\bUNITY_(?:VERTEX_INPUT_INSTANCE_ID|VERTEX_OUTPUT_STEREO|SETUP_INSTANCE_ID|INITIALIZE_VERTEX_OUTPUT_STEREO|SETUP_STEREO_EYE_INDEX_POST_VERTEX)\b"),
                        $"Editor-only flat shader '{displayPath}' must not retain unused XR vertex plumbing.");
                    consumedEditorOnlyShaders.Add(displayPath);
                    continue;
                }

                if (ExternalVertexProviders.TryGetValue(entryKey, out string provider))
                {
                    Assert.IsTrue(
                        Includes(body, provider),
                        $"External stereo provider '{entryKey}' must include '{provider}'.");
                    consumedExternalProviders.Add(entryKey);
                    continue;
                }

                string effectiveSource = ExpandPackageIncludes(
                    body,
                    Path.GetDirectoryName(shaderPath),
                    packageRoot,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));

                AssertVertexFunctionExists(effectiveSource, entryPoint, entryKey);

                if (SerializedResolveVertexEntries.Contains(entryKey))
                {
                    consumedSerializedResolveEntries.Add(entryKey);
                    continue;
                }

                AssertStereoVertexContract(effectiveSource, entryKey);
            }
        }

        CollectionAssert.AreEquivalent(
            ExternalVertexProviders.Keys,
            consumedExternalProviders,
            "A stereo-provider exemption is stale or a known external entry point was not visited.");
        CollectionAssert.AreEquivalent(
            SerializedResolveVertexEntries,
            consumedSerializedResolveEntries,
            "A serialized resolve vertex exemption is stale or its dedicated blur entry point was not visited.");
        CollectionAssert.AreEquivalent(
            EditorOnlyFlatShaders,
            consumedEditorOnlyShaders,
            "An Editor-only shader exemption is stale or its flat program was not visited.");
    }

    [TestCase("Assets/NowUI/Extensions/Sdf/NowSdfShaderV1.cginc", "vert")]
    [TestCase("Assets/NowUI/Extensions/Sdf/NowSdfShaderV2.cginc", "vert")]
    public void PublicSdfEntryPointIncludesSupportStereoInstancing(string assetPath, string entryPoint)
    {
        string fullPath = AssetPathToFullPath(assetPath);
        Assert.IsTrue(File.Exists(fullPath), $"Missing public shader include '{assetPath}'.");

        string source = StripComments(File.ReadAllText(fullPath));
        AssertVertexFunctionExists(source, entryPoint, assetPath);
        AssertStereoVertexContract(source, assetPath);
    }

    [Test]
    public void XrRenderedGeometryProgramsGenerateInstancingVariants()
    {
        string packageRoot = GetPackageRoot();
        string[] shaderPaths = Directory
            .EnumerateFiles(packageRoot, "*.shader", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var consumedExemptions = new HashSet<string>(StringComparer.Ordinal);

        for (int shaderIndex = 0; shaderIndex < shaderPaths.Length; ++shaderIndex)
        {
            string shaderPath = shaderPaths[shaderIndex];
            string displayPath = ToAssetPath(shaderPath);
            string source = StripComments(File.ReadAllText(shaderPath));
            MatchCollection programs = ProgramPattern.Matches(source);
            bool isExempt = InstancingPragmaExemptShaders.TryGetValue(
                displayPath,
                out string exemptionReason);

            if (isExempt)
                consumedExemptions.Add(displayPath);

            for (int programIndex = 0; programIndex < programs.Count; ++programIndex)
            {
                string body = ProgramSourceWithSharedIncludes(source, programs[programIndex]);
                int pragmaCount = OrdinaryInstancingPragmaPattern.Matches(body).Count;

                if (isExempt)
                {
                    Assert.Zero(
                        pragmaCount,
                        $"Fullscreen/editor helper shader '{displayPath}' program {programIndex} " +
                        $"must not generate an instancing variant. {exemptionReason}");
                    continue;
                }

                Assert.AreEqual(
                    1,
                    pragmaCount,
                    $"XR-rendered geometry shader '{displayPath}' program {programIndex} must declare " +
                    "exactly one #pragma multi_compile_instancing so Unity emits the instanced " +
                    "variant used by single-pass-instanced rendering.");
            }
        }

        CollectionAssert.AreEquivalent(
            InstancingPragmaExemptShaders.Keys,
            consumedExemptions,
            "An instancing-pragma exemption is stale or its helper shader was not visited.");
    }

    [Test]
    public void GlassArrayBackdropSamplingIsCompiledForEveryCameraVariant()
    {
        string source = StripComments(File.ReadAllText(
            AssetPathToFullPath("Assets/NowUI/Assets/Shaders/UIGlass.shader")));

        Assert.IsFalse(
            source.Contains("#if defined(UNITY_STEREO_INSTANCING_ENABLED)"),
            "Array backdrop resources and samples must also exist in MultiPass, SceneView, and spectator variants.");
        Assert.AreEqual(
            4,
            Regex.Matches(
                source,
                @"UNITY_SAMPLE_TEX2DARRAY\s*\([^;]+NowGlassBackdropSlice\s*\(",
                RegexOptions.Singleline).Count,
            "Both global/material blurred and sharp backdrops must select an eye-safe array slice.");
        Assert.IsTrue(
            Regex.IsMatch(
                source,
                @"float\s+NowGlassBackdropSlice\s*\(\s*float\s+sliceCount\s*\).*?min\s*\(.*?unity_StereoEyeIndex.*?max\s*\(\s*0\.0\s*,\s*sliceCount\s*-\s*1\.0\s*\)",
                RegexOptions.Singleline),
            "Array sampling must clamp the eye index so a one-slice XR array remains valid for the right eye.");
    }

    [TestCase("CG", "ENDCG")]
    [TestCase("HLSL", "ENDHLSL")]
    public void SharedIncludesResolveVertexFunctionsAndPragmas(string language, string terminator)
    {
        string source = "Shader \"SharedVertex\" {\n" + language + "INCLUDE\n" +
            "float4 SharedVertex(float4 position) { return position; }\n" +
            "#pragma multi_compile_instancing\n" + terminator + "\n" +
            "SubShader { Pass {\n" + language + "PROGRAM\n" +
            "#pragma vertex SharedVertex\n" + terminator + "\n} } }";
        var programs = ProgramPattern.Matches(source);
        Assert.AreEqual(1, programs.Count);
        string effective = ProgramSourceWithSharedIncludes(source, programs[0]);
        AssertVertexFunctionExists(effective, "SharedVertex", "shared-include fixture");
        Assert.AreEqual(1, OrdinaryInstancingPragmaPattern.Matches(effective).Count);
    }

    [Test]
    public void SharedIncludesDoNotLeakIntoSiblingSubshaders()
    {
        const string source = @"Shader ""ScopedVertex"" {
            SubShader {
                CGINCLUDE
                float4 ScopedVertex(float4 position) { return position; }
                #pragma multi_compile_instancing
                ENDCG
                Pass {
                    CGPROGRAM
                    #pragma vertex ScopedVertex
                    ENDCG
                }
            }
            SubShader {
                Pass {
                    CGPROGRAM
                    #pragma vertex ScopedVertex
                    ENDCG
                }
            }
        }";
        var programs = ProgramPattern.Matches(source);
        Assert.AreEqual(2, programs.Count);
        AssertVertexFunctionExists(
            ProgramSourceWithSharedIncludes(source, programs[0]), "ScopedVertex", "owning SubShader");
        string sibling = ProgramSourceWithSharedIncludes(source, programs[1]);
        Assert.Throws<AssertionException>(() =>
            AssertVertexFunctionExists(sibling, "ScopedVertex", "sibling SubShader"));
        Assert.Zero(OrdinaryInstancingPragmaPattern.Matches(sibling).Count,
            "A sibling's instancing pragma must not satisfy the XR geometry gate.");
    }

    [Test]
    public void ImageFieldBakingPassesUseTheirSharedUnityBlitVertexProvider()
    {
        const string assetPath = "Assets/NowUI/Extensions/Sdf/NowSdfImageField.shader";
        string source = StripComments(File.ReadAllText(AssetPathToFullPath(assetPath)));
        var programs = ProgramPattern.Matches(source);
        string[] fragments = { "SeedFragment", "FloodFragment", "ResolveFragment", "StampFragment", "DilateFragment" };
        Assert.AreEqual(fragments.Length, programs.Count);
        for (int i = 0; i < programs.Count; ++i)
        {
            string effective = ProgramSourceWithSharedIncludes(source, programs[i]);
            Assert.AreEqual("vert_img", VertexPragmaPattern.Match(effective).Groups["entry"].Value);
            Assert.IsTrue(Includes(effective, "UnityCG.cginc"), fragments[i]);
            AssertVertexFunctionExists(effective, fragments[i], assetPath);
            Assert.IsTrue(Regex.IsMatch(effective, @"(?m)^\s*#pragma\s+fragment\s+" + fragments[i] + @"\s*$"));
            Assert.Zero(OrdinaryInstancingPragmaPattern.Matches(effective).Count);
        }
    }

    static string ProgramSourceWithSharedIncludes(string shaderSource, Match program)
    {
        var effective = new StringBuilder();
        var programScopes = EnclosingShaderScopes(shaderSource, program.Index);
        string language = program.Groups["language"].Value;
        foreach (Match include in SharedIncludePattern.Matches(shaderSource))
        {
            if (include.Groups["language"].Value != language)
                continue;
            var includeScopes = EnclosingShaderScopes(shaderSource, include.Index);
            int includeOwner = includeScopes[includeScopes.Count - 1];
            if (programScopes.Contains(includeOwner))
                effective.AppendLine(include.Groups["body"].Value);
        }
        effective.Append(program.Groups["body"].Value);
        return effective.ToString();
    }

    static List<int> EnclosingShaderScopes(string source, int position)
    {
        // ShaderLab shared includes apply at Shader/SubShader scope. Retain
        // their owning brace so a sibling pass/subshader cannot provide missing
        // functions or instancing pragmas to an unrelated geometry program.
        var scopes = new List<int> { -1 };
        bool quoted = false;
        for (int i = 0; i < position; ++i)
        {
            char value = source[i];
            if (quoted && value == '\\')
            {
                ++i;
                continue;
            }
            if (value == '"')
                quoted = !quoted;
            else if (!quoted && value == '{')
                scopes.Add(i);
            else if (!quoted && value == '}' && scopes.Count > 1)
                scopes.RemoveAt(scopes.Count - 1);
        }
        return scopes;
    }

    static void AssertStereoVertexContract(string source, string owner)
    {
        Assert.IsTrue(
            Regex.IsMatch(source, @"\bUNITY_VERTEX_INPUT_INSTANCE_ID\b"),
            $"Stereo vertex entry '{owner}' is missing UNITY_VERTEX_INPUT_INSTANCE_ID.");
        Assert.IsTrue(
            Regex.IsMatch(source, @"\bUNITY_VERTEX_OUTPUT_STEREO\b"),
            $"Stereo vertex entry '{owner}' is missing UNITY_VERTEX_OUTPUT_STEREO.");
        Assert.IsTrue(
            Regex.IsMatch(source, @"\bUNITY_SETUP_INSTANCE_ID\s*\("),
            $"Stereo vertex entry '{owner}' is missing UNITY_SETUP_INSTANCE_ID(...).");
        Assert.IsTrue(
            Regex.IsMatch(source, @"\bUNITY_INITIALIZE_VERTEX_OUTPUT_STEREO\s*\("),
            $"Stereo vertex entry '{owner}' is missing UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(...).");
    }

    static void AssertVertexFunctionExists(string source, string entryPoint, string owner)
    {
        string functionPattern =
            $@"\b[A-Za-z_][A-Za-z0-9_]*(?:\s*<[^>]+>)?\s+{Regex.Escape(entryPoint)}\s*\(";

        Assert.IsTrue(
            Regex.IsMatch(source, functionPattern),
            $"Shader entry '{owner}' does not resolve to a package-local vertex function.");
    }

    static string ExpandPackageIncludes(
        string source,
        string ownerDirectory,
        string packageRoot,
        HashSet<string> visited)
    {
        var effectiveSource = new StringBuilder(source);
        MatchCollection includes = IncludePattern.Matches(source);

        for (int includeIndex = 0; includeIndex < includes.Count; ++includeIndex)
        {
            string includePath = includes[includeIndex].Groups["path"].Value;
            string resolvedPath = ResolvePackageInclude(includePath, ownerDirectory, packageRoot);

            if (resolvedPath == null || !visited.Add(resolvedPath))
                continue;

            string includeSource = StripComments(File.ReadAllText(resolvedPath));
            effectiveSource.AppendLine();
            effectiveSource.Append(ExpandPackageIncludes(
                includeSource,
                Path.GetDirectoryName(resolvedPath),
                packageRoot,
                visited));
        }

        return effectiveSource.ToString();
    }

    static string ResolvePackageInclude(string includePath, string ownerDirectory, string packageRoot)
    {
        string normalizedInclude = includePath.Replace('\\', '/');
        string candidate;

        if (normalizedInclude.StartsWith(PackageIncludePrefix, StringComparison.Ordinal))
        {
            candidate = Path.Combine(
                packageRoot,
                normalizedInclude.Substring(PackageIncludePrefix.Length));
        }
        else if (normalizedInclude.StartsWith(PackageAssetRoot + "/", StringComparison.Ordinal))
        {
            candidate = Path.Combine(GetProjectRoot(), normalizedInclude);
        }
        else
        {
            candidate = Path.Combine(ownerDirectory, normalizedInclude);
        }

        string fullPath = Path.GetFullPath(candidate);
        string rootPrefix = packageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return null;

        return fullPath;
    }

    static bool Includes(string source, string includePath)
    {
        MatchCollection includes = IncludePattern.Matches(source);

        for (int i = 0; i < includes.Count; ++i)
        {
            if (string.Equals(includes[i].Groups["path"].Value, includePath, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static string StripComments(string source)
    {
        source = Regex.Replace(
            source,
            @"/\*.*?\*/",
            match => PreserveLineBreaks(match.Value),
            RegexOptions.Singleline);
        return Regex.Replace(source, @"//[^\r\n]*", string.Empty);
    }

    static string PreserveLineBreaks(string value)
    {
        char[] characters = value.ToCharArray();

        for (int i = 0; i < characters.Length; ++i)
        {
            if (characters[i] != '\r' && characters[i] != '\n')
                characters[i] = ' ';
        }

        return new string(characters);
    }

    static string GetPackageRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "NowUI"));
    }

    static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    static string AssetPathToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(GetProjectRoot(), assetPath));
    }

    static string ToAssetPath(string fullPath)
    {
        string projectRoot = GetProjectRoot().TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(fullPath);

        if (!normalizedPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            return normalizedPath.Replace('\\', '/');

        return normalizedPath.Substring(projectRoot.Length).Replace('\\', '/');
    }
}

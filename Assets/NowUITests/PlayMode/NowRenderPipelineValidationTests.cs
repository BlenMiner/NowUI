using System;
using NUnit.Framework;

/// <summary>
/// Turns optional render-pipeline compilation into an explicit CI assertion.
/// Normal project test runs pass without requiring either optional package;
/// the pipeline workflow sets NOWUI_EXPECT_RENDER_PIPELINE and must load both
/// the Unity pipeline runtime and NowUI's matching integration assembly.
/// </summary>
public sealed class NowRenderPipelineValidationTests
{
    [Test]
    [Category("RenderPipelineValidation")]
    public void ExpectedOptionalRenderPipelineIntegrationIsLoaded()
    {
        string expected = Environment.GetEnvironmentVariable("NOWUI_EXPECT_RENDER_PIPELINE");

        if (string.IsNullOrWhiteSpace(expected))
        {
            Assert.Pass("No optional render pipeline was requested for this test run.");
            return;
        }

        expected = expected.Trim().ToLowerInvariant();

        switch (expected)
        {
            case "urp":
                AssertTypeIsLoaded(
                    "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " +
                    "Unity.RenderPipelines.Universal.Runtime",
                    "The URP package did not load.");
                AssertTypeIsLoaded(
                    "NowUI.NowUniversalRendererFeature, NowUI.URP",
                    "NowUI.URP did not compile and load against the installed URP package.");
                break;

            case "hdrp":
                AssertTypeIsLoaded(
                    "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, " +
                    "Unity.RenderPipelines.HighDefinition.Runtime",
                    "The HDRP package did not load.");
                AssertTypeIsLoaded(
                    "NowUI.NowHighDefinitionCustomPass, NowUI.HDRP",
                    "NowUI.HDRP did not compile and load against the installed HDRP package.");
                break;

            default:
                Assert.Fail(
                    $"Unsupported NOWUI_EXPECT_RENDER_PIPELINE value '{expected}'. " +
                    "Expected 'urp' or 'hdrp'.");
                break;
        }
    }

    static void AssertTypeIsLoaded(string assemblyQualifiedName, string message)
    {
        Assert.NotNull(Type.GetType(assemblyQualifiedName, throwOnError: false), message);
    }
}

using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

public sealed class RuntimeReelRenderingTests
{
    [Test]
    public void OasisReelLampShader_IsAvailableAndSupported()
    {
        var shader = Shader.Find("Oasis/ReelLamp");
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
    }

    [Test]
    public void CreateMaterial_UsesCustomReelLampShader()
    {
        var reel = Reel();
        var machine = Machine();
        var material = RuntimeReelRenderer.CreateMaterial(machine, reel);
        try
        {
            Assert.IsNotNull(material);
            Assert.AreEqual("Oasis/ReelLamp", material.shader.name);
            Assert.AreSame(reel.BandTexture.Texture, material.mainTexture);
            Assert.IsEmpty(machine.Warnings);
        }
        finally
        {
            if (material != null) Object.DestroyImmediate(material);
            reel.BandTexture.Unload();
        }
    }

    [Test]
    public void Configure_DerivesVerticalCentersFromStopCountAndMaskProperties()
    {
        var reel = Reel();
        var mask = new Texture2D(2, 2);
        reel.TransmissionMaskTexture = new RuntimeTextureAsset("mask", mask);
        reel.reelLamps = new[]
        {
            new FaceRuntimeReelLampManifestEntry { position = "top", lampId = 1, localVerticalCenter = 0.12f, radius = 0.2f, intensity = 0.8f },
            new FaceRuntimeReelLampManifestEntry { position = "middle", lampId = 2, localVerticalCenter = 0.47f, radius = 0.3f, intensity = 0.9f },
            new FaceRuntimeReelLampManifestEntry { position = "bottom", lampId = 3, localVerticalCenter = 0.91f, radius = 0.4f, intensity = 1.1f }
        };
        var go = new GameObject("reel");
        var renderer = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Oasis/ReelLamp"));
        try
        {
            var binding = new RuntimeReelRenderBinding(go, material, renderer, reel);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var centers = block.GetVector(RuntimeFaceShaderProperties.ReelLampVerticalCenters);
            Assert.AreEqual(0.6545f, centers.x, 0.0001f);
            Assert.AreEqual(0.5f, centers.y, 0.0001f);
            Assert.AreEqual(0.3455f, centers.z, 0.0001f);
            var radii = block.GetVector(RuntimeFaceShaderProperties.ReelLampRadii);
            Assert.AreEqual(0.2f, radii.x, 0.0001f);
            Assert.AreEqual(0.3f, radii.y, 0.0001f);
            Assert.AreEqual(0.4f, radii.z, 0.0001f);
            Assert.AreEqual(1f, block.GetFloat(RuntimeFaceShaderProperties.ReelTransmissionMaskEnabled), 0.0001f);
            Assert.AreSame(mask, block.GetTexture(RuntimeFaceShaderProperties.ReelTransmissionMaskTexture));
            binding.Dispose();
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (material != null) Object.DestroyImmediate(material);
            reel.BandTexture.Unload();
            reel.TransmissionMaskTexture.Unload();
        }
    }

    [Test]
    public void NegativeLampSentinelRemainsOffAndLogicalZeroIsSupported()
    {
        var reel = Reel();
        reel.reelLamps = new[]
        {
            new FaceRuntimeReelLampManifestEntry { lampId = -1, localVerticalCenter = 0.2f, radius = 0.4f, intensity = 1f },
            new FaceRuntimeReelLampManifestEntry { lampId = 0, localVerticalCenter = 0.5f, radius = 0.4f, intensity = 1f },
            new FaceRuntimeReelLampManifestEntry { lampId = 1, localVerticalCenter = 0.8f, radius = 0.4f, intensity = 1f }
        };
        var go = new GameObject("reel");
        var renderer = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Oasis/ReelLamp"));
        try
        {
            var binding = new RuntimeReelRenderBinding(go, material, renderer, reel);
            var lampState = new RuntimeLampState();
            Assert.IsTrue(lampState.IsValidLampNumber(0));
            lampState.SetBrightness(0, 0.25f);
            lampState.SetBrightness(1, 0.5f);
            Assert.IsTrue(binding.ApplyLampState(lampState));
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var brightness = block.GetVector(RuntimeFaceShaderProperties.ReelLampBrightness);
            Assert.AreEqual(0f, brightness.x, 0.0001f);
            Assert.AreEqual(0.25f, brightness.y, 0.0001f);
            Assert.AreEqual(0.5f, brightness.z, 0.0001f);
            binding.Dispose();
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (material != null) Object.DestroyImmediate(material);
            reel.BandTexture.Unload();
        }
    }


    [Test]
    public void DisabledReelLampsKeepAssignmentsButBrightnessStaysOff()
    {
        var reel = Reel();
        reel.reelLampsEnabled = false;
        reel.reelLamps = new[]
        {
            new FaceRuntimeReelLampManifestEntry { lampId = 5, localVerticalCenter = 0.2f, radius = 0.4f, intensity = 1f },
            new FaceRuntimeReelLampManifestEntry { lampId = 4, localVerticalCenter = 0.5f, radius = 0.4f, intensity = 1f },
            new FaceRuntimeReelLampManifestEntry { lampId = 3, localVerticalCenter = 0.8f, radius = 0.4f, intensity = 1f }
        };
        var go = new GameObject("reel");
        var renderer = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Oasis/ReelLamp"));
        try
        {
            var binding = new RuntimeReelRenderBinding(go, material, renderer, reel);
            var lampState = new RuntimeLampState();
            lampState.SetBrightness(5, 1f);
            lampState.SetBrightness(4, 1f);
            lampState.SetBrightness(3, 1f);
            Assert.IsFalse(binding.ApplyLampState(lampState));
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(Vector4.zero, block.GetVector(RuntimeFaceShaderProperties.ReelLampBrightness));
            binding.Dispose();
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (material != null) Object.DestroyImmediate(material);
            reel.BandTexture.Unload();
        }
    }

    [Test]
    public void LampBrightnessUpdate_DoesNotRecreateMaterial()
    {
        var reel = Reel();
        reel.reelLamps = new[] { new FaceRuntimeReelLampManifestEntry { lampId = 1, localVerticalCenter = 0.2f, radius = 0.4f, intensity = 1f } };
        var go = new GameObject("reel");
        var renderer = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Oasis/ReelLamp"));
        renderer.sharedMaterial = material;
        try
        {
            var binding = new RuntimeReelRenderBinding(go, material, renderer, reel);
            var before = renderer.sharedMaterial;
            var lampState = new RuntimeLampState();
            lampState.SetBrightness(1, 0.25f);
            Assert.IsTrue(binding.ApplyLampState(lampState));
            Assert.AreSame(before, renderer.sharedMaterial);
            binding.Dispose();
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (material != null) Object.DestroyImmediate(material);
            reel.BandTexture.Unload();
        }
    }

    [Test]
    public void DerivedVerticalCenters_MatchProjectedStopCounts()
    {
        AssertCenters(12, 0.75f, 0.5f, 0.25f);
        AssertCenters(16, 0.6913f, 0.5f, 0.3087f);
        AssertCenters(25, 0.6243f, 0.5f, 0.3757f);
    }

    [Test]
    public void AspectCorrection_EqualPhysicalDistancesHaveEqualFieldStrength()
    {
        var fieldA = RuntimeReelLampGeometry.EvaluateField(new Vector2(0.5f + 0.6f, 0.5f), 0.5f, 0.2f, 70f, 280f);
        var fieldB = RuntimeReelLampGeometry.EvaluateField(new Vector2(0.5f, 0.5f + 0.15f), 0.5f, 0.2f, 70f, 280f);
        Assert.AreEqual(fieldA, fieldB, 0.0001f);

        var squareA = RuntimeReelLampGeometry.EvaluateField(new Vector2(0.6f, 0.5f), 0.5f, 0.15f, 100f, 100f);
        var squareB = RuntimeReelLampGeometry.EvaluateField(new Vector2(0.5f, 0.6f), 0.5f, 0.15f, 100f, 100f);
        Assert.AreEqual(squareA, squareB, 0.0001f);
    }

    [Test]
    public void AutomaticRadius_IsStopDerivedAndPrimarilyIlluminatesOneSymbol()
    {
        var centers = RuntimeReelLampGeometry.DeriveVerticalCenters(12);
        var radius = RuntimeReelLampGeometry.DeriveAutomaticRadius(12);
        var own = RuntimeReelLampGeometry.EvaluateField(new Vector2(0.5f, centers.x), centers.x, radius, 70f, 190f);
        var middle = RuntimeReelLampGeometry.EvaluateField(new Vector2(0.5f, centers.y), centers.x, radius, 70f, 190f);
        Assert.Greater(own, 0.95f);
        Assert.Less(middle, 0.05f);
    }

    [Test]
    public void AutomaticRadius_MatchesStopCountsAndDecreasesWithMoreStops()
    {
        var twelve = RuntimeReelLampGeometry.DeriveAutomaticRadius(12);
        var sixteen = RuntimeReelLampGeometry.DeriveAutomaticRadius(16);
        var twentyFive = RuntimeReelLampGeometry.DeriveAutomaticRadius(25);
        Assert.AreEqual(0.1875f, twelve, 0.000001f);
        Assert.AreEqual(0.143506f, sixteen, 0.000001f);
        Assert.AreEqual(0.093259f, twentyFive, 0.000001f);
        Assert.Greater(twelve, sixteen);
        Assert.Greater(sixteen, twentyFive);
    }

    [Test]
    public void ResolveRadius_NonPositiveIsAutomaticAndPositiveIsExactOverride()
    {
        var automatic = RuntimeReelLampGeometry.DeriveAutomaticRadius(16);
        Assert.AreEqual(automatic, RuntimeReelLampGeometry.ResolveRadius(0f, 16));
        Assert.AreEqual(automatic, RuntimeReelLampGeometry.ResolveRadius(-1f, 16));
        Assert.AreEqual(0.234f, RuntimeReelLampGeometry.ResolveRadius(0.234f, 16));
    }

    [Test]
    public void Configure_ZeroAndNegativeRadiiUseAutomaticWhilePositiveOverrides()
    {
        var reel = Reel();
        reel.stops = 16;
        reel.reelLamps = new[]
        {
            new FaceRuntimeReelLampManifestEntry { radius = 0f, intensity = 1f },
            new FaceRuntimeReelLampManifestEntry { radius = -0.5f, intensity = 1f },
            new FaceRuntimeReelLampManifestEntry { radius = 0.2f, intensity = 1f }
        };
        var go = new GameObject("reel");
        var renderer = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Oasis/ReelLamp"));
        try
        {
            var binding = new RuntimeReelRenderBinding(go, material, renderer, reel);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var radii = block.GetVector(RuntimeFaceShaderProperties.ReelLampRadii);
            Assert.AreEqual(0.143506f, radii.x, 0.000001f);
            Assert.AreEqual(0.143506f, radii.y, 0.000001f);
            Assert.AreEqual(0.2f, radii.z, 0.000001f);
            binding.Dispose();
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (material != null) Object.DestroyImmediate(material);
            reel.BandTexture.Unload();
        }
    }

    [Test]
    public void ChangingStopCountChangesCentersAndAutomaticRadius()
    {
        Assert.AreNotEqual(RuntimeReelLampGeometry.DeriveVerticalCenters(12), RuntimeReelLampGeometry.DeriveVerticalCenters(25));
        Assert.AreNotEqual(RuntimeReelLampGeometry.DeriveAutomaticRadius(12), RuntimeReelLampGeometry.DeriveAutomaticRadius(25));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [TestCase(RuntimeReelLampDiagnosticMode.ForceTop, 8f, 8f, 0f, 0f)]
    [TestCase(RuntimeReelLampDiagnosticMode.ForceMiddle, 8f, 0f, 8f, 0f)]
    [TestCase(RuntimeReelLampDiagnosticMode.ForceBottom, 8f, 0f, 0f, 8f)]
    public void ReelLampDiagnostics_ForcedModesSelectChannelAndApplyMultiplier(RuntimeReelLampDiagnosticMode mode, float multiplier, float top, float middle, float bottom)
    {
        var result = RuntimeReelLampDiagnostics.SelectBrightness(new Vector4(0.2f, 0.3f, 0.4f, 0f), mode, multiplier);
        Assert.AreEqual(new Vector4(top, middle, bottom, 0f), result);
    }

    [Test]
    public void ReelLampDiagnostics_FollowStateDefaultsToUnmodifiedBrightness()
    {
        var brightness = new Vector4(0.2f, 0.3f, 0.4f, 0f);
        Assert.AreEqual(brightness, RuntimeReelLampDiagnostics.SelectBrightness(brightness, RuntimeReelLampDiagnosticMode.FollowLampState, 1f));
    }
#endif

    [Test]
    public void ApertureProperties_DoNotUseBandOffset()
    {
        var reel = Reel();
        reel.bandOffset = 0.25f;
        var go = new GameObject("reel");
        var renderer = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Oasis/ReelLamp"));
        try
        {
            var binding = new RuntimeReelRenderBinding(go, material, renderer, reel);
            binding.ConfigureAperture(new Vector3(1f, 2f, 3f), Vector3.right, Vector3.up, 0.07f, 0.19f);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 0f), block.GetVector(RuntimeFaceShaderProperties.ReelApertureCenterWS));
            Assert.AreEqual(new Vector4(0.07f, 0.19f, 0f, 0f), block.GetVector(RuntimeFaceShaderProperties.ReelApertureSize));
            binding.Dispose();
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (material != null) Object.DestroyImmediate(material);
            reel.BandTexture.Unload();
        }
    }

    private static void AssertCenters(int stops, float top, float middle, float bottom)
    {
        var centers = RuntimeReelLampGeometry.DeriveVerticalCenters(stops);
        Assert.AreEqual(top, centers.x, 0.001f);
        Assert.AreEqual(middle, centers.y, 0.0001f);
        Assert.AreEqual(bottom, centers.z, 0.001f);
    }

    private static FaceRuntimeReelManifestEntry Reel()
    {
        return new FaceRuntimeReelManifestEntry
        {
            objectId = "reel", machineReference = "reel", reelBand = "band", stops = 20,
            physicalWidth = 50f, physicalRadius = 105f, BandTexture = new RuntimeTextureAsset("band", new Texture2D(2, 2))
        };
    }

    private static RuntimeMachine Machine()
    {
        return new RuntimeMachine(new ResolvedRuntimeBuild(string.Empty, new MachineRuntimeManifest(), string.Empty, new CabinetRuntimeManifest(), string.Empty, new MachineRuntimeFaceReference[0]), null);
    }
}

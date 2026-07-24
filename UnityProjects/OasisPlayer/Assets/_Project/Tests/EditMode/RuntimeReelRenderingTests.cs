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
    public void Configure_WritesAllThreeImportedVerticalCentersAndMaskProperties()
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
            Assert.AreEqual(0.12f, centers.x, 0.0001f);
            Assert.AreEqual(0.47f, centers.y, 0.0001f);
            Assert.AreEqual(0.91f, centers.z, 0.0001f);
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
    public void LampSentinelAndInvalidZeroRemainOffForOneBasedLampState()
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
            Assert.IsFalse(lampState.IsValidLampNumber(0));
            lampState.SetBrightness(1, 0.5f);
            Assert.IsTrue(binding.ApplyLampState(lampState));
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var brightness = block.GetVector(RuntimeFaceShaderProperties.ReelLampBrightness);
            Assert.AreEqual(0f, brightness.x, 0.0001f);
            Assert.AreEqual(0f, brightness.y, 0.0001f);
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
    public void FixedWindowUv_IsIndependentOfWorldTransforms()
    {
        var bandUv = new Vector2(0.25f, 0.75f);
        var offset = RuntimeReelShaderCoordinateHelper.ToWindowUvOffset(24f, false, 0f);
        var a = RuntimeReelShaderCoordinateHelper.ToFixedWindowUv(bandUv, offset);
        var b = RuntimeReelShaderCoordinateHelper.ToFixedWindowUv(bandUv, offset);
        Assert.AreEqual(a, b);
        Assert.AreEqual(0.25f, a.x, 0.0001f);
        Assert.AreEqual(0.5f, a.y, 0.0001f);
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

using System.IO;
using System.Linq;
using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeBuildLoaderManifestTests
    {
        [Test]
        public void FaceRuntimeContractUsesSchema10WithoutObsoleteCabinetReelTargetId()
        {
            Assert.AreEqual(10, FaceRuntimeContract.SchemaVersion);
            Assert.True(FaceRuntimeContract.IsSupportedSchemaVersion(10));
            Assert.False(FaceRuntimeContract.IsSupportedSchemaVersion(9));
            var json = JsonUtility.ToJson(new FaceRuntimeManifest
            {
                schemaVersion = FaceRuntimeContract.SchemaVersion,
                reels = new[]
                {
                    new FaceRuntimeReelManifestEntry
                    {
                        objectId = "mount-3",
                        machineReference = "reel:3",
                        reelBand = "reels/mount-3.png",
                        stops = 20
                    }
                }
            });

            StringAssert.Contains("\"schemaVersion\":10", json);
            StringAssert.Contains("\"objectId\":\"mount-3\"", json);
            StringAssert.Contains("\"machineReference\":\"reel:3\"", json);
            StringAssert.DoesNotContain("cabinetReelTargetId", json);
        }

        [TestCase(RuntimeFaceFrontSideExtensions.NormalValue, RuntimeFaceFrontSide.Normal, false)]
        [TestCase(RuntimeFaceFrontSideExtensions.InvertedValue, RuntimeFaceFrontSide.Inverted, true)]
        public void MachineRuntimeJsonLoadsFrontSide(string frontSide, RuntimeFaceFrontSide expected, bool expectedInverted)
        {
            var root = Path.Combine(Application.temporaryCachePath, "OasisRuntimeBuildLoaderTests", System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "cabinet"));
                File.WriteAllBytes(Path.Combine(root, "cabinet", "cabinet.glb"), new byte[] { 1, 2, 3 });
                File.WriteAllText(Path.Combine(root, "cabinet", "cabinet.runtime.json"), "{\"schema\":\"oasis.cabinet.runtime\",\"schemaVersion\":5,\"cabinetId\":\"cabinet\",\"glb\":\"cabinet.glb\",\"scale\":1,\"upAxis\":\"Y\",\"reflections\":[]}");
                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":5,\"machineId\":\"machine\",\"displayName\":\"machine\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"runtime\":{\"kind\":\"Emulation\",\"platform\":\"None\",\"executionSupportedByPlayer\":false,\"platformSettingsJson\":\"{\\\"programRom1Path\\\":\\\"game.bin\\\"}\"},\"inputs\":[],\"faces\":[{\"faceId\":\"face\",\"assetName\":\"Face\",\"cabinetFaceTargetId\":\"target\",\"frontSide\":\"" + frontSide + "\",\"faceRotation\":90,\"faceFlipHorizontal\":true,\"manifest\":\"faces/Face/face.runtime.json\"}]}");

                Assert.True(RuntimeBuildLoader.TryLoad(root, out var build, out var error), error);
                Assert.AreEqual("{\"programRom1Path\":\"game.bin\"}", build.Machine.runtime.platformSettingsJson);
                Assert.False(build.Machine.runtime.executionSupportedByPlayer);
                var reference = build.Faces.Single();

                Assert.AreEqual(frontSide, reference.frontSide);
                Assert.AreEqual(expected, RuntimeFaceFrontSideExtensions.Parse(reference.frontSide));
                Assert.AreEqual(expectedInverted, reference.IsInverted());
                Assert.AreEqual(90, reference.faceRotation);
                Assert.True(reference.faceFlipHorizontal);

                var target = new GameObject("OasisFace_Target");
                var artworkTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                var maskTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                try
                {
                    var runtimeFace = new RuntimeFace(
                        reference,
                        new FaceRuntimeManifest { schemaVersion = 2, faceId = "face", width = 1, height = 1, artwork = "artwork.png", mask = "mask.png" },
                        target.transform,
                        new RuntimeTextureAsset("artwork.png", artworkTexture),
                        new RuntimeTextureAsset("mask.png", maskTexture));

                    Assert.AreSame(reference, runtimeFace.Reference);
                    Assert.AreEqual(expectedInverted, runtimeFace.Reference.IsInverted());
                    Assert.AreEqual(90, runtimeFace.Reference.faceRotation);
                    Assert.True(runtimeFace.Reference.faceFlipHorizontal);
                }
                finally
                {
                    Object.DestroyImmediate(target);
                    Object.DestroyImmediate(artworkTexture);
                    Object.DestroyImmediate(maskTexture);
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}

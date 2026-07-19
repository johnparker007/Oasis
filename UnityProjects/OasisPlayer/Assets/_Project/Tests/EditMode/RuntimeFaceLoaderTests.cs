using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeFaceLoaderTests
    {
        [Test]
        public void DeclaredLookupTexturesLoadWithLookupDataRole()
        {
            var root = CreateBuildRoot();
            var cabinet = CreateCabinetTarget("OasisFace_Front");
            var assetLoader = new RecordingTextureAssetLoader();
            try
            {
                WriteFaceManifest(root, "faces/front/face.runtime.json", true, true);
                var machine = CreateMachine(root, cabinet, "faces/front/face.runtime.json");

                new RuntimeFaceLoader(assetLoader).LoadFaces(machine);

                Assert.AreEqual(1, machine.Faces.Count);
                Assert.NotNull(machine.Faces[0].TrayId);
                Assert.NotNull(machine.Faces[0].LampIds0);
                Assert.NotNull(machine.Faces[0].LampWeights0);
                Assert.AreEqual(RuntimeTextureRole.Artwork, assetLoader.Roles[Path.Combine(root, "faces/front/artwork.png")]);
                Assert.AreEqual(RuntimeTextureRole.Mask, assetLoader.Roles[Path.Combine(root, "faces/front/mask.png")]);
                Assert.AreEqual(RuntimeTextureRole.LookupData, assetLoader.Roles[Path.Combine(root, "faces/front/trayId.png")]);
                Assert.AreEqual(RuntimeTextureRole.LookupData, assetLoader.Roles[Path.Combine(root, "faces/front/lampIds0.png")]);
                Assert.AreEqual(RuntimeTextureRole.LookupData, assetLoader.Roles[Path.Combine(root, "faces/front/lampWeights0.png")]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cabinet);
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void MissingDeclaredLookupTextureWarnsButRegistersFace()
        {
            var root = CreateBuildRoot();
            var cabinet = CreateCabinetTarget("OasisFace_Front");
            var assetLoader = new RecordingTextureAssetLoader();
            try
            {
                WriteFaceManifest(root, "faces/front/face.runtime.json", true, true);
                assetLoader.FailPaths.Add(Path.Combine(root, "faces/front/lampWeights0.png"));
                var machine = CreateMachine(root, cabinet, "faces/front/face.runtime.json");

                new RuntimeFaceLoader(assetLoader).LoadFaces(machine);

                Assert.AreEqual(1, machine.Faces.Count);
                Assert.Null(machine.Faces[0].LampWeights0);
                Assert.That(machine.Warnings, Has.Some.Contains("lamp weights 0 texture"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cabinet);
                Directory.Delete(root, true);
            }
        }

        private static string CreateBuildRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisPlayerFaceLoaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static GameObject CreateCabinetTarget(string name)
        {
            var cabinet = new GameObject("Cabinet");
            var target = new GameObject(name);
            target.transform.SetParent(cabinet.transform);
            return cabinet;
        }

        private static RuntimeMachine CreateMachine(string root, GameObject cabinet, string manifestPath)
        {
            var reference = new MachineRuntimeFaceReference { faceId = "front", cabinetFaceTargetId = "front", manifest = manifestPath };
            var build = new ResolvedRuntimeBuild(
                root,
                new MachineRuntimeManifest { schema = RuntimeBuildLoader.MachineSchema, schemaVersion = 1, faces = new[] { reference } },
                string.Empty,
                new CabinetRuntimeManifest(),
                string.Empty,
                new[] { reference });
            return new RuntimeMachine(build, cabinet);
        }

        private static void WriteFaceManifest(string root, string relativePath, bool includeMask, bool includeLookups)
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var mask = includeMask ? "mask.png" : string.Empty;
            var lookups = includeLookups ? @",
  ""trayId"": ""trayId.png"",
  ""lampIds0"": ""lampIds0.png"",
  ""lampWeights0"": ""lampWeights0.png""" : string.Empty;
            File.WriteAllText(path, "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"faceId\": \"front\",\n" +
                "  \"width\": 1,\n" +
                "  \"height\": 1,\n" +
                "  \"artwork\": \"artwork.png\",\n" +
                "  \"mask\": \"" + mask + "\"" + lookups + "\n" +
                "}");
        }

        private sealed class RecordingTextureAssetLoader : IRuntimeTextureAssetLoader
        {
            public readonly Dictionary<string, RuntimeTextureRole> Roles = new Dictionary<string, RuntimeTextureRole>(StringComparer.Ordinal);
            public readonly HashSet<string> FailPaths = new HashSet<string>(StringComparer.Ordinal);

            public bool TryLoad(string path, RuntimeTextureRole role, out RuntimeTextureAsset asset, out string error)
            {
                Roles[path] = role;
                if (FailPaths.Contains(path))
                {
                    asset = null;
                    error = "configured missing texture";
                    return false;
                }

                asset = new RuntimeTextureAsset(path, new Texture2D(1, 1, TextureFormat.RGBA32, false, role == RuntimeTextureRole.LookupData));
                error = string.Empty;
                return true;
            }
        }
    }
}

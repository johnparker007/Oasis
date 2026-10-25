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
        public void StrictReaderPreservesEditorExportedInputMetadataAndNullableCoinValues()
        {
            var manifest = RuntimeManifestJsonReader.ReadMachine("{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":12,\"interaction\":{\"kind\":\"Buttons\"},\"machineId\":\"machine\",\"displayName\":\"Pool\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"anchors\":[],\"runtime\":{\"kind\":\"Emulation\",\"platform\":\"None\",\"platformSettingsJson\":\"{}\",\"behavior\":null},\"inputs\":[{\"id\":\"coin\",\"kind\":2,\"buttonNumber\":\"3\",\"coinInput\":true,\"coinChannel\":2,\"coinValue\":null,\"inverted\":false,\"rawMfmeShortcut\":\"1\",\"keyboardShortcut\":\"D1\",\"linkedVisualElementId\":null,\"notes\":\"Imported\"}]}");
            Assert.IsNull(manifest.runtime.behavior);
            Assert.AreEqual(2, manifest.inputs[0].kind);
            Assert.AreEqual(2, manifest.inputs[0].coinChannel);
            Assert.IsNull(manifest.inputs[0].coinValue);
            Assert.AreEqual("D1", manifest.inputs[0].keyboardShortcut);
            Assert.AreEqual("Imported", manifest.inputs[0].notes);
        }
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
                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":12,\"interaction\":{\"kind\":\"Buttons\"},\"machineId\":\"machine\",\"displayName\":\"machine\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"runtime\":{\"kind\":\"Emulation\",\"platform\":\"None\",\"platformSettingsJson\":\"{\\\"programRom1Path\\\":\\\"game.bin\\\"}\",\"behavior\":null},\"inputs\":[],\"anchors\":[],\"faces\":[{\"faceId\":\"face\",\"assetName\":\"Face\",\"cabinetFaceTargetId\":\"target\",\"frontSide\":\"" + frontSide + "\",\"faceRotation\":90,\"faceFlipHorizontal\":true,\"manifest\":\"faces/Face/face.runtime.json\"}]}");

                Assert.True(RuntimeBuildLoader.TryLoad(root, out var build, out var error), error);
                Assert.AreEqual("{\"programRom1Path\":\"game.bin\"}", build.Machine.runtime.platformSettingsJson);
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

        [TestCase(11, "Emulation")]
        [TestCase(12, "Scripted")]
        public void MachineRuntimeJsonRejectsObsoleteSchemaAndUnsupportedRuntime(int schemaVersion, string kind)
        {
            var root = Path.Combine(Application.temporaryCachePath, "OasisRuntimeBuildLoaderTests", System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "cabinet"));
                File.WriteAllBytes(Path.Combine(root, "cabinet", "cabinet.glb"), new byte[] { 1 });
                File.WriteAllText(Path.Combine(root, "cabinet", "cabinet.runtime.json"), "{\"schema\":\"oasis.cabinet.runtime\",\"schemaVersion\":5,\"cabinetId\":\"cabinet\",\"glb\":\"cabinet.glb\",\"scale\":1,\"upAxis\":\"Y\",\"reflections\":[]}");
                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":" + schemaVersion + ",\"interaction\":{\"kind\":\"Buttons\"},\"machineId\":\"machine\",\"displayName\":\"machine\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"runtime\":{\"kind\":\"" + kind + "\",\"platform\":\"Impact\",\"platformSettingsJson\":\"{}\"},\"inputs\":[],\"anchors\":[],\"faces\":[]}");

                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var error));
                StringAssert.Contains(schemaVersion == 11 ? "Unsupported machine manifest" : "Unknown Machine runtime kind", error);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase("None", true)]
        [TestCase("Impact", true)]
        [TestCase("MPU5", true)]
        [TestCase("Epoch", true)]
        [TestCase("MPU3", true)]
        [TestCase("MaygayM1", true)]
        [TestCase("Scorpion4", true)]
        [TestCase("MPU4", false)]
        public void MachineRuntimeJsonAcceptsExactlyCurrentEmulationPlatforms(string platform, bool expected)
        {
            var root = Path.Combine(Application.temporaryCachePath, "OasisRuntimeBuildLoaderTests", System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "cabinet"));
                File.WriteAllBytes(Path.Combine(root, "cabinet", "cabinet.glb"), new byte[] { 1 });
                File.WriteAllText(Path.Combine(root, "cabinet", "cabinet.runtime.json"), "{\"schema\":\"oasis.cabinet.runtime\",\"schemaVersion\":5,\"cabinetId\":\"cabinet\",\"glb\":\"cabinet.glb\",\"scale\":1,\"upAxis\":\"Y\",\"reflections\":[]}");
                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":12,\"interaction\":{\"kind\":\"Buttons\"},\"machineId\":\"machine\",\"displayName\":\"machine\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"runtime\":{\"kind\":\"Emulation\",\"platform\":\"" + platform + "\",\"platformSettingsJson\":\"{}\",\"behavior\":null},\"inputs\":[],\"anchors\":[],\"faces\":[]}");

                Assert.AreEqual(expected, RuntimeBuildLoader.TryLoad(root, out _, out var error), error);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void MachineRuntimeJsonLoadsReusableObjectDefinitionWithoutInstantiatingIt()
        {
            var root = Path.Combine(Application.temporaryCachePath, "OasisRuntimeBuildLoaderTests", System.Guid.NewGuid().ToString("N"));
            const string id = "11111111-2222-3333-4444-555555555555";
            const string nodeId = "aaaaaaaa-2222-3333-4444-555555555555";
            const string transformId = "bbbbbbbb-2222-3333-4444-555555555555";
            const string modelId = "cccccccc-2222-3333-4444-555555555555";
            const string colliderId = "dddddddd-2222-3333-4444-555555555555";
            const string bodyId = "eeeeeeee-2222-3333-4444-555555555555";
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "cabinet"));
                Directory.CreateDirectory(Path.Combine(root, "objects", id));
                Directory.CreateDirectory(Path.Combine(root, "objects", id, "models"));
                File.WriteAllBytes(Path.Combine(root, "cabinet", "cabinet.glb"), new byte[] { 1 });
                File.WriteAllText(Path.Combine(root, "cabinet", "cabinet.runtime.json"), "{\"schema\":\"oasis.cabinet.runtime\",\"schemaVersion\":5,\"cabinetId\":\"cabinet\",\"glb\":\"cabinet.glb\",\"scale\":1,\"upAxis\":\"Y\",\"reflections\":[]}");
                File.WriteAllBytes(Path.Combine(root, "objects", id, "models", "ball.glb"), new byte[] { 2 });
                var objectJson = "{\"schema\":\"oasis.object3d.runtime\",\"schemaVersion\":2,\"definitionId\":\"" + id + "\",\"displayName\":\"Pool Ball\",\"root\":{" +
                    "\"id\":\"" + nodeId + "\",\"name\":\"Root\",\"components\":[" +
                    "{\"componentId\":\"" + transformId + "\",\"kind\":\"Transform\",\"contractVersion\":1,\"transform\":{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotationEulerDegrees\":{\"x\":0,\"y\":0,\"z\":0},\"scale\":{\"x\":1,\"y\":1,\"z\":1}}}," +
                    "{\"componentId\":\"" + modelId + "\",\"kind\":\"Model\",\"contractVersion\":1,\"model\":{\"path\":\"models/ball.glb\",\"scale\":0.5,\"upAxis\":\"Y\"}}," +
                    "{\"componentId\":\"" + colliderId + "\",\"kind\":\"Collider\",\"contractVersion\":1,\"collider\":{\"kind\":\"Sphere\",\"center\":[0,0,0],\"radius\":0.25,\"sourceFrame\":\"ModelLocal\",\"isTrigger\":false}}," +
                    "{\"componentId\":\"" + bodyId + "\",\"kind\":\"Rigidbody\",\"contractVersion\":1,\"rigidbody\":{\"mode\":\"Dynamic\",\"mass\":0.17,\"useGravity\":true}}],\"children\":[]}}";
                File.WriteAllText(Path.Combine(root, "objects", id, "object.runtime.json"), objectJson);
                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":12,\"interaction\":{\"kind\":\"Buttons\"},\"machineId\":\"machine\",\"displayName\":\"Pool\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"runtime\":{\"kind\":\"Emulation\",\"platform\":\"None\",\"platformSettingsJson\":\"{}\",\"behavior\":null},\"inputs\":[],\"anchors\":[],\"faces\":[],\"objectInstances\":[{\"id\":\"cueBall\",\"displayName\":\"Cue Ball\",\"definitionId\":\"" + id + "\",\"definitionManifest\":\"objects/" + id + "/object.runtime.json\",\"transform\":{\"position\":{\"x\":1,\"y\":2,\"z\":3},\"rotationEulerDegrees\":{\"x\":10,\"y\":20,\"z\":30},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"parentId\":null,\"active\":true}]}");

                Assert.True(RuntimeBuildLoader.TryLoad(root, out var build, out var error), error);
                Assert.AreEqual("cueBall", build.Machine.objectInstances.Single().id);
                Assert.AreEqual(20f, build.Machine.objectInstances.Single().transform.rotationEulerDegrees.y);
                Assert.True(build.Machine.objectInstances.Single().active);
                Assert.AreEqual("Pool Ball", build.ObjectDefinitions[id].Manifest.displayName);
                Assert.AreEqual("Sphere", build.ObjectDefinitions[id].Manifest.root.GetComponent("Collider").collider.kind);
                Assert.AreEqual(0.17f, build.ObjectDefinitions[id].Manifest.root.GetComponent("Rigidbody").rigidbody.mass, 0.0001f);
                Assert.AreEqual(Path.GetFullPath(Path.Combine(root, "objects", id, "models", "ball.glb")), build.ObjectDefinitions[id].ModelPaths["models/ball.glb"]);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase("\"parentId\":\"missing\"", "missing parent")]
        [TestCase("\"parentId\":\"a\"", "cannot parent itself")]
        public void MachineRuntimeJsonRejectsInvalidObjectParents(string parentFields, string expectedError)
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var instance = "{\"id\":\"a\",\"displayName\":\"A\",\"definitionId\":\"" + DefinitionId + "\",\"definitionManifest\":\"objects/" + DefinitionId + "/object.runtime.json\",\"transform\":" + IdentityTransformJson() + "," + parentFields + ",\"active\":true}";
                WriteObjectAndMachine(root, BasicObjectManifest(), "[" + instance + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var error));
                StringAssert.Contains(expectedError, error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void MachineRuntimeJsonRejectsObjectParentCycles()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var first = MachineInstanceJson("a", "b");
                var second = MachineInstanceJson("b", "a");
                WriteObjectAndMachine(root, BasicObjectManifest(), "[" + first + "," + second + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var error));
                StringAssert.Contains("parent hierarchy contains a cycle", error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void MachineRuntimeJsonRejectsDuplicateAndMalformedFields()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"machineId\":\"one\",\"machineId\":\"two\"}");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var duplicateError));
                StringAssert.Contains("Duplicate field 'machineId'", duplicateError);

                File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{ definitely not json }");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var malformedError));
                StringAssert.Contains("Machine manifest is invalid JSON", malformedError);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void MachineRuntimeJsonRejectsNestedActiveBodiesAcrossPlacedObjects()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var body = "{\"componentId\":\"" + BodyId + "\",\"kind\":\"Rigidbody\",\"contractVersion\":1,\"rigidbody\":{\"mode\":\"Dynamic\",\"mass\":1,\"useGravity\":true}}";
                var node = "{\"id\":\"" + NodeId + "\",\"name\":\"Root\",\"components\":[" + TransformComponentJson() + "," + body + "],\"children\":[]}";
                var parent = MachineInstanceJson("parent", null);
                var child = MachineInstanceJson("child", "parent");
                WriteObjectAndMachine(root, BasicObjectManifest(node), "[" + parent + "," + child + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var error));
                StringAssert.Contains("nested body ownership is unsupported", error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [TestCase(1, "Transform", "Unsupported or invalid Object3D manifest")]
        [TestCase(2, "Script", "unknown component kind")]
        public void ObjectRuntimeJsonRejectsObsoleteVersionAndUnknownComponent(int version, string kind, string expectedError)
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var components = kind == "Transform" ? TransformComponentJson() : "{\"componentId\":\"" + TransformId + "\",\"kind\":\"Script\",\"contractVersion\":1}";
                var manifest = BasicObjectManifest(components, "[]", version);
                WriteObjectAndMachine(root, manifest, "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var error));
                StringAssert.Contains(expectedError, error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void ObjectRuntimeJsonRejectsDuplicateNodeIdsAndMultipleActiveBodiesInSubtree()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var duplicateNode = "{\"id\":\"" + NodeId + "\",\"name\":\"Root\",\"components\":[" + TransformComponentJson() + "],\"children\":[{\"id\":\"" + NodeId + "\",\"name\":\"Child\",\"components\":[" + TransformComponentJson() + "],\"children\":[]}]}";
                WriteObjectAndMachine(root, BasicObjectManifest(duplicateNode), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var duplicateError));
                StringAssert.Contains("node ID must be a unique stable GUID", duplicateError);

                var body = "{\"componentId\":\"" + BodyId + "\",\"kind\":\"Rigidbody\",\"contractVersion\":1,\"rigidbody\":{\"mode\":\"Dynamic\",\"mass\":1,\"useGravity\":true}}";
                var childBody = "{\"componentId\":\"" + ChildBodyId + "\",\"kind\":\"Rigidbody\",\"contractVersion\":1,\"rigidbody\":{\"mode\":\"Dynamic\",\"mass\":1,\"useGravity\":true}}";
                var bodyNode = "{\"id\":\"" + NodeId + "\",\"name\":\"Root\",\"components\":[" + TransformComponentJson() + "," + body + "],\"children\":[{\"id\":\"" + ChildNodeId + "\",\"name\":\"Child\",\"components\":[" +
                    "{\"componentId\":\"" + ChildTransformId + "\",\"kind\":\"Transform\",\"contractVersion\":1,\"transform\":{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotationEulerDegrees\":{\"x\":0,\"y\":0,\"z\":0},\"scale\":{\"x\":1,\"y\":1,\"z\":1}}}," + childBody + "],\"children\":[]}]}";
                WriteObjectAndMachine(root, BasicObjectManifest(bodyNode), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var bodyError));
                StringAssert.Contains("only one non-Static Rigidbody", bodyError);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void ObjectRuntimeJsonRejectsModelPathOutsideDefinitionPackage()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var model = "{\"componentId\":\"" + ModelId + "\",\"kind\":\"Model\",\"contractVersion\":1,\"model\":{\"path\":\"../outside.glb\",\"scale\":1,\"upAxis\":\"Y\"}}";
                var node = "{\"id\":\"" + NodeId + "\",\"name\":\"Root\",\"components\":[" + TransformComponentJson() + "," + model + "],\"children\":[]}";
                WriteObjectAndMachine(root, BasicObjectManifest(node), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var error));
                StringAssert.Contains("path traversal outside the build root", error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void ObjectRuntimeJsonRejectsUnknownFieldsAndMismatchedComponentPayloads()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var rootNode = "{\"id\":\"" + NodeId + "\",\"name\":\"Root\",\"components\":[" + TransformComponentJson() + "],\"children\":[],\"surprise\":true}";
                WriteObjectAndMachine(root, BasicObjectManifest(rootNode), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var unknownError));
                StringAssert.Contains("unknown field 'surprise'", unknownError);

                var mismatched = TransformComponentJson().Replace("\"transform\":", "\"model\":{\"path\":\"unused.glb\",\"scale\":1,\"upAxis\":\"Y\"},\"transform\":");
                WriteObjectAndMachine(root, BasicObjectManifest(mismatched, "[]"), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var payloadError));
                StringAssert.Contains("payload must match its kind exactly", payloadError);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void ObjectRuntimeJsonRejectsMissingTransformVectorFields()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var transform = "{\"componentId\":\"" + TransformId + "\",\"kind\":\"Transform\",\"contractVersion\":1,\"transform\":{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotationEulerDegrees\":{\"x\":0,\"y\":0,\"z\":0}}}";
                WriteObjectAndMachine(root, BasicObjectManifest(transform, "[]"), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var error));
                StringAssert.Contains("required field 'scale' is missing", error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void ObjectRuntimeJsonRejectsNullAndOutOfRangeTransformValues()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                var nullVector = TransformComponentJson().Replace("\"position\":{\"x\":0,\"y\":0,\"z\":0}", "\"position\":null");
                WriteObjectAndMachine(root, BasicObjectManifest(nullVector, "[]"), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var nullError));
                StringAssert.Contains("Transform must have finite position/rotation and positive finite scale", nullError);

                var outOfRange = TransformComponentJson().Replace("\"x\":0", "\"x\":1e39");
                WriteObjectAndMachine(root, BasicObjectManifest(outOfRange, "[]"), "[" + MachineInstanceJson("root", null) + "]");
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out var rangeError));
                StringAssert.Contains("expected a finite number", rangeError);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private const string DefinitionId = "11111111-2222-3333-4444-555555555555";
        private const string NodeId = "aaaaaaaa-2222-3333-4444-555555555555";
        private const string ChildNodeId = "ffffffff-2222-3333-4444-555555555555";
        private const string TransformId = "bbbbbbbb-2222-3333-4444-555555555555";
        private const string ChildTransformId = "99999999-2222-3333-4444-555555555555";
        private const string ModelId = "cccccccc-2222-3333-4444-555555555555";
        private const string BodyId = "eeeeeeee-2222-3333-4444-555555555555";
        private const string ChildBodyId = "88888888-2222-3333-4444-555555555555";

        [Test]
        public void DeclaredOnlyPrefabIsResolvedWithoutAnyPlacedInstance()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                WriteObjectAndMachine(root, BasicObjectManifest(TransformComponentJson(), "[]"), "[]");
                var path = Path.Combine(root, "machine.runtime.json");
                var text = File.ReadAllText(path).Replace("\"objectInstances\":[]", "\"objectInstances\":[],\"prefabDependencies\":[{\"alias\":\"unplaced\",\"definitionId\":\"" + DefinitionId + "\",\"definitionManifest\":\"objects/" + DefinitionId + "/object.runtime.json\"}]");
                File.WriteAllText(path, text);
                Assert.True(RuntimeBuildLoader.TryLoad(root, out var build, out var error), error);
                Assert.AreEqual(0, build.Machine.objectInstances.Length);
                Assert.AreSame(build.ObjectDefinitions[DefinitionId], build.PrefabDependencies["unplaced"]);
                File.WriteAllText(path, text.Replace("\"alias\":\"unplaced\"", "\"alias\":\"bad alias\""));
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out error));
                StringAssert.Contains("alias", error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void GeometrySurfaceRequiresPositiveDimensionsAndFiniteFrame()
        {
            var root = NewMinimalBuildRoot();
            try
            {
                WriteObjectAndMachine(root, BasicObjectManifest(TransformComponentJson(), "[]"), "[]");
                var path = Path.Combine(root, "machine.runtime.json");
                var text = File.ReadAllText(path).Replace("\"objectInstances\":[]", "\"objectInstances\":[],\"geometrySurfaces\":[{\"id\":\"table\",\"position\":{\"x\":0,\"y\":0.8,\"z\":0},\"rotationEulerDegrees\":{\"x\":0,\"y\":15,\"z\":0},\"width\":0.78,\"length\":1.58}]");
                File.WriteAllText(path, text);
                Assert.True(RuntimeBuildLoader.TryLoad(root, out var build, out var error), error);
                Assert.AreEqual("table", build.Machine.geometrySurfaces[0].id);
                File.WriteAllText(path, text.Replace("\"width\":0.78", "\"width\":0"));
                Assert.False(RuntimeBuildLoader.TryLoad(root, out _, out error));
                StringAssert.Contains("Geometry surface", error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static string NewMinimalBuildRoot()
        {
            var root = Path.Combine(Application.temporaryCachePath, "OasisRuntimeBuildLoaderTests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "cabinet"));
            Directory.CreateDirectory(Path.Combine(root, "objects", DefinitionId));
            File.WriteAllBytes(Path.Combine(root, "cabinet", "cabinet.glb"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(root, "cabinet", "cabinet.runtime.json"), "{\"schema\":\"oasis.cabinet.runtime\",\"schemaVersion\":5,\"cabinetId\":\"cabinet\",\"glb\":\"cabinet.glb\",\"scale\":1,\"upAxis\":\"Y\",\"reflections\":[]}");
            return root;
        }

        private static void WriteObjectAndMachine(string root, string objectManifest, string objectInstances)
        {
            File.WriteAllText(Path.Combine(root, "objects", DefinitionId, "object.runtime.json"), objectManifest);
            File.WriteAllText(Path.Combine(root, "machine.runtime.json"), "{\"schema\":\"oasis.machine.runtime\",\"schemaVersion\":12,\"interaction\":{\"kind\":\"Buttons\"},\"machineId\":\"machine\",\"displayName\":\"Machine\",\"cabinetManifest\":\"cabinet/cabinet.runtime.json\",\"runtime\":{\"kind\":\"Emulation\",\"platform\":\"None\",\"platformSettingsJson\":\"{}\",\"behavior\":null},\"inputs\":[],\"anchors\":[],\"faces\":[],\"objectInstances\":" + objectInstances + "}");
        }

        private static string BasicObjectManifest() => BasicObjectManifest("{" + "\"id\":\"" + NodeId + "\",\"name\":\"Root\",\"components\":[" + TransformComponentJson() + "],\"children\":[]}");
        private static string BasicObjectManifest(string components, string children, int version = 2) => BasicObjectManifest("{" + "\"id\":\"" + NodeId + "\",\"name\":\"Root\",\"components\":[" + components + "],\"children\":" + children + "}", version);
        private static string BasicObjectManifest(string rootNode, int version = 2) => "{\"schema\":\"oasis.object3d.runtime\",\"schemaVersion\":" + version + ",\"definitionId\":\"" + DefinitionId + "\",\"displayName\":\"Test\",\"root\":" + rootNode + "}";
        private static string TransformComponentJson() => "{\"componentId\":\"" + TransformId + "\",\"kind\":\"Transform\",\"contractVersion\":1,\"transform\":{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotationEulerDegrees\":{\"x\":0,\"y\":0,\"z\":0},\"scale\":{\"x\":1,\"y\":1,\"z\":1}}}";
        private static string MachineInstanceJson(string id, string parentId) => "{\"id\":\"" + id + "\",\"displayName\":\"" + id + "\",\"definitionId\":\"" + DefinitionId + "\",\"definitionManifest\":\"objects/" + DefinitionId + "/object.runtime.json\",\"transform\":" + IdentityTransformJson() + ",\"parentId\":" + (parentId == null ? "null" : "\"" + parentId + "\"") + ",\"active\":true}";
        private static string IdentityTransformJson() => "{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotationEulerDegrees\":{\"x\":0,\"y\":0,\"z\":0},\"scale\":{\"x\":1,\"y\":1,\"z\":1}}";
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class ArcadeLayoutTests
    {
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<RuntimeMachine> _machines = new List<RuntimeMachine>();
        [TearDown] public void Cleanup()
        {
            foreach (var machine in _machines) machine.UnloadAssets();
            foreach (var root in _roots) if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [TestCase("../outside")] [TestCase("machines/../../outside")]
        [TestCase("C:/outside")] [TestCase("/outside")] [TestCase("machines//pool")]
        [TestCase("machines/./pool")] [TestCase("\\\\host\\share")]
        public void DependencyPathsAreContained(string path)
        { Assert.Throws<InvalidDataException>(() => ArcadeLayoutManifest.ResolveBuildPath(Path.GetTempPath(), path)); }

        [Test]
        public void LayoutRejectsDuplicatesNonfinitePosesAndInvalidDimensions()
        {
            var layout = Layout(); layout.Validate();
            layout.machines[1].id = "left"; Assert.Throws<InvalidDataException>(() => layout.Validate());
            layout.machines[1].id = "right"; layout.machines[1].yaw = float.NaN; Assert.Throws<InvalidDataException>(() => layout.Validate());
            layout.machines[1].yaw = 0; layout.width = float.PositiveInfinity; Assert.Throws<InvalidDataException>(() => layout.Validate());
        }

        [Test]
        public void MeasuredBoundsRejectWallOverlapMachineOverlapAndVisitorInside()
        {
            var layout = Layout();
            var boxes = new[] { new Bounds(new Vector3(-2, 1, 0), new Vector3(1, 2, 1)), new Bounds(new Vector3(2, 1, 0), new Vector3(1, 2, 1)) };
            layout.ValidateCabinetBounds(boxes);
            boxes[1].center = boxes[0].center; Assert.Throws<InvalidDataException>(() => layout.ValidateCabinetBounds(boxes));
            boxes[1].center = new Vector3(6, 1, 0); Assert.Throws<InvalidDataException>(() => layout.ValidateCabinetBounds(boxes));
            boxes[1].center = new Vector3(2, 1, 0); layout.visitorSpawn = V(-2, 0, 0); Assert.Throws<InvalidDataException>(() => layout.ValidateCabinetBounds(boxes));
        }

        [Test]
        public void StrictLayoutReaderRequiresPlacementFieldsAndRejectsUnknownFields()
        {
            var json = JsonUtility.ToJson(Layout());
            RuntimeManifestJsonReader.ReadArcade(json).Validate();
            Assert.Throws<FormatException>(() => RuntimeManifestJsonReader.ReadArcade(json.Replace("\"visitorYaw\":0", "\"unexpected\":0")));
            Assert.Throws<FormatException>(() => RuntimeManifestJsonReader.ReadArcade(json.Replace(",\"yaw\":0", "")));
        }

        [Test]
        public void OneVisitorRoutesOnlySelectedMachineAndReleasesBeforeSwitch()
        {
            var left = Machine("Left", -2); var right = Machine("Right", 2);
            var host = Root("Room"); var camera = Root("Camera").AddComponent<Camera>();
            var room = host.AddComponent<PlayableMachineRoom>();
            room.InitializeArcade(new[] { new ArcadeMachineStation("left", left), new ArcadeMachineStation("right", right) }, camera, Layout());
            room.enabled = false;
            var visitor = host.transform.Find("Arcade visitor"); visitor.position = new Vector3(-2, .05f, -1.7f);
            var a = left.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>();
            var b = right.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>();
            Assert.True(room.EnterMachine("left")); Assert.AreEqual("left", room.ActiveStationId);
            a.Poll(_ => true); b.Poll(_ => true);
            Assert.True(left.Inputs["start"].IsPressed); Assert.False(right.Inputs["start"].IsPressed);
            room.LeaveMachine(); Assert.False(left.Inputs["start"].IsPressed);
            visitor.position = new Vector3(2, .05f, -1.7f);
            Assert.True(room.EnterMachine("right")); a.Poll(_ => true); b.Poll(_ => true);
            Assert.False(left.Inputs["start"].IsPressed); Assert.True(right.Inputs["start"].IsPressed);
            right.UnloadAssets(); room.LeaveMachine(); Assert.False(a.enabled); Assert.False(b.enabled);
        }

        [Test]
        public void InteractionProfilesRejectUnsupportedKindsAndBrokenReferences()
        {
            var manifest = new MachineRuntimeManifest();
            Assert.Throws<InvalidDataException>(() => new MachineInteractionDefinition { kind = "AirHockey" }.Validate(manifest));
            Assert.Throws<InvalidDataException>(() => new MachineInteractionDefinition { kind = "Buttons", cuePrefabAlias = "cue" }.Validate(manifest));
            Assert.Throws<InvalidDataException>(() => new MachineInteractionDefinition { kind = "Pool", cuePrefabAlias = "cue", playingSurfaceId = "table" }.Validate(manifest));
            manifest.prefabDependencies = new[] { new MachineRuntimePrefabDependency { alias = "cue" } };
            manifest.geometrySurfaces = new[] { new MachineRuntimeGeometrySurface { id = "table" } };
            new MachineInteractionDefinition { kind = "Pool", cuePrefabAlias = "cue", playingSurfaceId = "table" }.Validate(manifest);
        }

        private RuntimeMachine Machine(string name, float x)
        {
            var root = Root(name); root.transform.position = new Vector3(x, 0, 0);
            var cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube); cabinet.transform.SetParent(root.transform, false); cabinet.transform.localPosition = Vector3.up; cabinet.transform.localScale = new Vector3(1, 2, 1);
            var manifest = new MachineRuntimeManifest { anchors = Array.Empty<MachineRuntimeAnchor>(), interaction = new MachineInteractionDefinition(),
                runtime = new MachineRuntimeManifestDefinition { kind = "Oasis" }, inputs = new[] { new MachineInputDefinition { id = "start", keyboardShortcut = "Space" } } };
            var machine = new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition>()), cabinet);
            machine.SetMachineSpace(root.transform); _machines.Add(machine); return machine;
        }
        private GameObject Root(string name) { var root = new GameObject(name); _roots.Add(root); return root; }
        private static RuntimeVector3Definition V(float x, float y, float z) { return new RuntimeVector3Definition { x = x, y = y, z = z }; }
        private static ArcadeLayoutManifest Layout()
        {
            return new ArcadeLayoutManifest { schema = "oasis.arcade.runtime", schemaVersion = 1, width = 12, length = 10, height = 4,
                visitorSpawn = V(0, 0, -3), machines = new[] { new ArcadeMachinePlacement { id = "left", build = "machines/left", position = V(-2, 0, 0) }, new ArcadeMachinePlacement { id = "right", build = "machines/right", position = V(2, 0, 0) } } };
        }
    }
}

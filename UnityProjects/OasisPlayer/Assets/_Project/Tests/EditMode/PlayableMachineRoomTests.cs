using System;
using System.Collections.Generic;
using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using OasisPlayer.Loading;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class PlayableMachineRoomTests
    {
        private GameObject _root;
        private RuntimeMachine _machine;
        [SetUp] public void Setup() { _root = new GameObject("Test root"); }
        [TearDown] public void Cleanup() { if (_machine != null) _machine.UnloadAssets(); UnityEngine.Object.DestroyImmediate(_root); }

        [Test]
        public void RoomFitsRotatedCabinetWithoutMovingMachineAndReleasesInputsWhenLeaving()
        {
            var machine = Machine();
            var cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube); cabinet.transform.SetParent(_root.transform);
            cabinet.transform.localPosition = new Vector3(3, 1, 4); cabinet.transform.localScale = new Vector3(2, 2, 4);
            cabinet.transform.localRotation = Quaternion.Euler(0, 35, 0);
            // Use a separate machine with this visible Cabinet.
            _machine = new RuntimeMachine(machine.Build, cabinet); _machine.SetMachineSpace(_root.transform);
            var camera = new GameObject("Camera"); camera.transform.SetParent(_root.transform); var view = camera.AddComponent<Camera>();
            var host = new GameObject("Room"); host.transform.SetParent(_root.transform);
            var room = host.AddComponent<PlayableMachineRoom>();
            var original = cabinet.transform.position;
            room.Initialize(_machine, view, MachineInteractionProfile.Buttons, false);
            room.enabled = false;
            Assert.AreEqual(original, cabinet.transform.position);
            foreach (var wall in host.GetComponentsInChildren<BoxCollider>())
                if (wall.name.Contains("wall")) Assert.False(wall.bounds.Intersects(room.CabinetBounds), wall.name);
            Assert.True(room.EnterMachine());
            var controls = _root.GetComponent<RuntimeInputDevelopmentControls>();
            controls.Poll(key => key == KeyCode.R);
            Assert.True(_machine.Inputs["reset"].IsPressed);
            room.LeaveMachine();
            Assert.False(_machine.Inputs["reset"].IsPressed);
            Assert.False(controls.enabled);
            Assert.False(room.IsEngaged);
            host.transform.Find("Arcade visitor").position = new Vector3(100, 0, 100);
            Assert.False(room.EnterMachine());
        }

        [TestCase(float.NaN, 1f)] [TestCase(1f, float.PositiveInfinity)]
        [TestCase(1f, -1f)] [TestCase(1f, 7f)]
        public void InvalidShotsDoNotReachPhysics(float direction, float speed)
        {
            var pool = new PoolInteraction(Machine());
            Assert.False(pool.TryShoot(new Vector3(direction, 0, 1), speed, out _));
        }

        [Test]
        public void ShotRejectsMissingDuplicateMovingKinematicAndUnloadedCue()
        {
            var machine = Machine(); var pool = new PoolInteraction(machine);
            Assert.False(pool.TryGetCue(out _));
            var ball = AddBall(machine, "cue", "cueBall");
            Assert.True(pool.TryGetCue(out _));
            ball.Rigidbody.isKinematic = true; Assert.False(pool.TryGetCue(out _)); ball.Rigidbody.isKinematic = false;
            ball.Rigidbody.linearVelocity = Vector3.right;
            Assert.False(pool.TryShoot(Vector3.forward, 2, out var message)); StringAssert.Contains("stop", message);
            ball.Rigidbody.linearVelocity = Vector3.zero;
            AddBall(machine, "duplicate", "cueBall"); Assert.False(pool.TryGetCue(out _));
            machine.UnloadAssets(); Assert.False(pool.TryGetCue(out _));
        }

        [Test]
        public void GeometryBoundsRejectMissingAndNonfiniteCabinet()
        {
            Assert.Throws<ArgumentException>(() => PlayableMachineRoom.MeasureCabinet(Machine()));
        }

        private RuntimeObjectInstance AddBall(RuntimeMachine machine, string id, string alias)
        {
            var ball = new GameObject(id); ball.transform.SetParent(_root.transform);
            var body = ball.AddComponent<Rigidbody>(); body.useGravity = false;
            var sphere = ball.AddComponent<SphereCollider>(); sphere.radius = .024f;
            var obj = new RuntimeObjectInstance(id, alias, "ball", null, ball, sphere, body, new MachineRuntimeObjectTransform());
            machine.RegisterObject(obj); return obj;
        }
        private RuntimeMachine Machine()
        {
            var manifest = new MachineRuntimeManifest { anchors = Array.Empty<MachineRuntimeAnchor>(),
                inputs = new[] { new MachineInputDefinition { id = "reset", name = "Reset", keyboardShortcut = "R" } },
                runtime = new MachineRuntimeManifestDefinition { kind = "Oasis" } };
            _machine = new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(),
                new Dictionary<string, ResolvedRuntimeObjectDefinition>()), null);
            _machine.SetMachineSpace(_root.transform);
            return _machine;
        }
    }
}

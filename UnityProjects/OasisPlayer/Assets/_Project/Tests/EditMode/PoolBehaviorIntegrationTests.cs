using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oasis.Scripting;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class PoolBehaviorIntegrationTests
    {
        private static readonly string[] Pockets = { "PocketLeftCorner", "PocketLeftMiddle", "PocketLeftFarCorner", "PocketRightCorner", "PocketRightMiddle", "PocketRightFarCorner" };
        private static readonly Vector3 Rack = new Vector3(1, 2, 3);
        private static readonly Vector3 Tray = new Vector3(1, 4, 5);
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<RuntimeMachine> _machines = new List<RuntimeMachine>();
        private readonly List<string> _faults = new List<string>();
        private RuntimeMachine _machine;
        private RuntimeOasisScriptBehavior _behavior;
        private RuntimeTriggerRelay[] _pockets;

        [TearDown]
        public void TearDown()
        {
            foreach (var machine in _machines) machine.UnloadAssets();
            for (var i = _roots.Count - 1; i >= 0; i--)
                if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _machines.Clear(); _roots.Clear(); _faults.Clear();
        }

        private void Load()
        {
            // Read the committed sample; no independently maintained fixture program.
            var source = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../WindowsNetProjects/OasisEditor/Examples/Pool/behavior.oasis")));
            var compiled = OasisScriptCompiler.Compile(source, "Pool/behavior.oasis");
            Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));
            var manifest = new MachineRuntimeManifest
            {
                machineId = "pool-integration", runtime = new MachineRuntimeManifestDefinition { kind = "Oasis" },
                anchors = new[] { Anchor("rackBall01", Rack), Anchor("traySlot01", Tray) },
                inputs = new[]
                {
                    new MachineInputDefinition { id = "rerack", name = "Rerack" },
                    new MachineInputDefinition { id = "newGame", name = "New game" }
                }
            };
            _machine = new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "",
                Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition>(), compiled.Program), null);
            _machines.Add(_machine);
            var frame = Root("MachineSpace");
            frame.transform.position = new Vector3(10, 20, 30);
            frame.transform.rotation = Quaternion.Euler(0, 45, 0);
            AddBall("ball01", frame.transform);
            _pockets = Pockets.Select(Relay).ToArray();
            _behavior = RuntimeOasisScriptBehavior.AttachTo(_machine, _faults.Add);
            _machine.CompleteStartup();
            At("ball01", Rack);
        }

        private RuntimeObjectInstance AddBall(string id, Transform parent)
        {
            var root = Root(id); root.transform.SetParent(parent, false);
            var collider = root.AddComponent<SphereCollider>();
            var body = root.AddComponent<Rigidbody>(); body.useGravity = false;
            var authored = new MachineRuntimeObjectTransform
            { position = Vector(Vector3.zero), rotationEulerDegrees = Vector(Vector3.zero), scale = Vector(Vector3.one) };
            var instance = new RuntimeObjectInstance(id, id, "test-ball", null, root, collider, body, authored);
            _machine.RegisterObject(instance);
            Dirty(instance); root.SetActive(false);
            return instance;
        }

        private RuntimeTriggerRelay Relay(string id)
        {
            var root = Root("OasisTrigger_" + id); var collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true;
            _machine.RegisterTrigger(new RuntimeTrigger(id, collider));
            var relay = root.AddComponent<RuntimeTriggerRelay>(); relay.Initialize(_machine, id); return relay;
        }

        private GameObject Root(string name) { var root = new GameObject(name); _roots.Add(root); return root; }
        private static RuntimeVector3Definition Vector(Vector3 p) { return new RuntimeVector3Definition { x = p.x, y = p.y, z = p.z }; }
        private static MachineRuntimeAnchor Anchor(string id, Vector3 p)
        { return new MachineRuntimeAnchor { id = id, displayName = id, position = Vector(p), rotationEulerDegrees = Vector(new Vector3(0, 25, 0)) }; }
        private static void Dirty(RuntimeObjectInstance ball)
        { ball.Rigidbody.linearVelocity = new Vector3(1, 2, 3); ball.Rigidbody.angularVelocity = new Vector3(3, 2, 1); }

        private void At(string id, Vector3 position)
        {
            var ball = _machine.GetObject(id);
            Assert.True(ball.Root.activeSelf);
            Assert.That(Vector3.Distance(position, ball.Root.transform.localPosition), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(ball.Rigidbody.position, ball.Root.transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0, 25, 0), ball.Root.transform.localRotation), Is.LessThan(.001f));
            Assert.AreEqual(Vector3.zero, ball.Rigidbody.linearVelocity);
            Assert.AreEqual(Vector3.zero, ball.Rigidbody.angularVelocity);
            Assert.False(_behavior.IsFaulted); Assert.IsEmpty(_faults);
        }

        private void Pocket(string ball = "ball01", int index = 0) { _pockets[index].PublishEntered(_machine.GetObject(ball).Collider); }
        private void Input(string id) { _machine.SetInputState(id, true); _machine.SetInputState(id, false); }

        [Test]
        public void StartupActivatesBallAndPlacesLiveRootWithBothVelocitiesCleared()
        {
            Load();
            Assert.AreEqual(1, _machine.Objects.Count);
            At("ball01", Rack);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EveryPocketCollectsOnceAndDuplicatesCannotMoveOrClearMotionAgain(int index)
        {
            Load(); var ball = _machine.GetObject("ball01"); Dirty(ball);
            Pocket(index: index); At("ball01", Tray);
            // Make a second teleport/velocity command observable rather than merely idempotent.
            ball.Root.transform.localPosition = new Vector3(7, 8, 9);
            ball.Rigidbody.position = ball.Root.transform.position;
            Dirty(ball);
            var position = ball.Root.transform.localPosition;
            var bodyPosition = ball.Rigidbody.position;
            var linear = ball.Rigidbody.linearVelocity; var angular = ball.Rigidbody.angularVelocity;
            Pocket(index: index);
            foreach (var pocket in _pockets) pocket.PublishEntered(ball.Collider);
            _pockets[index].PublishExited(ball.Collider);
            Assert.AreEqual(position, ball.Root.transform.localPosition);
            Assert.AreEqual(bodyPosition, ball.Rigidbody.position);
            Assert.AreEqual(linear, ball.Rigidbody.linearVelocity);
            Assert.AreEqual(angular, ball.Rigidbody.angularVelocity);
            Assert.True(ball.Root.activeSelf); Assert.False(_behavior.IsFaulted); Assert.IsEmpty(_faults);
        }

        [Test]
        public void UnrelatedTriggerAndObjectPayloadsDoNotChangeBallOrCollectionState()
        {
            Load(); var ball = _machine.GetObject("ball01");
            Relay("other").PublishEntered(ball.Collider); At("ball01", Rack);
            // A test-only unrelated registered object proves payload membership filtering.
            var decoration = AddBall("decoration", ball.Root.transform.parent);
            decoration.Root.SetActive(true);
            var position = decoration.Root.transform.localPosition;
            var linear = decoration.Rigidbody.linearVelocity; var angular = decoration.Rigidbody.angularVelocity;
            foreach (var pocket in _pockets) pocket.PublishEntered(decoration.Collider);
            Assert.AreEqual(position, decoration.Root.transform.localPosition);
            Assert.AreEqual(linear, decoration.Rigidbody.linearVelocity);
            Assert.AreEqual(angular, decoration.Rigidbody.angularVelocity);
            At("ball01", Rack); Pocket(); At("ball01", Tray);
        }

        [TestCase("rerack", false)] [TestCase("rerack", true)]
        [TestCase("newGame", false)] [TestCase("newGame", true)]
        public void BothLogicalResetInputsRestoreBallAndPermitRecollection(string input, bool collectFirst)
        {
            Load();
            if (collectFirst) { Pocket(); At("ball01", Tray); }
            var ball = _machine.GetObject("ball01"); Dirty(ball); ball.Root.SetActive(false);
            Input(input); At("ball01", Rack);
            Pocket(index: 5); At("ball01", Tray);
        }

        [Test]
        public void UnloadDisposesOldSessionAndReloadAllowsFreshCollection()
        {
            Load(); Pocket(); At("ball01", Tray);
            var old = _machine; var oldBehavior = _behavior; var oldRelay = _pockets[0];
            old.UnloadAssets(); Assert.True(oldBehavior.IsDisposed);
            old.Events.Publish(new RuntimeTriggerEnteredEvent(Pockets[0], "ball01"));
            Load(); oldRelay.PublishEntered(_machine.GetObject("ball01").Collider);
            At("ball01", Rack); Pocket(); At("ball01", Tray);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public void DevelopmentInputCallbackMayUnloadMachineDuringMultipleTransitions()
        {
            Load(); var controls = Root("InputControls").AddComponent<RuntimeInputDevelopmentControls>(); controls.Initialize(_machine);
            controls.Configure(new[]
            {
                new RuntimeInputDevelopmentControls.Binding { key = KeyCode.R, inputId = "rerack" },
                new RuntimeInputDevelopmentControls.Binding { key = KeyCode.N, inputId = "newGame" }
            });
            _machine.Events.Subscribe(e => { if (e is RuntimeInputPressedEvent) _machine.UnloadAssets(); });
            Assert.DoesNotThrow(() => controls.Poll(k => true));
            Assert.True(_behavior.IsDisposed); Assert.DoesNotThrow(() => controls.Poll(k => true));
            Assert.DoesNotThrow(() => controls.enabled = false);
        }

        [Test]
        public void DevelopmentKeysAggregateTransitionsAndReleaseOnDisableReconfigureAndUnload()
        {
            Load(); var controls = Root("InputControls").AddComponent<RuntimeInputDevelopmentControls>(); controls.Initialize(_machine);
            controls.Configure(new[]
            {
                new RuntimeInputDevelopmentControls.Binding { key = KeyCode.R, inputId = "rerack" },
                new RuntimeInputDevelopmentControls.Binding { key = KeyCode.T, inputId = "rerack" },
                new RuntimeInputDevelopmentControls.Binding { key = KeyCode.N, inputId = "newGame" },
                new RuntimeInputDevelopmentControls.Binding { key = KeyCode.X, inputId = "undeclared" }
            });
            var events = new List<string>(); _machine.Events.Subscribe(e =>
            { if (e is RuntimeInputPressedEvent p) events.Add("+" + p.InputId); if (e is RuntimeInputReleasedEvent r) events.Add("-" + r.InputId); });
            Pocket("ball01"); controls.Poll(k => k == KeyCode.R); At("ball01", Rack);
            controls.Poll(k => k == KeyCode.R); controls.Poll(k => k == KeyCode.T);
            Assert.AreEqual(new[] { "+rerack" }, events);
            controls.Poll(k => false); Assert.AreEqual(new[] { "+rerack", "-rerack" }, events);
            Pocket("ball01"); controls.Poll(k => k == KeyCode.N); At("ball01", Rack);
            controls.enabled = false; Assert.AreEqual("-newGame", events.Last());
            controls.Poll(k => true); Assert.AreEqual(4, events.Count);
            controls.enabled = true; controls.Poll(k => k == KeyCode.N);
            controls.Configure(Array.Empty<RuntimeInputDevelopmentControls.Binding>()); Assert.AreEqual("-newGame", events.Last());
            controls.Configure(new[] { new RuntimeInputDevelopmentControls.Binding { key = KeyCode.R, inputId = "rerack" } });
            controls.Poll(k => true); var old = _machine; Load(); controls.Initialize(_machine);
            Assert.False(old.Inputs["rerack"].IsPressed); controls.Poll(k => true); Assert.True(_machine.Inputs["rerack"].IsPressed);
            controls.SendMessage("OnApplicationFocus", false); Assert.False(_machine.Inputs["rerack"].IsPressed);
            controls.Poll(k => true); Assert.False(_machine.Inputs["rerack"].IsPressed);
            controls.SendMessage("OnApplicationFocus", true); controls.Poll(k => true);
            _machine.UnloadAssets(); Assert.DoesNotThrow(() => controls.Poll(k => true)); controls.enabled = false;
        }
#endif
    }
}

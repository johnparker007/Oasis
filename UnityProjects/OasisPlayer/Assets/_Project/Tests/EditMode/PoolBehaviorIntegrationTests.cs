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
            var anchors = new List<MachineRuntimeAnchor>();
            for (var i = 1; i <= 15; i++)
            { anchors.Add(Anchor("rackBall" + i.ToString("00"), Rack(i))); anchors.Add(Anchor("traySlot" + i.ToString("00"), Tray(i))); }
            anchors.Add(Anchor("rackCueBall", Cue));
            var manifest = new MachineRuntimeManifest
            {
                machineId = "pool-integration", runtime = new MachineRuntimeManifestDefinition { kind = "Oasis" },
                anchors = anchors.ToArray(), inputs = new[]
                { new MachineInputDefinition { id = "rerack", name = "Rerack" }, new MachineInputDefinition { id = "newGame", name = "New game" } }
            };
            _machine = new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "",
                Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition>(), compiled.Program), null);
            _machines.Add(_machine);
            var frame = Root("MachineSpace");
            frame.transform.position = new Vector3(10, 20, 30);
            frame.transform.rotation = Quaternion.Euler(0, 45, 0);
            for (var i = 1; i <= 15; i++) AddBall("ball" + i.ToString("00"), frame.transform);
            AddBall("cueBall", frame.transform); AddBall("decoration", frame.transform);
            _pockets = Pockets.Select(Relay).ToArray();
            _behavior = RuntimeOasisScriptBehavior.AttachTo(_machine, _faults.Add);
            _machine.CompleteStartup();
            AssertRack();
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
        private static Vector3 Rack(int i) { return new Vector3(i, 2, 3); }
        private static Vector3 Tray(int i) { return new Vector3(i, 4, 5); }
        private static readonly Vector3 Cue = new Vector3(-1, 2, 3);
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
        private void AssertRack()
        { for (var i = 1; i <= 15; i++) At("ball" + i.ToString("00"), Rack(i)); At("cueBall", Cue); }
        private void Pocket(string ball, int index = 0) { _pockets[index].PublishEntered(_machine.GetObject(ball).Collider); }
        private void Input(string id) { _machine.SetInputState(id, true); _machine.SetInputState(id, false); }

        [Test]
        public void CanonicalScriptUsesEveryPocketConsecutiveSlotsAndExactlyFifteenAllocations()
        {
            Load();
            var order = new[] { 8, 1, 15, 2, 14, 3, 13, 4, 12, 5, 11, 6, 10, 7, 9 };
            for (var slot = 1; slot <= 15; slot++)
            {
                var id = "ball" + order[slot - 1].ToString("00"); Dirty(_machine.GetObject(id));
                Pocket(id, (slot - 1) % 6); At(id, Tray(slot));
                Pocket(id, (slot - 1) % 6); Pocket(id, slot % 6); At(id, Tray(slot));
                _pockets[slot % 6].PublishExited(_machine.GetObject(id).Collider); At(id, Tray(slot));
            }
            for (var slot = 1; slot <= 15; slot++)
            { var id = "ball" + order[slot - 1].ToString("00"); Pocket(id); At(id, Tray(slot)); }
        }

        [Test]
        public void UnrelatedRelaysAndObjectsAreIgnoredAndRepeatedCueScratchesDoNotUseTray()
        {
            Load();
            var unrelated = Relay("other"); var first = _machine.GetObject("ball01");
            unrelated.PublishEntered(first.Collider); At("ball01", Rack(1));
            unrelated.PublishEntered(_machine.GetObject("cueBall").Collider); At("cueBall", Cue);
            var decoration = _machine.GetObject("decoration"); decoration.Root.SetActive(true);
            var position = decoration.Root.transform.localPosition; var velocity = decoration.Rigidbody.linearVelocity;
            foreach (var pocket in _pockets) pocket.PublishEntered(decoration.Collider);
            Assert.AreEqual(position, decoration.Root.transform.localPosition); Assert.AreEqual(velocity, decoration.Rigidbody.linearVelocity);
            Pocket("ball08"); At("ball08", Tray(1));
            for (var i = 0; i < 6; i++)
            {
                var cue = _machine.GetObject("cueBall"); Dirty(cue); cue.Root.transform.localPosition = new Vector3(50, 0, 0);
                Pocket("cueBall", i); Pocket("cueBall", i); At("cueBall", Cue); At("ball08", Tray(1));
            }
            Pocket("ball08", 1); At("ball08", Tray(1)); Pocket("ball01", 2); At("ball01", Tray(2));
        }

        [TestCase("rerack", 3)] [TestCase("rerack", 15)] [TestCase("newGame", 3)] [TestCase("newGame", 15)]
        public void LogicalResetInputsRestoreEveryLiveBallAndAllowRecollection(string input, int count)
        {
            Load(); for (var i = 1; i <= count; i++) Pocket("ball" + i.ToString("00"), i % 6);
            foreach (var ball in Enumerable.Range(1, 15).Select(i => _machine.GetObject("ball" + i.ToString("00"))).Append(_machine.GetObject("cueBall")))
            { Dirty(ball); ball.Root.SetActive(false); }
            Input(input); AssertRack(); Pocket("ball01"); At("ball01", Tray(1));
        }

        [Test]
        public void UnloadDisposesOldSessionAndReloadStartsCollectionAtSlotOne()
        {
            Load(); Pocket("ball08"); Pocket("ball01"); At("ball01", Tray(2));
            var old = _machine; var oldBehavior = _behavior; var oldRelay = _pockets[0];
            _machine.UnloadAssets(); Assert.True(oldBehavior.IsDisposed);
            old.Events.Publish(new RuntimeTriggerEnteredEvent(Pockets[0], "ball08"));
            Load(); oldRelay.PublishEntered(_machine.GetObject("ball08").Collider);
            At("ball08", Rack(8)); Pocket("ball08"); At("ball08", Tray(1));
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
            Pocket("ball08"); controls.Poll(k => k == KeyCode.R); At("ball08", Rack(8));
            controls.Poll(k => k == KeyCode.R); controls.Poll(k => k == KeyCode.T);
            Assert.AreEqual(new[] { "+rerack" }, events);
            controls.Poll(k => false); Assert.AreEqual(new[] { "+rerack", "-rerack" }, events);
            Pocket("ball08"); controls.Poll(k => k == KeyCode.N); At("ball08", Rack(8));
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

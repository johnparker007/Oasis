using System;
using System.Collections.Generic;
using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeBehaviorTests
    {
        private readonly List<GameObject> _roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _roots.Count - 1; i >= 0; i--) if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
        }

        [Test]
        public void EventDispatcherIsOrderedUnsubscribableAndSessionScoped()
        {
            var first = Machine();
            var second = Machine();
            var calls = new List<string>();
            Action<RuntimeMachineEvent> one = value => calls.Add("one:" + value.GetType().Name);
            Action<RuntimeMachineEvent> two = value => calls.Add("two:" + value.GetType().Name);
            first.Events.Subscribe(one);
            first.Events.Subscribe(two);
            first.Events.Publish(new RuntimeMachineStartedEvent());
            Assert.AreEqual(new[] { "one:RuntimeMachineStartedEvent", "two:RuntimeMachineStartedEvent" }, calls);
            Assert.True(first.Events.Unsubscribe(one));
            first.Events.Publish(new RuntimeMachineStartedEvent());
            Assert.AreEqual(3, calls.Count);
            second.Events.Publish(new RuntimeMachineStartedEvent());
            Assert.AreEqual(3, calls.Count);
        }

        [Test]
        public void SubscriberExceptionPropagatesAndMachineStartsOnlyOnce()
        {
            var machine = Machine();
            var starts = 0;
            machine.Events.Subscribe(value => { if (value is RuntimeMachineStartedEvent) starts++; });
            machine.CompleteStartup();
            machine.CompleteStartup();
            Assert.AreEqual(1, starts);
            machine.Events.Subscribe(_ => throw new ApplicationException("subscriber failed"));
            StringAssert.Contains("subscriber failed", Assert.Throws<ApplicationException>(() => machine.Events.Publish(new RuntimeMachineStartedEvent())).Message);
        }

        [Test]
        public void InputsPublishOnlyStateTransitionsAndRejectUnknownIds()
        {
            var machine = Machine(inputs: new[] { new MachineInputDefinition { id = "rerack", name = "Re-rack" } });
            var events = new List<RuntimeMachineEvent>();
            machine.Events.Subscribe(events.Add);
            Assert.AreEqual("Re-rack", machine.Inputs["rerack"].Name);
            Assert.True(machine.SetInputState("rerack", true));
            Assert.False(machine.SetInputState("rerack", true));
            Assert.True(machine.SetInputState("rerack", false));
            Assert.False(machine.SetInputState("rerack", false));
            Assert.IsInstanceOf<RuntimeInputPressedEvent>(events[0]);
            Assert.AreEqual("rerack", ((RuntimeInputPressedEvent)events[0]).InputId);
            Assert.IsInstanceOf<RuntimeInputReleasedEvent>(events[1]);
            Assert.Throws<KeyNotFoundException>(() => machine.SetInputState("missing", true));
        }

        [Test]
        public void CommandsUseAuthoritativeRootPreserveTeleportVelocityAndResetAuthoredState()
        {
            var machine = Machine(new[] { Anchor("traySlot01", 8, 7, 6) });
            var instance = AddObject(machine, "ball08", true, new Vector3(1, 2, 3), new Vector3(10, 20, 30), new Vector3(2, 3, 4));
            instance.Rigidbody.linearVelocity = new Vector3(4, 5, 6);
            instance.Rigidbody.angularVelocity = new Vector3(1, 2, 3);

            machine.Commands.SetActive("ball08", false);
            Assert.False(instance.Root.activeSelf);
            Assert.AreSame(instance, machine.GetObject("ball08"));
            machine.Commands.SetActive("ball08", true);
            machine.Commands.Teleport("ball08", "traySlot01");
            Assert.AreEqual(new Vector3(8, 7, 6), instance.Root.transform.localPosition);
            Assert.AreEqual(new Vector3(4, 5, 6), instance.Rigidbody.linearVelocity);
            machine.Commands.SetVelocity("ball08", RuntimeVector3.Zero);
            machine.Commands.SetAngularVelocity("ball08", RuntimeVector3.Zero);
            Assert.AreEqual(Vector3.zero, instance.Rigidbody.linearVelocity);
            Assert.AreEqual(Vector3.zero, instance.Rigidbody.angularVelocity);
            machine.Commands.SetVelocity("ball08", new RuntimeVector3(2, 3, 4));
            machine.Commands.SetAngularVelocity("ball08", new RuntimeVector3(5, 6, 7));
            Assert.AreEqual(new Vector3(2, 3, 4), instance.Rigidbody.linearVelocity);
            Assert.AreEqual(new Vector3(5, 6, 7), instance.Rigidbody.angularVelocity);
            machine.Commands.ApplyImpulse("ball08", new RuntimeVector3(1, 0, 0));
            Assert.Greater(instance.Rigidbody.linearVelocity.x, 0);

            instance.Root.SetActive(false);
            machine.Commands.ResetObject("ball08");
            Assert.True(instance.Root.activeSelf);
            Assert.AreEqual(new Vector3(1, 2, 3), instance.Root.transform.localPosition);
            Assert.That(Quaternion.Angle(Quaternion.Euler(10, 20, 30), instance.Root.transform.localRotation), Is.LessThan(.001f));
            Assert.AreEqual(new Vector3(2, 3, 4), instance.Root.transform.localScale);
            Assert.AreEqual(Vector3.zero, instance.Rigidbody.linearVelocity);
            Assert.AreSame(instance, machine.GetObject("ball08"));
        }

        [Test]
        public void DirectPoseDoesNotApplyDefinitionCorrectionAndBodyCommandsRequireBody()
        {
            var machine = Machine();
            var instance = AddObject(machine, "mole1", false, Vector3.zero, Vector3.zero, Vector3.one);
            machine.Commands.Teleport("mole1", new RuntimePose(new RuntimeVector3(1, 2, 3), new RuntimeVector3(4, 5, 6)));
            Assert.AreEqual(new Vector3(1, 2, 3), instance.Root.transform.localPosition);
            Assert.AreEqual(Vector3.one, instance.Root.transform.localScale);
            StringAssert.Contains("no Rigidbody", Assert.Throws<InvalidOperationException>(() => machine.Commands.SetVelocity("mole1", RuntimeVector3.Zero)).Message);
        }

        [Test]
        public void TimersAreDeterministicOneShotReplaceableStoppableAndClearedOnUnload()
        {
            var machine = Machine();
            var elapsed = new List<string>();
            machine.Events.Subscribe(value => { if (value is RuntimeTimerElapsedEvent timer) elapsed.Add(timer.TimerId); });
            machine.Commands.StartTimer("rerackDelay", .5f);
            machine.Timers.Advance(.49f);
            Assert.AreEqual(0, elapsed.Count);
            machine.Commands.StartTimer("rerackDelay", .5f);
            machine.Timers.Advance(.5f);
            machine.Timers.Advance(1);
            Assert.AreEqual(new[] { "rerackDelay" }, elapsed);
            Assert.AreEqual(0, machine.Timers.Count);
            machine.Commands.StartTimer("stopped", 1);
            Assert.True(machine.Commands.StopTimer("stopped"));
            machine.Timers.Advance(2);
            Assert.AreEqual(1, elapsed.Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => machine.Commands.StartTimer("bad", float.NaN));
            machine.Commands.StartTimer("active", 1);
            var other = Machine();
            Assert.AreEqual(0, other.Timers.Count);
            machine.UnloadAssets();
            Assert.AreEqual(0, machine.Timers.Count);
        }

        [Test]
        public void PoolStyleTriggerAndCommandsUseOnlyStableIds()
        {
            var machine = Machine(new[] { Anchor("traySlot01", 3, 2, 1) });
            var ball = AddObject(machine, "ball08", true, Vector3.zero, Vector3.zero, Vector3.one);
            var triggerRoot = Root("OasisTrigger_PocketLeftCorner");
            var triggerCollider = triggerRoot.AddComponent<BoxCollider>(); triggerCollider.isTrigger = true;
            machine.RegisterTrigger(new RuntimeTrigger("PocketLeftCorner", triggerCollider));
            var relay = triggerRoot.AddComponent<RuntimeTriggerRelay>();
            relay.Initialize(machine, "PocketLeftCorner");
            RuntimeTriggerEnteredEvent received = null;
            RuntimeTriggerExitedEvent exited = null;
            machine.Events.Subscribe(value => { received = value as RuntimeTriggerEnteredEvent ?? received; exited = value as RuntimeTriggerExitedEvent ?? exited; });
            relay.PublishEntered(ball.Collider);
            relay.PublishExited(ball.Collider);
            Assert.AreEqual("PocketLeftCorner", received.TriggerId);
            Assert.AreEqual("ball08", received.ObjectId);
            Assert.AreEqual("PocketLeftCorner", exited.TriggerId);
            Assert.AreEqual("ball08", exited.ObjectId);
            machine.Commands.Teleport("ball08", "traySlot01");
            machine.Commands.SetVelocity("ball08", RuntimeVector3.Zero);
            machine.Commands.SetAngularVelocity("ball08", RuntimeVector3.Zero);
            Assert.AreEqual(new Vector3(3, 2, 1), ball.Root.transform.localPosition);
        }

        [Test]
        public void WhacAMoleStyleAnchorsAndTimerNeedNoGameSpecificApi()
        {
            var machine = Machine(new[] { Anchor("mole1Up", 0, 1, 0), Anchor("mole1Down", 0, 0, 0) });
            var mole = AddObject(machine, "mole1", false, Vector3.zero, Vector3.zero, Vector3.one);
            machine.Commands.Teleport("mole1", "mole1Up");
            Assert.AreEqual(new Vector3(0, 1, 0), mole.Root.transform.localPosition);
            machine.Commands.Teleport("mole1", "mole1Down");
            machine.Commands.StartTimer("raiseAgain", .2f);
            RuntimeTimerElapsedEvent elapsed = null;
            machine.Events.Subscribe(value => elapsed = value as RuntimeTimerElapsedEvent ?? elapsed);
            machine.Timers.Advance(.2f);
            Assert.AreEqual("raiseAgain", elapsed.TimerId);
        }

        [Test]
        public void CollisionRelayIsDirectionalAndIgnoresUnnamedStaticCollider()
        {
            var machine = Machine();
            var first = AddObject(machine, "one", true, Vector3.zero, Vector3.zero, Vector3.one);
            var second = AddObject(machine, "two", true, Vector3.zero, Vector3.zero, Vector3.one);
            var received = new List<RuntimeMachineEvent>(); machine.Events.Subscribe(received.Add);
            first.Root.GetComponent<RuntimeObjectIdentity>().PublishCollisionEntered(second.Collider);
            first.Root.GetComponent<RuntimeObjectIdentity>().PublishCollisionExited(second.Collider);
            Assert.AreEqual("one", ((RuntimeCollisionEnteredEvent)received[0]).ObjectId);
            Assert.AreEqual("two", ((RuntimeCollisionEnteredEvent)received[0]).OtherObjectId);
            Assert.IsInstanceOf<RuntimeCollisionExitedEvent>(received[1]);
            var cabinet = Root("Cabinet").AddComponent<BoxCollider>();
            first.Root.GetComponent<RuntimeObjectIdentity>().PublishCollisionEntered(cabinet);
            Assert.AreEqual(2, received.Count);
        }

        [Test]
        public void NullObjectIdentityDoesNotResolve()
        {
            var machine = Machine();
            Assert.False(machine.TryGetObjectIdentity(null, out var objectId));
            Assert.IsNull(objectId);
        }

        private RuntimeMachine Machine(MachineRuntimeAnchor[] anchors = null, MachineInputDefinition[] inputs = null)
        {
            var manifest = new MachineRuntimeManifest { anchors = anchors ?? Array.Empty<MachineRuntimeAnchor>(), objectInstances = Array.Empty<MachineRuntimeObjectInstance>(), inputs = inputs ?? Array.Empty<MachineInputDefinition>() };
            return new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition>()), null);
        }
        private RuntimeObjectInstance AddObject(RuntimeMachine machine, string id, bool body, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            var root = Root(id); root.transform.localPosition = position; root.transform.localRotation = Quaternion.Euler(rotation); root.transform.localScale = scale;
            var collider = root.AddComponent<SphereCollider>(); var rigidbody = body ? root.AddComponent<Rigidbody>() : null;
            var authored = new MachineRuntimeObjectTransform { position = Vector(position), rotationEulerDegrees = Vector(rotation), scale = Vector(scale) };
            var instance = new RuntimeObjectInstance(id, id, "definition", null, root, collider, rigidbody, authored); machine.RegisterObject(instance); return instance;
        }
        private static MachineRuntimeAnchor Anchor(string id, float x, float y, float z) { return new MachineRuntimeAnchor { id = id, displayName = id, position = Vector(x, y, z), rotationEulerDegrees = Vector(0, 0, 0) }; }
        private static RuntimeVector3Definition Vector(Vector3 value) { return Vector(value.x, value.y, value.z); }
        private static RuntimeVector3Definition Vector(float x, float y, float z) { return new RuntimeVector3Definition { x = x, y = y, z = z }; }
        private GameObject Root(string name) { var value = new GameObject(name); _roots.Add(value); return value; }
    }
}

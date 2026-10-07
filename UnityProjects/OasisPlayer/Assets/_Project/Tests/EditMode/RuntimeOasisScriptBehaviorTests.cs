using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Oasis.Scripting;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeOasisScriptBehaviorTests
    {
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<RuntimeMachine> _machines = new List<RuntimeMachine>();
        private readonly List<string> _faults = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var machine in _machines) machine.UnloadAssets();
            _machines.Clear();
            for (var i = _roots.Count - 1; i >= 0; i--)
                if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            _faults.Clear();
        }

        [Test]
        public void HostConvertsRawObjectAndAnchorIdsAndVectorsThroughA7()
        {
            var machine = Machine(anchors: new[] { Anchor("rackBall08", 8, 7, 6) });
            var ball = AddObject(machine, "ball08", true);
            using (var host = new RuntimeMachineOasisScriptHost(machine))
            {
                Ok(host.Teleport(OasisScriptReferenceValue.Object("ball08"), OasisScriptReferenceValue.Anchor("rackBall08")));
                Assert.AreEqual(new Vector3(8, 7, 6), ball.Root.transform.localPosition);
                Ok(host.SetVelocity(OasisScriptReferenceValue.Object("ball08"), new OasisScriptVec3Value(1.25, -2.5, 3.75)));
                Ok(host.SetAngularVelocity(OasisScriptReferenceValue.Object("ball08"), new OasisScriptVec3Value(4, 5, 6)));
                Assert.AreEqual(new Vector3(1.25f, -2.5f, 3.75f), ball.Rigidbody.linearVelocity);
                Assert.AreEqual(new Vector3(4, 5, 6), ball.Rigidbody.angularVelocity);
            }
        }

        [TestCase("SetActive")]
        [TestCase("Teleport")]
        [TestCase("TeleportPose")]
        [TestCase("SetVelocity")]
        [TestCase("SetAngularVelocity")]
        [TestCase("ApplyImpulse")]
        [TestCase("ResetObject")]
        public void HostRejectsWrongDomainAndNullObjectReferences(string command)
        {
            var machine = Machine(anchors: new[] { Anchor("target", 1, 2, 3) });
            var instance = AddObject(machine, "foo", true);
            using (var host = new RuntimeMachineOasisScriptHost(machine))
            {
                foreach (var value in new[] { OasisScriptReferenceValue.Anchor("foo"), null })
                {
                    var zero = new OasisScriptVec3Value(0, 0, 0);
                    OasisScriptHostResult result;
                    switch (command)
                    {
                        case "SetActive": result = host.SetActive(value, false); break;
                        case "Teleport": result = host.Teleport(value, OasisScriptReferenceValue.Anchor("target")); break;
                        case "TeleportPose": result = host.TeleportPose(value, zero, zero); break;
                        case "SetVelocity": result = host.SetVelocity(value, zero); break;
                        case "SetAngularVelocity": result = host.SetAngularVelocity(value, zero); break;
                        case "ApplyImpulse": result = host.ApplyImpulse(value, zero); break;
                        default: result = host.ResetObject(value); break;
                    }
                    Assert.False(result.Success);
                    StringAssert.Contains("ObjectRef", result.Error);
                    Assert.True(instance.Root.activeSelf);
                    Assert.AreEqual(Vector3.zero, instance.Root.transform.localPosition);
                    Assert.AreEqual(Vector3.zero, instance.Rigidbody.linearVelocity);
                }
            }
        }

        [Test]
        public void HostDefensivelyValidatesAnchorsVectorsNumericRangeAndTimerArguments()
        {
            var machine = Machine();
            var instance = AddObject(machine, "foo", true);
            using (var host = new RuntimeMachineOasisScriptHost(machine))
            {
                var obj = OasisScriptReferenceValue.Object("foo");
                var zero = new OasisScriptVec3Value(0, 0, 0);
                StringAssert.Contains("AnchorRef", host.Teleport(obj, OasisScriptReferenceValue.Object("foo")).Error);
                StringAssert.Contains("AnchorRef", host.Teleport(obj, null).Error);
                StringAssert.Contains("Invalid ObjectRef ID", host.ResetObject(OasisScriptReferenceValue.Object("object:foo")).Error);
                Assert.False(host.TeleportPose(obj, null, zero).Success);
                Assert.False(host.TeleportPose(obj, zero, null).Success);
                Assert.False(host.SetVelocity(obj, null).Success);
                Assert.False(host.SetAngularVelocity(obj, null).Success);
                Assert.False(host.ApplyImpulse(obj, null).Success);
                StringAssert.Contains("float range", host.SetVelocity(obj, new OasisScriptVec3Value(double.MaxValue, 0, 0)).Error);
                Assert.False(host.StartTimer("bad", double.MaxValue).Success);
                Assert.False(host.StartTimer("bad", double.NaN).Success);
                Assert.False(host.StartTimer("bad", double.PositiveInfinity).Success);
                Assert.False(host.StartTimer("bad", -.5).Success);
                Assert.False(host.StartTimer("bad", -double.Epsilon).Success);
                Assert.False(host.StartTimer("invalid timer", .5).Success);
                Assert.False(host.StopTimer(null).Success);
                Assert.AreEqual(0, machine.Timers.Count);
                Assert.AreEqual(Vector3.zero, instance.Rigidbody.linearVelocity);
            }
        }

        [Test]
        public void HostDelegatesStartReplaceStopAndAbsentStopToA7Timers()
        {
            var machine = Machine();
            var elapsed = new List<string>();
            machine.Events.Subscribe(value => { if (value is RuntimeTimerElapsedEvent timer) elapsed.Add(timer.TimerId); });
            using (var host = new RuntimeMachineOasisScriptHost(machine))
            {
                Ok(host.StartTimer("hide", .5));
                Assert.AreEqual(1, machine.Timers.Count);
                machine.Timers.Advance(.25f);
                Ok(host.StartTimer("hide", .5));
                machine.Timers.Advance(.25f);
                Assert.IsEmpty(elapsed);
                Ok(host.StopTimer("hide"));
                Ok(host.StopTimer("hide"));
                machine.Timers.Advance(1);
                Assert.IsEmpty(elapsed);
                Ok(host.StartTimer("hide", .5));
                machine.Timers.Advance(.5f);
                Assert.AreEqual(new[] { "hide" }, elapsed);
                Assert.AreEqual(0, machine.Timers.Count);
            }
        }

        [Test]
        public void HostReturnsNormalCommandErrorsAndUsefulUnexpectedFailure()
        {
            var machine = Machine(anchors: new[] { Anchor("target", 0, 0, 0) });
            AddObject(machine, "decoration", false);
            using (var host = new RuntimeMachineOasisScriptHost(machine))
            {
                var obj = OasisScriptReferenceValue.Object("decoration");
                StringAssert.Contains("missing", host.Teleport(OasisScriptReferenceValue.Object("missing"), OasisScriptReferenceValue.Anchor("target")).Error);
                StringAssert.Contains("missingAnchor", host.Teleport(obj, OasisScriptReferenceValue.Anchor("missingAnchor")).Error);
                StringAssert.Contains("no Rigidbody", host.SetVelocity(obj, new OasisScriptVec3Value(0, 0, 0)).Error);
                StringAssert.Contains("no Rigidbody", host.SetAngularVelocity(obj, new OasisScriptVec3Value(0, 0, 0)).Error);
                StringAssert.Contains("no Rigidbody", host.ApplyImpulse(obj, new OasisScriptVec3Value(1, 0, 0)).Error);
                // Damaged live instance exercises the unexpected-exception boundary without a fake host.
                machine.RegisterObject(new RuntimeObjectInstance("damaged", "damaged", "def", null, Root("damaged"), null, null, null));
                var failure = host.ResetObject(OasisScriptReferenceValue.Object("damaged"));
                Assert.False(failure.Success);
                StringAssert.Contains("object.reset", failure.Error);
                StringAssert.Contains("unexpected NullReferenceException", failure.Error);
            }
        }

        [Test]
        public void DetachedOrUnloadedHostCannotReachRuntimeCommands()
        {
            var machine = Machine();
            var host = new RuntimeMachineOasisScriptHost(machine);
            host.Dispose();
            host.Dispose();
            Assert.False(host.StartTimer("late", 1).Success);
            var otherHost = new RuntimeMachineOasisScriptHost(machine);
            machine.UnloadAssets();
            Assert.False(otherHost.StartTimer("late", 1).Success);
            Assert.AreEqual(0, machine.Timers.Count);
            otherHost.Dispose();
        }

        [Test]
        public void HostSourceUsesA7BoundaryWithoutDirectUnityManipulation()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/RuntimeBuild/RuntimeMachineOasisScriptHost.cs"));
            foreach (var forbidden in new[] { "UnityEngine", "GameObject", "Transform", "Rigidbody", "Collider", "ForceMode", ".Root", ".Timers", "StartsWith", "Substring" })
                StringAssert.DoesNotContain(forbidden, source);
        }

        private static IEnumerable<TestCaseData> EventCases()
        {
            yield return new TestCaseData(new RuntimeMachineStartedEvent(), "machine.started", Array.Empty<string>(), Array.Empty<OasisScriptType>());
            yield return new TestCaseData(new RuntimeInputPressedEvent("reset"), "input.pressed", new[] { "reset" }, new[] { OasisScriptType.InputRef });
            yield return new TestCaseData(new RuntimeInputReleasedEvent("reset"), "input.released", new[] { "reset" }, new[] { OasisScriptType.InputRef });
            yield return new TestCaseData(new RuntimeTriggerEnteredEvent("PocketLeftCorner", "ball08"), "trigger.entered", new[] { "PocketLeftCorner", "ball08" }, new[] { OasisScriptType.TriggerRef, OasisScriptType.ObjectRef });
            yield return new TestCaseData(new RuntimeTriggerExitedEvent("PocketLeftCorner", "ball08"), "trigger.exited", new[] { "PocketLeftCorner", "ball08" }, new[] { OasisScriptType.TriggerRef, OasisScriptType.ObjectRef });
            yield return new TestCaseData(new RuntimeCollisionEnteredEvent("ball08", "ball09"), "collision.entered", new[] { "ball08", "ball09" }, new[] { OasisScriptType.ObjectRef, OasisScriptType.ObjectRef });
            yield return new TestCaseData(new RuntimeCollisionExitedEvent("ball08", "ball09"), "collision.exited", new[] { "ball08", "ball09" }, new[] { OasisScriptType.ObjectRef, OasisScriptType.ObjectRef });
            yield return new TestCaseData(new RuntimeTimerElapsedEvent("hide"), "timer.elapsed", new[] { "hide" }, new[] { OasisScriptType.String });
        }

        [TestCaseSource(nameof(EventCases))]
        public void EveryA7EventProducesCanonicalTypedValues(RuntimeMachineEvent value, string name, string[] ids, OasisScriptType[] types)
        {
            Assert.True(RuntimeOasisScriptBehavior.TryTranslateEvent(value, out var translated));
            Assert.AreEqual(name, translated.Name);
            Assert.AreEqual(ids.Length, translated.Arguments.Count);
            for (var i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(types[i], translated.Arguments[i].Type);
                var reference = translated.Arguments[i] as OasisScriptReferenceValue;
                Assert.AreEqual(ids[i], reference != null ? reference.Id : ((OasisScriptStringValue)translated.Arguments[i]).Value);
            }
        }

        [Test]
        public void DispatchCompletesSynchronouslyAndHandlersRetainSourceOrder()
        {
            var machine = Machine("state count = 0; on input.pressed(input:reset) { count = count * 10 + 1; } on input.pressed(input:reset) { count = count * 10 + 2; object.teleport_pose(object:foo, vec3(count,0,0), vec3(0,0,0)); }");
            var instance = AddObject(machine, "foo", false);
            var behavior = Attach(machine);
            machine.Events.Subscribe(value =>
            {
                if (value is RuntimeInputPressedEvent) Assert.AreEqual(new Vector3(12, 0, 0), instance.Root.transform.localPosition);
            });
            Assert.True(machine.SetInputState("reset", true));
            Assert.AreEqual(new Vector3(12, 0, 0), instance.Root.transform.localPosition);
            Assert.False(behavior.IsFaulted);
        }

        [Test]
        public void MachineStartedUsesA7AndRunsExactlyOnceAfterAttachment()
        {
            var machine = Machine("state starts = 0; on machine.started() { starts = starts + 1; object.teleport_pose(object:foo, vec3(starts,0,0), vec3(0,0,0)); }");
            var instance = AddObject(machine, "foo", false);
            Attach(machine);
            Assert.AreEqual(Vector3.zero, instance.Root.transform.localPosition);
            machine.CompleteStartup();
            Assert.AreEqual(new Vector3(1, 0, 0), instance.Root.transform.localPosition);
            machine.CompleteStartup();
            Assert.AreEqual(new Vector3(1, 0, 0), instance.Root.transform.localPosition);
            Assert.Throws<InvalidOperationException>(() => Attach(machine));
        }

        [Test]
        public void EmulationCreatesNoAdapterAndOasisRequiresCompiledProgram()
        {
            var emulation = Machine(kind: "Emulation");
            Assert.IsNull(RuntimeOasisScriptBehavior.AttachTo(emulation));
            emulation.CompleteStartup();
            var manifest = new MachineRuntimeManifest { anchors = Array.Empty<MachineRuntimeAnchor>(), runtime = new MachineRuntimeManifestDefinition { kind = "Oasis" } };
            var machine = new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition>()), null);
            _machines.Add(machine);
            StringAssert.Contains("compiled behavior/behavior.oasis", Assert.Throws<InvalidOperationException>(() => Attach(machine)).Message);
            Assert.False(machine.HasStarted);
        }

        [Test]
        public void DuplicateAttachmentIsRejectedWithoutDetachingTheOriginal()
        {
            var machine = Machine("on machine.started() { timer.start(\"alive\", 1); }");
            var original = Attach(machine);
            Assert.Throws<InvalidOperationException>(() => Attach(machine));
            machine.CompleteStartup();
            Assert.AreEqual(1, machine.Timers.Count);
            Assert.False(original.IsDisposed);
        }

        [Test]
        public void DisposeUnsubscribesEvenBeforeMachineUnload()
        {
            var machine = Machine("on input.pressed(input:reset) { timer.start(\"late\", 1); }");
            var behavior = Attach(machine);
            behavior.Dispose();
            behavior.Dispose();
            machine.SetInputState("reset", true);
            Assert.AreEqual(0, machine.Timers.Count);
            Assert.True(behavior.IsDisposed);
        }

        [Test]
        public void MachineUnloadDisposesAdapterAndSnapshotCallbackCannotReachOldScript()
        {
            var machine = Machine("on input.pressed(input:reset) { timer.start(\"late\", 1); }");
            machine.Events.Subscribe(value => machine.UnloadAssets());
            var behavior = Attach(machine);
            Assert.DoesNotThrow(() => machine.SetInputState("reset", true));
            Assert.True(behavior.IsDisposed);
            machine.Events.Publish(new RuntimeInputPressedEvent("reset"));
            Assert.AreEqual(0, machine.Timers.Count);
            Assert.IsEmpty(_faults);
        }

        [Test]
        public void InitializationFaultReportsOnceAndNeverSubscribesOrStarts()
        {
            var machine = Machine("const broken = 1 / 0; on machine.started() { timer.start(\"bad\", 1); }");
            var exception = Assert.Throws<InvalidOperationException>(() => Attach(machine));
            StringAssert.Contains("initialization", exception.Message);
            StringAssert.Contains("OSR1002", exception.Message);
            Assert.False(machine.HasStarted);
            machine.Events.Publish(new RuntimeMachineStartedEvent());
            Assert.AreEqual(0, machine.Timers.Count);
            Assert.AreEqual(1, _faults.Count);
        }

        [TestCase("object.teleport(object:missing, anchor:target);", "missing")]
        [TestCase("object.teleport(object:foo, anchor:missingAnchor);", "missingAnchor")]
        [TestCase("object.set_velocity(object:foo, vec3(0,0,0));", "no Rigidbody")]
        public void HostFailureFaultsOnceWithoutEscapingThePublisher(string command, string expected)
        {
            var machine = Machine("on input.pressed(input:reset) { " + command + " timer.start(\"afterFault\", 1); } on input.released(input:reset) { object.set_active(object:foo, false); }", new[] { Anchor("target", 1, 2, 3) });
            var instance = AddObject(machine, "foo", false);
            var behavior = Attach(machine);
            machine.CompleteStartup();
            Assert.DoesNotThrow(() => machine.SetInputState("reset", true));
            var diagnostic = behavior.LastRuntimeDiagnostic;
            Assert.True(behavior.IsFaulted);
            Assert.True(machine.IsActive);
            Assert.AreEqual("OSR1005", diagnostic.Code);
            Assert.AreEqual("input.pressed", diagnostic.EventName);
            Assert.AreEqual("behavior/behavior.oasis", diagnostic.SourceName);
            Assert.AreEqual(1, diagnostic.Line);
            Assert.Greater(diagnostic.Column, 0);
            StringAssert.Contains(expected, diagnostic.Message);
            machine.SetInputState("reset", false);
            machine.SetInputState("reset", true);
            Assert.AreSame(diagnostic, behavior.LastRuntimeDiagnostic);
            Assert.AreEqual(1, _faults.Count);
            StringAssert.Contains("Machine 'Adapter test'", _faults[0]);
            StringAssert.Contains("behavior/behavior.oasis:1:", _faults[0]);
            StringAssert.Contains("OSR1005", _faults[0]);
            StringAssert.Contains("while handling input.pressed", _faults[0]);
            Assert.AreEqual(0, machine.Timers.Count);
            Assert.True(instance.Root.activeSelf);
        }

        [Test]
        public void MachineStartedHandlerFaultKeepsMachineLoadedAndDisablesLaterEvents()
        {
            var machine = Machine("on machine.started() { object.reset(object:missing); } on input.pressed(input:reset) { timer.start(\"late\", 1); }");
            var behavior = Attach(machine);
            Assert.DoesNotThrow(() => machine.CompleteStartup());
            Assert.True(machine.HasStarted);
            Assert.True(machine.IsActive);
            Assert.AreEqual("machine.started", behavior.LastRuntimeDiagnostic.EventName);
            machine.SetInputState("reset", true);
            Assert.AreEqual(0, machine.Timers.Count);
            Assert.AreEqual(1, _faults.Count);
        }

        [Test]
        public void PoolTriggerRelayRunsScriptAndMovesLiveBallAndClearsBothVelocities()
        {
            var machine = Machine("on trigger.entered(trigger:PocketLeftCorner, ball) { object.teleport(ball, anchor:traySlot01); object.set_velocity(ball, vec3(0,0,0)); object.set_angular_velocity(ball, vec3(0,0,0)); } on trigger.exited(trigger:PocketLeftCorner, ball) { object.set_active(ball, false); }", new[] { Anchor("traySlot01", 3, 2, 1) });
            var ball = AddObject(machine, "ball08", true);
            ball.Rigidbody.linearVelocity = new Vector3(4, 5, 6);
            ball.Rigidbody.angularVelocity = new Vector3(1, 2, 3);
            var root = Root("OasisTrigger_PocketLeftCorner");
            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            machine.RegisterTrigger(new RuntimeTrigger("PocketLeftCorner", collider));
            var relay = root.AddComponent<RuntimeTriggerRelay>();
            relay.Initialize(machine, "PocketLeftCorner");
            Attach(machine);
            machine.CompleteStartup();
            relay.PublishEntered(ball.Collider);
            Assert.AreEqual(new Vector3(3, 2, 1), ball.Root.transform.localPosition);
            Assert.AreEqual(Vector3.zero, ball.Rigidbody.linearVelocity);
            Assert.AreEqual(Vector3.zero, ball.Rigidbody.angularVelocity);
            relay.PublishExited(ball.Collider);
            Assert.False(ball.Root.activeSelf);
            Assert.AreEqual(1, root.GetComponents<RuntimeTriggerRelay>().Length);
            Assert.IsEmpty(_faults);
        }

        [Test]
        public void LogicalInputPressedAndReleasedDriveActualA7Commands()
        {
            var machine = Machine("on input.pressed(input:reset) { object.teleport(object:ball08, anchor:rackBall08); object.set_velocity(object:ball08, vec3(0,0,0)); } on input.released(input:reset) { object.set_angular_velocity(object:ball08, vec3(0,0,0)); }", new[] { Anchor("rackBall08", 7, 8, 9) });
            var ball = AddObject(machine, "ball08", true);
            ball.Rigidbody.linearVelocity = new Vector3(1, 2, 3);
            ball.Rigidbody.angularVelocity = new Vector3(4, 5, 6);
            Attach(machine);
            machine.SetInputState("reset", true);
            Assert.AreEqual(new Vector3(7, 8, 9), ball.Root.transform.localPosition);
            Assert.AreEqual(Vector3.zero, ball.Rigidbody.linearVelocity);
            Assert.AreEqual(new Vector3(4, 5, 6), ball.Rigidbody.angularVelocity);
            machine.SetInputState("reset", false);
            Assert.AreEqual(Vector3.zero, ball.Rigidbody.angularVelocity);
        }

        [Test]
        public void CollisionBridgePreservesDirectionAndDispatchesEnteredAndExited()
        {
            var machine = Machine("on collision.entered(object:one, other) { object.teleport_pose(other, vec3(4,5,6), vec3(0,0,0)); } on collision.exited(object:one, other) { object.set_active(other, false); }");
            var first = AddObject(machine, "one", true);
            var second = AddObject(machine, "two", true);
            Attach(machine);
            second.Root.GetComponent<RuntimeObjectIdentity>().PublishCollisionEntered(first.Collider);
            Assert.AreEqual(Vector3.zero, second.Root.transform.localPosition);
            first.Root.GetComponent<RuntimeObjectIdentity>().PublishCollisionEntered(second.Collider);
            Assert.AreEqual(new Vector3(4, 5, 6), second.Root.transform.localPosition);
            Assert.AreEqual(Vector3.zero, first.Root.transform.localPosition);
            first.Root.GetComponent<RuntimeObjectIdentity>().PublishCollisionExited(second.Collider);
            Assert.False(second.Root.activeSelf);
            Assert.AreEqual(1, first.Root.GetComponents<RuntimeObjectIdentity>().Length);
        }

        [Test]
        public void WhacAMoleStartedAndA7TimerFeedbackRaiseThenLowerLiveObject()
        {
            var machine = Machine("on machine.started() { object.teleport(object:mole1, anchor:mole1Up); timer.start(\"hide\", 0.5); } on timer.elapsed(\"hide\") { object.teleport(object:mole1, anchor:mole1Down); }", new[] { Anchor("mole1Up", 0, 1, 0), Anchor("mole1Down", 0, 0, 0) });
            var mole = AddObject(machine, "mole1", false);
            Attach(machine);
            machine.CompleteStartup();
            Assert.AreEqual(new Vector3(0, 1, 0), mole.Root.transform.localPosition);
            Assert.AreEqual(1, machine.Timers.Count);
            var elapsed = 0;
            machine.Events.Subscribe(value => { if (value is RuntimeTimerElapsedEvent) elapsed++; });
            machine.Timers.Advance(.25f);
            Assert.AreEqual(new Vector3(0, 1, 0), mole.Root.transform.localPosition);
            machine.Timers.Advance(.25f);
            Assert.AreEqual(Vector3.zero, mole.Root.transform.localPosition);
            Assert.AreEqual(0, machine.Timers.Count);
            machine.Timers.Advance(1);
            Assert.AreEqual(1, elapsed);
            Assert.IsEmpty(_faults);
        }

        [Test]
        public void DirectPoseUsesMachineSpaceAndDoesNotReapplyIntrinsicCorrection()
        {
            var machine = Machine("on input.pressed(input:reset) { object.teleport_pose(object:foo, vec3(1,2,3), vec3(0,90,0)); }");
            var instance = AddObject(machine, "foo", true);
            var parent = Root("MachineSpace");
            parent.transform.position = new Vector3(10, 20, 30);
            parent.transform.rotation = Quaternion.Euler(0, 45, 0);
            instance.Root.transform.SetParent(parent.transform, false);
            var correction = Root("ModelCorrection");
            correction.transform.SetParent(instance.Root.transform, false);
            correction.transform.localScale = Vector3.one * .25f;
            correction.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            Attach(machine);
            machine.SetInputState("reset", true);
            Assert.That(Vector3.Distance(new Vector3(1, 2, 3), instance.Root.transform.localPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0, 90, 0), instance.Root.transform.localRotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(parent.transform.TransformPoint(new Vector3(1, 2, 3)), instance.Rigidbody.position), Is.LessThan(.001f));
            Assert.AreEqual(Vector3.one * .25f, correction.transform.localScale);
            Assert.That(Quaternion.Angle(Quaternion.Euler(-90, 0, 0), correction.transform.localRotation), Is.LessThan(.001f));
        }

        [Test]
        public void SetActivePreservesRegistryAndCanReactivateThroughScript()
        {
            var machine = Machine("on input.pressed(input:reset) { object.set_active(object:foo, false); } on input.released(input:reset) { object.set_active(object:foo, true); }");
            var instance = AddObject(machine, "foo", false);
            Attach(machine);
            machine.SetInputState("reset", true);
            Assert.AreSame(instance, machine.GetObject("foo"));
            Assert.False(instance.Root.activeSelf);
            machine.SetInputState("reset", false);
            Assert.True(instance.Root.activeSelf);
            Assert.AreSame(instance, machine.GetObject("foo"));
        }

        [Test]
        public void ApplyImpulseDelegatesToA7WithoutAnotherPhysicsPolicy()
        {
            var machine = Machine("on input.pressed(input:reset) { object.apply_impulse(object:foo, vec3(2,0,0)); }");
            var instance = AddObject(machine, "foo", true);
            instance.Rigidbody.mass = 2;
            instance.Rigidbody.linearDamping = 0;
            Attach(machine);
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                machine.SetInputState("reset", true);
                Physics.Simulate(.02f);
                Assert.That(instance.Rigidbody.linearVelocity.x, Is.EqualTo(1f).Within(.001f));
                Assert.That(instance.Rigidbody.linearVelocity.y, Is.EqualTo(0f).Within(.001f));
                Assert.That(instance.Rigidbody.linearVelocity.z, Is.EqualTo(0f).Within(.001f));
            }
            finally { Physics.simulationMode = previousMode; }
            Assert.IsEmpty(_faults);
        }

        [Test]
        public void ResetUsesSettledA7SemanticsWithoutReinstantiation()
        {
            var machine = Machine("on input.pressed(input:reset) { object.reset(object:foo); }");
            var instance = AddObject(machine, "foo", true, new Vector3(1, 2, 3));
            var root = instance.Root;
            instance.AuthoredInitialTransform.rotationEulerDegrees = Vector(10, 20, 30);
            instance.AuthoredInitialTransform.scale = Vector(2, 3, 4);
            instance.Root.transform.localPosition = new Vector3(9, 8, 7);
            instance.Root.transform.localRotation = Quaternion.Euler(40, 50, 60);
            instance.Root.transform.localScale = Vector3.one * 5;
            instance.Rigidbody.linearVelocity = new Vector3(1, 2, 3);
            instance.Rigidbody.angularVelocity = new Vector3(4, 5, 6);
            instance.Root.SetActive(false);
            Attach(machine);
            machine.SetInputState("reset", true);
            Assert.AreSame(instance, machine.GetObject("foo"));
            Assert.AreSame(root, instance.Root);
            Assert.True(root.activeSelf);
            Assert.AreEqual(new Vector3(1, 2, 3), root.transform.localPosition);
            Assert.That(Quaternion.Angle(Quaternion.Euler(10, 20, 30), root.transform.localRotation), Is.LessThan(.001f));
            Assert.AreEqual(new Vector3(2, 3, 4), root.transform.localScale);
            Assert.AreEqual(Vector3.zero, instance.Rigidbody.linearVelocity);
            Assert.AreEqual(Vector3.zero, instance.Rigidbody.angularVelocity);
        }

        private RuntimeOasisScriptBehavior Attach(RuntimeMachine machine) { return RuntimeOasisScriptBehavior.AttachTo(machine, _faults.Add); }
        private static void Ok(OasisScriptHostResult result) { Assert.True(result.Success, result.Error); }
        private RuntimeMachine Machine(string source = "", MachineRuntimeAnchor[] anchors = null, string kind = "Oasis")
        {
            OasisScriptProgram program = null;
            if (kind == "Oasis")
            {
                var compiled = OasisScriptCompiler.Compile(source, "behavior/behavior.oasis");
                Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));
                program = compiled.Program;
            }
            var manifest = new MachineRuntimeManifest { machineId = "adapter-test", displayName = "Adapter test", runtime = new MachineRuntimeManifestDefinition { kind = kind }, anchors = anchors ?? Array.Empty<MachineRuntimeAnchor>(), inputs = new[] { new MachineInputDefinition { id = "reset", name = "Reset" } } };
            var machine = new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition>(), program), null);
            _machines.Add(machine);
            return machine;
        }
        private RuntimeObjectInstance AddObject(RuntimeMachine machine, string id, bool body, Vector3 position = default)
        {
            var root = Root(id);
            root.transform.localPosition = position;
            var collider = root.AddComponent<SphereCollider>();
            var rigidbody = body ? root.AddComponent<Rigidbody>() : null;
            if (rigidbody != null) rigidbody.useGravity = false;
            var authored = new MachineRuntimeObjectTransform { position = Vector(position.x, position.y, position.z), rotationEulerDegrees = Vector(0, 0, 0), scale = Vector(1, 1, 1) };
            var instance = new RuntimeObjectInstance(id, id, "def", null, root, collider, rigidbody, authored);
            machine.RegisterObject(instance);
            return instance;
        }
        private static MachineRuntimeAnchor Anchor(string id, float x, float y, float z) { return new MachineRuntimeAnchor { id = id, displayName = id, position = Vector(x, y, z), rotationEulerDegrees = Vector(0, 0, 0) }; }
        private static RuntimeVector3Definition Vector(float x, float y, float z) { return new RuntimeVector3Definition { x = x, y = y, z = z }; }
        private GameObject Root(string name) { var root = new GameObject(name); _roots.Add(root); return root; }
    }
}

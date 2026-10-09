using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Oasis.Scripting;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;
using UnityEngine.TestTools;

namespace OasisPlayer.Tests
{
    public sealed class OasisScriptMachinePreviewTests
    {
        private GameObject _spawn;
        private FakeCabinetLoader _cabinet;
        private FakeObjectLoader _objects;
        private MachinePreviewLoader _loader;

        [SetUp]
        public void Setup()
        {
            _spawn = new GameObject("MachineSpawn");
            _cabinet = new FakeCabinetLoader();
            _objects = new FakeObjectLoader();
            _loader = new MachinePreviewLoader(_cabinet, new RuntimeFaceLoader(new PngRuntimeTextureAssetLoader()), new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory()), _objects);
        }

        [TearDown]
        public void TearDown()
        {
            _loader.Unload();
            if (_spawn != null) UnityEngine.Object.DestroyImmediate(_spawn);
        }

        [Test]
        public async Task OasisStartupRunsOnceWithObjectsAnchorsInputsTriggersAndDriversAvailable()
        {
            var build = Build("on machine.started() { object.teleport(object:foo, anchor:target); timer.start(\"ready\", 0.5); } on input.pressed(input:reset) { object.teleport_pose(object:foo, vec3(9,8,7), vec3(0,0,0)); } on trigger.entered(trigger:PocketLeftCorner, ball) { object.teleport(ball, anchor:target); }");
            var machine = await _loader.LoadAsync(build);
            Assert.True(_loader.HasOasisBehavior);
            Assert.IsNull(_loader.OasisBehaviorFault);
            Assert.True(machine.HasStarted);
            Assert.True(machine.IsActive);
            var instance = machine.GetObject("foo");
            Assert.AreEqual(new Vector3(1, 2, 3), instance.Root.transform.localPosition);
            Assert.True(machine.Anchors.ContainsKey("target"));
            Assert.True(machine.Inputs.ContainsKey("reset"));
            Assert.True(machine.Triggers.ContainsKey("PocketLeftCorner"));
            Assert.AreEqual(1, machine.Timers.Count);
            var sessionRoot = _spawn.transform.GetChild(0);
            Assert.AreSame(machine, sessionRoot.GetComponent<RuntimeMachineStateUpdater>().Machine);
            Assert.NotNull(sessionRoot.GetComponent<RuntimeBehaviorDriver>());
            machine.SetInputState("reset", true);
            Assert.AreEqual(new Vector3(9, 8, 7), instance.Root.transform.localPosition);
            machine.CompleteStartup();
            Assert.AreEqual(new Vector3(9, 8, 7), instance.Root.transform.localPosition);
            machine.Cabinet.GetComponentInChildren<RuntimeTriggerRelay>().PublishEntered(instance.Collider);
            Assert.AreEqual(new Vector3(1, 2, 3), instance.Root.transform.localPosition);
            Assert.AreEqual(1, machine.Cabinet.GetComponentsInChildren<RuntimeTriggerRelay>().Length);
            Assert.AreEqual(1, instance.Root.GetComponents<RuntimeObjectIdentity>().Length);
        }

        [Test]
        public async Task LoaderUnloadAndReloadCreateIndependentStateAndDetachOldSubscription()
        {
            var build = Build("state n = 0; on machine.started() { n = n + 1; object.teleport_pose(object:foo, vec3(n,0,0), vec3(0,0,0)); } on input.pressed(input:reset) { n = n + 1; object.teleport_pose(object:foo, vec3(n,0,0), vec3(0,0,0)); timer.start(\"active\", 1); }");
            var first = await _loader.LoadAsync(build);
            var firstRoot = first.GetObject("foo").Root;
            first.SetInputState("reset", true);
            Assert.AreEqual(new Vector3(2, 0, 0), firstRoot.transform.localPosition);
            _loader.Unload();
            Assert.False(_loader.HasOasisBehavior);
            Assert.IsNull(_loader.OasisBehaviorFault);
            Assert.False(first.IsActive);
            Assert.True(firstRoot == null);
            Assert.AreEqual(0, first.Timers.Count);
            Assert.AreEqual(0, _spawn.transform.childCount);
            Assert.DoesNotThrow(() => first.Events.Publish(new RuntimeInputPressedEvent("reset")));
            var second = await _loader.LoadAsync(build);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(new Vector3(1, 0, 0), second.GetObject("foo").Root.transform.localPosition);
            Assert.AreEqual(0, second.Timers.Count);
            first.Events.Publish(new RuntimeInputPressedEvent("reset"));
            Assert.AreEqual(new Vector3(1, 0, 0), second.GetObject("foo").Root.transform.localPosition);
            Assert.AreEqual(2, _objects.LoadCount);
            Assert.AreEqual(1, _objects.DisposeCount);
        }

        [Test]
        public async Task EmulationDoesNotAttachScriptsAndOasisReplacementClearsPreviousPreview()
        {
            // Even a constructed Emulation build carrying a program cannot create a hybrid session.
            var emulation = Build("on machine.started() { object.set_active(object:foo, false); }", "Emulation");
            var first = await _loader.LoadAsync(emulation);
            var oldRoot = first.GetObject("foo").Root;
            Assert.False(_loader.HasOasisBehavior);
            Assert.True(oldRoot.activeSelf);
            var second = await _loader.LoadAsync(Build("on machine.started() { object.teleport(object:foo, anchor:target); }"));
            Assert.False(first.IsActive);
            Assert.True(oldRoot == null);
            Assert.AreEqual(1, _cabinet.UnloadCount);
            Assert.AreEqual(1, _objects.DisposeCount);
            Assert.AreEqual(1, _spawn.transform.childCount);
            Assert.True(_loader.HasOasisBehavior);
            Assert.AreEqual(new Vector3(1, 2, 3), second.GetObject("foo").Root.transform.localPosition);
            var third = await _loader.LoadAsync(emulation);
            Assert.False(second.IsActive);
            Assert.False(_loader.HasOasisBehavior);
            Assert.True(third.GetObject("foo").Root.activeSelf);
        }

        [Test]
        public void InitializationFaultAbortsStartupAndCleansPartialSceneDefinitionsAndRegistries()
        {
            var driversBefore = UnityEngine.Object.FindObjectsByType<RuntimeBehaviorDriver>(FindObjectsSortMode.None).Length;
            LogAssert.Expect(LogType.Error, new Regex("Oasis Script fault in Machine 'Preview test': behavior/behavior\\.oasis:1:[0-9]+ OSR1002 .* while handling initialization"));
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadAsync(Build("const broken = 1 / 0; on machine.started() { timer.start(\"mustNotRun\", 1); }")));
            StringAssert.Contains("initialization", exception.Message);
            Assert.AreEqual(1, _cabinet.UnloadCount);
            Assert.AreEqual(1, _objects.DisposeCount);
            Assert.AreEqual(0, _spawn.transform.childCount);
            Assert.False(_loader.HasOasisBehavior);
            var failed = _cabinet.LastUnloadedMachine;
            Assert.NotNull(failed);
            Assert.False(failed.HasStarted);
            Assert.False(failed.IsActive);
            Assert.IsEmpty(failed.Objects);
            Assert.IsEmpty(failed.Anchors);
            Assert.IsEmpty(failed.Triggers);
            Assert.IsEmpty(failed.Inputs);
            Assert.AreEqual(0, failed.Timers.Count);
            Assert.AreEqual(driversBefore, UnityEngine.Object.FindObjectsByType<RuntimeBehaviorDriver>(FindObjectsSortMode.None).Length);
            failed.Events.Publish(new RuntimeMachineStartedEvent());
            Assert.AreEqual(0, failed.Timers.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingOasisProgramFailsAndCleansTheNormalLoad()
        {
            var build = Build("");
            var missing = new ResolvedRuntimeBuild(build.BuildRoot, build.Machine, build.CabinetManifestPath, build.Cabinet, build.GlbPath, build.Faces.ToArray(), build.ObjectDefinitions);
            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadAsync(missing));
            StringAssert.Contains("compiled behavior/behavior.oasis", exception.Message);
            Assert.AreEqual(0, _spawn.transform.childCount);
            Assert.AreEqual(1, _objects.DisposeCount);
            Assert.False(_cabinet.LastUnloadedMachine.HasStarted);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task HandlerFaultLogsOnceAndKeepsLoadedMachineInspectable(bool duringStartup)
        {
            var eventName = duringStartup ? "machine.started" : "input.pressed";
            var source = duringStartup
                ? "on machine.started() { object.reset(object:missing); } on input.pressed(input:reset) { object.set_active(object:foo, false); }"
                : "on input.pressed(input:reset) { object.reset(object:missing); } on input.released(input:reset) { object.set_active(object:foo, false); }";
            LogAssert.Expect(LogType.Error, new Regex("Oasis Script fault in Machine 'Preview test': behavior/behavior\\.oasis:1:[0-9]+ OSR1005 .*missing.* while handling " + Regex.Escape(eventName)));
            var machine = await _loader.LoadAsync(Build(source));
            machine.SetInputState("reset", true);
            Assert.True(_loader.HasOasisBehavior);
            Assert.True(machine.IsActive);
            Assert.True(machine.HasStarted);
            Assert.AreEqual("OSR1005", _loader.OasisBehaviorFault.Code);
            Assert.AreEqual(eventName, _loader.OasisBehaviorFault.EventName);
            var diagnostic = _loader.OasisBehaviorFault;
            machine.SetInputState("reset", false);
            machine.SetInputState("reset", true);
            machine.Events.Publish(new RuntimeTriggerEnteredEvent("PocketLeftCorner", "foo"));
            Assert.AreSame(diagnostic, _loader.OasisBehaviorFault);
            Assert.True(machine.GetObject("foo").Root.activeSelf);
            Assert.AreEqual(0, _cabinet.UnloadCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task DirectMachineUnloadAlsoDetachesLoaderOwnedBehavior()
        {
            var machine = await _loader.LoadAsync(Build("on input.pressed(input:reset) { timer.start(\"late\", 1); }"));
            machine.UnloadAssets();
            Assert.False(_loader.HasOasisBehavior);
            machine.Events.Publish(new RuntimeInputPressedEvent("reset"));
            Assert.AreEqual(0, machine.Timers.Count);
            _loader.Unload();
            Assert.AreEqual(0, _spawn.transform.childCount);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public async Task PreviewCreatesOneKeyboardOwnerAndRestoresAuthoredBindingsAfterReload()
        {
            var build = Build("on input.pressed(input:reset) { object.teleport(object:foo, anchor:target); }");
            build.Machine.inputs[0].keyboardShortcut = "R";
            var first = await _loader.LoadAsync(build);
            var owners = _spawn.GetComponentsInChildren<RuntimeInputDevelopmentControls>();
            Assert.AreEqual(1, owners.Length);
            var controls = owners[0];
            Assert.AreEqual(KeyCode.R, controls.Bindings.Single().key);
            controls.Poll(k => k == KeyCode.R);
            Assert.AreEqual(new Vector3(1, 2, 3), first.GetObject("foo").Root.transform.localPosition);
            controls.Bindings.Single().key = KeyCode.T;
            controls.Poll(k => false); controls.Poll(k => k == KeyCode.T);
            Assert.True(first.Inputs["reset"].IsPressed);
            var second = await _loader.LoadAsync(build);
            Assert.False(first.IsActive);
            Assert.True(controls == null);
            owners = _spawn.GetComponentsInChildren<RuntimeInputDevelopmentControls>();
            Assert.AreEqual(1, owners.Length);
            Assert.AreEqual(KeyCode.R, owners[0].Bindings.Single().key);
            Assert.False(second.Inputs["reset"].IsPressed);
            _loader.Unload();
            Assert.IsEmpty(_spawn.GetComponentsInChildren<RuntimeInputDevelopmentControls>());
        }

        [Test]
        public async Task UnassignedAndUnsupportedInputsLoadAsNoneAndRemainUsable()
        {
            var build = Build("");
            build.Machine.inputs = new[]
            {
                new MachineInputDefinition { id = "unassigned", name = "No key" },
                new MachineInputDefinition { id = "unsupported", name = "Bad shortcut", keyboardShortcut = "Ctrl+R" }
            };
            LogAssert.Expect(LogType.Warning, new Regex("Machine input 'unsupported' has unsupported keyboard shortcut 'Ctrl\\+R'"));
            var machine = await _loader.LoadAsync(build);
            var controls = _spawn.GetComponentInChildren<RuntimeInputDevelopmentControls>();
            Assert.AreEqual(new[] { "unassigned", "unsupported" }, controls.Bindings.Select(b => b.inputId));
            Assert.True(controls.Bindings.All(b => b.key == KeyCode.None));
            controls.Poll(k => true);
            Assert.True(machine.Inputs.Values.All(i => !i.IsPressed));
            machine.SetInputState("unsupported", true); Assert.True(machine.Inputs["unsupported"].IsPressed);
            machine.SetInputState("unsupported", false);
            controls.Bindings[0].key = KeyCode.T;
            controls.Poll(k => k == KeyCode.T); Assert.True(machine.Inputs["unassigned"].IsPressed);
            controls.Configure(Array.Empty<RuntimeInputDevelopmentControls.Binding>());
            Assert.False(machine.Inputs["unassigned"].IsPressed);
            Assert.True(machine.IsActive); Assert.IsNull(_loader.OasisBehaviorFault);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task EmulationAuthoredShortcutsDoNotEnableDevelopmentKeyboardDispatch()
        {
            var build = Build("", "Emulation"); build.Machine.inputs[0].keyboardShortcut = "R";
            var machine = await _loader.LoadAsync(build);
            var controls = _spawn.GetComponentInChildren<RuntimeInputDevelopmentControls>();
            Assert.IsEmpty(controls.Bindings);
            controls.Poll(k => true);
            Assert.False(machine.Inputs["reset"].IsPressed);
            Assert.False(_loader.HasOasisBehavior);
        }
#endif

        private static ResolvedRuntimeBuild Build(string source, string kind = "Oasis")
        {
            var compiled = OasisScriptCompiler.Compile(source, "behavior/behavior.oasis");
            Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));
            var manifest = new MachineRuntimeManifest
            {
                machineId = "preview-test", displayName = "Preview test",
                runtime = new MachineRuntimeManifestDefinition { kind = kind },
                anchors = new[] { new MachineRuntimeAnchor { id = "target", displayName = "Target", position = Vector(1, 2, 3), rotationEulerDegrees = Vector(0, 0, 0) } },
                inputs = new[] { new MachineInputDefinition { id = "reset", name = "Reset" } },
                objectInstances = new[] { new MachineRuntimeObjectInstance { id = "foo", displayName = "Foo", definitionId = "def", transform = new MachineRuntimeObjectTransform { position = Vector(0, 0, 0), rotationEulerDegrees = Vector(0, 0, 0), scale = Vector(1, 1, 1) } } }
            };
            var definition = new ResolvedRuntimeObjectDefinition("", "", new Object3DRuntimeManifest
            {
                definitionId = "def", displayName = "Small test object", modelScale = .5f, upAxis = "Z",
                collider = new Object3DRuntimeCollider { kind = "Sphere", center = new[] { 0f, 0f, 0f }, radius = .25f },
                rigidbody = new Object3DRuntimeRigidbody { enabled = true, mass = 1, useGravity = false }
            });
            return new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition> { { "def", definition } }, compiled.Program);
        }

        private static RuntimeVector3Definition Vector(float x, float y, float z) { return new RuntimeVector3Definition { x = x, y = y, z = z }; }

        private sealed class FakeCabinetLoader : ICabinetModelLoader
        {
            public int UnloadCount;
            public RuntimeMachine LastUnloadedMachine;
            public Task<GameObject> LoadAsync(string path, Transform parent)
            {
                var cabinet = new GameObject("TestCabinet");
                cabinet.transform.SetParent(parent, false);
                var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trigger.name = "OasisTrigger_PocketLeftCorner";
                trigger.transform.SetParent(cabinet.transform, false);
                UnityEngine.Object.DestroyImmediate(trigger.GetComponent<BoxCollider>());
                CabinetSemanticGeometrySetup.Setup(cabinet);
                return Task.FromResult(cabinet);
            }
            public void Unload(GameObject root)
            {
                UnloadCount++;
                var updater = root.GetComponent<RuntimeMachineStateUpdater>();
                LastUnloadedMachine = updater != null ? updater.Machine : null;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class FakeObjectLoader : IObject3DModelLoader
        {
            public int LoadCount;
            public int DisposeCount;
            public Task<IObject3DModelDefinition> LoadAsync(string path)
            {
                LoadCount++;
                return Task.FromResult<IObject3DModelDefinition>(new Definition(this));
            }
            private sealed class Definition : IObject3DModelDefinition
            {
                private readonly FakeObjectLoader _owner;
                public Definition(FakeObjectLoader owner) { _owner = owner; }
                public Task<bool> InstantiateAsync(Transform parent)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(parent, false);
                    UnityEngine.Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
                    return Task.FromResult(true);
                }
                public void Dispose() { _owner.DisposeCount++; }
            }
        }
    }
}

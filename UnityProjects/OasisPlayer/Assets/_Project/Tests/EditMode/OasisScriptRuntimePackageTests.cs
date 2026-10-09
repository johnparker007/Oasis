using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oasis.Scripting;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class OasisScriptRuntimePackageTests
    {
        private string _root;
        [SetUp] public void Setup()
        {
            _root = Path.Combine(Application.temporaryCachePath, "OasisScriptPackageTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "cabinet"));
            Directory.CreateDirectory(Path.Combine(_root, "behavior"));
            File.WriteAllBytes(Path.Combine(_root, "cabinet", "cabinet.glb"), new byte[] { 1 });
            File.WriteAllText(Path.Combine(_root, "cabinet", "cabinet.runtime.json"), "{\"schema\":\"oasis.cabinet.runtime\",\"schemaVersion\":5,\"cabinetId\":\"cabinet\",\"glb\":\"cabinet.glb\",\"scale\":1,\"upAxis\":\"Y\",\"reflections\":[]}");
            WriteMachine();
            File.WriteAllText(Path.Combine(_root, "behavior", "behavior.oasis"), "state score = 0; on machine.started() { timer.stop(\"host-call\"); score = score + 1; }");
        }
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        private void WriteMachine(int version = 9, string kind = "Oasis", string source = "behavior/behavior.oasis", MachineInputDefinition[] inputs = null)
        {
            // JsonUtility escaping keeps path test cases valid JSON on Windows too.
            var runtime = new MachineRuntimeManifestDefinition { kind = kind };
            if (kind == "Emulation") { runtime.platform = "None"; runtime.platformSettingsJson = "{}"; }
            else runtime.behavior = new MachineRuntimeBehavior { kind = "OasisScript", source = source };
            var manifest = new MachineRuntimeManifest { schema = RuntimeBuildLoader.MachineSchema, schemaVersion = version, machineId = "machine", displayName = "Pool", cabinetManifest = "cabinet/cabinet.runtime.json", anchors = Array.Empty<MachineRuntimeAnchor>(), runtime = runtime, inputs = inputs ?? Array.Empty<MachineInputDefinition>() };
            File.WriteAllText(Path.Combine(_root, "machine.runtime.json"), JsonUtility.ToJson(manifest));
        }
        [TestCase("Emulation")] [TestCase("Oasis")]
        public void Schema9RuntimeKindsLoad(string kind)
        {
            WriteMachine(kind: kind);
            Assert.True(RuntimeBuildLoader.TryLoad(_root, out var build, out var error), error);
            Assert.AreEqual(kind, build.Machine.runtime.kind);
            if (kind == "Oasis") Assert.IsInstanceOf<OasisScriptProgram>(build.ScriptProgram);
            else Assert.IsNull(build.ScriptProgram);
        }
        [Test]
        public void LoadedPackagePreservesAuthoredShortcutsAndRegistersEveryLogicalId()
        {
            WriteMachine(inputs: new[]
            {
                new MachineInputDefinition { id = "rerack", name = "Return to rack", keyboardShortcut = "R" },
                new MachineInputDefinition { id = "newGame", name = "Start again", keyboardShortcut = "N" },
                new MachineInputDefinition { id = "unassigned", keyboardShortcut = "" },
                new MachineInputDefinition { id = "unsupported", keyboardShortcut = "Ctrl+R" }
            });
            Assert.True(RuntimeBuildLoader.TryLoad(_root, out var build, out var error), error);
            Assert.AreEqual(new[] { "R", "N", "", "Ctrl+R" }, build.Machine.inputs.Select(i => i.keyboardShortcut));
            var machine = new RuntimeMachine(build, null);
            try
            {
                Assert.AreEqual(4, machine.Inputs.Count);
                Assert.AreEqual("Return to rack", machine.Inputs["rerack"].Name);
                foreach (var id in new[] { "unassigned", "unsupported" })
                {
                    machine.SetInputState(id, true); Assert.True(machine.Inputs[id].IsPressed);
                    machine.SetInputState(id, false); Assert.False(machine.Inputs[id].IsPressed);
                }
            }
            finally { machine.UnloadAssets(); }
        }

        [Test] public void PreviousSchemaRejected()
        { WriteMachine(version: 8); Assert.False(RuntimeBuildLoader.TryLoad(_root, out _, out var error)); StringAssert.Contains("schema/version", error); }
        [Test] public void UnknownRuntimeRejected()
        { WriteMachine(kind: "Unknown"); Assert.False(RuntimeBuildLoader.TryLoad(_root, out _, out var error)); StringAssert.Contains("Unknown Machine runtime", error); }
        [TestCase("../outside.oasis")] [TestCase("behavior/../../outside.oasis")] [TestCase("/outside.oasis")]
        [TestCase("C:\\outside.oasis")] [TestCase("..\\outside.oasis")]
        public void SourceContainmentIsEnforced(string source)
        { WriteMachine(source: source); Assert.False(RuntimeBuildLoader.TryLoad(_root, out _, out var error)); StringAssert.Contains("behavior", error); }
        [Test] public void MissingSourceRejected()
        { File.Delete(Path.Combine(_root, "behavior", "behavior.oasis")); Assert.False(RuntimeBuildLoader.TryLoad(_root, out _, out var error)); StringAssert.Contains("behavior/behavior.oasis is missing", error); }
        [Test] public void InvalidSourceRejectedWithCompilerLocation()
        {
            File.WriteAllText(Path.Combine(_root, "behavior", "behavior.oasis"), "on machine.started() { object.reset(1); }");
            Assert.False(RuntimeBuildLoader.TryLoad(_root, out _, out var error)); StringAssert.Contains("OS2303", error); StringAssert.Contains("behavior/behavior.oasis:1:", error);
        }
        [Test] public void ValidProgramIsSessionReadyWithoutExecutingAtLoad()
        {
            Assert.True(RuntimeBuildLoader.TryLoad(_root, out var build, out var error), error);
            var host = new RecordingHost(); var session = new OasisScriptSession(build.ScriptProgram, host);
            Assert.False(session.IsFaulted); Assert.True(session.TryGetState("score", out var score)); Assert.AreEqual(0, ((OasisScriptNumberValue)score).Value);
            Assert.AreEqual(0, host.Calls); // Creating a session initializes globals, never emits MachineStarted.
            Assert.True(session.Dispatch(OasisScriptEvent.MachineStarted())); Assert.AreEqual(1, host.Calls);
            session.TryGetState("score", out score); Assert.AreEqual(1, ((OasisScriptNumberValue)score).Value);
        }
        [Test] public void CompiledOasisProgramDoesNotEmitStartupUntilTheA7MachineStarts()
        {
            Assert.True(RuntimeBuildLoader.TryLoad(_root, out var build, out var error), error);
            var machine = new RuntimeMachine(build, null);
            try
            {
                var behavior = RuntimeOasisScriptBehavior.AttachTo(machine);
                Assert.False(machine.HasStarted);
                Assert.False(behavior.IsFaulted);
                machine.CompleteStartup();
                Assert.True(machine.HasStarted);
                Assert.False(behavior.IsFaulted);
            }
            finally { machine.UnloadAssets(); }
        }
        [Test] public void UnityUsesCanonicalScriptingPackageAndNoCopiedImplementation()
        {
            var repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var canonical = Path.Combine(repository, "WindowsNetProjects", "OasisEditor", "Oasis.Scripting", "Package");
            var manifest = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            StringAssert.Contains("\"com.oasis.scripting\": \"file:../../../WindowsNetProjects/OasisEditor/Oasis.Scripting/Package\"", manifest);
            var packagePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "../../../WindowsNetProjects/OasisEditor/Oasis.Scripting/Package"));
            Assert.AreEqual(canonical, packagePath);
            Assert.True(File.Exists(Path.Combine(canonical, "Runtime", "OasisScriptSession.cs")));
            StringAssert.Contains("\"noEngineReferences\": true", File.ReadAllText(Path.Combine(canonical, "Runtime", "Oasis.Scripting.asmdef")));
            Assert.AreEqual("Oasis.Scripting", typeof(OasisScriptCompiler).Assembly.GetName().Name);
            Assert.IsEmpty(Directory.GetFiles(Application.dataPath, "OasisScriptCompiler.cs", SearchOption.AllDirectories));
            Assert.IsEmpty(Directory.GetFiles(Application.dataPath, "OasisScriptSession.cs", SearchOption.AllDirectories));
        }
        private sealed class RecordingHost : IOasisScriptHost
        {
            public int Calls;
            private OasisScriptHostResult Record() { Calls++; return OasisScriptHostResult.Ok; }
            public OasisScriptHostResult SetActive(OasisScriptReferenceValue obj, bool active) => Record();
            public OasisScriptHostResult Teleport(OasisScriptReferenceValue obj, OasisScriptReferenceValue anchor) => Record();
            public OasisScriptHostResult TeleportPose(OasisScriptReferenceValue obj, OasisScriptVec3Value position, OasisScriptVec3Value rotation) => Record();
            public OasisScriptHostResult SetVelocity(OasisScriptReferenceValue obj, OasisScriptVec3Value velocity) => Record();
            public OasisScriptHostResult SetAngularVelocity(OasisScriptReferenceValue obj, OasisScriptVec3Value velocity) => Record();
            public OasisScriptHostResult ApplyImpulse(OasisScriptReferenceValue obj, OasisScriptVec3Value impulse) => Record();
            public OasisScriptHostResult ResetObject(OasisScriptReferenceValue obj) => Record();
            public OasisScriptHostResult StartTimer(string name, double seconds) => Record();
            public OasisScriptHostResult StopTimer(string name) => Record();
        }
    }
}

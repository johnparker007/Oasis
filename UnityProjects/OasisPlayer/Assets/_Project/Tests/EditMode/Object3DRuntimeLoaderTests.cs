using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class Object3DRuntimeLoaderTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void RuntimeMachineRegistersLooksUpAndRejectsDuplicateObjectIds()
        {
            var machine = Machine(Array.Empty<MachineRuntimeObjectInstance>(), Definition("None"));
            var root = NewRoot("first");
            var instance = new RuntimeObjectInstance("ball01", "Ball 1", "def", machine.Build.ObjectDefinitions["def"], root, null, null, Transform());
            machine.RegisterObject(instance);

            Assert.AreSame(instance, machine.GetObject("ball01"));
            Assert.True(machine.TryGetObject("ball01", out var found));
            Assert.AreSame(instance, found);
            Assert.False(machine.TryGetObject("missing", out _));
            StringAssert.Contains("missing", Assert.Throws<KeyNotFoundException>(() => machine.GetObject("missing")).Message);
            StringAssert.Contains("ball01", Assert.Throws<InvalidOperationException>(() => machine.RegisterObject(instance)).Message);
        }

        [Test]
        public async Task SharedDefinitionCreatesIndependentPoolObjectsAndLoadsOnlyOnce()
        {
            var declarations = new[] { Instance("cueBall", 1), Instance("ball01", 2), Instance("ball08", 3) };
            var definition = Definition("Sphere", rigidbody: true);
            var machine = Machine(declarations, definition);
            var parent = NewRoot("Machine");
            var modelLoader = new FakeModelLoader();
            using (var loader = new Object3DRuntimeLoader(modelLoader)) await loader.LoadAsync(machine, parent.transform);

            Assert.AreEqual(1, modelLoader.LoadCount);
            Assert.AreEqual(3, modelLoader.Definition.InstantiateCount);
            Assert.AreEqual(3, machine.Objects.Count);
            Assert.AreNotSame(machine.GetObject("cueBall").Root, machine.GetObject("ball01").Root);
            foreach (var id in new[] { "cueBall", "ball01", "ball08" })
            {
                var live = machine.GetObject(id);
                Assert.AreSame(definition, live.Definition);
                Assert.IsInstanceOf<SphereCollider>(live.Collider);
                Assert.NotNull(live.Rigidbody);
                Assert.AreSame(live.Root, live.Rigidbody.gameObject);
                Assert.AreSame(live.Rigidbody, live.Collider.attachedRigidbody);
                Assert.AreEqual(0.17f, live.Rigidbody.mass);
                Assert.True(live.Rigidbody.useGravity);
                Assert.True(live.Root.GetComponentInChildren<MeshRenderer>().enabled);
            }
            Assert.AreEqual(new Vector3(1, 2, 3), machine.GetObject("cueBall").Root.transform.localPosition);
            Assert.AreEqual(new Vector3(2, 3, 4), machine.GetObject("ball01").Root.transform.localPosition);
            WithScriptPhysics(() =>
            {
                foreach (var id in new[] { "cueBall", "ball01", "ball08" }) machine.GetObject(id).Rigidbody.useGravity = false;
                machine.GetObject("cueBall").Rigidbody.position = new Vector3(10, 0, 0);
                machine.GetObject("ball01").Rigidbody.position = new Vector3(20, 0, 0);
                machine.GetObject("ball08").Rigidbody.position = new Vector3(30, 0, 0);
                Physics.Simulate(.02f);
            });
            AssertVector(new Vector3(10, 0, 0), machine.GetObject("cueBall").Root.transform.position);
            AssertVector(new Vector3(20, 0, 0), machine.GetObject("ball01").Root.transform.position);
            AssertVector(new Vector3(30, 0, 0), machine.GetObject("ball08").Root.transform.position);
        }

        [Test]
        public async Task ReusesGlbImportAcrossDifferentDefinitionIdsWithSameAbsolutePath()
        {
            var firstDefinition = Definition("None");
            var secondDefinition = Definition("None");
            secondDefinition.Manifest.definitionId = "other-def";
            var first = Instance("first", 0);
            var second = Instance("second", 1);
            second.definitionId = "other-def";
            var definitions = new Dictionary<string, ResolvedRuntimeObjectDefinition>
            {
                { "def", firstDefinition },
                { "other-def", secondDefinition }
            };
            var machine = Machine(new[] { first, second }, definitions);
            var parent = NewRoot("Machine");
            var modelLoader = new FakeModelLoader();
            using (var loader = new Object3DRuntimeLoader(modelLoader)) await loader.LoadAsync(machine, parent.transform);

            Assert.AreEqual(1, modelLoader.LoadCount);
            Assert.AreEqual(2, machine.Objects.Count);
            Assert.AreNotSame(machine.GetObject("first").Root, machine.GetObject("second").Root);
        }

        [Test]
        public async Task AppliesPlacementSeparatelyFromIntrinsicScaleAndXAxisConversion()
        {
            var declaration = Instance("object", 4);
            declaration.transform.rotationEulerDegrees = Vector(13, 27, 41);
            declaration.transform.scale = Vector(2, 3, 4);
            var definition = Definition("None", upAxis: "X", modelScale: .5f);
            var machine = Machine(new[] { declaration }, definition);
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);

            var root = machine.GetObject("object").Root.transform;
            Assert.That(Quaternion.Angle(Quaternion.Euler(13, 27, 41), root.localRotation), Is.LessThan(.001f));
            AssertVector(new Vector3(2, 3, 4), root.localScale);
            AssertVector(Vector3.one * .5f, root.GetChild(0).localScale);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0, 0, 90), root.GetChild(0).localRotation), Is.LessThan(.001f));
        }

        [TestCase("Sphere")]
        [TestCase("Box")]
        [TestCase("Capsule")]
        public async Task CreatesAndConfiguresPrimitiveCollider(string kind)
        {
            var definition = Definition(kind);
            var machine = Machine(new[] { Instance("shape", 0) }, definition);
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);

            var collider = machine.GetObject("shape").Collider;
            if (kind == "Sphere") { var value = (SphereCollider)collider; Assert.AreEqual(new Vector3(1, 2, 3), value.center); Assert.AreEqual(.25f, value.radius); }
            if (kind == "Box") { var value = (BoxCollider)collider; Assert.AreEqual(new Vector3(1, 2, 3), value.center); Assert.AreEqual(new Vector3(4, 5, 6), value.size); }
            if (kind == "Capsule") { var value = (CapsuleCollider)collider; Assert.AreEqual(new Vector3(1, 2, 3), value.center); Assert.AreEqual(.25f, value.radius); Assert.AreEqual(2f, value.height); Assert.AreEqual(2, value.direction); }
        }

        [Test]
        public async Task PrimitivePhysicsCorrectionMatchesModelScaleAndNonYUpAxis()
        {
            var definition = Definition("Sphere", rigidbody: true, upAxis: "Z", modelScale: .5f);
            var machine = Machine(new[] { Instance("ball", 0) }, definition);
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);

            var live = machine.GetObject("ball");
            var modelCorrection = live.Root.transform.Find("ModelCorrection");
            var physicsCorrection = live.Root.transform.Find("PhysicsCorrection");
            Assert.AreEqual(modelCorrection.localScale, physicsCorrection.localScale);
            Assert.That(Quaternion.Angle(modelCorrection.localRotation, physicsCorrection.localRotation), Is.LessThan(.001f));
            Assert.AreEqual(Vector3.one * .5f, physicsCorrection.localScale);
            Assert.That(Quaternion.Angle(Quaternion.Euler(-90, 0, 0), physicsCorrection.localRotation), Is.LessThan(.001f));
            Assert.AreSame(live.Rigidbody, live.Collider.attachedRigidbody);
        }

        [TestCase("Box")]
        [TestCase("Capsule")]
        public async Task OrientedPrimitiveColliderUsesExactIntrinsicCorrection(string kind)
        {
            var machine = Machine(new[] { Instance("shape", 0) }, Definition(kind, rigidbody: true, upAxis: "X", modelScale: .25f));
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);

            var live = machine.GetObject("shape");
            Assert.AreEqual("PhysicsCorrection", live.Collider.gameObject.name);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0, 0, 90), live.Collider.transform.localRotation), Is.LessThan(.001f));
            Assert.AreEqual(Vector3.one * .25f, live.Collider.transform.localScale);
            if (kind == "Capsule") Assert.AreEqual(2, ((CapsuleCollider)live.Collider).direction);
            Assert.AreSame(live.Rigidbody, live.Collider.attachedRigidbody);
        }

        [Test]
        public async Task NoneColliderAndDisabledRigidbodyCreateNeither()
        {
            var machine = Machine(new[] { Instance("plain", 0) }, Definition("None"));
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);
            Assert.Null(machine.GetObject("plain").Collider);
            Assert.Null(machine.GetObject("plain").Rigidbody);
        }

        [Test]
        public async Task MeshColliderUsesOnlyUsableModelMesh()
        {
            var machine = Machine(new[] { Instance("mesh", 0) }, Definition("Mesh", rigidbody: true));
            var parent = NewRoot("Machine");
            var fake = new FakeModelLoader();
            using (var loader = new Object3DRuntimeLoader(fake)) await loader.LoadAsync(machine, parent.transform);
            Assert.IsInstanceOf<MeshCollider>(machine.GetObject("mesh").Collider);
            Assert.AreSame(fake.Definition.Mesh, ((MeshCollider)machine.GetObject("mesh").Collider).sharedMesh);
            Assert.AreSame(machine.GetObject("mesh").Rigidbody, machine.GetObject("mesh").Collider.attachedRigidbody);
            Assert.AreSame(machine.GetObject("mesh").Root, machine.GetObject("mesh").Rigidbody.gameObject);
        }

        [Test]
        public void MultipleMeshFailureIdentifiesInstanceAndCleansPartialObjects()
        {
            var machine = Machine(new[] { Instance("ball08", 0) }, Definition("Mesh"));
            var parent = NewRoot("Machine");
            var fake = new FakeModelLoader { MeshCount = 2 };
            using (var loader = new Object3DRuntimeLoader(fake))
            {
                var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync(machine, parent.transform));
                StringAssert.Contains("ball08", error.Message);
                StringAssert.Contains("exactly one usable mesh", error.Message);
            }
            Assert.AreEqual(0, machine.Objects.Count);
            Assert.IsNull(parent.transform.Find("Objects"));
        }

        [Test]
        public async Task MachineUnloadDestroysInstancesAndClearsRegistry()
        {
            var machine = Machine(new[] { Instance("ball", 0) }, Definition("Sphere"));
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);
            var root = machine.GetObject("ball").Root;
            machine.UnloadAssets();
            Assert.AreEqual(0, machine.Objects.Count);
            Assert.True(root == null);
        }

        [Test]
        public async Task MovingRigidbodyMovesRegisteredAuthoritativeRoot()
        {
            var machine = Machine(new[] { Instance("ball", 0) }, Definition("Sphere", rigidbody: true));
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);
            var live = machine.GetObject("ball");

            WithScriptPhysics(() =>
            {
                live.Rigidbody.useGravity = false;
                live.Rigidbody.position = new Vector3(9, 8, 7);
                Physics.Simulate(.02f);
            });

            Assert.AreSame(live.Root.transform, live.Rigidbody.transform);
            AssertVector(new Vector3(9, 8, 7), live.Root.transform.position);
            Assert.AreEqual(new Vector3(0, 1, 2), live.AuthoredInitialTransform.position.Value);
        }

        [Test]
        public async Task BuildsPrefabChildrenAndRegistersStableNodeIds()
        {
            var definition = Definition("None");
            var rootNode = definition.Manifest.root;
            var childId = "child-node-guid";
            rootNode.children = new[] { new Object3DRuntimeNode
            {
                id = childId, name = "Articulated Child", children = Array.Empty<Object3DRuntimeNode>(),
                components = new[]
                {
                    new Object3DRuntimeComponent { componentId = "child-transform", kind = "Transform", contractVersion = 1, transform = new Object3DRuntimeTransform { position = Vector(1, 2, 3), rotationEulerDegrees = Vector(0, 45, 0), scale = Vector(1, 2, 1) } },
                    new Object3DRuntimeComponent { componentId = "child-collider", kind = "Collider", contractVersion = 1, collider = new Object3DRuntimeCollider { kind = "Sphere", center = new[] { 0f, 0f, 0f }, radius = .5f, sourceFrame = "NodeLocal" } }
                }
            } };
            var machine = Machine(new[] { Instance("ball01", 0) }, definition);
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);

            var rootKey = machine.GetObject("ball01").Id;
            var childKey = "@node:6:ball01:child-node-guid";
            Assert.AreEqual(2, machine.ObjectNodes.Count);
            Assert.AreEqual(rootKey, machine.GetObjectNode(rootKey).RuntimeId);
            var childNode = machine.GetObjectNode(childKey);
            Assert.AreEqual(childId, childNode.PrefabNodeId);
            Assert.AreEqual("Articulated Child", childNode.Name);
            Assert.AreEqual(new Vector3(1, 2, 3), childNode.Transform.localPosition);
            Assert.AreEqual(new Vector3(1, 2, 1), childNode.Transform.localScale);
            Assert.AreSame(machine.GetObject("ball01").Root.transform, childNode.Transform.parent);
            Assert.AreSame(childNode.GameObject, childNode.Collider.gameObject);
            Assert.IsNull(childNode.Rigidbody);
            Assert.IsNull(machine.GetObject("ball01").Rigidbody);
        }

        [Test]
        public async Task CombinesPlacementAndPrefabRootTransformOnAuthoritativeObject()
        {
            var declaration = Instance("ball", 2);
            declaration.transform.rotationEulerDegrees = Vector(0, 90, 0);
            declaration.transform.scale = Vector(2, 2, 2);
            var definition = Definition("None");
            var transform = definition.Manifest.root.GetComponent("Transform");
            transform.transform.position = Vector(1, 0, 0);
            var machine = Machine(new[] { declaration }, definition);
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);

            var live = machine.GetObject("ball");
            AssertVector(new Vector3(2, 3, 2), live.Root.transform.localPosition);
            AssertVector(new Vector3(2, 2, 2), live.Root.transform.localScale);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0, 90, 0), live.Root.transform.localRotation), Is.LessThan(.001f));
            AssertVector(live.Root.transform.localPosition, live.AuthoredInitialTransform.position.Value);
            AssertVector(live.Root.transform.localScale, live.AuthoredInitialTransform.scale.Value);
        }

        [Test]
        public async Task RejectsUnrepresentableShearWhenCombiningRootAndPlacement()
        {
            var declaration = Instance("sheared", 0);
            declaration.transform.scale = Vector(2, 1, 1);
            var definition = Definition("Sphere");
            definition.Manifest.root.GetComponent("Transform").transform.rotationEulerDegrees = Vector(0, 45, 0);
            var machine = Machine(new[] { declaration }, definition);
            var parent = NewRoot("Machine");

            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader()))
            {
                var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync(machine, parent.transform));
                StringAssert.Contains("shear", error.Message);
            }

            Assert.AreEqual(0, machine.Objects.Count);
            Assert.IsNull(parent.transform.Find("Objects"));
        }

        [Test]
        public async Task LoadsAuthoredParentBeforeChildAndRejectsParentCycles()
        {
            var parentDeclaration = Instance("parent", 0);
            var childDeclaration = Instance("child", 0);
            childDeclaration.parentId = "parent";
            var machine = Machine(new[] { childDeclaration, parentDeclaration }, Definition("None"));
            var parent = NewRoot("Machine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader())) await loader.LoadAsync(machine, parent.transform);
            Assert.AreSame(machine.GetObject("parent").Root.transform, machine.GetObject("child").Root.transform.parent);

            var cycleA = Instance("cycleA", 0); cycleA.parentId = "cycleB";
            var cycleB = Instance("cycleB", 0); cycleB.parentId = "cycleA";
            var cyclicMachine = Machine(new[] { cycleA, cycleB }, Definition("None"));
            var cycleRoot = NewRoot("CycleMachine");
            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader()))
            {
                var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync(cyclicMachine, cycleRoot.transform));
                StringAssert.Contains("cycle", error.Message);
            }
            Assert.AreEqual(0, cyclicMachine.Objects.Count);
            Assert.IsNull(cycleRoot.transform.Find("Objects"));
        }

        [Test]
        public async Task RejectsActiveRigidbodyInChildInstanceWhenAncestorOwnsOne()
        {
            var parentDeclaration = Instance("movingParent", 0);
            var childDeclaration = Instance("movingChild", 0);
            childDeclaration.parentId = parentDeclaration.id;
            var machine = Machine(new[] { childDeclaration, parentDeclaration }, Definition("Sphere", rigidbody: true));
            var parent = NewRoot("Machine");

            using (var loader = new Object3DRuntimeLoader(new FakeModelLoader()))
            {
                var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync(machine, parent.transform));
                StringAssert.Contains("active Rigidbody", error.Message);
                StringAssert.Contains("movingParent", error.Message);
            }

            Assert.AreEqual(0, machine.Objects.Count);
            Assert.IsNull(parent.transform.Find("Objects"));
        }

        private RuntimeMachine Machine(MachineRuntimeObjectInstance[] instances, ResolvedRuntimeObjectDefinition definition)
        {
            var definitions = new Dictionary<string, ResolvedRuntimeObjectDefinition> { { "def", definition } };
            return Machine(instances, definitions);
        }

        private RuntimeMachine Machine(MachineRuntimeObjectInstance[] instances, IReadOnlyDictionary<string, ResolvedRuntimeObjectDefinition> definitions)
        {
            var manifest = new MachineRuntimeManifest { anchors = Array.Empty<MachineRuntimeAnchor>(), objectInstances = instances };
            return new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(), definitions), null);
        }

        private static ResolvedRuntimeObjectDefinition Definition(string kind, bool rigidbody = false, string upAxis = "Y", float modelScale = 1)
        {
            var transform = new Object3DRuntimeComponent { componentId = "transform", kind = "Transform", contractVersion = 1, transform = new Object3DRuntimeTransform
            { position = Vector(0, 0, 0), rotationEulerDegrees = Vector(0, 0, 0), scale = Vector(1, 1, 1) } };
            var model = new Object3DRuntimeComponent { componentId = "model", kind = "Model", contractVersion = 1, model = new Object3DRuntimeModel { path = "model.glb", scale = modelScale, upAxis = upAxis } };
            var collider = new Object3DRuntimeComponent { componentId = "collider", kind = "Collider", contractVersion = 1, collider = new Object3DRuntimeCollider
            { kind = kind, center = kind == "None" || kind == "Mesh" ? Array.Empty<float>() : new[] { 1f, 2f, 3f }, radius = kind == "Sphere" || kind == "Capsule" ? .25f : 0f,
                size = kind == "Box" ? new[] { 4f, 5f, 6f } : Array.Empty<float>(), height = kind == "Capsule" ? 2f : 0f, axis = kind == "Capsule" ? "Z" : string.Empty, sourceFrame = "ModelLocal", isTrigger = false } };
            var body = new Object3DRuntimeComponent { componentId = "body", kind = "Rigidbody", contractVersion = 1, rigidbody = new Object3DRuntimeRigidbody
            { mode = rigidbody ? "Dynamic" : "Static", mass = rigidbody ? .17f : 0f, useGravity = rigidbody } };
            var root = new Object3DRuntimeNode { id = "root-node-guid", name = "Root", components = new[] { transform, model, collider, body }, children = Array.Empty<Object3DRuntimeNode>() };
            var manifest = new Object3DRuntimeManifest { schema = RuntimeBuildLoader.ObjectSchema, schemaVersion = RuntimeBuildLoader.ObjectSchemaVersion,
                definitionId = "def", displayName = "Pool Ball", root = root };
            var modelPaths = new Dictionary<string, string> { { "model.glb", "model" } };
            return new ResolvedRuntimeObjectDefinition("manifest", modelPaths, manifest);
        }

        private static MachineRuntimeObjectInstance Instance(string id, float x)
        {
            return new MachineRuntimeObjectInstance { id = id, displayName = id, definitionId = "def", transform = new MachineRuntimeObjectTransform { position = Vector(x, x + 1, x + 2), rotationEulerDegrees = Vector(0, 0, 0), scale = Vector(1, 1, 1) } };
        }

        private static MachineRuntimeObjectTransform Transform() { return new MachineRuntimeObjectTransform { position = Vector(0, 0, 0), rotationEulerDegrees = Vector(0, 0, 0), scale = Vector(1, 1, 1) }; }
        private static RuntimeVector3Definition Vector(float x, float y, float z) { return new RuntimeVector3Definition { x = x, y = y, z = z }; }
        private static void AssertVector(Vector3 expected, Vector3 actual) { Assert.That(Vector3.Distance(expected, actual), Is.LessThan(.001f), "Expected " + expected + " but got " + actual); }
        private static void WithScriptPhysics(Action action)
        {
            var previous = Physics.simulationMode;
            try { Physics.simulationMode = SimulationMode.Script; action(); }
            finally { Physics.simulationMode = previous; }
        }
        private GameObject NewRoot(string name) { var value = new GameObject(name); _created.Add(value); return value; }

        private sealed class FakeModelLoader : IObject3DModelLoader
        {
            public int LoadCount;
            public int MeshCount = 1;
            public FakeDefinition Definition;
            public Task<IObject3DModelDefinition> LoadAsync(string path) { LoadCount++; Definition = new FakeDefinition(MeshCount); return Task.FromResult<IObject3DModelDefinition>(Definition); }
        }

        private sealed class FakeDefinition : IObject3DModelDefinition
        {
            private readonly int _meshCount;
            public FakeDefinition(int meshCount) { _meshCount = meshCount; }
            public int InstantiateCount;
            public Mesh Mesh;
            public Task<bool> InstantiateAsync(Transform parent)
            {
                InstantiateCount++;
                for (var i = 0; i < _meshCount; i++)
                {
                    var child = new GameObject("Visual" + i); child.transform.SetParent(parent, false);
                    var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
                    if (Mesh == null) Mesh = mesh;
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    child.AddComponent<MeshRenderer>();
                }
                return Task.FromResult(true);
            }
            public void Dispose() { }
        }
    }
}

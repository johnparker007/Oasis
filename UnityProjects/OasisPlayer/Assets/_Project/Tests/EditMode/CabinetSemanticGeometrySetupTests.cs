using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections.Generic;

namespace OasisPlayer.Tests
{
    public sealed class CabinetSemanticGeometrySetupTests
    {
        private GameObject _root;
        private Mesh _mesh;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Cabinet");
            _mesh = new Mesh { name = "OrdinaryMesh", vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            _material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_mesh);
            Object.DestroyImmediate(_material);
        }

        [TestCase("OasisCollider_Surface", CabinetSemanticGeometryKind.Collider)]
        [TestCase("OasisTrigger_Pocket", CabinetSemanticGeometryKind.Trigger)]
        [TestCase("OasisFace_Display", CabinetSemanticGeometryKind.FaceTarget)]
        [TestCase("Ordinary", CabinetSemanticGeometryKind.Visual)]
        [TestCase("COL_Surface", CabinetSemanticGeometryKind.Visual)]
        [TestCase("TRG_Pocket", CabinetSemanticGeometryKind.Visual)]
        public void ClassifyNameMatchesAuthoredContract(string name, CabinetSemanticGeometryKind expected)
        {
            Assert.AreEqual(expected, CabinetSemanticGeometry.ClassifyName(name));
        }

        [Test]
        public void SemanticNodeNameTakesPrecedenceOverMeshName()
        {
            Assert.AreEqual(CabinetSemanticGeometryKind.Collider,
                CabinetSemanticGeometry.Classify("OasisCollider_Node", "OasisTrigger_Mesh"));
            Assert.AreEqual(CabinetSemanticGeometryKind.FaceTarget,
                CabinetSemanticGeometry.Classify("OasisFace_Node", "OasisCollider_Mesh"));
        }

        [Test]
        public void SemanticMeshNameClassifiesOrdinaryNode()
        {
            Assert.AreEqual(CabinetSemanticGeometryKind.Trigger,
                CabinetSemanticGeometry.Classify("OrdinaryNode", "OasisTrigger_Mesh"));
        }

        [TestCase("OasisCollider_Surface", false)]
        [TestCase("OasisTrigger_Pocket", true)]
        public void SetupAddsOneColliderUsingInstantiatedMeshAndHidesRenderer(string name, bool isTrigger)
        {
            var instance = CreateMeshInstance(name, _mesh, true);

            var summary = $"Cabinet semantic geometry: Colliders: {(isTrigger ? 0 : 1)}, Triggers: {(isTrigger ? 1 : 0)}";
            LogAssert.Expect(LogType.Log, summary);
            var first = CabinetSemanticGeometrySetup.Setup(_root);
            LogAssert.Expect(LogType.Log, summary);
            CabinetSemanticGeometrySetup.Setup(_root);

            var colliders = instance.GetComponents<MeshCollider>();
            Assert.AreEqual(1, colliders.Length);
            Assert.AreSame(_mesh, colliders[0].sharedMesh);
            Assert.AreEqual(isTrigger, colliders[0].isTrigger);
            Assert.AreEqual(isTrigger, colliders[0].convex);
            Assert.False(instance.GetComponent<MeshRenderer>().enabled);
            Assert.AreEqual(isTrigger ? 0 : 1, first.ColliderCount);
            Assert.AreEqual(isTrigger ? 1 : 0, first.TriggerCount);
            Assert.IsNull(instance.GetComponent<Rigidbody>());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MeshSemanticWorksWithoutMaterialOrUvs()
        {
            _mesh.name = "OasisCollider_SharedMesh";
            Assert.AreEqual(0, _mesh.uv.Length);
            var instance = CreateMeshInstance("OrdinaryNode", _mesh, false);

            CabinetSemanticGeometrySetup.Setup(_root);

            Assert.AreSame(_mesh, instance.GetComponent<MeshCollider>().sharedMesh);
            Assert.False(instance.GetComponent<MeshRenderer>().enabled);
            Assert.AreEqual(0, instance.GetComponent<MeshRenderer>().sharedMaterials.Length);
        }

        [Test]
        public void VisualAndFaceRemainRenderedAndDoNotReceivePhysics()
        {
            var visual = CreateMeshInstance("OrdinaryVisual", _mesh, true);
            var face = CreateMeshInstance("OasisFace_Display", _mesh, true);

            var result = CabinetSemanticGeometrySetup.Setup(_root);

            Assert.AreEqual(0, result.ColliderCount);
            Assert.AreEqual(0, result.TriggerCount);
            Assert.IsNull(visual.GetComponent<MeshCollider>());
            Assert.IsNull(face.GetComponent<MeshCollider>());
            Assert.True(visual.GetComponent<MeshRenderer>().enabled);
            Assert.True(face.GetComponent<MeshRenderer>().enabled);
        }

        [Test]
        public void SetupDoesNotChangeHierarchyOrTransforms()
        {
            var parent = new GameObject("Parent");
            parent.transform.SetParent(_root.transform, false);
            var instance = CreateMeshInstance("OasisCollider_Surface", _mesh, true, parent.transform);
            instance.transform.localPosition = new Vector3(1, 2, 3);
            instance.transform.localRotation = Quaternion.Euler(10, 20, 30);
            instance.transform.localScale = new Vector3(2, 3, 4);
            var position = instance.transform.localPosition;
            var rotation = instance.transform.localRotation;
            var scale = instance.transform.localScale;

            CabinetSemanticGeometrySetup.Setup(_root);

            Assert.AreSame(parent.transform, instance.transform.parent);
            Assert.AreEqual(position, instance.transform.localPosition);
            Assert.AreEqual(rotation, instance.transform.localRotation);
            Assert.AreEqual(scale, instance.transform.localScale);
        }

        [Test]
        public void SemanticNodeWithoutInstantiatedMeshFailsWithIdentity()
        {
            new GameObject("OasisTrigger_Broken").transform.SetParent(_root.transform, false);
            var exception = Assert.Throws<System.InvalidOperationException>(() => CabinetSemanticGeometrySetup.Setup(_root));
            StringAssert.Contains("OasisTrigger_Broken", exception.Message);
            StringAssert.Contains("no MeshFilter mesh", exception.Message);
        }

        [Test]
        public void RegisterTriggersUsesWinningSemanticNameAndAddsNoRigidbody()
        {
            _mesh.name = "OasisTrigger_PocketLeftCorner";
            var instance = CreateMeshInstance("OrdinaryNode", _mesh, true);
            CabinetSemanticGeometrySetup.Setup(_root);
            var machine = Machine();
            LogAssert.Expect(LogType.Log, "Cabinet semantic triggers registered: 1");
            Assert.AreEqual(1, CabinetSemanticGeometrySetup.RegisterTriggers(_root, machine));
            Assert.AreSame(instance.GetComponent<MeshCollider>(), machine.GetTrigger("PocketLeftCorner").Collider);
            Assert.IsNull(_root.GetComponentInChildren<Rigidbody>());
        }

        [Test]
        public void DuplicateAndInvalidTriggerIdsFailClearly()
        {
            CreateMeshInstance("OasisTrigger_Pocket", _mesh, true);
            CreateMeshInstance("OasisTrigger_Pocket", _mesh, true);
            CabinetSemanticGeometrySetup.Setup(_root);
            StringAssert.Contains("Duplicate Cabinet trigger ID 'Pocket'", Assert.Throws<System.InvalidOperationException>(() => CabinetSemanticGeometrySetup.RegisterTriggers(_root, Machine())).Message);

            Object.DestroyImmediate(_root);
            _root = new GameObject("Cabinet");
            CreateMeshInstance("OasisTrigger_", _mesh, true);
            CabinetSemanticGeometrySetup.Setup(_root);
            StringAssert.Contains("empty or invalid", Assert.Throws<System.InvalidOperationException>(() => CabinetSemanticGeometrySetup.RegisterTriggers(_root, Machine())).Message);
        }

        [Test]
        public void TriggerRelayIgnoresColliderWithoutRegisteredObjectIdentity()
        {
            var trigger = CreateMeshInstance("OasisTrigger_Pocket", _mesh, true);
            CabinetSemanticGeometrySetup.Setup(_root);
            var machine = Machine();
            LogAssert.Expect(LogType.Log, "Cabinet semantic triggers registered: 1");
            CabinetSemanticGeometrySetup.RegisterTriggers(_root, machine);
            var events = 0; machine.Events.Subscribe(_ => events++);
            var unrelated = new GameObject("Unrelated");
            try { trigger.GetComponent<RuntimeTriggerRelay>().PublishEntered(unrelated.AddComponent<BoxCollider>()); }
            finally { Object.DestroyImmediate(unrelated); }
            Assert.AreEqual(0, events);
        }

        private static RuntimeMachine Machine()
        {
            var manifest = new MachineRuntimeManifest { anchors = System.Array.Empty<MachineRuntimeAnchor>() };
            return new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", System.Array.Empty<MachineRuntimeFaceReference>(), new Dictionary<string, ResolvedRuntimeObjectDefinition>()), null);
        }

        private GameObject CreateMeshInstance(string name, Mesh mesh, bool withMaterial, Transform parent = null)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent != null ? parent : _root.transform, false);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = instance.AddComponent<MeshRenderer>();
            if (withMaterial) renderer.sharedMaterial = _material;
            return instance;
        }
    }
}

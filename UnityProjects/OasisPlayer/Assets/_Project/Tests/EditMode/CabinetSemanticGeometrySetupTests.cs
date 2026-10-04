using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

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

            var first = CabinetSemanticGeometrySetup.Setup(_root);
            CabinetSemanticGeometrySetup.Setup(_root);

            var colliders = instance.GetComponents<MeshCollider>();
            Assert.AreEqual(1, colliders.Length);
            Assert.AreSame(_mesh, colliders[0].sharedMesh);
            Assert.AreEqual(isTrigger, colliders[0].isTrigger);
            Assert.False(colliders[0].convex);
            Assert.False(instance.GetComponent<MeshRenderer>().enabled);
            Assert.AreEqual(isTrigger ? 0 : 1, first.ColliderCount);
            Assert.AreEqual(isTrigger ? 1 : 0, first.TriggerCount);
            Assert.IsNull(instance.GetComponent<Rigidbody>());
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

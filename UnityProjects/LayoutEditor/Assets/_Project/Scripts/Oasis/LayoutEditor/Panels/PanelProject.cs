using RuntimeInspectorNamespace;
using UnityEngine;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelProject : PanelBase
    {
        private const string kPseudoSceneName = "Assets";

        private RuntimeHierarchy _runtimeHierarchy = null;

        private Transform _runtimeHierarchyAssetsParentDirTransform;
        private Transform _runtimeHierarchyAssetsChildDirTransform;


        protected override void Awake()
        {
            base.Awake();

            
        }

        protected override void AddListeners()
        {
            // TODO listeners for when Assets changed?
        }

        protected override void RemoveListeners()
        {
        }

        protected override void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            _runtimeHierarchy = GetComponentInChildren<RuntimeHierarchy>(true);

            _runtimeHierarchy.CreatePseudoScene(kPseudoSceneName);

            // TEST CODE - CREATE SOME GAME OBJECTS TO MIRROR ASSETS DIR
            _runtimeHierarchyAssetsParentDirTransform = new GameObject("Test Parent Dir").transform;

            _runtimeHierarchyAssetsChildDirTransform = new GameObject("Test Child Dir").transform;
            _runtimeHierarchyAssetsChildDirTransform.parent = _runtimeHierarchyAssetsParentDirTransform;


            _initialised = true;
        }

        protected override void Populate()
        {
            _runtimeHierarchy.AddToPseudoScene(kPseudoSceneName, _runtimeHierarchyAssetsParentDirTransform);
        }
    }

}

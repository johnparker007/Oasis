using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.LayoutEditor
{
    public class SelectionController : MonoBehaviour
    {
        public UnityEvent OnSelectionChange;
        public UnityEvent OnSelectionMove;
        public UnityEvent OnSelectionRotate;
        public UnityEvent OnSelectionDelete;

        public List<EditorComponent> SelectedEditorComponents = new List<EditorComponent>();

        private bool _suppressNextPointerClickSelection;
        private Coroutine _clearPointerClickSuppressionCoroutine;

        private void Awake()
        {
            AddListeners();
        }

        public void OnDestroy()
        {
            if (_clearPointerClickSuppressionCoroutine != null)
            {
                StopCoroutine(_clearPointerClickSuppressionCoroutine);
                _clearPointerClickSuppressionCoroutine = null;
            }

            RemoveListeners();
        }

        public void SelectObject(EditorComponent editorComponent)
        {
            // this might be a bit hacky, when doing multiselect it seems to be adding one twice?
            if (SelectedEditorComponents.Contains(editorComponent))
            {
                return;
            }

            //editorComponent.ArcadeEditorObjectHighlight.SetHightlightColor(ArcadeEditorObjectHighlight.HighlightColorType.Selected);
            SelectedEditorComponents.Add(editorComponent);

            SetOutlineSelected(editorComponent, true);

            OnSelectionChange.Invoke();
        }

        public void DeselectObject(EditorComponent editorComponent)
        {
            //editorComponent.ArcadeEditorObjectHighlight.SetHightlightColor(ArcadeEditorObjectHighlight.HighlightColorType.Unselected);
            if (SelectedEditorComponents.Remove(editorComponent))
            {
                SetOutlineSelected(editorComponent, false);
            }

            OnSelectionChange.Invoke();
        }

        public void DeselectAllObjects()
        {
            foreach (EditorComponent selectedEditorComponent in SelectedEditorComponents)
            {
                SetOutlineSelected(selectedEditorComponent, false);
            }
            SelectedEditorComponents.Clear();

            OnSelectionChange.Invoke();
        }

        public void MoveSelectedObjects(Vector2 deltaPosition)
        {
            foreach (EditorComponent selectedEditorComponent in SelectedEditorComponents)
            {
                Vector3 position = selectedEditorComponent.transform.position;
                position.x += deltaPosition.x;
                position.z += deltaPosition.y;

                selectedEditorComponent.transform.position = position;
            }

            OnSelectionMove.Invoke();
        }

        //public void RotateSelectedObjects(float deltaAngle)
        //{
        //    if (SelectedObjects.Count == 0)
        //    {
        //        return;
        //    }

        //    ArcadeEditorObject selectedGameObject = SelectedObjects[SelectedObjects.Count - 1];

        //    GameObject temporaryGameObject = new GameObject();
        //    temporaryGameObject.transform.position = selectedGameObject.transform.position;

        //    foreach (ArcadeEditorObject arcadeEditorObject in SelectedObjects)
        //    {
        //        arcadeEditorObject.transform.SetParent(temporaryGameObject.transform, true);
        //    }

        //    Vector3 eulerAngles = Vector3.zero;
        //    eulerAngles.y = deltaAngle;
        //    temporaryGameObject.transform.transform.localEulerAngles = eulerAngles;

        //    foreach (ArcadeEditorObject arcadeEditorObject in SelectedObjects)
        //    {
        //        arcadeEditorObject.transform.SetParent(null, true);
        //    }

        //    Destroy(temporaryGameObject);

        //    //foreach (ArcadeEditorObject arcadeEditorObject in SelectedObjects)
        //    //{
        //    //    Vector3 eulerAngles = arcadeEditorObject.transform.rotation.eulerAngles;
        //    //    eulerAngles.y += deltaAngle;
        //    //    arcadeEditorObject.transform.transform.localEulerAngles = eulerAngles;
        //    //}

        //    OnSelectionRotate.Invoke();
        //}

        public void DeleteSelectedObjects()
        {
            foreach (EditorComponent selectedEditorComponent in SelectedEditorComponents)
            {
                SetOutlineSelected(selectedEditorComponent, false);
                Destroy(selectedEditorComponent.gameObject);
            }

            SelectedEditorComponents.Clear();

            OnSelectionDelete.Invoke();
            OnSelectionChange.Invoke();
        }

        private void SetOutlineSelected(EditorComponent editorComponent, bool selected)
        {
            if (editorComponent == null)
            {
                return;
            }

            EditorComponentSelectionOutline outline = editorComponent.GetComponent<EditorComponentSelectionOutline>();
            if (outline == null)
            {
                outline = editorComponent.gameObject.AddComponent<EditorComponentSelectionOutline>();
            }

            outline.SetSelected(selected);
        }

        public void SuppressNextPointerClickSelection()
        {
            _suppressNextPointerClickSelection = true;

            if (_clearPointerClickSuppressionCoroutine != null)
            {
                StopCoroutine(_clearPointerClickSuppressionCoroutine);
            }

            _clearPointerClickSuppressionCoroutine = StartCoroutine(ClearPointerClickSuppressionAfterPointerUp());
        }

        private IEnumerator ClearPointerClickSuppressionAfterPointerUp()
        {
            while (UnityEngine.Input.GetMouseButton(0))
            {
                yield return null;
            }

            yield return null;

            _suppressNextPointerClickSelection = false;
            _clearPointerClickSuppressionCoroutine = null;
        }

        private void AddListeners()
        {
            Editor.Instance.OnEditorViewEnabled.AddListener(OnEditorViewEnabled);
            Editor.Instance.OnEditorViewDisabled.AddListener(OnEditorViewDisabled);
        }

        private void RemoveListeners()
        {
            Editor.Instance.OnEditorViewEnabled.RemoveListener(OnEditorViewEnabled);
            Editor.Instance.OnEditorViewDisabled.RemoveListener(OnEditorViewDisabled);
        }

        private void OnEditorViewEnabled(EditorView editorView)
        {
            editorView.OnPointerClickEvent.AddListener(OnEditorViewPointerClick);
        }

        private void OnEditorViewDisabled(EditorView editorView)
        {
            editorView.OnPointerClickEvent.RemoveListener(OnEditorViewPointerClick);
        }

        private void OnEditorViewPointerClick(List<EditorComponent> editorComponents)
        {
            if (_suppressNextPointerClickSelection)
            {
                _suppressNextPointerClickSelection = false;
                if (_clearPointerClickSuppressionCoroutine != null)
                {
                    StopCoroutine(_clearPointerClickSuppressionCoroutine);
                    _clearPointerClickSuppressionCoroutine = null;
                }
                return;
            }

            // TODO this is just test code!
            DeselectAllObjects();

            if (editorComponents == null || editorComponents.Count == 0)
            {
                return;
            }

            SelectObject(editorComponents[0]);
        }
    }
}

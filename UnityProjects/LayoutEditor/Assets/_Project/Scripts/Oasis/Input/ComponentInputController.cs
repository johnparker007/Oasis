using System.Collections;
using System.Collections.Generic;
using DynamicPanels;

namespace Oasis.Input
{
    using UnityEngine;
    using Oasis.LayoutEditor;
    using static Oasis.Layout.ComponentInput;
    using Oasis.Layout;

    public class ComponentInputController : MonoBehaviour
    {
        public bool DebugOutputChanges;

        private Dictionary<KeyCode, bool> _keyCodeStates = new Dictionary<KeyCode, bool>();
        private Dictionary<KeyCode, bool> _keyCodeStateDeltas = new Dictionary<KeyCode, bool>();
        private List<InputData> _inputDatas = new List<InputData>();
        private bool _wasInputActive;

        private void Start()
        {
            Editor.Instance.OnLayoutSet.AddListener(OnLayoutSet);
        }

        private void Update()
        {
            bool isMameRunning = Editor.Instance != null
                && Editor.Instance.MameController != null
                && Editor.Instance.MameController.IsRunning;

            bool isBaseViewActive = Editor.Instance != null
                && Editor.Instance.TabController != null
                && Editor.Instance.TabController.IsTabActive(TabController.TabTypes.BaseView);

            bool shouldProcessInput = isMameRunning && isBaseViewActive;

            if (!shouldProcessInput && _wasInputActive)
            {
                ForceReleaseInputs(isMameRunning);
            }

            // TODO attempting to restart use of DynamicPanels, keeping the source unaltered,
            // 'GlobalSelectedPanelTab' will ideally need to be reimplemented outside of the core
            // DynamicPanels package

            //if(PanelManager.Instance.GlobalSelectedPanelTab == null
            //    || PanelManager.Instance.GlobalSelectedPanelTab.Content.GetComponentInChildren<EditorView>() == null)
            //{
            //    return;
            //}

            _keyCodeStateDeltas.Clear();

            foreach (KeyCode keyCode in _keyCodeStates.Keys)
            {
                bool state = Input.GetKey(keyCode);

                if(state != _keyCodeStates[keyCode])
                {
                    if(DebugOutputChanges)
                    {
                        Debug.LogError("keycode state changed for keycode "
                            + keyCode
                            + " from "
                            + _keyCodeStates[keyCode]
                            + " to "
                            + state);
                    }

                    _keyCodeStateDeltas.Add(keyCode, state);
                }
            }

            foreach (KeyCode keyCode in _keyCodeStateDeltas.Keys)
            {
                bool newState = _keyCodeStateDeltas[keyCode];

                foreach(InputData inputData in _inputDatas)
                {
                    if(inputData.KeyCode == keyCode)
                    {
                        if (shouldProcessInput)
                        {
                            Editor.Instance.MameController.SetButtonState(inputData.ButtonNumber, newState);
                        }
                    }
                }

                if (shouldProcessInput)
                {
                    _keyCodeStates[keyCode] = newState;
                }
            }

            _wasInputActive = shouldProcessInput;
        }

        private void OnDestroy()
        {
            Editor.Instance.OnLayoutSet.RemoveListener(OnLayoutSet);

            //LayoutEditor.Layout.OnAddComponent.RemoveListener(OnLayoutAddComponent);
        }

        // TODO this will need rebuilding each time Component changed/added/deleted in Layout Editor
        private void RebuildActiveKeycodes()
        {
            _keyCodeStates.Clear();
            _inputDatas.Clear();

            foreach (Layout.Component component in Editor.Instance.Project.Layout.BaseView.Data.Components)
            {
                if(!component.GetType().IsSubclassOf(typeof(Layout.ComponentInput)))
                {
                    continue;
                }

                Layout.ComponentInput.InputData inputData = ((Layout.ComponentInput)component).Input;

                if(!inputData.Enabled)
                {
                    continue;
                }

                _keyCodeStates.TryAdd(inputData.KeyCode, false);

                _inputDatas.Add(inputData);
            }
        }

        private void OnLayoutSet(LayoutObject layout)
        {
            if(layout != null)
            {
                layout.OnAddComponent.AddListener(OnLayoutAddComponent);
            }
            // TODO else remove listener if set?  Also in OnDestroy if set?
        }

        private void OnLayoutAddComponent(Layout.Component component, View view)
        {
            RebuildActiveKeycodes();
        }

        private void ForceReleaseInputs(bool isMameRunning)
        {
            if (Editor.Instance == null || Editor.Instance.MameController == null)
            {
                return;
            }

            foreach (InputData inputData in _inputDatas)
            {
                if (_keyCodeStates.TryGetValue(inputData.KeyCode, out bool currentState) && currentState)
                {
                    if (isMameRunning)
                    {
                        Editor.Instance.MameController.SetButtonState(inputData.ButtonNumber, false);
                    }

                    _keyCodeStates[inputData.KeyCode] = false;
                }
            }
        }
    }

}

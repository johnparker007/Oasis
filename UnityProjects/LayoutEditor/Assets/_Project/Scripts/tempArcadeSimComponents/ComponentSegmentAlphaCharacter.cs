using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TempArcadeSimComponents
{
    public class ComponentSegmentAlphaCharacter : MonoBehaviour
    {
        private ComponentSegmentAlpha _ownerSegmentAlpha = null;
        private MeshRenderer _meshRenderer = null;

        private int _shaderColorParameterId = 0;

        private List<Material> _fontMaterials = null;

        public byte CharacterIndex
        {
            get;
            private set;
        }

        //private void Awake()
        //{
        //    _ownerSegmentAlpha = GetComponentInParent<ComponentSegmentAlpha>();
        //    _meshRenderer = GetComponent<MeshRenderer>();

        //    _shaderColorParameterId = Shader.PropertyToID("_Color");

        //    _fontMaterials = new List<Material>();
        //    foreach (Material fontMaterial in _ownerSegmentAlpha.FontMaterials)
        //    {
        //        _fontMaterials.Add(new Material(fontMaterial));
        //    }
        //}

        //private void Start()
        //{
        //    if (_ownerSegmentAlpha.Machine.Arcade != null)
        //    {
        //        _ownerSegmentAlpha.Machine.Arcade.ArcadeMachineController.OnMachineEngaged.AddListener(OnMachineEngaged);
        //        _ownerSegmentAlpha.Machine.Arcade.ArcadeMachineController.OnMachineDisengaged.AddListener(OnMachineDisengaged);

        //        if(SceneManager.GetActiveScene().name == "Arcade")
        //        {
        //            gameObject.SetActive(false);
        //        }
        //    }
        //}

        //private void OnDestroy()
        //{
        //    if (_ownerSegmentAlpha.Machine.Arcade != null)
        //    {
        //        _ownerSegmentAlpha.Machine.Arcade.ArcadeMachineController.OnMachineEngaged.RemoveListener(OnMachineEngaged);
        //        _ownerSegmentAlpha.Machine.Arcade.ArcadeMachineController.OnMachineDisengaged.RemoveListener(OnMachineDisengaged);
        //    }

        //    if (_fontMaterials != null)
        //    {
        //        foreach (Material fontMaterial in _fontMaterials)
        //        {
        //            if(fontMaterial != null)
        //            {
        //                Destroy(fontMaterial);
        //            }
        //        }
        //    }
        //}

        //public void SetCharacter(int characterIndex)
        //{
        //    CharacterIndex = (byte)characterIndex;

        //    // TOIMPROVE should probably track this and only set if changed?
        //    //_meshRenderer.material = _ownerSegmentAlpha.FontMaterials[characterIndex];
        //    //_meshRenderer.material = _fontMaterials[characterIndex]; // still leaks, after a long time

        //    // DISABLING TO MOVE OVER TO USING THE FAST RENDERER
        //    //_meshRenderer.sharedMaterial = _fontMaterials[CharacterIndex];
        //}

        //public void SetBrightness(float value)
        //{
        //    Color color = Color.white;
        //    color.a = value;
        //    _meshRenderer.sharedMaterial.SetColor(_shaderColorParameterId, color);
        //}

        //private void OnMachineEngaged(Machine machine)
        //{
        //    if (machine != _ownerSegmentAlpha.Machine)
        //    {
        //        return;
        //    }

        //    gameObject.SetActive(true);
        //}

        //private void OnMachineDisengaged(Machine machine)
        //{
        //    if (machine != _ownerSegmentAlpha.Machine)
        //    {
        //        return;
        //    }

        //    gameObject.SetActive(false);
        //}
    }

}

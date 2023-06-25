using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TempArcadeSimComponents
{
    public class ComponentLamp : ComponentBase
    {
        //public int LampIndex;
        //public Converter.MFMELampType MFMELampType;

        //public bool ReelLamp;
        //public ComponentReel ReelLampOwnerReel;

        //public string MFMECoinNote;
        //public string MFMEEffect;

        //public bool MFMELampIsLed;

        //public bool Transparency;
        //public bool InvertLamp;
        //public Converter.MFMELampShape MFMELampShape;
        //public int MFMEButtonNumber;

        //public KeyCode ShortcutKeyCode = KeyCode.None;

        //// not a unity layer, just for the converter editor to use for working out the draw z offsets to fix z fighting with DMII shader
        //public int ConverterGPURenderSortingLayer;

        //public GameObject RenderObject;
        //public Renderer RenderComponent;

        //public TexturePackerMetaData TexturePackerMetadata;
        //public TexturePackerFrame TexturePackerFrame;
        //public Material TextureImporterMaterial;

        //public bool DisableRendering;

        //public int ComponentLampIndex;

        //private BoxCollider _boxCollider = null;

        //private Color _materialColor = Color.white;
        //private int _materialColorPropertyId = 0;

        //public float _currentAlpha = 1f;
        //private MaterialPropertyBlock _materialPropertyBlock;

        //private LampRenderer _lampRenderer = null;


        //public string TextureName
        //{
        //    get
        //    {
        //        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        //        Texture texture = meshRenderer.sharedMaterial.GetTexture("_MainTex");

        //        return texture.name;
        //    }
        //}

        //public Vector2 TextureImporterTiling
        //{
        //    get
        //    {
        //        int sheetWidth = TexturePackerMetadata.size.w;
        //        int sheetHeight = TexturePackerMetadata.size.h;

        //        int spriteWidth = TexturePackerFrame.frame.w;
        //        int spriteHeight = TexturePackerFrame.frame.h;

        //        float scaleX = (float)spriteWidth / sheetWidth;
        //        float scaleY = (float)spriteHeight / sheetHeight;

        //        Vector2 tiling = Vector2.zero;
        //        tiling.x = scaleX;
        //        tiling.y = scaleY;

        //        return tiling;
        //    }
        //}

        //public Vector2 TextureImporterOffset
        //{
        //    get
        //    {
        //        int sheetWidth = TexturePackerMetadata.size.w;
        //        int sheetHeight = TexturePackerMetadata.size.h;

        //        int spriteWidth = TexturePackerFrame.frame.w;
        //        int spriteHeight = TexturePackerFrame.frame.h;

        //        int spriteX = TexturePackerFrame.frame.x;
        //        int spriteY = TexturePackerFrame.frame.y;
        //        int correctedSpriteY = sheetHeight - spriteY - spriteHeight;


        //        float offsetX = (float)spriteX / sheetWidth;
        //        //            float offsetY = (float)spriteY / sheetHeight;
        //        float offsetY = (float)correctedSpriteY / sheetHeight;

        //        Vector2 offset = Vector2.zero;
        //        offset.x = offsetX;
        //        offset.y = offsetY;

        //        return offset;
        //    }
        //}



        //public bool IsLit
        //{
        //    get;
        //    set;
        //}

        //protected override void Awake()
        //{
        //    base.Awake();

        //    _lampRenderer = GetComponentInParent<LampRenderer>();

        //    _materialColorPropertyId = Shader.PropertyToID("_Color");
        //    _materialPropertyBlock = new MaterialPropertyBlock();

        //    if (SceneManager.GetActiveScene().name == "Arcade")
        //    {
        //        if (ShortcutKeyCode != KeyCode.None)
        //        {
        //            // TODO try setting up a MeshCollider with a unity primitive Quad as the mesh, could be quicker, seems cleaner than a 3d box!

        //            // TOIMPROVE - could extend scale of quad provided no other buttons too close - to allow for more forgiving click detection

        //            _boxCollider = gameObject.AddComponent(typeof(BoxCollider)) as BoxCollider;
        //            _boxCollider.isTrigger = true;

        //            gameObject.layer = LayerMask.NameToLayer("ComponentLamps");
        //        }
        //    }

        //}

        //protected override void Start()
        //{
        //    base.Start();

        //    if(_machine.Arcade != null)
        //    {
        //        _machine.Arcade.ArcadeMachineController.OnMachineEngaged.AddListener(OnMachineEngaged);
        //    }
        //}

        //protected override void OnDestroy()
        //{
        //    base.OnDestroy();

        //    if (_machine.Arcade != null)
        //    {
        //        _machine.Arcade.ArcadeMachineController.OnMachineEngaged.RemoveListener(OnMachineEngaged);
        //    }
        //}

        //public override void UpdateComponent()
        //{
        //    UpdateComponentIsLit();

        //    //if(RenderComponent.enabled != IsLit) // TODO check if this optimisation necessary, esp once codec in place
        //    {
        //        //RenderComponent.enabled = !DisableRendering && IsLit;
        //        if (RenderComponent != null)
        //        {
        //            RenderComponent.enabled = false; // JUST DISABLING FOR NOW - this legacy stuff needs stripping out entirely really...
        //        }
        //    }

        //    if (_machine.RenderDataRecorder.RecorderMode != RenderDataRecorder.Mode.Play)
        //    {
        //        if(_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
        //        {
        //            //if (IsLit)
        //            {
        //                float alpha;
        //                //if (_machine.EmulatorScraper.gameObject.activeSelf)
        //                if(_machine.EmulatorScraper.CurrentUwcWindowTextureBeingScraped != null)
        //                {
        //                    //alpha = Mathf.InverseLerp(kFullyUnlitMFMELampMatrixBlueValue, kFullyLitMFMELampMatrixBlueValue, _scrapedPixel.b);
        //                    alpha = GetLampBrightness();
        //                    _lampRenderer.SetLampBrightness(ComponentLampIndex, alpha);
        //                }
        //                else
        //                {
        //                    alpha = 0f;
        //                }

        //                //if(_materialColor.a != alpha)
        //                //{
        //                //    _materialColor.a = alpha;
        //                //    RenderComponent.material.SetColor(_materialColorPropertyId, _materialColor);
        //                //}

        //                //                    if(_currentAlpha != alpha)
        //                {
        //                    Color color = Color.white;
        //                    color.a = 1f;

        //                    if(!DisableRendering)
        //                    {
        //                        _materialPropertyBlock.SetColor(_materialColorPropertyId, color);
        //                        RenderComponent.SetPropertyBlock(_materialPropertyBlock);
        //                    }

        //                    _currentAlpha = alpha;
        //                }
        //            }
        //        }


        //    }
        //}

        //public float GetLampBrightness()
        //{
        //    if(MFMELampIsLed)
        //    {
        //        return DataLayoutReader.GetLedPixelBrightness(EmulatorScraper, LampIndex);
        //    }
        //    else
        //    {
        //        return DataLayoutReader.GetLampPixelBrightness(EmulatorScraper, LampIndex);
        //    }
        //}

        //private void UpdateComponentIsLit()  
        //{  
        //    if (_machine.RenderDataRecorder.RecorderMode != RenderDataRecorder.Mode.Play
        //        //            && _machine.EmulatorScraper.gameObject.activeSelf)
        //            && _machine.EmulatorScraper.CurrentUwcWindowTextureBeingScraped != null)
        //    {
        //        IsLit = DataLayoutReader.GetLampPixelBrightness(EmulatorScraper, LampIndex) > 0f;
        //        if (InvertLamp)
        //        {
        //            IsLit = !IsLit;
        //        }
        //    }
        //}

        //private void OnMachineEngaged(Machine machine)
        //{
        //    if(machine != _machine)
        //    {
        //        return;
        //    }

        //    IsLit = false;
        //    _currentAlpha = 0f;
        //    _lampRenderer.SetLampBrightness(ComponentLampIndex, 0f);
        //}

    }

}

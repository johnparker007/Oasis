using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using uWindowCapture;

namespace TempArcadeSimComponents
{
	public class ComponentReel : ComponentBase
	{
		public static readonly int kReelLampColumns = 3;
		public static readonly int kReelLampRows = 5;
		public static readonly int kReelLampCount = kReelLampColumns * kReelLampRows;

		//public int MFMEReelNumber;
		//public int MFMEReelStops;
		//public int MFMEReelHalfSteps;
		//public int MFMEReelResolution;
		//public int MFMEReelBandOffset;
		//public int MFMEReelOptoTab;
		//public int MFMEReelHeight;
		//public int MFMEReelWidthDiff;
		//public int MFMEReelBounce;
		//public bool MFMEReelHorizontal;
		//public bool MFMEReelReversed;

		//public bool MFMEReelLamps;
		//public bool MFMEReelLampsLEDs;
		//public bool MFMEReelMirrored;

		//public SerialisableNullable<int>[] MFMELampNumbers = new SerialisableNullable<int>[kReelLampCount];

		//public int MFMEWinLinesCount;

		//// in MFME MFMEWinLinesOffset actually changes the reel band offset 
		//// - could be MFME bug, will need to replicate setting in data layout
		//public int MFMEWinLinesOffset; 

		//public bool MFMEReelHasOverlay;

		//public Rect NormalisedFlatPanelRect;

		//public Vector2Int ReelPosition;

		//public float TextureOffsetToAlignReelMFME;
		//public float TextureOffsetToAlignReelMAME;

		//public Vector2 TextureOffset;

		//[Tooltip("(1f = perfectly match mfme reel, lower values for drag)")]
		//public float LerpVelocity;

		//public ComponentLamp[] ReelComponentLamps = new ComponentLamp[kReelLampCount];

		//private float _currentVelocity = 0f;
		//private float _targetVelocity = 0f;

		//private float _currentPosition = 0f;

		////private Color _scrapedPixelReelPosition;

		//private Material _material = null;

		//private AddressableMaterialInitialiser _addressableMaterialInitialiser = null;



		//public float TextureOffsetToAlignReel
		//   {
		//	get
		//       {
		//		if(_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
		//           {
		//			return TextureOffsetToAlignReelMFME;
		//		}
		//		else
		//           {
		//			return TextureOffsetToAlignReelMAME;
		//		}
		//       }
		//   }


		//   protected override void Awake()
		//   {
		//       base.Awake();

		//	_addressableMaterialInitialiser = GetComponent<AddressableMaterialInitialiser>();
		//	if(_addressableMaterialInitialiser != null)
		//       {
		//		_addressableMaterialInitialiser.OnInitialised.AddListener(OnAddressableMaterialInitialised);
		//       }
		//}

		//   protected override void Start()
		//{
		//	base.Start();

		//	AssignReelComponentLamps();

		//	if(_machine.MachineASDataController != null)
		//       {
		//		_machine.MachineASDataController.OnASDataLoaded.AddListener(OnASDataLoaded);
		//	}

		//	if(_machine.Arcade != null)
		//       {
		//		_machine.Arcade.ArcadeMachineController.OnMachineDisengaged.AddListener(OnMachineDisengaged);
		//	}
		//}

		//   protected override void OnDestroy()
		//   {
		//       base.OnDestroy();

		//	if(_material != null)
		//       {
		//		Destroy(_material);
		//	}

		//	if (_addressableMaterialInitialiser != null)
		//	{
		//		_addressableMaterialInitialiser.OnInitialised.RemoveListener(OnAddressableMaterialInitialised);
		//	}

		//	if (_machine.MachineASDataController != null)
		//	{
		//		_machine.MachineASDataController.OnASDataLoaded.RemoveListener(OnASDataLoaded);
		//	}

		//	if (_machine.Arcade != null)
		//	{
		//		_machine.Arcade.ArcadeMachineController.OnMachineDisengaged.RemoveListener(OnMachineDisengaged);
		//	}
		//}

		//private void AssignReelComponentLamps()
		//   {
		//	for(int reelLampIndex = 0; reelLampIndex < kReelLampCount; ++reelLampIndex)
		//       {
		//		AssignReelComponentLamp(reelLampIndex);
		//	}
		//}

		//private void AssignReelComponentLamp(int reelLampIndex)
		//{
		//	if(MFMELampNumbers[reelLampIndex] == null)
		//       {
		//		return;
		//       }

		//	ComponentLamp[] lamps = transform.parent.GetComponentsInChildren<ComponentLamp>();
		//	foreach (ComponentLamp lamp in lamps)
		//	{
		//		if(!lamp.ReelLamp)
		//           {
		//			continue;
		//           }

		//		if (lamp.LampIndex == MFMELampNumbers[reelLampIndex])
		//           {
		//			ReelComponentLamps[reelLampIndex] = lamp;
		//			lamp.ReelLampOwnerReel = this;
		//           }
		//	}
		//}

		//// THis isn't going to work, since we'd need to also apply the internal MFME data for band offset etc...
		//// may be easier to start up my own save file for these...
		//public void InitialiseToNormalisedOffset(float normalisedOffset)
		//   {
		//	//_textureOffset.x = 1f - normalisedOffset + (TextureOffsetToAlignReel * 0.5f); 
		//	TextureOffset.x = normalisedOffset + (TextureOffsetToAlignReel * 0.5f);

		//	if(_material != null)
		//       {
		//		_material.SetTextureOffset("_MainTex", TextureOffset);
		//	}
		//}

		////protected override void UpdateScrapedPixel()
		////{
		////	// reel position
		////	DataLayoutReadPosition = GetReelPosition(MFMEReelNumber);
		////	base.UpdateScrapedPixel();
		////	_scrapedPixelReelPosition = _scrapedPixel;
		////}

		//public override void UpdateComponent()
		//   {
		//	//base.UpdateComponent();

		//	float reelPixelBrightness = DataLayoutReader.GetReelPixelBrightness(EmulatorScraper, MFMEReelNumber);

		//	//float targetPosition = 1f - _scrapedPixelReelPosition.r + TextureOffsetToAlignReel;
		//	float targetPosition = 1f - reelPixelBrightness + TextureOffsetToAlignReel;

		//	_targetVelocity = targetPosition - _currentPosition;

		//	// ***************** TOIMPROVE HACKY FIX FOR WRAPAROUND
		//	if (Mathf.Abs(_currentVelocity - _targetVelocity) > 0.5f)
		//	{
		//		_currentVelocity = _targetVelocity;
		//	}

		//	_currentVelocity = Mathf.Lerp(_currentVelocity, _targetVelocity, LerpVelocity);

		//	_currentPosition += _currentVelocity;

		//	if(UserSettingsController.Instance != null
		//		&& UserSettingsController.Instance.SettingsData.Graphics.ReelPhysics.Value)
		//       {
		//		TextureOffset.x = _currentPosition;
		//	}
		//	else
		//       {
		//		TextureOffset.x = targetPosition;
		//	}

		//	if (TextureOffset.x > 1f)
		//	{
		//		TextureOffset.x -= 1f;
		//	}
		//	else if(TextureOffset.x < 0f)
		//       {
		//		TextureOffset.x += 1f;
		//	}

		//	// TOIMPROVE - should be using precalced ShaderIDs for all these

		//	_material.SetTextureOffset("_MainTex", TextureOffset);
		//       //_renderer.material.SetTextureOffset("_diffuseMap", _textureOffset);// XXX for testing, needs to be reenabled!

		//       // **************** NEW REEL LAMPS TEST:
		//       //_renderer.material.SetFloat("_LampBrightness0", ComponentLamp.GetLampBrightness(_scrapedPixelLamp0Brightness));
		//       //_renderer.material.SetFloat("_LampBrightness1", ComponentLamp.GetLampBrightness(_scrapedPixelLamp1Brightness));
		//       //_renderer.material.SetFloat("_LampBrightness2", ComponentLamp.GetLampBrightness(_scrapedPixelLamp2Brightness));

		//	if(_material != null)
		//       {
		//		if (ReelComponentLamps[1] != null)
		//		{
		//			//_renderer.material.SetFloat("_LampBrightness0", ReelComponentLamps[1].GetLampBrightness());
		//			SetReelLamp1MaterialProperty(ReelComponentLamps[1].GetLampBrightness());
		//		}
		//		if (ReelComponentLamps[2] != null)
		//		{
		//			//_renderer.material.SetFloat("_LampBrightness1", ReelComponentLamps[2].GetLampBrightness());
		//			SetReelLamp2MaterialProperty(ReelComponentLamps[2].GetLampBrightness());
		//		}
		//		if (ReelComponentLamps[3] != null)
		//		{
		//			//_renderer.material.SetFloat("_LampBrightness2", ReelComponentLamps[3].GetLampBrightness());
		//			SetReelLamp3MaterialProperty(ReelComponentLamps[3].GetLampBrightness());
		//		}
		//	}

		//	float lamp6Brightness = 0f;
		//	float lamp7Brightness = 0f;
		//	float lamp8Brightness = 0f;
		//	if (ReelComponentLamps[6] != null)
		//	{
		//		lamp6Brightness = ReelComponentLamps[6].GetLampBrightness();
		//	}
		//	if (ReelComponentLamps[7] != null)
		//	{
		//		lamp7Brightness = ReelComponentLamps[7].GetLampBrightness();
		//	}
		//	if (ReelComponentLamps[8] != null)
		//	{
		//		lamp8Brightness = ReelComponentLamps[8].GetLampBrightness();
		//	}
		//	float maximumColumn2LampBrightness = Mathf.Max(lamp6Brightness, lamp7Brightness, lamp8Brightness);

		//	//_renderer.material.SetFloat("_LampColumn2Brightness", maximumColumn2LampBrightness);
		//	SetReelLedMaterialProperty(maximumColumn2LampBrightness);
		//}

		//// TODO - should be using ShaderIDs for all setFloats etc

		//// TODO better way of doing this:
		//public void SetReelLamp1MaterialProperty(float brightness)
		//   {
		//	_material.SetFloat("_LampBrightness0", brightness);
		//}

		//public void SetReelLamp2MaterialProperty(float brightness)
		//{
		//	_material.SetFloat("_LampBrightness1", brightness);
		//}

		//public void SetReelLamp3MaterialProperty(float brightness)
		//{
		//	_material.SetFloat("_LampBrightness2", brightness);
		//}

		//public void SetReelLedMaterialProperty(float brightness)
		//{
		//	_material.SetFloat("_LampColumn2Brightness", brightness);
		//}

		//public void UpdateReelLamp(ComponentLamp reelLamp, float brightness)
		//   {
		//	// TOIMPROVE - dictionaries or something to optimise this
		//	if (_material != null)
		//	{
		//		if (reelLamp == ReelComponentLamps[1])
		//		{
		//			_material.SetFloat("_LampBrightness0", brightness);
		//		}
		//		else if (reelLamp == ReelComponentLamps[2])
		//		{
		//			_material.SetFloat("_LampBrightness1", brightness);
		//		}
		//		else if (reelLamp == ReelComponentLamps[3])
		//		{
		//			_material.SetFloat("_LampBrightness2", brightness);
		//		}
		//	}
		//}

		////public Vector2Int GetReelPosition(int mfmeReelNumber)
		////{
		////	const int kReelSpacingX = 2;

		////	const int kMFMEReelFirstX = 35;
		////	const int kMFMEReelY = 2;

		////	const int kMAMEReelFirstX = 34;
		////	const int kMAMEReelY = 3;

		////	int firstX;
		////	int y;
		////	if (_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
		////       {
		////		firstX = kMFMEReelFirstX;
		////		y = kMFMEReelY;
		////       }
		////	else
		////       {
		////		firstX = kMAMEReelFirstX;
		////		y = kMAMEReelY;
		////	}

		////	Vector2Int ReelPosition = Vector2Int.zero;
		////       ReelPosition.x = firstX + (mfmeReelNumber * kReelSpacingX);
		////       ReelPosition.y = y;

		////       return ReelPosition;
		////}

		//public void OnASDataLoaded(MachineASDataController.ASData asData)
		//   {
		//	StartCoroutine(OnASDataLoadedCoroutine(asData));
		//   }

		//private IEnumerator OnASDataLoadedCoroutine(MachineASDataController.ASData asData)
		//   {
		//	yield return new WaitUntil( () => _machine.AddressablesInitialised);

		//	foreach (int reelNumber in asData.ReelTextureOffsets.Keys)
		//	{
		//		if (reelNumber == MFMEReelNumber)
		//		{
		//			TextureOffset.x = asData.ReelTextureOffsets[reelNumber];

		//			if (_material != null)
		//			{
		//				_material.SetTextureOffset("_MainTex", TextureOffset);
		//			}
		//		}
		//	}
		//}

		//private void OnMachineDisengaged(Machine machine)
		//   {
		//	if(machine != _machine)
		//       {
		//		return;
		//       }

		//	SetReelLedMaterialProperty(0f);
		//   }

		//private void OnAddressableMaterialInitialised()
		//   {
		//	_material = new Material(_renderer.sharedMaterial);
		//	_renderer.sharedMaterial = _material;
		//}
	}

}


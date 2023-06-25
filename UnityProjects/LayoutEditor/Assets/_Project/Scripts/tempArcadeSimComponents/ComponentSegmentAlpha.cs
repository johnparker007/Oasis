using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TempArcadeSimComponents
{
    public class ComponentSegmentAlpha : ComponentBase
    {
        public enum MFMECharacterSetType
        {
            OldCharset,
            OKI1937,
            BFMCharset
        }

        //    private const int kMFMEDataLayoutStandardCharacterPixelHeight = 6;
        //    private const int kMFMEDataLayoutMFMEOverdrawnCharacterPixelHeight = 1; // fullstop, may include others such as quote marks?
        //    private const int kMFMEDataLayoutBrightnessRowPixelHeight = 1;
        //    private const int kMFMEDataLayoutTotalPixelHeight = kMFMEDataLayoutStandardCharacterPixelHeight + kMFMEDataLayoutMFMEOverdrawnCharacterPixelHeight + kMFMEDataLayoutBrightnessRowPixelHeight;

        //    private const int kMFMEDataLayoutOffPixelBrightnessByte = 70;
        //    private const float kMFMEDataLayoutOffPixelBrightness = kMFMEDataLayoutOffPixelBrightnessByte / 255f;

        //    private const int kMAMEVfdCharacterWidth = 8;
        //    private const int kMAMEVfdCharacterHeight = 11;

        //    private const int kMAMEDataLayoutFullOnPixelBrightnessByte = 215;
        //    private const float kMAMEDataLayoutFullOnPixelBrightness = kMAMEDataLayoutFullOnPixelBrightnessByte / 255f;


        //    // for detecting on/off segments from MAME scraped Vfd Character (diagram in ComponentSegmentAlphaEditor.cs)
        //    public static readonly int kSegmentTopHorizontalX = 4; // segment #0 from above diagram
        //    public static readonly int kSegmentTopHorizontalY = 0; // segment #0 from above diagram

        //    public static readonly int kSegmentTopLeftVerticalX = 1; // segment #1 from above diagram
        //    public static readonly int kSegmentTopLeftVerticalY = 2; // segment #1 from above diagram

        //    public static readonly int kSegmentTopLeftDiagonalX = 2; // segment #2 from above diagram
        //    public static readonly int kSegmentTopLeftDiagonalY = 3; // segment #2 from above diagram

        //    public static readonly int kSegmentTopCenterVerticalX = 3; // segment #3 from above diagram
        //    public static readonly int kSegmentTopCenterVerticalY = 3; // segment #3 from above diagram

        //    public static readonly int kSegmentTopRightDiagonalX = 4; // segment #4 from above diagram
        //    public static readonly int kSegmentTopRightDiagonalY = 3; // segment #4 from above diagram

        //    public static readonly int kSegmentTopRightVerticalX = 6; // segment #5 from above diagram
        //    public static readonly int kSegmentTopRightVerticalY = 2; // segment #5 from above diagram

        //    public static readonly int kSegmentCenterLeftHorizontalX = 2; // segment #6 from above diagram
        //    public static readonly int kSegmentCenterLeftHorizontalY = 5; // segment #6 from above diagram

        //    public static readonly int kSegmentCenterRightHorizontalX = 4; // segment #7 from above diagram
        //    public static readonly int kSegmentCenterRightHorizontalY = 5; // segment #7 from above diagram

        //    public static readonly int kSegmentBottomLeftVerticalX = 0; // segment #8 from above diagram
        //    public static readonly int kSegmentBottomLeftVerticalY = 8; // segment #8 from above diagram

        //    public static readonly int kSegmentBottomLeftDiagonalX = 2; // segment #9 from above diagram
        //    public static readonly int kSegmentBottomLeftDiagonalY = 7; // segment #9 from above diagram

        //    public static readonly int kSegmentBottomCenterVerticalX = 3; // segment #10 from above diagram
        //    public static readonly int kSegmentBottomCenterVerticalY = 7; // segment #10 from above diagram

        //    public static readonly int kSegmentBottomRightDiagonalX = 4; // segment #11 from above diagram
        //    public static readonly int kSegmentBottomRightDiagonalY = 7; // segment #11 from above diagram

        //    public static readonly int kSegmentBottomRightVerticalX = 5; // segment #12 from above diagram
        //    public static readonly int kSegmentBottomRightVerticalY = 8; // segment #12 from above diagram

        //    public static readonly int kSegmentBottomHorizontalX = 4; // segment #13 from above diagram
        //    public static readonly int kSegmentBottomHorizontalY = 10; // segment #13 from above diagram

        //    public static readonly int kSegmentDecimalPointX = 6; // segment #14 from above diagram
        //    public static readonly int kSegmentDecimalPointY = 10; // segment #14 from above diagram

        //    public int FixedDataLayoutReadPositionX
        //    {
        //        get
        //        {
        //            if (_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
        //            {
        //                return 34;
        //            }
        //            else
        //            {
        //                return 34;
        //            }
        //        }
        //    }

        //    public int FixedDataLayoutReadPositionY
        //    {
        //        get
        //        {
        //            if (_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
        //            {
        //                return 5;
        //            }
        //            else
        //            {
        //                return 10;
        //            }
        //        }
        //    }

        //    public int MFMESegmentAlphaNumber;
        //    public MFMECharacterSetType MFMECharacterSet;
        //    public bool MFMEReversed; 
        //    public Color MFMEOnColor;
        //    public bool MFME16Segment;

        //    public List<Material> FontMaterials;
        //    public List<ComponentSegmentAlphaCharacter> Characters;
        //    public List<ComponentSegmentAlphaCharacter> DotCharacters;

        //    private Color[] _scrapedPixels;

        //    private SegmentAlphaRenderer _segmentAlphaRenderer = null;

        //    public float Brightness
        //    {
        //        get;
        //        private set;
        //    }

        //    public bool[] DotOn
        //    {
        //        get;
        //        private set;
        //    }


        //    private static List<Vector2Int> MAMESegmentScrapePositions = new List<Vector2Int>(); 

        //    private List<ushort> Segment16BitFlagsPerFontCharacter = new List<ushort>() 
        //    { 

        //// these values were generated from the GenerateSegmentLookupToClipboard function:
        //// the bits represent on/off segments, the ordering is detailed in ComponentSegmentAlphaEditor.cs
        //16383,
        //4579,
        //13481,
        //8451,
        //13353,
        //8515,
        //323,
        //12675,
        //4578,
        //9225,
        //12576,
        //2386,
        //8450,
        //4406,
        //6438,
        //12579,
        //483,
        //14627,
        //2531,
        //12483,
        //1033,
        //12578,
        //786,
        //6946,
        //2580,
        //1044,
        //8721,
        //258,
        //2052,
        //4128,
        //9416,
        //8192,
        //0,
        //12587,
        //10,
        //8912,
        //13515,
        //12595,
        //12707,
        //16,
        //2064,
        //516,
        //3804,
        //1224,
        //0,
        //192,
        //0,
        //528,
        //13107,
        //1032,
        //8673,
        //12513,
        //4322,
        //12483,
        //12739,
        //4129,
        //12771,
        //12515,
        //13603,
        //13091,
        //12643,
        //8384,
        //12583,
        //1185,



        //    };

        //    public int MAMEScrapedPixelsWidth
        //    {
        //        get
        //        {
        //            int additionalWidthToCaptureBrightnessCharacter = 3 * kMAMEVfdCharacterWidth;

        //            return (Characters.Count * kMAMEVfdCharacterWidth) + additionalWidthToCaptureBrightnessCharacter;
        //        }
        //    }

        //    public byte[] GetCharacters()
        //    {
        //        // TOIMPROVE - optimise, don't malloc every frame (low priority, this is not used for playback of recordings)
        //        byte[] characters = new byte[Characters.Count];

        //        for(int characterIndex = 0; characterIndex < Characters.Count; ++characterIndex)
        //        {
        //            characters[characterIndex] = Characters[characterIndex].CharacterIndex;
        //        }

        //        return characters;
        //    }


        //    protected override void Awake()
        //    {
        //        base.Awake();

        //        _segmentAlphaRenderer = GetComponentInParent<SegmentAlphaRenderer>();

        //        if (SceneManager.GetActiveScene().name != "Converter")
        //        {
        //            // TOIMRPOVE perhaps temporary, if can autodetect and make transparent the Alpha area on the background image and have this component behind like the reels
        //            // sets all Alphas to be exactly the same size, that seems about right - in theory should work as they are same szie in relity across all machines?
        //            const float kFixedScaleX = 0.2391021f;
        //            const float kFixedScaleY = 0.04291825f;

        //            Vector3 localScale = transform.localScale;
        //            localScale.x = kFixedScaleX;
        //            localScale.y = kFixedScaleY;
        //            transform.localScale = localScale;

        //            // XXX temporary for performance, hard disable until machine becomes active:
        //            //gameObject.SetActive(false);
        //        }

        //        if(MAMESegmentScrapePositions.Count == 0)
        //        {
        //            // static list hasn't been set up, this must be the first ComponentSegmentAlpha to initialise
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentTopHorizontalX, kSegmentTopHorizontalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentTopLeftVerticalX, kSegmentTopLeftVerticalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentTopLeftDiagonalX, kSegmentTopLeftDiagonalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentTopCenterVerticalX, kSegmentTopCenterVerticalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentTopRightDiagonalX, kSegmentTopRightDiagonalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentTopRightVerticalX, kSegmentTopRightVerticalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentCenterLeftHorizontalX, kSegmentCenterLeftHorizontalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentCenterRightHorizontalX, kSegmentCenterRightHorizontalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentBottomLeftVerticalX, kSegmentBottomLeftVerticalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentBottomLeftDiagonalX, kSegmentBottomLeftDiagonalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentBottomCenterVerticalX, kSegmentBottomCenterVerticalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentBottomRightDiagonalX, kSegmentBottomRightDiagonalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentBottomRightVerticalX, kSegmentBottomRightVerticalY));
        //            MAMESegmentScrapePositions.Add(new Vector2Int(kSegmentBottomHorizontalX, kSegmentBottomHorizontalY));
        //        }
        //    }

        //    protected override void Start()
        //    {
        //        base.Start();

        //        // Calling these manually during the BuildExtractPrefabFromExtractArtifacts stage:
        //        //PositionAndScaleCharacters();
        //        //SetAllDotsAsDotCharacter();

        //        DotOn = new bool[Characters.Count];
        //    }

        //    private void OnEnable()
        //    {

        //    }

        //    private void OnDisable()
        //    {
        //    }


        //    public void PositionAndScaleCharacters()
        //    {
        //        float characterXScale = 1f / Characters.Count;
        //        for (int characterIndex = 0; characterIndex < Characters.Count; ++characterIndex)
        //        {
        //            ComponentSegmentAlphaCharacter character = Characters[characterIndex];
        //            ComponentSegmentAlphaCharacter dotCharacter = DotCharacters[characterIndex];

        //            Vector3 localPosition = Vector3.zero;
        //            localPosition.x = -0.5f + (characterXScale * 0.5f) + (characterXScale * characterIndex);
        //            localPosition.z = -0.00001f;
        //            character.transform.localPosition = localPosition;
        //            dotCharacter.transform.localPosition = localPosition;

        //            Vector3 localScale = Vector3.one;
        //            localScale.x = characterXScale;
        //            character.transform.localScale = localScale;
        //            dotCharacter.transform.localScale = localScale;
        //        }
        //    }

        //    public void SetAllDotsAsDotCharacter()
        //    {
        //        const int kDotCharacterIndex = 46; // TODO this may be different for different char sets
        //        for (int characterIndex = 0; characterIndex < Characters.Count; ++characterIndex)
        //        {
        //            DotCharacters[characterIndex].SetCharacter(kDotCharacterIndex);
        //        }
        //    }

        //    public override void UpdateComponent()
        //    {
        //        // no base.UpdateComponent as don't care about scraping the standard single pixel

        //        if (_machine.RenderDataRecorder.RecorderMode == RenderDataRecorder.Mode.Play)
        //        {
        //            return;
        //        }

        //        UpdateScrapedPixels();

        //        if(_scrapedPixels == null)
        //        {
        //            return;
        //        }

        //        Brightness = GetBrightnessFromScrapedPixels();

        //        GetDotStatesFromScrapedPixels();

        //        ParseScrapedPixelsToIntegersAndSetCharacters();
        //        //SetCharactersBrightness();

        //        // update fast renderer:
        //        _segmentAlphaRenderer.SetAlphaState(Brightness, GetCharacters());
        //    }

        //    // TOIMPROVE - all this should be updated to use Color32 bytes, as it's more efficient
        //    private void UpdateScrapedPixels()
        //    {
        //        if (EmulatorScraper != null && EmulatorScraper.ScrapedTexture != null)
        //        {
        //            if(_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
        //            {
        //                _scrapedPixels = EmulatorScraper.ScrapedTexture.GetPixels(
        //                    FixedDataLayoutReadPositionX, 
        //                    FixedDataLayoutReadPositionY,
        //                    Characters.Count,
        //                    kMFMEDataLayoutTotalPixelHeight);
        //            }
        //            else
        //            {
        //                _scrapedPixels = EmulatorScraper.ScrapedTexture.GetPixels(
        //                    FixedDataLayoutReadPositionX,
        //                    FixedDataLayoutReadPositionY,
        //                    MAMEScrapedPixelsWidth,
        //                    kMAMEVfdCharacterHeight);
        //            }
        //        }
        //    }

        //    private void ParseScrapedPixelsToIntegersAndSetCharacters()
        //    {
        //        if(_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
        //        {
        //            for (int characterIndex = 0; characterIndex < Characters.Count; ++characterIndex)
        //            {
        //                int value = GetIntegerFromScrapedPixelsMFME(characterIndex);

        //                // needs to be 1 more to marry up with what MFME is putting out with current data layout, means no '@' character until
        //                // properly fixed, probably by making the 'binary' image for the data layout and the alpha output itself wider
        //                value += 1;
        //                value = Mathf.Clamp(value, 0, FontMaterials.Count - 1); // won't be needed once we have fixed the issue requiring the +1 hack above

        //                Characters[characterIndex].SetCharacter(value);
        //            }
        //        }
        //        else
        //        {
        //            for (int characterIndex = 0; characterIndex < Characters.Count; ++characterIndex)
        //            {
        //                int value = GetIntegerFromScrapedPixelsMAME(characterIndex);

        //                Characters[characterIndex].SetCharacter(value);
        //            }
        //        }
        //    }

        //    private int GetIntegerFromScrapedPixelsMFME(int characterIndex)
        //    {
        //        int value = 0;
        //        for (int row = 0; row < kMFMEDataLayoutStandardCharacterPixelHeight; ++row)
        //        {
        //            Color scrapedPixel = _scrapedPixels[(row * Characters.Count) + characterIndex];

        //            bool pixelOff = scrapedPixel.r <= kMFMEDataLayoutOffPixelBrightness;

        //            if (!pixelOff)
        //            {
        //                value += 1 << row;

        //                if(Brightness == 0f)
        //                {

        //                    //_brightness = (scrapedPixel.r - kDataLayoutOffPixelBrightness)
        //                }
        //            }
        //        }

        //        // 70 and below == off
        //        // 71-255 is the scale of on pixels from 0-255

        //        return value;
        //    }

        //    private int GetIntegerFromScrapedPixelsMAME(int characterIndex)
        //    {
        //        ushort segmentFlags = 0;

        //        //if(characterIndex == 4)
        //        //{
        //        //    Debug.Log("Here");
        //        //}

        //        for(int segmentIndex = 0; segmentIndex < MAMESegmentScrapePositions.Count; ++segmentIndex)
        //        {
        //            Vector2Int scrapePixelLocalPosition = MAMESegmentScrapePositions[segmentIndex];
        //            Vector2Int scrapePosition = Vector2Int.zero;
        //            scrapePosition.x = kMAMEVfdCharacterWidth * characterIndex;

        //            scrapePosition += scrapePixelLocalPosition;

        //            Color scrapedPixel = _scrapedPixels[(scrapePosition.y * MAMEScrapedPixelsWidth) + scrapePosition.x];

        //            int segmentOn = scrapedPixel.r > 0.25f ? 1 : 0;

        //            segmentFlags |= (ushort)(segmentOn << segmentIndex);
        //        }

        //        // TODO something faster like a Dictionary for this lookup
        //        int scrapedCharacterIndex = Segment16BitFlagsPerFontCharacter.FindIndex(x => x.Equals(segmentFlags));
        //        if (scrapedCharacterIndex >= 0)
        //        {
        //            return scrapedCharacterIndex;
        //        }
        //        else
        //        {
        //            //Debug.LogError("COuldn't find " + segmentFlags); // XXX this creates A LOT of debug output spam, only uncomment for temp testing
        //            return 0;
        //        }
        //    }

        //    private void GetDotStatesFromScrapedPixels()
        //    {
        //        for (int characterIndex = 0; characterIndex < Characters.Count; ++characterIndex)
        //        {
        //            GetDotStateFromScrapedPixel(characterIndex);
        //        }
        //    }

        //    private void GetDotStateFromScrapedPixel(int characterIndex)
        //    {
        //        Color scrapedPixel;
        //        if (_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
        //        {
        //            int zeroBasedDotRow = kMFMEDataLayoutStandardCharacterPixelHeight;

        //            scrapedPixel = _scrapedPixels[(zeroBasedDotRow * Characters.Count) + characterIndex];

        //            // TODO this is a fudge while getting fast renderers working with engaged machines
        //            if(DotOn == null)
        //            {
        //                DotOn = new bool[Characters.Count];
        //            }

        //            DotOn[characterIndex] = scrapedPixel.r > 0f;
        //        }
        //        else
        //        {
        //            Vector2Int scrapePixelLocalPosition = Vector2Int.zero; // TODO cache this for speed
        //            scrapePixelLocalPosition.x = kSegmentDecimalPointX;
        //            scrapePixelLocalPosition.y = kSegmentDecimalPointY;

        //            Vector2Int scrapePosition = Vector2Int.zero;
        //            scrapePosition.x = kMAMEVfdCharacterWidth * characterIndex;

        //            scrapePosition += scrapePixelLocalPosition;

        //            scrapedPixel = _scrapedPixels[(scrapePosition.y * MAMEScrapedPixelsWidth) + scrapePosition.x];

        //            bool dotSegmentOn = scrapedPixel.r > 0.25f;

        //            DotOn[characterIndex] = dotSegmentOn;
        //        }
        //    }

        //    private float GetBrightnessFromScrapedPixels()
        //    {
        //        if(_machine.EmulatorType == EmulatorConfigurationData.EmulatorTypes.MFME)
        //        {
        //            // entire bottom line is always the brightness as that's how it's set on the font,
        //            // so can get the bottomright pixel 

        //            float scrapedPixelBrightness = _scrapedPixels[(kMFMEDataLayoutTotalPixelHeight * Characters.Count) - 1].r;

        //            // need to correct as MFME is lerping it between full 255 white and the gray at 70
        //            float correctedBrightness = Mathf.InverseLerp(kMFMEDataLayoutOffPixelBrightness, 1f, scrapedPixelBrightness);

        //            return correctedBrightness;
        //        }
        //        else
        //        {
        //            int pixelIndex = _scrapedPixels.Length - 1 - kMAMEVfdCharacterWidth;
        //            float scrapedPixelBrightness = _scrapedPixels[pixelIndex].r;

        //            // need to correct as MAME is lerping it between 0 off and ~215 (for full brightness) on the dot
        //            float correctedBrightness = Mathf.InverseLerp(0, kMAMEDataLayoutFullOnPixelBrightness, scrapedPixelBrightness);

        //            //Debug.Log("scrapedPixelBrightness == " + scrapedPixelBrightness
        //            //    + "    corrected pixel brightness == " + correctedBrightness
        //            //    + "   _scrapedPixels length == " + _scrapedPixels.Length
        //            //    + "   pixelIndex == " + pixelIndex);

        //            return correctedBrightness;
        //        }

        //    }

        //    private void SetCharactersBrightness()
        //    {
        //        for (int characterIndex = 0; characterIndex < Characters.Count; ++characterIndex)
        //        {


        //            // OLD MeshRenderer system, replacing with fast renderer:

        //            //Characters[characterIndex].SetBrightness(Brightness);

        //            //if(DotOn[characterIndex])
        //            //{
        //            //    DotCharacters[characterIndex].SetBrightness(Brightness);
        //            //}
        //            //else
        //            //{
        //            //    DotCharacters[characterIndex].SetBrightness(0f);
        //            //}
        //        }
        //    }

    }


}


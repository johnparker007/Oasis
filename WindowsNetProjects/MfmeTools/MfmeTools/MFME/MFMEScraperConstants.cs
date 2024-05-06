using Oasis.MfmeTools.Shared.UnityWrappers;
using System.Collections.Generic;
using System.Linq;

namespace Oasis.MfmeTools.Mfme
{
    public static class MFMEScraperConstants
    {
        // properties window component type tab
        public static readonly int kComponentTypeTabX = 9;
        public static readonly int kComponentTypeTabY = 31;
        public static readonly int kComponentTypeTabWidth = 94;
        public static readonly int kComponentTypeTabHeight = 19;

        // interior top-left pixel of text fields:

        public static readonly int kPropertiesPreviousButton_X = 811;
        public static readonly int kPropertiesPreviousButton_Y = 591;

        public static readonly int kPropertiesNextButton_X = 826;
        public static readonly int kPropertiesNextButton_Y = kPropertiesPreviousButton_Y;

        public static readonly int kComponentPositionAngle_X = 698;
        public static readonly int kComponentPositionAngle_Y = 471;

        public static readonly int kPropertiesUndoButton_X = 728;
        public static readonly int kPropertiesUndoButton_Y = 591;


        // generic component fields:
        public static readonly int kComponentPositionX_X = 489;
        public static readonly int kComponentPositionX_Y = 435;

        public static readonly int kComponentPositionY_X = 489;
        public static readonly int kComponentPositionY_Y = 462;

        public static readonly int kComponentPositionWidth_X = 585;
        public static readonly int kComponentPositionWidth_Y = 435;

        public static readonly int kComponentPositionHeight_X = 585;
        public static readonly int kComponentPositionHeight_Y = 462;

        public static readonly int kComponentAngle_X = 680;
        public static readonly int kComponentAngle_Y = 462;

        public static readonly int kComponentTextBox_X = 470;
        public static readonly int kComponentTextBox_Y = 489;
        public static readonly int kComponentTextBox_Width = 347;
        public static readonly int kComponentTextBox_Height = 77;

        public static readonly int kComponentPreviewWindowCenterX;
        public static readonly int kComponentPreviewWindowCenterY;

        public static readonly int kPropertiesOverlayTab_CenterX = 535;
        public static readonly int kPropertiesOverlayTab_CenterY = 40;

        public static readonly int kPropertiesOverlayImage_TopLeftX = 471;
        public static readonly int kPropertiesOverlayImage_TopLeftY = 54;
        public static readonly int kPropertiesOverlayImage_Width = 350;
        public static readonly int kPropertiesOverlayImage_Height = 350;

        public static readonly int kPropertiesOverlayImage_CenterX = kPropertiesOverlayImage_TopLeftX + (kPropertiesOverlayImage_Width / 2);
        public static readonly int kPropertiesOverlayImage_CenterY = kPropertiesOverlayImage_TopLeftY + (kPropertiesOverlayImage_Height / 2);


        // background fields
        public static readonly int kPropertiesBackgroundImage_X = 17;
        public static readonly int kPropertiesBackgroundImage_Y = 95;
        public static readonly int kPropertiesBackgroundImage_Width = 426;
        public static readonly int kPropertiesBackgroundImage_Height = 235;

        public static readonly int kPropertiesBackgroundImage_CenterX = kPropertiesBackgroundImage_X + (kPropertiesBackgroundImage_Width / 2);
        public static readonly int kPropertiesBackgroundImage_CenterY = kPropertiesBackgroundImage_Y + (kPropertiesBackgroundImage_Height / 2);

        // reel fields
        public static readonly int kPropertiesReelNumber_X = 20;
        public static readonly int kPropertiesReelNumber_Y = 223;

        public static readonly int kPropertiesReelStops_X = kPropertiesReelNumber_X;
        public static readonly int kPropertiesReelStops_Y = kPropertiesReelNumber_Y + (27 * 1);

        public static readonly int kPropertiesReelHalfSteps_X = kPropertiesReelNumber_X;
        public static readonly int kPropertiesReelHalfSteps_Y = kPropertiesReelNumber_Y + (27 * 2);

        public static readonly int kPropertiesReelResolution_X = kPropertiesReelNumber_X;
        public static readonly int kPropertiesReelResolution_Y = kPropertiesReelNumber_Y + (27 * 3);

        public static readonly int kPropertiesReelBandOffset_X = kPropertiesReelNumber_X;
        public static readonly int kPropertiesReelBandOffset_Y = kPropertiesReelNumber_Y + (27 * 4);

        public static readonly int kPropertiesReelOptoTab_X = kPropertiesReelNumber_X;
        public static readonly int kPropertiesReelOptoTab_Y = kPropertiesReelNumber_Y + (27 * 5);

        public static readonly int kPropertiesReelHeight_X = kPropertiesReelNumber_X;
        public static readonly int kPropertiesReelHeight_Y = kPropertiesReelNumber_Y + (27 * 6);

        public static readonly int kPropertiesReelWidthDiff_X = kPropertiesReelNumber_X;
        public static readonly int kPropertiesReelWidthDiff_Y = kPropertiesReelNumber_Y + (27 * 7);

        public static readonly int kPropertiesReelReversedCheckbox_X = 140;
        public static readonly int kPropertiesReelReversedCheckbox_Y = 220;

        public static readonly int kPropertiesReelHorizontalCheckbox_X = 140;
        public static readonly int kPropertiesReelHorizontalCheckbox_Y = 252;

        public static readonly int kPropertiesReelLampsCheckbox_X = 325;
        public static readonly int kPropertiesReelLampsCheckbox_Y = 223;

        public static readonly int kPropertiesReelLampsLEDsCheckbox_X = 325;
        public static readonly int kPropertiesReelLampsLEDsCheckbox_Y = 239;

        public static readonly int kPropertiesReelMirroredCheckbox_X = 325;
        public static readonly int kPropertiesReelMirroredCheckbox_Y = 271;

        public static readonly int kPropertiesReelLamp1Number_X = 325;
        public static readonly int kPropertiesReelLamp1Number_Y = 311;

        public static readonly int kPropertiesReelLamp2Number_X = 325;
        public static readonly int kPropertiesReelLamp2Number_Y = 336;

        public static readonly int kPropertiesReelLamp3Number_X = 325;
        public static readonly int kPropertiesReelLamp3Number_Y = 361;

        public static readonly int kPropertiesReelLamp4Number_X = 325;
        public static readonly int kPropertiesReelLamp4Number_Y = 386;

        public static readonly int kPropertiesReelLamp5Number_X = 325;
        public static readonly int kPropertiesReelLamp5Number_Y = 411;

        public static readonly int kPropertiesReelLamp6Number_X = 367;
        public static readonly int kPropertiesReelLamp6Number_Y = 311;

        public static readonly int kPropertiesReelLamp7Number_X = 367;
        public static readonly int kPropertiesReelLamp7Number_Y = 336;

        public static readonly int kPropertiesReelLamp8Number_X = 367;
        public static readonly int kPropertiesReelLamp8Number_Y = 361;

        public static readonly int kPropertiesReelLamp9Number_X = 367;
        public static readonly int kPropertiesReelLamp9Number_Y = 386;

        public static readonly int kPropertiesReelLamp10Number_X = 367;
        public static readonly int kPropertiesReelLamp10Number_Y = 411;

        public static readonly int kPropertiesReelLamp11Number_X = 413;
        public static readonly int kPropertiesReelLamp11Number_Y = 311;

        public static readonly int kPropertiesReelLamp12Number_X = 413;
        public static readonly int kPropertiesReelLamp12Number_Y = 336;

        public static readonly int kPropertiesReelLamp13Number_X = 413;
        public static readonly int kPropertiesReelLamp13Number_Y = 361;

        public static readonly int kPropertiesReelLamp14Number_X = 413;
        public static readonly int kPropertiesReelLamp14Number_Y = 386;

        public static readonly int kPropertiesReelLamp15Number_X = 413;
        public static readonly int kPropertiesReelLamp15Number_Y = 411;

        public static readonly int kPropertiesReelWinLinesCount_X = 20;
        public static readonly int kPropertiesReelWinLinesCount_Y = 510;

        public static readonly int kPropertiesReelWinLinesOffset_X = 82;
        public static readonly int kPropertiesReelWinLinesOffset_Y = 510;

        public static readonly int kPropertiesReelImage_CenterX = 378;
        public static readonly int kPropertiesReelImage_CenterY = 128;

        public static readonly int kPropertiesReelFilter_X = 157;
        public static readonly int kPropertiesReelFilter_Y = 345;

        public static readonly int kPropertiesReelBounce_X = kPropertiesReelFilter_X;
        public static readonly int kPropertiesReelBounce_Y = 368;


        // band reel fields
        public static readonly int kPropertiesBandReelNumber_X = 20;
        public static readonly int kPropertiesBandReelNumber_Y = 228;

        public static readonly int kPropertiesBandReelStops_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelStops_Y = 253;

        public static readonly int kPropertiesBandReelHalfSteps_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelHalfSteps_Y = 278;

        public static readonly int kPropertiesBandReelView_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelView_Y = 303;

        public static readonly int kPropertiesBandReelOffset_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelOffset_Y = 328;

        public static readonly int kPropertiesBandReelSpacing_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelSpacing_Y = 353;

        public static readonly int kPropertiesBandReelOptoTab_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelOptoTab_Y = 378;

        public static readonly int kPropertiesBandReelReversedCheckbox_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelReversedCheckbox_Y = 406;

        public static readonly int kPropertiesBandReelInvertedCheckbox_X = 93;
        public static readonly int kPropertiesBandReelInvertedCheckbox_Y = kPropertiesBandReelReversedCheckbox_Y;

        public static readonly int kPropertiesBandReelVerticalCheckbox_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelVerticalCheckbox_Y = 426;

        public static readonly int kPropertiesBandReelOpaqueCheckbox_X = kPropertiesBandReelNumber_X;
        public static readonly int kPropertiesBandReelOpaqueCheckbox_Y = 446;

        public static readonly int kPropertiesBandReelLampsCheckbox_X = 308;
        public static readonly int kPropertiesBandReelLampsCheckbox_Y = 235;

        public static readonly int kPropertiesBandReelCustomCheckbox_X = 365;
        public static readonly int kPropertiesBandReelCustomCheckbox_Y = kPropertiesBandReelLampsCheckbox_Y;

        public static readonly int kPropertiesBandReelLampNumber1a_X = 320;
        public static readonly int kPropertiesBandReelLampNumber1a_Y = 286;

        public static readonly int kPropertiesBandReelLampNumber1b_X = 372;
        public static readonly int kPropertiesBandReelLampNumber1b_Y = kPropertiesBandReelLampNumber1a_Y;

        public static readonly int kPropertiesBandReelLampNumber2a_X = kPropertiesBandReelLampNumber1a_X;
        public static readonly int kPropertiesBandReelLampNumber2a_Y = 311;

        public static readonly int kPropertiesBandReelLampNumber2b_X = kPropertiesBandReelLampNumber1b_X;
        public static readonly int kPropertiesBandReelLampNumber2b_Y = kPropertiesBandReelLampNumber2a_Y;

        public static readonly int kPropertiesBandReelLampNumber3a_X = kPropertiesBandReelLampNumber1a_X;
        public static readonly int kPropertiesBandReelLampNumber3a_Y = 336;

        public static readonly int kPropertiesBandReelLampNumber3b_X = kPropertiesBandReelLampNumber1b_X;
        public static readonly int kPropertiesBandReelLampNumber3b_Y = kPropertiesBandReelLampNumber3a_Y;

        public static readonly int kPropertiesBandReelLampNumber4a_X = kPropertiesBandReelLampNumber1a_X;
        public static readonly int kPropertiesBandReelLampNumber4a_Y = 361;

        public static readonly int kPropertiesBandReelLampNumber4b_X = kPropertiesBandReelLampNumber1b_X;
        public static readonly int kPropertiesBandReelLampNumber4b_Y = kPropertiesBandReelLampNumber4a_Y;

        public static readonly int kPropertiesBandReelLampNumber5a_X = kPropertiesBandReelLampNumber1a_X;
        public static readonly int kPropertiesBandReelLampNumber5a_Y = 386;

        public static readonly int kPropertiesBandReelLampNumber5b_X = kPropertiesBandReelLampNumber1b_X;
        public static readonly int kPropertiesBandReelLampNumber5b_Y = kPropertiesBandReelLampNumber5a_Y;

        public static readonly int kPropertiesBandReelMask1Tab_CenterX = 331;
        public static readonly int kPropertiesBandReelMask1Tab_CenterY = 263;

        public static readonly int kPropertiesBandReelMask2Tab_CenterX = 375;
        public static readonly int kPropertiesBandReelMask2Tab_CenterY = kPropertiesBandReelMask1Tab_CenterY;

        public static readonly int kPropertiesBandReelMask3Tab_CenterX = 420;
        public static readonly int kPropertiesBandReelMask3Tab_CenterY = kPropertiesBandReelMask1Tab_CenterY;

        public static readonly int kPropertiesReelBandImage_CenterX = 380;
        public static readonly int kPropertiesReelBandImage_CenterY = 129;


        // disc reel fields
        public static readonly int kPropertiesDiscReelNumber_X = 20;
        public static readonly int kPropertiesDiscReelNumber_Y = 228;

        public static readonly int kPropertiesDiscReelStops_X = kPropertiesDiscReelNumber_X;
        public static readonly int kPropertiesDiscReelStops_Y = 253;

        public static readonly int kPropertiesDiscReelHalfSteps_X = kPropertiesDiscReelNumber_X;
        public static readonly int kPropertiesDiscReelHalfSteps_Y = 278;

        public static readonly int kPropertiesDiscReelResolution_X = kPropertiesDiscReelNumber_X;
        public static readonly int kPropertiesDiscReelResolution_Y = 303;

        public static readonly int kPropertiesDiscReelOffset_X = kPropertiesDiscReelNumber_X;
        public static readonly int kPropertiesDiscReelOffset_Y = 328;

        public static readonly int kPropertiesDiscReelOptoTab_X = kPropertiesDiscReelNumber_X;
        public static readonly int kPropertiesDiscReelOptoTab_Y = 353;

        public static readonly int kPropertiesDiscReelBounce_X = kPropertiesDiscReelNumber_X;
        public static readonly int kPropertiesDiscReelBounce_Y = 378;

        public static readonly int kPropertiesDiscReelLampsCheckbox_X = 22;
        public static readonly int kPropertiesDiscReelLampsCheckbox_Y = 409;

        public static readonly int kPropertiesDiscReelReversedCheckbox_X = 81;
        public static readonly int kPropertiesDiscReelReversedCheckbox_Y = kPropertiesDiscReelLampsCheckbox_Y;

        public static readonly int kPropertiesDiscReelInvertedCheckbox_X = kPropertiesDiscReelLampsCheckbox_X;
        public static readonly int kPropertiesDiscReelInvertedCheckbox_Y = 426;

        public static readonly int kPropertiesDiscReelTransparentCheckbox_X = kPropertiesDiscReelReversedCheckbox_X;
        public static readonly int kPropertiesDiscReelTransparentCheckbox_Y = kPropertiesDiscReelInvertedCheckbox_Y;

        public static readonly int kPropertiesDiscReelOuterH_X = 179;
        public static readonly int kPropertiesDiscReelOuterH_Y = 228;

        public static readonly int kPropertiesDiscReelOuterL_X = kPropertiesDiscReelOuterH_X;
        public static readonly int kPropertiesDiscReelOuterL_Y = 253;

        public static readonly int kPropertiesDiscReelOuterLampSize_X = kPropertiesDiscReelOuterH_X;
        public static readonly int kPropertiesDiscReelOuterLampSize_Y = 278;

        public static readonly int kPropertiesDiscReelInnerH_X = kPropertiesDiscReelOuterH_X;
        public static readonly int kPropertiesDiscReelInnerH_Y = 319;

        public static readonly int kPropertiesDiscReelInnerL_X = kPropertiesDiscReelOuterH_X;
        public static readonly int kPropertiesDiscReelInnerL_Y = 344;

        public static readonly int kPropertiesDiscReelInnerLampSize_X = kPropertiesDiscReelOuterH_X;
        public static readonly int kPropertiesDiscReelInnerLampSize_Y = 369;

        public static readonly int kPropertiesDiscReelLampPositionsLamps_X = 378;
        public static readonly int kPropertiesDiscReelLampPositionsLamps_Y = 228;

        public static readonly int kPropertiesDiscReelLampPositionsLamp_X = kPropertiesDiscReelLampPositionsLamps_X;
        public static readonly int kPropertiesDiscReelLampPositionsLamp_Y = 255;

        public static readonly int kPropertiesDiscReelLampPositionsNumber_X = kPropertiesDiscReelLampPositionsLamps_X;
        public static readonly int kPropertiesDiscReelLampPositionsNumber_Y = 280;

        public static readonly int kPropertiesDiscReelLampPositionsOffset_X = kPropertiesDiscReelLampPositionsLamps_X;
        public static readonly int kPropertiesDiscReelLampPositionsOffset_Y = 305;

        public static readonly int kPropertiesDiscReelLampPositionsGapCheckbox_X = kPropertiesDiscReelLampPositionsLamps_X;
        public static readonly int kPropertiesDiscReelLampPositionsGapCheckbox_Y = 333;

        public static readonly int kPropertiesDiscReelDiscImage_CenterX = 82;
        public static readonly int kPropertiesDiscReelDiscImage_CenterY = 133;

        public static readonly int kPropertiesDiscReelDiscOverlayImage_TopLeftX = 179;
        public static readonly int kPropertiesDiscReelDiscOverlayImage_TopLeftY = 75;
        public static readonly int kPropertiesDiscReelDiscOverlayImage_Width = 116;
        public static readonly int kPropertiesDiscReelDiscOverlayImage_Height = 116;
        public static readonly int kPropertiesDiscReelDiscOverlayImage_CenterX = kPropertiesDiscReelDiscOverlayImage_TopLeftX + (kPropertiesDiscReelDiscOverlayImage_Width / 2);
        public static readonly int kPropertiesDiscReelDiscOverlayImage_CenterY = kPropertiesDiscReelDiscOverlayImage_TopLeftY + (kPropertiesDiscReelDiscOverlayImage_Height / 2);


        // flip reel fields
        public static readonly int kPropertiesFlipReelNumber_X = 20;
        public static readonly int kPropertiesFlipReelNumber_Y = 228;

        public static readonly int kPropertiesFlipReelStops_X = kPropertiesFlipReelNumber_X;
        public static readonly int kPropertiesFlipReelStops_Y = 253;

        public static readonly int kPropertiesFlipReelHalfSteps_X = kPropertiesFlipReelNumber_X;
        public static readonly int kPropertiesFlipReelHalfSteps_Y = 278;

        public static readonly int kPropertiesFlipReelOffset_X = kPropertiesFlipReelNumber_X;
        public static readonly int kPropertiesFlipReelOffset_Y = 332;

        public static readonly int kPropertiesFlipReelReversedCheckbox_X = kPropertiesFlipReelNumber_X;
        public static readonly int kPropertiesFlipReelReversedCheckbox_Y = 380;

        public static readonly int kPropertiesFlipReelInvertedCheckbox_X = 93;
        public static readonly int kPropertiesFlipReelInvertedCheckbox_Y = kPropertiesFlipReelReversedCheckbox_Y;

        public static readonly int kPropertiesFlipReelBorderColourbox_X = 173;
        public static readonly int kPropertiesFlipReelBorderColourbox_Y = 233;

        public static readonly int kPropertiesFlipReelBorderWidth_X = kPropertiesFlipReelBorderColourbox_X;
        public static readonly int kPropertiesFlipReelBorderWidth_Y = 268;

        public static readonly int kPropertiesFlipReelLampsCheckbox_X = 269;
        public static readonly int kPropertiesFlipReelLampsCheckbox_Y = 235;

        public static readonly int kPropertiesFlipReelLamp1_X = kPropertiesFlipReelLampsCheckbox_X;
        public static readonly int kPropertiesFlipReelLamp1_Y = 255;

        public static readonly int kPropertiesFlipReelLamp2_X = kPropertiesFlipReelLampsCheckbox_X;
        public static readonly int kPropertiesFlipReelLamp2_Y = 282;

        public static readonly int kPropertiesFlipReelLamp3_X = kPropertiesFlipReelLampsCheckbox_X;
        public static readonly int kPropertiesFlipReelLamp3_Y = 309;

        public static readonly int kPropertiesFlipReelLampMask1Image_TopLeftX = 24;
        public static readonly int kPropertiesFlipReelLampMask1Image_TopLeftY = 75;
        public static readonly int kPropertiesFlipReelLampMask1Image_Width = 61;
        public static readonly int kPropertiesFlipReelLampMask1Image_Height = 61;
        public static readonly int kPropertiesFlipReelLampMask1Image_CenterX = kPropertiesFlipReelLampMask1Image_TopLeftX + (kPropertiesFlipReelLampMask1Image_Width / 2);
        public static readonly int kPropertiesFlipReelLampMask1Image_CenterY = kPropertiesFlipReelLampMask1Image_TopLeftY + (kPropertiesFlipReelLampMask1Image_Height / 2);

        public static readonly int kPropertiesFlipReelLampMask2Image_TopLeftX = kPropertiesFlipReelLampMask1Image_TopLeftX;
        public static readonly int kPropertiesFlipReelLampMask2Image_TopLeftY = 139;
        public static readonly int kPropertiesFlipReelLampMask2Image_Width = kPropertiesFlipReelLampMask1Image_Width;
        public static readonly int kPropertiesFlipReelLampMask2Image_Height = kPropertiesFlipReelLampMask1Image_Height;
        public static readonly int kPropertiesFlipReelLampMask2Image_CenterX = kPropertiesFlipReelLampMask2Image_TopLeftX + (kPropertiesFlipReelLampMask2Image_Width / 2);
        public static readonly int kPropertiesFlipReelLampMask2Image_CenterY = kPropertiesFlipReelLampMask2Image_TopLeftY + (kPropertiesFlipReelLampMask2Image_Height / 2);

        public static readonly int kPropertiesFlipReelLampMask3Image_TopLeftX = 127;
        public static readonly int kPropertiesFlipReelLampMask3Image_TopLeftY = kPropertiesFlipReelLampMask1Image_TopLeftY;
        public static readonly int kPropertiesFlipReelLampMask3Image_Width = kPropertiesFlipReelLampMask1Image_Width;
        public static readonly int kPropertiesFlipReelLampMask3Image_Height = kPropertiesFlipReelLampMask1Image_Height;
        public static readonly int kPropertiesFlipReelLampMask3Image_CenterX = kPropertiesFlipReelLampMask3Image_TopLeftX + (kPropertiesFlipReelLampMask3Image_Width / 2);
        public static readonly int kPropertiesFlipReelLampMask3Image_CenterY = kPropertiesFlipReelLampMask3Image_TopLeftY + (kPropertiesFlipReelLampMask3Image_Height / 2);

        public static readonly int kPropertiesFlipReelBandImage_CenterX = 380;
        public static readonly int kPropertiesFlipReelBandImage_CenterY = 131;


        // jpm bonus reel fields:
        public static readonly int kPropertiesJPMBonusReelLamp1_X = 20;
        public static readonly int kPropertiesJPMBonusReelLamp1_Y = 81;

        public static readonly int kPropertiesJPMBonusReelLamp2_X = 173;
        public static readonly int kPropertiesJPMBonusReelLamp2_Y = kPropertiesJPMBonusReelLamp1_Y;

        public static readonly int kPropertiesJPMBonusReelLamp3_X = kPropertiesJPMBonusReelLamp1_X;
        public static readonly int kPropertiesJPMBonusReelLamp3_Y = 266;

        public static readonly int kPropertiesJPMBonusReelLamp4_X = kPropertiesJPMBonusReelLamp2_X;
        public static readonly int kPropertiesJPMBonusReelLamp4_Y = kPropertiesJPMBonusReelLamp3_Y;

        public static readonly int kPropertiesJPMBonusReelNumber_X = 325;
        public static readonly int kPropertiesJPMBonusReelNumber_Y = 222;

        public static readonly int kPropertiesJPMBonusReelSymbolPos_X = kPropertiesJPMBonusReelNumber_X;
        public static readonly int kPropertiesJPMBonusReelSymbolPos_Y = 249;

        public static readonly int kPropertiesJPMBonusReelLamp1OnImage_TopLeftX = 84;
        public static readonly int kPropertiesJPMBonusReelLamp1OnImage_TopLeftY = 72;
        public static readonly int kPropertiesJPMBonusReelLamp1OnImage_Width = 63;
        public static readonly int kPropertiesJPMBonusReelLamp1OnImage_Height = 51;
        public static readonly int kPropertiesJPMBonusReelLamp1OnImage_CenterX = kPropertiesJPMBonusReelLamp1OnImage_TopLeftX + (kPropertiesJPMBonusReelLamp1OnImage_Width / 2);
        public static readonly int kPropertiesJPMBonusReelLamp1OnImage_CenterY = kPropertiesJPMBonusReelLamp1OnImage_TopLeftY + (kPropertiesJPMBonusReelLamp1OnImage_Height / 2);

        public static readonly int kPropertiesJPMBonusReelLamp2OnImage_TopLeftX = 237;
        public static readonly int kPropertiesJPMBonusReelLamp2OnImage_TopLeftY = kPropertiesJPMBonusReelLamp1OnImage_TopLeftY;
        public static readonly int kPropertiesJPMBonusReelLamp2OnImage_Width = kPropertiesJPMBonusReelLamp1OnImage_Width;
        public static readonly int kPropertiesJPMBonusReelLamp2OnImage_Height = kPropertiesJPMBonusReelLamp1OnImage_Height;
        public static readonly int kPropertiesJPMBonusReelLamp2OnImage_CenterX = kPropertiesJPMBonusReelLamp2OnImage_TopLeftX + (kPropertiesJPMBonusReelLamp2OnImage_Width / 2);
        public static readonly int kPropertiesJPMBonusReelLamp2OnImage_CenterY = kPropertiesJPMBonusReelLamp2OnImage_TopLeftY + (kPropertiesJPMBonusReelLamp2OnImage_Height / 2);

        public static readonly int kPropertiesJPMBonusReelLamp3OnImage_TopLeftX = kPropertiesJPMBonusReelLamp1OnImage_TopLeftX;
        public static readonly int kPropertiesJPMBonusReelLamp3OnImage_TopLeftY = 259;
        public static readonly int kPropertiesJPMBonusReelLamp3OnImage_Width = kPropertiesJPMBonusReelLamp1OnImage_Width;
        public static readonly int kPropertiesJPMBonusReelLamp3OnImage_Height = kPropertiesJPMBonusReelLamp1OnImage_Height;
        public static readonly int kPropertiesJPMBonusReelLamp3OnImage_CenterX = kPropertiesJPMBonusReelLamp3OnImage_TopLeftX + (kPropertiesJPMBonusReelLamp3OnImage_Width / 2);
        public static readonly int kPropertiesJPMBonusReelLamp3OnImage_CenterY = kPropertiesJPMBonusReelLamp3OnImage_TopLeftY + (kPropertiesJPMBonusReelLamp3OnImage_Height / 2);

        public static readonly int kPropertiesJPMBonusReelLamp4OnImage_TopLeftX = kPropertiesJPMBonusReelLamp2OnImage_TopLeftX;
        public static readonly int kPropertiesJPMBonusReelLamp4OnImage_TopLeftY = 257;
        public static readonly int kPropertiesJPMBonusReelLamp4OnImage_Width = kPropertiesJPMBonusReelLamp1OnImage_Width;
        public static readonly int kPropertiesJPMBonusReelLamp4OnImage_Height = kPropertiesJPMBonusReelLamp1OnImage_Height;
        public static readonly int kPropertiesJPMBonusReelLamp4OnImage_CenterX = kPropertiesJPMBonusReelLamp4OnImage_TopLeftX + (kPropertiesJPMBonusReelLamp4OnImage_Width / 2);
        public static readonly int kPropertiesJPMBonusReelLamp4OnImage_CenterY = kPropertiesJPMBonusReelLamp4OnImage_TopLeftY + (kPropertiesJPMBonusReelLamp4OnImage_Height / 2);

        public static readonly int kPropertiesJPMBonusReelMaskImage_TopLeftX = 391;
        public static readonly int kPropertiesJPMBonusReelMaskImage_TopLeftY = 72;
        public static readonly int kPropertiesJPMBonusReelMaskImage_Width = kPropertiesJPMBonusReelLamp1OnImage_Width;
        public static readonly int kPropertiesJPMBonusReelMaskImage_Height = kPropertiesJPMBonusReelLamp1OnImage_Height;
        public static readonly int kPropertiesJPMBonusReelMaskImage_CenterX = kPropertiesJPMBonusReelMaskImage_TopLeftX + (kPropertiesJPMBonusReelMaskImage_Width / 2);
        public static readonly int kPropertiesJPMBonusReelMaskImage_CenterY = kPropertiesJPMBonusReelMaskImage_TopLeftY + (kPropertiesJPMBonusReelMaskImage_Height / 2);

        public static readonly int kPropertiesJPMBonusReelBackgroundImage_TopLeftX = kPropertiesJPMBonusReelMaskImage_TopLeftX;
        public static readonly int kPropertiesJPMBonusReelBackgroundImage_TopLeftY = 142;
        public static readonly int kPropertiesJPMBonusReelBackgroundImage_Width = kPropertiesJPMBonusReelLamp1OnImage_Width;
        public static readonly int kPropertiesJPMBonusReelBackgroundImage_Height = kPropertiesJPMBonusReelLamp1OnImage_Height;
        public static readonly int kPropertiesJPMBonusReelBackgroundImage_CenterX = kPropertiesJPMBonusReelBackgroundImage_TopLeftX + (kPropertiesJPMBonusReelBackgroundImage_Width / 2);
        public static readonly int kPropertiesJPMBonusReelBackgroundImage_CenterY = kPropertiesJPMBonusReelBackgroundImage_TopLeftY + (kPropertiesJPMBonusReelBackgroundImage_Height / 2);


        // bfm alpha fields
        public static readonly int kPropertiesBFMAlphaReversedCheckbox_X = 20;
        public static readonly int kPropertiesBFMAlphaReversedCheckbox_Y = 281;

        public static readonly int kPropertiesBFMAlphaColourColorbox_X = kPropertiesBFMAlphaReversedCheckbox_X;
        public static readonly int kPropertiesBFMAlphaColourColorbox_Y = 300;

        public static readonly int kPropertiesBFMAlphaOffLevel_X = kPropertiesBFMAlphaReversedCheckbox_X;
        public static readonly int kPropertiesBFMAlphaOffLevel_Y = 325;

        public static readonly int kPropertiesBFMAlphaDigitWidth_X = kPropertiesBFMAlphaReversedCheckbox_X;
        public static readonly int kPropertiesBFMAlphaDigitWidth_Y = 350;

        public static readonly int kPropertiesBFMAlphaColumns_X = kPropertiesBFMAlphaReversedCheckbox_X;
        public static readonly int kPropertiesBFMAlphaColumns_Y = 375;


        // proconn matrix fields
        public static readonly int kPropertiesProconnMatrixDotSize_X = 20;
        public static readonly int kPropertiesProconnMatrixDotSize_Y = 289;

        public static readonly int kPropertiesProconnMatrixOnColourColorbox_X = 17;
        public static readonly int kPropertiesProconnMatrixOnColourColorbox_Y = 319;

        public static readonly int kPropertiesProconnMatrixOffColourColorbox_X = kPropertiesProconnMatrixOnColourColorbox_X;
        public static readonly int kPropertiesProconnMatrixOffColourColorbox_Y = 346;

        public static readonly int kPropertiesProconnMatrixBackgroundColourColorbox_X = kPropertiesProconnMatrixOnColourColorbox_X;
        public static readonly int kPropertiesProconnMatrixBackgroundColourColorbox_Y = 373;


        // epoch alpha fields
        public static readonly int kPropertiesEpochAlphaXSize_X = 13;
        public static readonly int kPropertiesEpochAlphaXSize_Y = 219;

        public static readonly int kPropertiesEpochAlphaYSize_X = kPropertiesEpochAlphaXSize_X;
        public static readonly int kPropertiesEpochAlphaYSize_Y = 244;

        public static readonly int kPropertiesEpochAlphaDotSpacing_X = kPropertiesEpochAlphaXSize_X;
        public static readonly int kPropertiesEpochAlphaDotSpacing_Y = 269;

        public static readonly int kPropertiesEpochAlphaDigitSpacing_X = kPropertiesEpochAlphaXSize_X;
        public static readonly int kPropertiesEpochAlphaDigitSpacing_Y = 294;

        public static readonly int kPropertiesEpochAlphaOnColourColorbox_X = 15;
        public static readonly int kPropertiesEpochAlphaOnColourColorbox_Y = 320;

        public static readonly int kPropertiesEpochAlphaOffColourColorbox_X = kPropertiesEpochAlphaOnColourColorbox_X;
        public static readonly int kPropertiesEpochAlphaOffColourColorbox_Y = 344;

        public static readonly int kPropertiesEpochAlphaBackgroundColourColorbox_X = kPropertiesEpochAlphaOnColourColorbox_X;
        public static readonly int kPropertiesEpochAlphaBackgroundColourColorbox_Y = 368;


        // igt vfd fields
        public static readonly int kPropertiesIgtVfdNumber_X = 20;
        public static readonly int kPropertiesIgtVfdNumber_Y = 235;

        public static readonly int kPropertiesIgtVfdDotSize_X = kPropertiesIgtVfdNumber_X;
        public static readonly int kPropertiesIgtVfdDotSize_Y = 262;

        public static readonly int kPropertiesIgtVfdDotSpacing_X = kPropertiesIgtVfdNumber_X;
        public static readonly int kPropertiesIgtVfdDotSpacing_Y = 289;

        public static readonly int kPropertiesIgtVfdOnColourColorbox_X = 21;
        public static readonly int kPropertiesIgtVfdOnColourColorbox_Y = 318;

        public static readonly int kPropertiesIgtVfdOffColourColorbox_X = kPropertiesIgtVfdOnColourColorbox_X;
        public static readonly int kPropertiesIgtVfdOffColourColorbox_Y = 342;

        public static readonly int kPropertiesIgtVfdBackgroundColourColorbox_X = kPropertiesIgtVfdOnColourColorbox_X;
        public static readonly int kPropertiesIgtVfdBackgroundColourColorbox_Y = 366;


        // plasma fields
        public static readonly int kPropertiesPlasmaDotSize_X = 21;
        public static readonly int kPropertiesPlasmaDotSize_Y = 289;

        public static readonly int kPropertiesPlasmaOnColourColorbox_X = 18;
        public static readonly int kPropertiesPlasmaOnColourColorbox_Y = 323;

        public static readonly int kPropertiesPlasmaOffColourColorbox_X = kPropertiesPlasmaOnColourColorbox_X;
        public static readonly int kPropertiesPlasmaOffColourColorbox_Y = 346;

        public static readonly int kPropertiesPlasmaBackgroundColourColorbox_X = kPropertiesPlasmaOnColourColorbox_X;
        public static readonly int kPropertiesPlasmaBackgroundColourColorbox_Y = 369;


        // dot matrix fields
        public static readonly int kPropertiesDotMatrixDotSize_X = 21;
        public static readonly int kPropertiesDotMatrixDotSize_Y = 291;

        public static readonly int kPropertiesDotMatrixOnColourColorbox_X = 18;
        public static readonly int kPropertiesDotMatrixOnColourColorbox_Y = 321;

        public static readonly int kPropertiesDotMatrixOffColourColorbox_X = kPropertiesDotMatrixOnColourColorbox_X;
        public static readonly int kPropertiesDotMatrixOffColourColorbox_Y = 346;

        public static readonly int kPropertiesDotMatrixBackgroundColourColorbox_X = kPropertiesDotMatrixOnColourColorbox_X;
        public static readonly int kPropertiesDotMatrixBackgroundColourColorbox_Y = 371;


        // bfm led fields
        public static readonly int kPropertiesBfmLedXSize_X = 20;
        public static readonly int kPropertiesBfmLedXSize_Y = 227;

        public static readonly int kPropertiesBfmLedYSize_X = kPropertiesBfmLedXSize_X;
        public static readonly int kPropertiesBfmLedYSize_Y = 252;

        public static readonly int kPropertiesBfmLedDigitSpacing_X = kPropertiesBfmLedXSize_X;
        public static readonly int kPropertiesBfmLedDigitSpacing_Y = 277;

        public static readonly int kPropertiesBfmLedLedSize_X = kPropertiesBfmLedXSize_X;
        public static readonly int kPropertiesBfmLedLedSize_Y = 300;

        public static readonly int kPropertiesBfmLedOnColourColorbox_X = kPropertiesBfmLedXSize_X;
        public static readonly int kPropertiesBfmLedOnColourColorbox_Y = 326;

        public static readonly int kPropertiesBfmLedOffColourColorbox_X = kPropertiesBfmLedXSize_X;
        public static readonly int kPropertiesBfmLedOffColourColorbox_Y = 350;

        public static readonly int kPropertiesBfmLedBackColourColorbox_X = kPropertiesBfmLedXSize_X;
        public static readonly int kPropertiesBfmLedBackColourColorbox_Y = 374;


        // bfm colour led fields
        public static readonly int kPropertiesBfmColourLedDotSize_X = 21;
        public static readonly int kPropertiesBfmColourLedDotSize_Y = 289;

        public static readonly int kPropertiesBfmColourLedSpacing_X = kPropertiesBfmColourLedDotSize_X;
        public static readonly int kPropertiesBfmColourLedSpacing_Y = 314;

        public static readonly int kPropertiesBfmColourLedOffColourColorbox_X = 18;
        public static readonly int kPropertiesBfmColourLedOffColourColorbox_Y = 344;

        public static readonly int kPropertiesBfmColourLedBackgroundColourColorbox_X = kPropertiesBfmColourLedOffColourColorbox_X;
        public static readonly int kPropertiesBfmColourLedBackgroundColourColorbox_Y = 369;


        // ace matrix fields
        public static readonly int kPropertiesAceMatrixDotSize_X = 15;
        public static readonly int kPropertiesAceMatrixDotSize_Y = 245;

        public static readonly int kPropertiesAceMatrixFlip180Checkbox_X = kPropertiesAceMatrixDotSize_X;
        public static readonly int kPropertiesAceMatrixFlip180Checkbox_Y = 276;

        public static readonly int kPropertiesAceMatrixVerticalCheckbox_X = kPropertiesAceMatrixDotSize_X;
        public static readonly int kPropertiesAceMatrixVerticalCheckbox_Y = 298;

        public static readonly int kPropertiesAceMatrixOnColourColorbox_X = kPropertiesAceMatrixDotSize_X;
        public static readonly int kPropertiesAceMatrixOnColourColorbox_Y = 319;

        public static readonly int kPropertiesAceMatrixOffColourColorbox_X = kPropertiesAceMatrixDotSize_X;
        public static readonly int kPropertiesAceMatrixOffColourColorbox_Y = 346;

        public static readonly int kPropertiesAceMatrixBackgroundColourColorbox_X = kPropertiesAceMatrixDotSize_X;
        public static readonly int kPropertiesAceMatrixBackgroundColourColorbox_Y = 373;


        // epoch matrix fields
        public static readonly int kPropertiesEpochMatrixDotSize_X = 21;
        public static readonly int kPropertiesEpochMatrixDotSize_Y = 245;

        public static readonly int kPropertiesEpochMatrixOffColourColorbox_X = 18;
        public static readonly int kPropertiesEpochMatrixOffColourColorbox_Y = 279;

        public static readonly int kPropertiesEpochMatrixOnColourLoColorbox_X = kPropertiesEpochMatrixOffColourColorbox_X;
        public static readonly int kPropertiesEpochMatrixOnColourLoColorbox_Y = 302;

        public static readonly int kPropertiesEpochMatrixOnColourMedColorbox_X = kPropertiesEpochMatrixOffColourColorbox_X;
        public static readonly int kPropertiesEpochMatrixOnColourMedColorbox_Y = 325;

        public static readonly int kPropertiesEpochMatrixOnColourHiColorbox_X = kPropertiesEpochMatrixOffColourColorbox_X;
        public static readonly int kPropertiesEpochMatrixOnColourHiColorbox_Y = 348;

        public static readonly int kPropertiesEpochMatrixBackgroundColourColorbox_X = kPropertiesEpochMatrixOffColourColorbox_X;
        public static readonly int kPropertiesEpochMatrixBackgroundColourColorbox_Y = 371;

        // barcrest / bwb video fields
        public static readonly int kPropertiesBarcrestBwbVideoNumber_X = 26;
        public static readonly int kPropertiesBarcrestBwbVideoNumber_Y = 295;

        public static readonly int kPropertiesBarcrestBwbVideoLeftSkew_X = kPropertiesBarcrestBwbVideoNumber_X;
        public static readonly int kPropertiesBarcrestBwbVideoLeftSkew_Y = 320;

        public static readonly int kPropertiesBarcrestBwbVideoRightSkew_X = kPropertiesBarcrestBwbVideoNumber_X;
        public static readonly int kPropertiesBarcrestBwbVideoRightSkew_Y = 344;

        // bfm video fields
        public static readonly int kPropertiesBfmVideoNumber_X = 22;
        public static readonly int kPropertiesBfmVideoNumber_Y = 295;

        public static readonly int kPropertiesBfmVideo600x800VRadioButton_TopLeftX = 21;
        public static readonly int kPropertiesBfmVideo600x800VRadioButton_TopLeftY = 320;

        public static readonly int kPropertiesBfmVideo480x640VRadioButton_TopLeftX = kPropertiesBfmVideo600x800VRadioButton_TopLeftX;
        public static readonly int kPropertiesBfmVideo480x640VRadioButton_TopLeftY = 340;

        public static readonly int kPropertiesBfmVideo800x600HRadioButton_TopLeftX = kPropertiesBfmVideo600x800VRadioButton_TopLeftX;
        public static readonly int kPropertiesBfmVideo800x600HRadioButton_TopLeftY = 359;

        public static readonly int kPropertiesBfmVideo640x480HRadioButton_TopLeftX = kPropertiesBfmVideo600x800VRadioButton_TopLeftX;
        public static readonly int kPropertiesBfmVideo640x480HRadioButton_TopLeftY = 378;

        // maygay video fields
        public static readonly int kPropertiesMaygayVideoNumber_X = 34;
        public static readonly int kPropertiesMaygayVideoNumber_Y = 328;

        public static readonly int kPropertiesMaygayVideoVerticalCheckbox_X = 35;
        public static readonly int kPropertiesMaygayVideoVerticalCheckbox_Y = 362;

        public static readonly int kPropertiesMaygayVideoQualityDropdown_X = kPropertiesMaygayVideoNumber_X;
        public static readonly int kPropertiesMaygayVideoQualityDropdown_Y = 389;

        // prism lamp fields
        public static readonly int kPropertiesPrismLampLamp1Number_X = 20;
        public static readonly int kPropertiesPrismLampLamp1Number_Y = 81;

        public static readonly int kPropertiesPrismLampLamp2Number_X = 173;
        public static readonly int kPropertiesPrismLampLamp2Number_Y = kPropertiesPrismLampLamp1Number_Y;

        public static readonly int kPropertiesPrismLampHorzSpacing_X = 326;
        public static readonly int kPropertiesPrismLampHorzSpacing_Y = 165;

        public static readonly int kPropertiesPrismLampVertSpacing_X = kPropertiesPrismLampHorzSpacing_X;
        public static readonly int kPropertiesPrismLampVertSpacing_Y = 187;

        public static readonly int kPropertiesPrismLampTilt_X = kPropertiesPrismLampHorzSpacing_X;
        public static readonly int kPropertiesPrismLampTilt_Y = 209;

        public static readonly int kPropertiesPrismLampStyleCheckbox_X = kPropertiesPrismLampHorzSpacing_X;
        public static readonly int kPropertiesPrismLampStyleCheckbox_Y = 243;

        public static readonly int kPropertiesPrismLampHorizontalCheckbox_X = kPropertiesPrismLampHorzSpacing_X;
        public static readonly int kPropertiesPrismLampHorizontalCheckbox_Y = 261;

        public static readonly int kPropertiesPrismLampCentreLineCheckbox_X = kPropertiesPrismLampHorzSpacing_X;
        public static readonly int kPropertiesPrismLampCentreLineCheckbox_Y = 279;

        public static readonly int kPropertiesPrismLampLamp1Image_TopLeftX = 84;
        public static readonly int kPropertiesPrismLampLamp1Image_TopLeftY = 72;
        public static readonly int kPropertiesPrismLampLamp1Image_Width = 63;
        public static readonly int kPropertiesPrismLampLamp1Image_Height = 51;
        public static readonly int kPropertiesPrismLampLamp1Image_CenterX = kPropertiesPrismLampLamp1Image_TopLeftX + (kPropertiesPrismLampLamp1Image_Width / 2);
        public static readonly int kPropertiesPrismLampLamp1Image_CenterY = kPropertiesPrismLampLamp1Image_TopLeftY + (kPropertiesPrismLampLamp1Image_Height / 2);

        public static readonly int kPropertiesPrismLampLamp1Mask_TopLeftX = kPropertiesPrismLampLamp1Image_TopLeftX;
        public static readonly int kPropertiesPrismLampLamp1Mask_TopLeftY = 142;
        public static readonly int kPropertiesPrismLampLamp1Mask_Width = kPropertiesPrismLampLamp1Image_Width;
        public static readonly int kPropertiesPrismLampLamp1Mask_Height = kPropertiesPrismLampLamp1Image_Height;
        public static readonly int kPropertiesPrismLampLamp1Mask_CenterX = kPropertiesPrismLampLamp1Mask_TopLeftX + (kPropertiesPrismLampLamp1Mask_Width / 2);
        public static readonly int kPropertiesPrismLampLamp1Mask_CenterY = kPropertiesPrismLampLamp1Mask_TopLeftY + (kPropertiesPrismLampLamp1Mask_Height / 2);

        public static readonly int kPropertiesPrismLampLamp2Image_TopLeftX = 237;
        public static readonly int kPropertiesPrismLampLamp2Image_TopLeftY = kPropertiesPrismLampLamp1Image_TopLeftY;
        public static readonly int kPropertiesPrismLampLamp2Image_Width = kPropertiesPrismLampLamp1Image_Width;
        public static readonly int kPropertiesPrismLampLamp2Image_Height = kPropertiesPrismLampLamp1Image_Height;
        public static readonly int kPropertiesPrismLampLamp2Image_CenterX = kPropertiesPrismLampLamp2Image_TopLeftX + (kPropertiesPrismLampLamp2Image_Width / 2);
        public static readonly int kPropertiesPrismLampLamp2Image_CenterY = kPropertiesPrismLampLamp2Image_TopLeftY + (kPropertiesPrismLampLamp2Image_Height / 2);

        public static readonly int kPropertiesPrismLampLamp2Mask_TopLeftX = kPropertiesPrismLampLamp2Image_TopLeftX;
        public static readonly int kPropertiesPrismLampLamp2Mask_TopLeftY = kPropertiesPrismLampLamp1Mask_TopLeftY;
        public static readonly int kPropertiesPrismLampLamp2Mask_Width = kPropertiesPrismLampLamp1Image_Width;
        public static readonly int kPropertiesPrismLampLamp2Mask_Height = kPropertiesPrismLampLamp1Image_Height;
        public static readonly int kPropertiesPrismLampLamp2Mask_CenterX = kPropertiesPrismLampLamp2Mask_TopLeftX + (kPropertiesPrismLampLamp2Mask_Width / 2);
        public static readonly int kPropertiesPrismLampLamp2Mask_CenterY = kPropertiesPrismLampLamp2Mask_TopLeftY + (kPropertiesPrismLampLamp2Mask_Height / 2);

        public static readonly int kPropertiesPrismLampOffImage_TopLeftX = 388;
        public static readonly int kPropertiesPrismLampOffImage_TopLeftY = 70;
        public static readonly int kPropertiesPrismLampOffImage_Width = kPropertiesPrismLampLamp1Image_Width;
        public static readonly int kPropertiesPrismLampOffImage_Height = kPropertiesPrismLampLamp1Image_Height;
        public static readonly int kPropertiesPrismLampOffImage_CenterX = kPropertiesPrismLampOffImage_TopLeftX + (kPropertiesPrismLampOffImage_Width / 2);
        public static readonly int kPropertiesPrismLampOffImage_CenterY = kPropertiesPrismLampOffImage_TopLeftY + (kPropertiesPrismLampOffImage_Height / 2);

        // bitmap fields
        public static readonly int kPropertiesBitmapTransparentCheckbox_X = 19;
        public static readonly int kPropertiesBitmapTransparentCheckbox_Y = 230;

        public static readonly int kPropertiesBitmapStretchFilterNearestRadioButton_X = 18;
        public static readonly int kPropertiesBitmapStretchFilterNearestRadioButton_Y = 285;

        public static readonly int kPropertiesBitmapStretchFilterDraftRadioButton_X = kPropertiesBitmapStretchFilterNearestRadioButton_X;
        public static readonly int kPropertiesBitmapStretchFilterDraftRadioButton_Y = 299;

        public static readonly int kPropertiesBitmapStretchFilterLinearRadioButton_X = kPropertiesBitmapStretchFilterNearestRadioButton_X;
        public static readonly int kPropertiesBitmapStretchFilterLinearRadioButton_Y = 313;

        public static readonly int kPropertiesBitmapStretchFilterCosineRadioButton_X = kPropertiesBitmapStretchFilterNearestRadioButton_X;
        public static readonly int kPropertiesBitmapStretchFilterCosineRadioButton_Y = 327;

        public static readonly int kPropertiesBitmapStretchFilterSplineRadioButton_X = kPropertiesBitmapStretchFilterNearestRadioButton_X;
        public static readonly int kPropertiesBitmapStretchFilterSplineRadioButton_Y = 341;

        public static readonly int kPropertiesBitmapStretchFilterLanczosRadioButton_X = kPropertiesBitmapStretchFilterNearestRadioButton_X;
        public static readonly int kPropertiesBitmapStretchFilterLanczosRadioButton_Y = 355;

        public static readonly int kPropertiesBitmapStretchFilterMitchellRadioButton_X = kPropertiesBitmapStretchFilterNearestRadioButton_X;
        public static readonly int kPropertiesBitmapStretchFilterMitchellRadioButton_Y = 369;

        public static readonly int kPropertiesBitmapImage_TopLeftX = 19;
        public static readonly int kPropertiesBitmapImage_TopLeftY = 70;
        public static readonly int kPropertiesBitmapImage_Width = 126;
        public static readonly int kPropertiesBitmapImage_Height = 126;
        public static readonly int kPropertiesBitmapImage_CenterX = kPropertiesBitmapImage_TopLeftX + (kPropertiesBitmapImage_Width / 2);
        public static readonly int kPropertiesBitmapImage_CenterY = kPropertiesBitmapImage_TopLeftY + (kPropertiesBitmapImage_Height / 2);



        // lamp fields

        public static readonly int kPropertiesLampNoOutlineCheckbox_X = 322;
        public static readonly int kPropertiesLampNoOutlineCheckbox_Y = 233;

        public static readonly int kPropertiesLampGraphicCheckbox_X = 402;
        public static readonly int kPropertiesLampGraphicCheckbox_Y = 233;

        public static readonly int kPropertiesLampTransparentCheckbox_X = 322;
        public static readonly int kPropertiesLampTransparentCheckbox_Y = 249;

        public static readonly int kPropertiesLampBlendCheckbox_X = 402;
        public static readonly int kPropertiesLampBlendCheckbox_Y = 249;

        public static readonly int kPropertiesLampInvertedCheckbox_X = 322;
        public static readonly int kPropertiesLampInvertedCheckbox_Y = 265;

        public static readonly int kPropertiesLampClickAllCheckbox_X = 402;
        public static readonly int kPropertiesLampClickAllCheckbox_Y = 265;

        public static readonly int kPropertiesLampLEDCheckbox_X = 322;
        public static readonly int kPropertiesLampLEDCheckbox_Y = 281;

        public static readonly int kPropertiesLampLockOutCheckbox_X = 402;
        public static readonly int kPropertiesLampLockOutCheckbox_Y = 281;

        public static readonly int kPropertiesLampRGBCheckbox_X = 322;
        public static readonly int kPropertiesLampRGBCheckbox_Y = 297;

        public static readonly int kPropertiesLampPreserveAspectRatioCheckbox_X = 322;
        public static readonly int kPropertiesLampPreserveAspectRatioCheckbox_Y = 313;

        public static readonly int kPropertiesLampButtonNumber_X = 322;
        public static readonly int kPropertiesLampButtonNumber_Y = 329;

        public static readonly int kPropertiesLampShortcut1_X = 322;
        public static readonly int kPropertiesLampShortcut1_Y = 431;

        public static readonly int kPropertiesLampShortcut2_X = 369;
        public static readonly int kPropertiesLampShortcut2_Y = 431;

        public static readonly int kPropertiesLampShape_X = 322;
        public static readonly int kPropertiesLampShape_Y = 527;

        public static readonly int kPropertiesLampCoinNote_X = 322;
        public static readonly int kPropertiesLampCoinNote_Y = 356;

        public static readonly int kPropertiesLampEffect_X = 322;
        public static readonly int kPropertiesLampEffect_Y = 381;

        // TODO Inhibit Lamp - needs to store if blank - nullable int?
        public static readonly int kPropertiesLampInhibitLamp_X = 322;
        public static readonly int kPropertiesLampInhibitLamp_Y = 407;

        public static readonly int kPropertiesLampTextColourColourbox_X = 322;
        public static readonly int kPropertiesLampTextColourColourbox_Y = 456;

        public static readonly int kPropertiesLampOutlineColourColourbox_X = 322;
        public static readonly int kPropertiesLampOutlineColourColourbox_Y = 478;

        public static readonly int kPropertiesLampXOff_X = 322;
        public static readonly int kPropertiesLampXOff_Y = 502;

        public static readonly int kPropertiesLampYOff_X = 395;
        public static readonly int kPropertiesLampYOff_Y = 502;

        public static readonly int kPropertiesLampShapeParameter1_X = 322;
        public static readonly int kPropertiesLampShapeParameter1_Y = 554;

        public static readonly int kPropertiesLampShapeParameter2_X = kPropertiesLampShapeParameter1_X;
        public static readonly int kPropertiesLampShapeParameter2_Y = kPropertiesLampShapeParameter1_Y + 23;

        public static readonly int kPropertiesLampOffImageColourbox_X = 323;
        public static readonly int kPropertiesLampOffImageColourbox_Y = 111;





        // lamp elements
        public static readonly int kPropertiesLamp1Index_X = 21;
        public static readonly int kPropertiesLamp1Index_Y = 81;

        public static readonly int kPropertiesLamp1OnColourbox_X = 21;
        public static readonly int kPropertiesLamp1OnColourbox_Y = 111;

        public static readonly int kPropertiesLampImage_Width = 63;
        public static readonly int kPropertiesLampImage_Height = 51;

        public static readonly int kPropertiesLamp1Image_TopLeftX = 85;
        public static readonly int kPropertiesLamp1Image_TopLeftY = 72;
        public static readonly int kPropertiesLamp1Image_CenterX = kPropertiesLamp1Image_TopLeftX + (kPropertiesLampImage_Width / 2);
        public static readonly int kPropertiesLamp1Image_CenterY = kPropertiesLamp1Image_TopLeftY + (kPropertiesLampImage_Height / 2);


        // dot alpha fields:
        public static readonly int kPropertiesDotAlphaNumber_X = 20;
        public static readonly int kPropertiesDotAlphaNumber_Y = 191;

        public static readonly int kPropertiesDotAlphaXSize_X = 20;
        public static readonly int kPropertiesDotAlphaXSize_Y = 218;

        public static readonly int kPropertiesDotAlphaYSize_X = 20;
        public static readonly int kPropertiesDotAlphaYSize_Y = 243;

        public static readonly int kPropertiesDotAlphaDotSpacing_X = 20;
        public static readonly int kPropertiesDotAlphaDotSpacing_Y = 268;

        public static readonly int kPropertiesDotAlphaDigitSpacing_X = 20;
        public static readonly int kPropertiesDotAlphaDigitSpacing_Y = 293;

        public static readonly int kPropertiesDotAlphaOnColourbox_X = 17;
        public static readonly int kPropertiesDotAlphaOnColourbox_Y = 323;

        public static readonly int kPropertiesDotAlphaOffColourbox_X = 17;
        public static readonly int kPropertiesDotAlphaOffColourbox_Y = 347;

        public static readonly int kPropertiesDotAlphaBackgroundColourbox_X = 17;
        public static readonly int kPropertiesDotAlphaBackgroundColourbox_Y = 371;


        // matrix alpha fields:
        public static readonly int kPropertiesMatrixAlphaNumber_X = 15;
        public static readonly int kPropertiesMatrixAlphaNumber_Y = 219;

        public static readonly int kPropertiesMatrixAlphaXSize_X = 15;
        public static readonly int kPropertiesMatrixAlphaXSize_Y = 243;

        public static readonly int kPropertiesMatrixAlphaYSize_X = 15;
        public static readonly int kPropertiesMatrixAlphaYSize_Y = 268;

        public static readonly int kPropertiesMatrixAlphaDotSpacing_X = 15;
        public static readonly int kPropertiesMatrixAlphaDotSpacing_Y = 293;


        // alpha new fields:
        public static readonly int kPropertiesAlphaNewNumber_X = 17;
        public static readonly int kPropertiesAlphaNewNumber_Y = 173;

        public static readonly int kPropertiesAlphaNewOldCharset_X = 17;
        public static readonly int kPropertiesAlphaNewOldCharset_Y = 198;

        public static readonly int kPropertiesAlphaNewOKI1937Charset_X = 17;
        public static readonly int kPropertiesAlphaNewOKI1937Charset_Y = 214;

        public static readonly int kPropertiesAlphaNewBFMCharset_X = 17;
        public static readonly int kPropertiesAlphaNewBFMCharset_Y = 230;

        public static readonly int kPropertiesAlphaNew16SegCheckbox_X = 18;
        public static readonly int kPropertiesAlphaNew16SegCheckbox_Y = 253;

        public static readonly int kPropertiesAlphaNewReversedCheckbox_X = 18;
        public static readonly int kPropertiesAlphaNewReversedCheckbox_Y = 271;

        public static readonly int kPropertiesAlphaNewOnColourbox_X = 153;
        public static readonly int kPropertiesAlphaNewOnColourbox_Y = 178;

        // alpha fields: (the bitmap version of alpha new, in old layouts plus used by data layout)
        public static readonly int kPropertiesAlphaNumber_X = 20;
        public static readonly int kPropertiesAlphaNumber_Y = 240;

        public static readonly int kPropertiesAlphaReversedCheckbox_X = 20;
        public static readonly int kPropertiesAlphaReversedCheckbox_Y = 271;

        public static readonly int kPropertiesAlphaOnColourbox_X = 20;
        public static readonly int kPropertiesAlphaOnColourbox_Y = 290;

        public static readonly int kPropertiesAlphaDigitWidth_X = 20;
        public static readonly int kPropertiesAlphaDigitWidth_Y = 315;

        public static readonly int kPropertiesAlphaColumns_X = 20;
        public static readonly int kPropertiesAlphaColumns_Y = 340;

        public static readonly int kPropertiesAlphaImage_CenterX = 225;
        public static readonly int kPropertiesAlphaImage_CenterY = 92;

        // seven segment fields:
        public static readonly int kPropertiesSevenSegmentNumber_X = 17;
        public static readonly int kPropertiesSevenSegmentNumber_Y = 83;

        public static readonly int kPropertiesSevenSegmentDPRightCheckbox_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentDPRightCheckbox_Y = 113;

        public static readonly int kPropertiesSevenSegmentAlphaCheckbox_X = 97;
        public static readonly int kPropertiesSevenSegmentAlphaCheckbox_Y = kPropertiesSevenSegmentDPRightCheckbox_Y;

        public static readonly int kPropertiesSevenSegmentDPOffCheckbox_X = 163;
        public static readonly int kPropertiesSevenSegmentDPOffCheckbox_Y = kPropertiesSevenSegmentDPRightCheckbox_Y;

        public static readonly int kPropertiesSevenSegmentDPOnCheckbox_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentDPOnCheckbox_Y = 133;

        public static readonly int kPropertiesSevenSegmentAutoDPCheckbox_X = kPropertiesSevenSegmentAlphaCheckbox_X;
        public static readonly int kPropertiesSevenSegmentAutoDPCheckbox_Y = kPropertiesSevenSegmentDPOnCheckbox_Y;

        public static readonly int kPropertiesSevenSegmentSixteenSegmentCheckbox_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentSixteenSegmentCheckbox_Y = 153;

        public static readonly int kPropertiesSevenSegmentZeroOnCheckbox_X = kPropertiesSevenSegmentAlphaCheckbox_X;
        public static readonly int kPropertiesSevenSegmentZeroOnCheckbox_Y = kPropertiesSevenSegmentSixteenSegmentCheckbox_Y;

        public static readonly int kPropertiesSevenSegmentTypeDropdown_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentTypeDropdown_Y = 177;

        public static readonly int kPropertiesSevenSegmentSegmentOnColorColorbox_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentSegmentOnColorColorbox_Y = 213;

        public static readonly int kPropertiesSevenSegmentSegmentOffColorColorbox_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentSegmentOffColorColorbox_Y = 238;

        public static readonly int kPropertiesSevenSegmentSegmentBackgroundColorColorbox_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentSegmentBackgroundColorColorbox_Y = 263;

        public static readonly int kPropertiesSevenSegmentThickness_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentThickness_Y = 311;

        public static readonly int kPropertiesSevenSegmentSpacing_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentSpacing_Y = 336;

        public static readonly int kPropertiesSevenSegmentHorzSpacing_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentHorzSpacing_Y = 361;

        public static readonly int kPropertiesSevenSegmentVertSpacing_X = kPropertiesSevenSegmentNumber_X;
        public static readonly int kPropertiesSevenSegmentVertSpacing_Y = 386;

        public static readonly int kPropertiesSevenSegmentOffset_X = 153;
        public static readonly int kPropertiesSevenSegmentOffset_Y = 289;

        public static readonly int kPropertiesSevenSegmentAngle_X = kPropertiesSevenSegmentOffset_X;
        public static readonly int kPropertiesSevenSegmentAngle_Y = 314;

        public static readonly int kPropertiesSevenSegmentSlant_X = kPropertiesSevenSegmentOffset_X;
        public static readonly int kPropertiesSevenSegmentSlant_Y = 338;

        public static readonly int kPropertiesSevenSegmentChop_X = kPropertiesSevenSegmentOffset_X;
        public static readonly int kPropertiesSevenSegmentChop_Y = 362;

        public static readonly int kPropertiesSevenSegmentCentre_X = kPropertiesSevenSegmentOffset_X;
        public static readonly int kPropertiesSevenSegmentCentre_Y = 386;

        public static readonly int kPropertiesSevenSegmentLampsProgrammableCheckbox_X = 262;
        public static readonly int kPropertiesSevenSegmentLampsProgrammableCheckbox_Y = 87;

        public static readonly int kPropertiesSevenSegmentLamps1_X = 265;
        public static readonly int kPropertiesSevenSegmentLamps1_Y = 111;

        public static readonly int kPropertiesSevenSegmentLamps2_X = kPropertiesSevenSegmentLamps1_X;
        public static readonly int kPropertiesSevenSegmentLamps2_Y = 136;

        public static readonly int kPropertiesSevenSegmentLamps3_X = kPropertiesSevenSegmentLamps1_X;
        public static readonly int kPropertiesSevenSegmentLamps3_Y = 161;

        public static readonly int kPropertiesSevenSegmentLamps4_X = kPropertiesSevenSegmentLamps1_X;
        public static readonly int kPropertiesSevenSegmentLamps4_Y = 186;

        public static readonly int kPropertiesSevenSegmentLamps5_X = kPropertiesSevenSegmentLamps1_X;
        public static readonly int kPropertiesSevenSegmentLamps5_Y = 211;

        public static readonly int kPropertiesSevenSegmentLamps6_X = kPropertiesSevenSegmentLamps1_X;
        public static readonly int kPropertiesSevenSegmentLamps6_Y = 236;

        public static readonly int kPropertiesSevenSegmentLamps7_X = kPropertiesSevenSegmentLamps1_X;
        public static readonly int kPropertiesSevenSegmentLamps7_Y = 261;

        public static readonly int kPropertiesSevenSegmentLamps8_X = kPropertiesSevenSegmentLamps1_X;
        public static readonly int kPropertiesSevenSegmentLamps8_Y = 286;

        public static readonly int kPropertiesSevenSegmentLamps9_X = 325;
        public static readonly int kPropertiesSevenSegmentLamps9_Y = kPropertiesSevenSegmentLamps1_Y;

        public static readonly int kPropertiesSevenSegmentLamps10_X = kPropertiesSevenSegmentLamps9_X;
        public static readonly int kPropertiesSevenSegmentLamps10_Y = kPropertiesSevenSegmentLamps2_Y;

        public static readonly int kPropertiesSevenSegmentLamps11_X = kPropertiesSevenSegmentLamps9_X;
        public static readonly int kPropertiesSevenSegmentLamps11_Y = kPropertiesSevenSegmentLamps3_Y;

        public static readonly int kPropertiesSevenSegmentLamps12_X = kPropertiesSevenSegmentLamps9_X;
        public static readonly int kPropertiesSevenSegmentLamps12_Y = kPropertiesSevenSegmentLamps4_Y;

        public static readonly int kPropertiesSevenSegmentLamps13_X = kPropertiesSevenSegmentLamps9_X;
        public static readonly int kPropertiesSevenSegmentLamps13_Y = kPropertiesSevenSegmentLamps5_Y;

        public static readonly int kPropertiesSevenSegmentLamps14_X = kPropertiesSevenSegmentLamps9_X;
        public static readonly int kPropertiesSevenSegmentLamps14_Y = kPropertiesSevenSegmentLamps6_Y;

        public static readonly int kPropertiesSevenSegmentLamps15_X = kPropertiesSevenSegmentLamps9_X;
        public static readonly int kPropertiesSevenSegmentLamps15_Y = kPropertiesSevenSegmentLamps7_Y;

        public static readonly int kPropertiesSevenSegmentLamps16_X = kPropertiesSevenSegmentLamps9_X;
        public static readonly int kPropertiesSevenSegmentLamps16_Y = kPropertiesSevenSegmentLamps8_Y;


        // seven segment block fields:
        public static readonly int kPropertiesSevenSegmentBlockWidth_X = 14;
        public static readonly int kPropertiesSevenSegmentBlockWidth_Y = 75;

        public static readonly int kPropertiesSevenSegmentBlockHeight_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockHeight_Y = 100;

        public static readonly int kPropertiesSevenSegmentBlockColumns_X = 116;
        public static readonly int kPropertiesSevenSegmentBlockColumns_Y = 75;

        public static readonly int kPropertiesSevenSegmentBlockRows_X = kPropertiesSevenSegmentBlockColumns_X;
        public static readonly int kPropertiesSevenSegmentBlockRows_Y = 99;

        public static readonly int kPropertiesSevenSegmentBlockRowSpacing_X = kPropertiesSevenSegmentBlockColumns_X;
        public static readonly int kPropertiesSevenSegmentBlockRowSpacing_Y = 125;

        public static readonly int kPropertiesSevenSegmentBlockColumnSpacing_X = kPropertiesSevenSegmentBlockColumns_X;
        public static readonly int kPropertiesSevenSegmentBlockColumnSpacing_Y = 150;

        public static readonly int kPropertiesSevenSegmentBlockOnColourColorbox_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockOnColourColorbox_Y = 208;

        public static readonly int kPropertiesSevenSegmentBlockOffColourColorbox_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockOffColourColorbox_Y = 233;

        public static readonly int kPropertiesSevenSegmentBlockBackColourColorbox_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockBackColourColorbox_Y = 258;

        public static readonly int kPropertiesSevenSegmentBlockTypeDropdown_X = 149;
        public static readonly int kPropertiesSevenSegmentBlockTypeDropdown_Y = 216;

        public static readonly int kPropertiesSevenSegmentBlockDPRightCheckbox_X = kPropertiesSevenSegmentBlockTypeDropdown_X;
        public static readonly int kPropertiesSevenSegmentBlockDPRightCheckbox_Y = 245;

        public static readonly int kPropertiesSevenSegmentBlock14SegmentCheckbox_X = kPropertiesSevenSegmentBlockTypeDropdown_X;
        public static readonly int kPropertiesSevenSegmentBlock14SegmentCheckbox_Y = 263;

        public static readonly int kPropertiesSevenSegmentBlockThickness_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockThickness_Y = 291;

        public static readonly int kPropertiesSevenSegmentBlockSpacing_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockSpacing_Y = 316;

        public static readonly int kPropertiesSevenSegmentBlockHorzSizePercent_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockHorzSizePercent_Y = 341;

        public static readonly int kPropertiesSevenSegmentBlockVertSizePercent_X = kPropertiesSevenSegmentBlockWidth_X;
        public static readonly int kPropertiesSevenSegmentBlockVertSizePercent_Y = 366;

        public static readonly int kPropertiesSevenSegmentBlockOffset_X = 149;
        public static readonly int kPropertiesSevenSegmentBlockOffset_Y = 291;

        public static readonly int kPropertiesSevenSegmentBlockAngle_X = kPropertiesSevenSegmentBlockOffset_X;
        public static readonly int kPropertiesSevenSegmentBlockAngle_Y = 316;

        public static readonly int kPropertiesSevenSegmentBlockSlant_X = kPropertiesSevenSegmentBlockOffset_X;
        public static readonly int kPropertiesSevenSegmentBlockSlant_Y = 343;

        public static readonly int kPropertiesSevenSegmentBlockChop_X = kPropertiesSevenSegmentBlockOffset_X;
        public static readonly int kPropertiesSevenSegmentBlockChop_Y = 366;

        public static readonly int kPropertiesSevenSegmentBlockCenter_X = kPropertiesSevenSegmentBlockOffset_X;
        public static readonly int kPropertiesSevenSegmentBlockCenter_Y = 391;

        // seven segment block - per digit fields
        public static readonly int kPropertiesSevenSegmentBlockDigitNumber_X = 266;
        public static readonly int kPropertiesSevenSegmentBlockDigitNumber_Y = 124;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X = 264;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_Y = 149;

        public static readonly int kPropertiesSevenSegmentBlockDigitVisibleCheckbox_X = kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitVisibleCheckbox_Y = 166;

        public static readonly int kPropertiesSevenSegmentBlockDigitDPOnCheckbox_X = kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitDPOnCheckbox_Y = 184;

        public static readonly int kPropertiesSevenSegmentBlockDigitDPOffCheckbox_X = kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitDPOffCheckbox_Y = 202;

        public static readonly int kPropertiesSevenSegmentBlockDigitAutoDPCheckbox_X = kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitAutoDPCheckbox_Y = 220;

        public static readonly int kPropertiesSevenSegmentBlockDigitZeroOnCheckbox_X = kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitZeroOnCheckbox_Y = 238;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X = 378;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_Y = 126;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment2Lamp_X = kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment2Lamp_Y = 149;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment3Lamp_X = kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment3Lamp_Y = 172;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment4Lamp_X = kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment4Lamp_Y = 195;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment5Lamp_X = kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment5Lamp_Y = 218;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment6Lamp_X = kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment6Lamp_Y = 241;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment7Lamp_X = kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment7Lamp_Y = 264;

        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment8Lamp_X = kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X;
        public static readonly int kPropertiesSevenSegmentBlockDigitProgrammableSegment8Lamp_Y = 287;

        // seven segment block - digit tab/navigation controls
        public static readonly int kPropertiesSevenSegmentBlockDigit1TabCenter_X = 274;
        public static readonly int kPropertiesSevenSegmentBlockDigit1TabCenter_Y = 75;

        public static readonly int kPropertiesSevenSegmentBlockDigit2TabCenter_X = 316;
        public static readonly int kPropertiesSevenSegmentBlockDigit2TabCenter_Y = kPropertiesSevenSegmentBlockDigit1TabCenter_Y;

        public static readonly int kPropertiesSevenSegmentBlockDigit3TabCenter_X = 358;
        public static readonly int kPropertiesSevenSegmentBlockDigit3TabCenter_Y = kPropertiesSevenSegmentBlockDigit1TabCenter_Y;

        public static readonly int kPropertiesSevenSegmentBlockDigit4TabCenter_X = 400;
        public static readonly int kPropertiesSevenSegmentBlockDigit4TabCenter_Y = kPropertiesSevenSegmentBlockDigit1TabCenter_Y;

        public static readonly int kPropertiesSevenSegmentBlockDigitTabLeftArrowCenter_X = 434;
        public static readonly int kPropertiesSevenSegmentBlockDigitTabLeftArrowCenter_Y = 73;

        public static readonly int kPropertiesSevenSegmentBlockDigitTabRightArrowCenter_X = 450;
        public static readonly int kPropertiesSevenSegmentBlockDigitTabRightArrowCenter_Y = kPropertiesSevenSegmentBlockDigitTabLeftArrowCenter_Y;

        // border fields
        public static readonly int kPropertiesBorderBorderWidth_X = 15;
        public static readonly int kPropertiesBorderBorderWidth_Y = 236;

        public static readonly int kPropertiesBorderSpacing_X = kPropertiesBorderBorderWidth_X;
        public static readonly int kPropertiesBorderSpacing_Y = 262;

        public static readonly int kPropertiesBorderOuterColorColorbox_X = kPropertiesBorderBorderWidth_X;
        public static readonly int kPropertiesBorderOuterColorColorbox_Y = 289;

        public static readonly int kPropertiesBorderInnerColorColorbox_X = kPropertiesBorderBorderWidth_X;
        public static readonly int kPropertiesBorderInnerColorColorbox_Y = 315;

        public static readonly int kPropertiesBorderOuterCheckbox_X = kPropertiesBorderBorderWidth_X;
        public static readonly int kPropertiesBorderOuterCheckbox_Y = 344;

        public static readonly int kPropertiesBorderInnerCheckbox_X = kPropertiesBorderBorderWidth_X;
        public static readonly int kPropertiesBorderInnerCheckbox_Y = 364;

        // checkbox fields
        public static readonly int kPropertiesCheckboxNumber_X = 17;
        public static readonly int kPropertiesCheckboxNumber_Y = 342;

        public static readonly int kPropertiesCheckboxTextColourbox_X = 17;
        public static readonly int kPropertiesCheckboxTextColourbox_Y = 372;

        // RGB Led fields
        public static readonly int kPropertiesRGBLedNumber_X = 29;
        public static readonly int kPropertiesRGBLedNumber_Y = 147;

        public static readonly int kPropertiesRGBLedRedLedNumber_X = 29;
        public static readonly int kPropertiesRGBLedRedLedNumber_Y = 182;

        public static readonly int kPropertiesRGBLedGreenLedNumber_X = 29;
        public static readonly int kPropertiesRGBLedGreenLedNumber_Y = 209;

        public static readonly int kPropertiesRGBLedBlueLedNumber_X = 29;
        public static readonly int kPropertiesRGBLedBlueLedNumber_Y = 236;

        public static readonly int kPropertiesRGBLedWhiteLedNumber_X = 29;
        public static readonly int kPropertiesRGBLedWhiteLedNumber_Y = 263;

        public static readonly int kPropertiesRGBLedMuxLEDCheckbox_X = 29;
        public static readonly int kPropertiesRGBLedMuxLEDCheckbox_Y = 290;

        public static readonly int kPropertiesRGBLedNoOutlineCheckbox_X = 29;
        public static readonly int kPropertiesRGBLedNoOutlineCheckbox_Y = 309;

        public static readonly int kPropertiesRGBLedNoShadowCheckbox_X = 29;
        public static readonly int kPropertiesRGBLedNoShadowCheckbox_Y = 328;

        public static readonly int kPropertiesRGBLedStyleDropdown_X = 29;
        public static readonly int kPropertiesRGBLedStyleDropdown_Y = 350;

        public static readonly int kPropertiesRGBLedAdjustedOffColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedOffColourbox_Y = 159;

        public static readonly int kPropertiesRGBLedAdjustedRedColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedRedColourbox_Y = 186;

        public static readonly int kPropertiesRGBLedAdjustedGreenColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedGreenColourbox_Y = 213;

        public static readonly int kPropertiesRGBLedAdjustedRedGreenColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedRedGreenColourbox_Y = 240;

        public static readonly int kPropertiesRGBLedAdjustedBlueColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedBlueColourbox_Y = 267;

        public static readonly int kPropertiesRGBLedAdjustedRedBlueColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedRedBlueColourbox_Y = 294;

        public static readonly int kPropertiesRGBLedAdjustedGreenBlueColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedGreenBlueColourbox_Y = 321;

        public static readonly int kPropertiesRGBLedAdjustedRedGreenBlueColourbox_X = 223;
        public static readonly int kPropertiesRGBLedAdjustedRedGreenBlueColourbox_Y = 348;

        // Led fields
        public static readonly int kPropertiesLedNumber_X = 15;
        public static readonly int kPropertiesLedNumber_Y = 152;

        public static readonly int kPropertiesLedLedCheckbox_X = 16;
        public static readonly int kPropertiesLedLedCheckbox_Y = 181;

        public static readonly int kPropertiesLedDigit_X = 16;
        public static readonly int kPropertiesLedDigit_Y = 210;

        public static readonly int kPropertiesLedSegmentDropdown_X = 13;
        public static readonly int kPropertiesLedSegmentDropdown_Y = 235;

        public static readonly int kPropertiesLedOnColourbox_X = 15;
        public static readonly int kPropertiesLedOnColourbox_Y = 305;

        public static readonly int kPropertiesLedOffColourbox_X = 15;
        public static readonly int kPropertiesLedOffColourbox_Y = 330;

        public static readonly int kPropertiesLedNoOutlineCheckbox_X = 16;
        public static readonly int kPropertiesLedNoOutlineCheckbox_Y = 355;

        public static readonly int kPropertiesLedNoShadowCheckbox_X = 16;
        public static readonly int kPropertiesLedNoShadowCheckbox_Y = 373;

        public static readonly int kPropertiesLedStyleDropdown_X = 16;
        public static readonly int kPropertiesLedStyleDropdown_Y = 394;

        // Frame fields
        public static readonly int kPropertiesFrameShapeDropdown_X = 20;
        public static readonly int kPropertiesFrameShapeDropdown_Y = 270;

        public static readonly int kPropertiesFrameBevelDropdown_X = 20;
        public static readonly int kPropertiesFrameBevelDropdown_Y = 295;

        // Label
        public static readonly int kPropertiesLabelLampNumber_X = 15;
        public static readonly int kPropertiesLabelLampNumber_Y = 270;

        public static readonly int kPropertiesLabelTransparentCheckbox_X = 15;
        public static readonly int kPropertiesLabelTransparentCheckbox_Y = 294;

        public static readonly int kPropertiesLabelTextColourbox_X = 15;
        public static readonly int kPropertiesLabelTextColourbox_Y = 313;

        public static readonly int kPropertiesLabelBackgroundColourbox_X = 15;
        public static readonly int kPropertiesLabelBackgroundColourbox_Y = 340;

        // Button
        public static readonly int kPropertiesButtonLampElement0LampNumber_X = 20;
        public static readonly int kPropertiesButtonLampElement0LampNumber_Y = 81;

        public static readonly int kPropertiesButtonLampElement0Color_X = 20;
        public static readonly int kPropertiesButtonLampElement0Color_Y = 111;

        public static readonly int kPropertiesButtonLampElement0OnImage_X = 84;
        public static readonly int kPropertiesButtonLampElement0OnImage_Y = 72;

        public static readonly int kPropertiesButtonLampElement0MaskImage_X = 84;
        public static readonly int kPropertiesButtonLampElement0MaskImage_Y = 142;

        public static readonly int kPropertiesButtonLampElement1LampNumber_X = 173;
        public static readonly int kPropertiesButtonLampElement1LampNumber_Y = 81;

        public static readonly int kPropertiesButtonLampElement1Color_X = 173;
        public static readonly int kPropertiesButtonLampElement1Color_Y = 111;

        public static readonly int kPropertiesButtonLampElement1OnImage_X = 237;
        public static readonly int kPropertiesButtonLampElement1OnImage_Y = 72;

        public static readonly int kPropertiesButtonLampElement1MaskImage_X = 237;
        public static readonly int kPropertiesButtonLampElement1MaskImage_Y = 142;

        public static readonly int kPropertiesButtonButtonNumber_X = 18;
        public static readonly int kPropertiesButtonButtonNumber_Y = 231;

        public static readonly int kPropertiesButtonCoinNote_X = 18;
        public static readonly int kPropertiesButtonCoinNote_Y = 258;

        public static readonly int kPropertiesButtonEffect_X = 18;
        public static readonly int kPropertiesButtonEffect_Y = 282;

        public static readonly int kPropertiesButtonInhibitLamp_X = 18;
        public static readonly int kPropertiesButtonInhibitLamp_Y = 309;

        public static readonly int kPropertiesButtonShortcut1_X = 18;
        public static readonly int kPropertiesButtonShortcut1_Y = 350;

        public static readonly int kPropertiesButtonShortcut2_X = 65;
        public static readonly int kPropertiesButtonShortcut2_Y = 350;

        public static readonly int kPropertiesButtonGraphicCheckbox_X = 20;
        public static readonly int kPropertiesButtonGraphicCheckbox_Y = 380;

        public static readonly int kPropertiesButtonInvertedCheckbox_X = 85;
        public static readonly int kPropertiesButtonInvertedCheckbox_Y = 380;

        public static readonly int kPropertiesButtonSplitCheckbox_X = 20;
        public static readonly int kPropertiesButtonSplitCheckbox_Y = 400;

        public static readonly int kPropertiesButtonLockOutCheckbox_X = 85;
        public static readonly int kPropertiesButtonLockOutCheckbox_Y = 400;

        public static readonly int kPropertiesButtonLEDCheckbox_X = 20;
        public static readonly int kPropertiesButtonLEDCheckbox_Y = 420;

        public static readonly int kPropertiesButtonXOff_X = 154;
        public static readonly int kPropertiesButtonXOff_Y = 377;

        public static readonly int kPropertiesButtonYOff_X = 154;
        public static readonly int kPropertiesButtonYOff_Y = 404;

        public static readonly int kPropertiesButtonTextColourbox_X = 20;
        public static readonly int kPropertiesButtonTextColourbox_Y = 443;

        public static readonly int kPropertiesButtonShapeDropdown_X = 20;
        public static readonly int kPropertiesButtonShapeDropdown_Y = 471;

        public static readonly int kPropertiesButtonOffImageColourbox_X = 325;
        public static readonly int kPropertiesButtonOffImageColourbox_Y = 111;


        // Z Order: pixel area
        public static readonly int kPropertiesZOrder_X = 577;
        public static readonly int kPropertiesZOrder_Y = 615;
        public static readonly int kPropertiesZOrder_Width = 28;
        public static readonly int kPropertiesZOrder_Height = 9;

        public static readonly int kDataLayoutEmptyArea_X = 600;
        public static readonly int kDataLayoutEmptyArea_Y = 80;

        public static Vector2Int GetPropertiesReelLampNumber_XY(int reelLampNumber)
        {
            Vector2Int position = new Vector2Int();
            switch (reelLampNumber)
            {
                case 0:
                    position.x = kPropertiesReelLamp1Number_X;
                    position.y = kPropertiesReelLamp1Number_Y;
                    break;
                case 1:
                    position.x = kPropertiesReelLamp2Number_X;
                    position.y = kPropertiesReelLamp2Number_Y;
                    break;
                case 2:
                    position.x = kPropertiesReelLamp3Number_X;
                    position.y = kPropertiesReelLamp3Number_Y;
                    break;
                case 3:
                    position.x = kPropertiesReelLamp4Number_X;
                    position.y = kPropertiesReelLamp4Number_Y;
                    break;
                case 4:
                    position.x = kPropertiesReelLamp5Number_X;
                    position.y = kPropertiesReelLamp5Number_Y;
                    break;
                case 5:
                    position.x = kPropertiesReelLamp6Number_X;
                    position.y = kPropertiesReelLamp6Number_Y;
                    break;
                case 6:
                    position.x = kPropertiesReelLamp7Number_X;
                    position.y = kPropertiesReelLamp7Number_Y;
                    break;
                case 7:
                    position.x = kPropertiesReelLamp8Number_X;
                    position.y = kPropertiesReelLamp8Number_Y;
                    break;
                case 8:
                    position.x = kPropertiesReelLamp9Number_X;
                    position.y = kPropertiesReelLamp9Number_Y;
                    break;
                case 9:
                    position.x = kPropertiesReelLamp10Number_X;
                    position.y = kPropertiesReelLamp10Number_Y;
                    break;
                case 10:
                    position.x = kPropertiesReelLamp11Number_X;
                    position.y = kPropertiesReelLamp11Number_Y;
                    break;
                case 11:
                    position.x = kPropertiesReelLamp12Number_X;
                    position.y = kPropertiesReelLamp12Number_Y;
                    break;
                case 12:
                    position.x = kPropertiesReelLamp13Number_X;
                    position.y = kPropertiesReelLamp13Number_Y;
                    break;
                case 13:
                    position.x = kPropertiesReelLamp14Number_X;
                    position.y = kPropertiesReelLamp14Number_Y;
                    break;
                case 14:
                    position.x = kPropertiesReelLamp15Number_X;
                    position.y = kPropertiesReelLamp15Number_Y;
                    break;
            }

            return position;
        }


        // Configuration pages

        // MPU4
        public static readonly int kConfigurationMPU4MeterInType1_DropdownX = 17;
        public static readonly int kConfigurationMPU4MeterInType1_DropdownY = 265;

        public static readonly int kConfigurationMPU4MeterInType2_DropdownX = kConfigurationMPU4MeterInType1_DropdownX;
        public static readonly int kConfigurationMPU4MeterInType2_DropdownY = 289;

        public static readonly int kConfigurationMPU4MeterInType3_DropdownX = kConfigurationMPU4MeterInType1_DropdownX;
        public static readonly int kConfigurationMPU4MeterInType3_DropdownY = 313;

        public static readonly int kConfigurationMPU4MeterInMultiplier1_DropdownX = 113;
        public static readonly int kConfigurationMPU4MeterInMultiplier1_DropdownY = kConfigurationMPU4MeterInType1_DropdownY;

        public static readonly int kConfigurationMPU4MeterInMultiplier2_DropdownX = kConfigurationMPU4MeterInMultiplier1_DropdownX;
        public static readonly int kConfigurationMPU4MeterInMultiplier2_DropdownY = kConfigurationMPU4MeterInType2_DropdownY;

        public static readonly int kConfigurationMPU4MeterInMultiplier3_DropdownX = kConfigurationMPU4MeterInMultiplier1_DropdownX;
        public static readonly int kConfigurationMPU4MeterInMultiplier3_DropdownY = kConfigurationMPU4MeterInType3_DropdownY;

        public static readonly int kConfigurationMPU4MeterOutType1_DropdownX = 169;
        public static readonly int kConfigurationMPU4MeterOutType1_DropdownY = kConfigurationMPU4MeterInType1_DropdownY;

        public static readonly int kConfigurationMPU4MeterOutType2_DropdownX = kConfigurationMPU4MeterOutType1_DropdownX;
        public static readonly int kConfigurationMPU4MeterOutType2_DropdownY = kConfigurationMPU4MeterInType2_DropdownY;

        public static readonly int kConfigurationMPU4MeterOutType3_DropdownX = kConfigurationMPU4MeterOutType1_DropdownX;
        public static readonly int kConfigurationMPU4MeterOutType3_DropdownY = kConfigurationMPU4MeterInType3_DropdownY;

        public static readonly int kConfigurationMPU4MeterOutMultiplier1_DropdownX = 265;
        public static readonly int kConfigurationMPU4MeterOutMultiplier1_DropdownY = kConfigurationMPU4MeterInType1_DropdownY;

        public static readonly int kConfigurationMPU4MeterOutMultiplier2_DropdownX = kConfigurationMPU4MeterOutMultiplier1_DropdownX;
        public static readonly int kConfigurationMPU4MeterOutMultiplier2_DropdownY = kConfigurationMPU4MeterInType2_DropdownY;

        public static readonly int kConfigurationMPU4MeterOutMultiplier3_DropdownX = kConfigurationMPU4MeterOutMultiplier1_DropdownX;
        public static readonly int kConfigurationMPU4MeterOutMultiplier3_DropdownY = kConfigurationMPU4MeterInType3_DropdownY;

        public static readonly int kConfigurationMPU4Stake_DropdownX = 15;
        public static readonly int kConfigurationMPU4Stake_DropdownY = 364;

        public static readonly int kConfigurationMPU4Prize_DropdownX = 63;
        public static readonly int kConfigurationMPU4Prize_DropdownY = kConfigurationMPU4Stake_DropdownY;

        public static readonly int kConfigurationMPU4Percentage_DropdownX = 130;
        public static readonly int kConfigurationMPU4Percentage_DropdownY = kConfigurationMPU4Stake_DropdownY;

        public static readonly int kConfigurationMPU4VolumeControlAuto_RadioButtonTopLeftX = 203;
        public static readonly int kConfigurationMPU4VolumeControlAuto_RadioButtonTopLeftY = 356;

        public static readonly int kConfigurationMPU4VolumeControlManual_RadioButtonTopLeftX = kConfigurationMPU4VolumeControlAuto_RadioButtonTopLeftX;
        public static readonly int kConfigurationMPU4VolumeControlManual_RadioButtonTopLeftY = 372;

        public static readonly int kConfigurationMPU4ROMPaging_DropdownX = 16;
        public static readonly int kConfigurationMPU4ROMPaging_DropdownY = 436;

        public static readonly int kConfigurationMPU4LVDNo_RadioButtonTopLeftX = 128;
        public static readonly int kConfigurationMPU4LVDNo_RadioButtonTopLeftY = 433;

        public static readonly int kConfigurationMPU4LVDYes_RadioButtonTopLeftX = kConfigurationMPU4LVDNo_RadioButtonTopLeftX;
        public static readonly int kConfigurationMPU4LVDYes_RadioButtonTopLeftY = 448;

        public static readonly int kConfigurationMPU4DisplayReel_RadioButtonTopLeftX = 190;
        public static readonly int kConfigurationMPU4DisplayReel_RadioButtonTopLeftY = 433;

        public static readonly int kConfigurationMPU4DisplayVideo_RadioButtonTopLeftX = kConfigurationMPU4DisplayReel_RadioButtonTopLeftX;
        public static readonly int kConfigurationMPU4DisplayVideo_RadioButtonTopLeftY = 448;

        public static readonly int kConfigurationMPU4LampTestPass_RadioButtonTopLeftX = 260;
        public static readonly int kConfigurationMPU4LampTestPass_RadioButtonTopLeftY = 435;

        public static readonly int kConfigurationMPU4LampTestFail_RadioButtonTopLeftX = kConfigurationMPU4LampTestPass_RadioButtonTopLeftX;
        public static readonly int kConfigurationMPU4LampTestFail_RadioButtonTopLeftY = 454;

        public static readonly int kConfigurationMPU4Payout_DropdownX = 323;
        public static readonly int kConfigurationMPU4Payout_DropdownY = 72;

        public static readonly int kConfigurationMPU4ExtenderAux1_DropdownX = kConfigurationMPU4Payout_DropdownX;
        public static readonly int kConfigurationMPU4ExtenderAux1_DropdownY = 123;

        public static readonly int kConfigurationMPU4SevenSegDisplay_DropdownX = kConfigurationMPU4Payout_DropdownX;
        public static readonly int kConfigurationMPU4SevenSegDisplay_DropdownY = 174;

        public static readonly int kConfigurationMPU4Reels_DropdownX = kConfigurationMPU4Payout_DropdownX;
        public static readonly int kConfigurationMPU4Reels_DropdownY = 225;

        public static readonly int kConfigurationMPU4Sound_DropdownX = kConfigurationMPU4Payout_DropdownX;
        public static readonly int kConfigurationMPU4Sound_DropdownY = 276;

        public static readonly int kConfigurationMPU4Encryption_DropdownX = kConfigurationMPU4Payout_DropdownX;
        public static readonly int kConfigurationMPU4Encryption_DropdownY = 327;

        public static readonly int kConfigurationMPU4Character_DropdownX = kConfigurationMPU4Payout_DropdownX;
        public static readonly int kConfigurationMPU4Character_DropdownY = 378;

        public static readonly int kConfigurationMPU4DataPak_DropdownX = kConfigurationMPU4Payout_DropdownX;
        public static readonly int kConfigurationMPU4DataPak_DropdownY = 429;

        public static readonly int kConfigurationMPU4SwitchService_X = 435;
        public static readonly int kConfigurationMPU4SwitchService_Y = 74;

        public static readonly int kConfigurationMPU4SwitchCash_X = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4SwitchCash_Y = 97;

        public static readonly int kConfigurationMPU4SwitchRefill_X = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4SwitchRefill_Y = 120;

        public static readonly int kConfigurationMPU4SwitchTest_X = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4SwitchTest_Y = 143;

        public static readonly int kConfigurationMPU4SwitchTopUp_X = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4SwitchTopUp_Y = 166;

        public static readonly int kConfigurationMPU4Aux1Invert_CheckboxX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4Aux1Invert_CheckboxY = 218;

        public static readonly int kConfigurationMPU4Aux2Invert_CheckboxX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4Aux2Invert_CheckboxY = 234;

        public static readonly int kConfigurationMPU4DoorInvert_CheckboxX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4DoorInvert_CheckboxY = 250;

        public static readonly int kConfigurationMPU4AlphaCableNormal_RadioButtonTopLeftX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4AlphaCableNormal_RadioButtonTopLeftY = 292;

        public static readonly int kConfigurationMPU4AlphaCableCR_RadioButtonTopLeftX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4AlphaCableCR_RadioButtonTopLeftY = 310;

        public static readonly int kConfigurationMPU4ModType2_RadioButtonTopLeftX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4ModType2_RadioButtonTopLeftY = 350;

        public static readonly int kConfigurationMPU4ModType4_RadioButtonTopLeftX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4ModType4_RadioButtonTopLeftY = 365;

        public static readonly int kConfigurationMPU4CabinetStyleDefault_RadioButtonTopLeftX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4CabinetStyleDefault_RadioButtonTopLeftY = 403;

        public static readonly int kConfigurationMPU4CabinetStyleRio_RadioButtonTopLeftX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4CabinetStyleRio_RadioButtonTopLeftY = 418;

        public static readonly int kConfigurationMPU4CabinetStyleGenesis_RadioButtonTopLeftX = kConfigurationMPU4SwitchService_X;
        public static readonly int kConfigurationMPU4CabinetStyleGenesis_RadioButtonTopLeftY = 433;

        // Scorpion 1
        public static readonly int kConfigurationScorpion1MeterInType1_DropdownX = 17;
        public static readonly int kConfigurationScorpion1MeterInType1_DropdownY = 265;

        public static readonly int kConfigurationScorpion1MeterInType2_DropdownX = kConfigurationScorpion1MeterInType1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterInType2_DropdownY = 289;

        public static readonly int kConfigurationScorpion1MeterInType3_DropdownX = kConfigurationScorpion1MeterInType1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterInType3_DropdownY = 313;

        public static readonly int kConfigurationScorpion1MeterInMultiplier1_DropdownX = 113;
        public static readonly int kConfigurationScorpion1MeterInMultiplier1_DropdownY = kConfigurationScorpion1MeterInType1_DropdownY;

        public static readonly int kConfigurationScorpion1MeterInMultiplier2_DropdownX = kConfigurationScorpion1MeterInMultiplier1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterInMultiplier2_DropdownY = kConfigurationScorpion1MeterInType2_DropdownY;

        public static readonly int kConfigurationScorpion1MeterInMultiplier3_DropdownX = kConfigurationScorpion1MeterInMultiplier1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterInMultiplier3_DropdownY = kConfigurationScorpion1MeterInType3_DropdownY;

        public static readonly int kConfigurationScorpion1MeterOutType1_DropdownX = 169;
        public static readonly int kConfigurationScorpion1MeterOutType1_DropdownY = kConfigurationScorpion1MeterInType1_DropdownY;

        public static readonly int kConfigurationScorpion1MeterOutType2_DropdownX = kConfigurationScorpion1MeterOutType1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterOutType2_DropdownY = kConfigurationScorpion1MeterInType2_DropdownY;

        public static readonly int kConfigurationScorpion1MeterOutType3_DropdownX = kConfigurationScorpion1MeterOutType1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterOutType3_DropdownY = kConfigurationScorpion1MeterInType3_DropdownY;

        public static readonly int kConfigurationScorpion1MeterOutMultiplier1_DropdownX = 265;
        public static readonly int kConfigurationScorpion1MeterOutMultiplier1_DropdownY = kConfigurationScorpion1MeterInType1_DropdownY;

        public static readonly int kConfigurationScorpion1MeterOutMultiplier2_DropdownX = kConfigurationScorpion1MeterOutMultiplier1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterOutMultiplier2_DropdownY = kConfigurationScorpion1MeterInType2_DropdownY;

        public static readonly int kConfigurationScorpion1MeterOutMultiplier3_DropdownX = kConfigurationScorpion1MeterOutMultiplier1_DropdownX;
        public static readonly int kConfigurationScorpion1MeterOutMultiplier3_DropdownY = kConfigurationScorpion1MeterInType3_DropdownY;

        public static readonly int kConfigurationScorpion1Stake_DropdownX = 15;
        public static readonly int kConfigurationScorpion1Stake_DropdownY = 364;

        public static readonly int kConfigurationScorpion1Prize_DropdownX = 63;
        public static readonly int kConfigurationScorpion1Prize_DropdownY = kConfigurationScorpion1Stake_DropdownY;

        public static readonly int kConfigurationScorpion1Percentage_DropdownX = 130;
        public static readonly int kConfigurationScorpion1Percentage_DropdownY = kConfigurationScorpion1Stake_DropdownY;

        public static readonly int kConfigurationScorpion1Encryption_DropdownX = 203;
        public static readonly int kConfigurationScorpion1Encryption_DropdownY = 259;

        public static readonly int kConfigurationScorpion1SwitchService_X = 324;
        public static readonly int kConfigurationScorpion1SwitchService_Y = 74;

        public static readonly int kConfigurationScorpion1SwitchCash_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchCash_Y = 97;

        public static readonly int kConfigurationScorpion1SwitchRefill_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchRefill_Y = 120;

        public static readonly int kConfigurationScorpion1SwitchTest_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchTest_Y = 143;

        public static readonly int kConfigurationScorpion1SwitchPaysense1_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchPaysense1_Y = 166;

        public static readonly int kConfigurationScorpion1SwitchPaysense2_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchPaysense2_Y = 189;

        public static readonly int kConfigurationScorpion1SwitchPaysense3_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchPaysense3_Y = 212;

        public static readonly int kConfigurationScorpion1SwitchPaysense4_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchPaysense4_Y = 235;

        public static readonly int kConfigurationScorpion1SwitchDMBusy_X = kConfigurationScorpion1SwitchService_X;
        public static readonly int kConfigurationScorpion1SwitchDMBusy_Y = 258;

        public static readonly int kConfigurationScorpion1DataPak_DropdownX = 323;
        public static readonly int kConfigurationScorpion1DataPak_DropdownY = 309;

        public static readonly int kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftX = 314;
        public static readonly int kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftY = 356;

        public static readonly int kConfigurationScorpion1SampledSoundOKI_RadioButtonTopLeftX = kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftX;
        public static readonly int kConfigurationScorpion1SampledSoundOKI_RadioButtonTopLeftY = 372;

        public static readonly int kConfigurationScorpion1SampledSoundGlobal_RadioButtonTopLeftX = kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftX;
        public static readonly int kConfigurationScorpion1SampledSoundGlobal_RadioButtonTopLeftY = 388;



    }
}

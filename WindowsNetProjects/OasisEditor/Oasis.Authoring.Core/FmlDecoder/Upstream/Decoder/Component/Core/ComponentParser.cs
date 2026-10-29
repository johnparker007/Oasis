using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Helper;
using MfmeFmlDecoder.src.Decoder.Component.Behaviours;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    internal class ComponentParser
    {
        private readonly BackgroundParser backgroundParser = new();
        private readonly ReelParser reelParser = new();
        private readonly LampParser lampParser = new();
        private readonly ButtonParser buttonParser = new();
        private readonly DiscReelParser discReelParser = new();
        private readonly AlphaParser alphaParser = new();
        private readonly BandReelParser bandReelParser = new();
        private readonly FrameParser frameParser = new();
        private readonly LabelParser labelParser = new();
        private readonly BitmapParser bitmapParser = new();
        private readonly BFMAlphaParser bfmAlphaParser = new();
        private readonly DotMatrixParser dotMatrixParser = new();
        private readonly SevenSegParser sevenSegParser = new();
        private readonly BarcrestBWBVideoParser barcrestBWBVideoParser = new();
        private readonly AceMatrixParser aceMatrixParser = new();
        private readonly ProconnMatrixParser proconnMatrixParser = new();
        private readonly LedParser ledParser = new();
        private readonly DotAlphaParser dotAlphaParser = new();
        private readonly CheckboxParser checkboxParser = new();
        private readonly BFMLEDParser bfmLedParser = new();
        private readonly EpochDotAlphaParser epochDotAlphaParser = new();
        private readonly AlphaNewParser alphaNewParser = new();
        private readonly MatrixAlphaParser matrixAlphaParser = new();
        private readonly BorderParser borderParser = new();
        private readonly SevenSegBlockParser sevenSegBlockParser = new();
        private readonly BFMVideoParser bfmVideoParser = new();
        private readonly IGTVfdParser igtVfdParser = new();
        private readonly AceVideoParser aceVideoParser = new();
        private readonly EpochMatrixParser epochMatrixParser = new();
        private readonly PlasmaDisplayParser plasmaDisplayParser = new();
        private readonly BFMColourLedParser bfmColourLedParser = new();
        private readonly RGBLedParser rgbLedParser = new();
        private readonly ReelBonusReelParser reelBonusReelParser = new();
        private readonly MaygayVideoParser maygayVideoParser = new();
        private readonly PrismLampParser prismLampParser = new();
        private readonly AstraMatrixParser astraMatrixParser = new();
        private readonly FlipReelParser flipReelParser = new();
        private readonly MaygayMatrixParser maygayMatrixParser = new();
        private readonly CoinmasterVideoParser coinmasterVideoParser = new();

        private readonly List<BaseComponent> components = new();
        private ComponentOrderingMode _ordering = ComponentOrderingMode.InFileOrder;


        public void ParseComponent(ParsingContext context, long componentOffset, uint componentId, uint length, byte[] data)
        {
            _ordering = context.Ordering;
            try
            {
                ParseComponentCore(context, componentOffset, componentId, length, data);
            }
            catch (UnknownComponentTypeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Component parse failed at file offset 0x{componentOffset:X8}, " +
                    $"component ID 0x{componentId:X8} ({componentId}): {ex.Message}",
                    ex);
            }
        }

        private void ParseComponentCore(ParsingContext context, long componentOffset, uint componentId, uint length, byte[] data)
        {
            switch (componentId)
            {
                case (uint)MFMEComponentType.Background:
                    components.Add(backgroundParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Reel:
                    components.Add(reelParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Lamp:
                    components.Add(lampParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Button:
                    components.Add(buttonParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.DiscReel:
                    components.Add(discReelParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Alpha:
                    components.Add(alphaParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.BandReel:
                    components.Add(bandReelParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Frame:
                    components.Add(frameParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Label:
                    components.Add(labelParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Bitmap:
                    components.Add(bitmapParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.BFMAlpha:
                    components.Add(bfmAlphaParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.DotMatrix:
                    components.Add(dotMatrixParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.SevenSeg:
                    components.Add(sevenSegParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.BarcrestBWBVideo:
                    components.Add(barcrestBWBVideoParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.AceMatrix:
                    components.Add(aceMatrixParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.ProconnMatrix:
                    components.Add(proconnMatrixParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Led:
                    components.Add(ledParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.DotAlpha:
                    components.Add(dotAlphaParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Checkbox:
                    components.Add(checkboxParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.BFMLED:
                    components.Add(bfmLedParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.EpochDotAlpha:
                    components.Add(epochDotAlphaParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.AlphaNew:
                    components.Add(alphaNewParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.MatrixAlpha:
                    components.Add(matrixAlphaParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.Border:
                    components.Add(borderParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.SevenSegBlock:
                    components.Add(sevenSegBlockParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.BFMVideo:
                    components.Add(bfmVideoParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.IGTVfd:
                    components.Add(igtVfdParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.AceVideo:
                    components.Add(aceVideoParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.EpochMatrix:
                    components.Add(epochMatrixParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.PlasmaDisplay:
                    components.Add(plasmaDisplayParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.BFMColourLed:
                    components.Add(bfmColourLedParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.RGBLed:
                    components.Add(rgbLedParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.ReelBonusReel:
                    components.Add(reelBonusReelParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.MaygayVideo:
                    components.Add(maygayVideoParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.PrismLamp:
                    components.Add(prismLampParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.AstraMatrix:
                    components.Add(astraMatrixParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.FlipReel:
                    components.Add(flipReelParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.MaygayMatrix:
                    components.Add(maygayMatrixParser.Parse(context, componentOffset, componentId, data));
                    break;
                case (uint)MFMEComponentType.CoinmasterVideo:
                    components.Add(coinmasterVideoParser.Parse(context, componentOffset, componentId, data));
                    break;
                default:
                    throw new UnknownComponentTypeException(componentId, componentOffset, length, data);
            }
        }

        public Layout ToLayout(LayoutFileHeader header = null)
        {
            LegacyBorderOrderBehaviour.Apply(components, _ordering);
            BorderOwnershipAssigner.Annotate(components);
            return new Layout(components, header);
        }
    }
}

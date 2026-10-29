using System.Text.Json.Serialization;

namespace MfmeFmlDecoder.src.Model.Component
{
    /// <summary>
    /// AOT-safe JSON metadata for every concrete component type.
    ///
    /// The serializer cannot discover these properties by reflection in a trimmed
    /// Native AOT build, so the source-generated context supplies the property and
    /// converter metadata up front. Keep this list in sync with the classes that
    /// derive from <see cref="BaseComponent"/>.
    /// </summary>
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(AceMatrix))]
    [JsonSerializable(typeof(AceVideo))]
    [JsonSerializable(typeof(Alpha))]
    [JsonSerializable(typeof(AlphaNew))]
    [JsonSerializable(typeof(AstraMatrix))]
    [JsonSerializable(typeof(BFMAlpha))]
    [JsonSerializable(typeof(BFMColourLed))]
    [JsonSerializable(typeof(BFMLED))]
    [JsonSerializable(typeof(BFMVideo))]
    [JsonSerializable(typeof(Background))]
    [JsonSerializable(typeof(BandReel))]
    [JsonSerializable(typeof(BarcrestBWBVideo))]
    [JsonSerializable(typeof(Bitmap))]
    [JsonSerializable(typeof(Border))]
    [JsonSerializable(typeof(Button))]
    [JsonSerializable(typeof(Checkbox))]
    [JsonSerializable(typeof(CoinmasterVideo))]
    [JsonSerializable(typeof(DiscReel))]
    [JsonSerializable(typeof(DotAlpha))]
    [JsonSerializable(typeof(DotMatrix))]
    [JsonSerializable(typeof(EpochDotAlpha))]
    [JsonSerializable(typeof(EpochMatrix))]
    [JsonSerializable(typeof(FlipReel))]
    [JsonSerializable(typeof(Frame))]
    [JsonSerializable(typeof(IGTVfd))]
    [JsonSerializable(typeof(Label))]
    [JsonSerializable(typeof(Lamp))]
    [JsonSerializable(typeof(Led))]
    [JsonSerializable(typeof(MatrixAlpha))]
    [JsonSerializable(typeof(MaygayMatrix))]
    [JsonSerializable(typeof(MaygayVideo))]
    [JsonSerializable(typeof(PlasmaDisplay))]
    [JsonSerializable(typeof(PrismLamp))]
    [JsonSerializable(typeof(ProconnMatrix))]
    [JsonSerializable(typeof(RGBLed))]
    [JsonSerializable(typeof(Reel))]
    [JsonSerializable(typeof(ReelBonusReel))]
    [JsonSerializable(typeof(SevenSeg))]
    [JsonSerializable(typeof(SevenSegBlock))]
    internal partial class ComponentJsonContext : JsonSerializerContext
    {
    }
}

namespace MfmeFmlDecoder.src.Model.Component
{
    internal sealed record FontTagEntry(
        uint Tag,
        string Role,
        string FontName,
        uint FontSize,
        byte ScriptStyleRaw,
        string ScriptStyleName,
        string TextColour,
        byte FontStyle
    );
}

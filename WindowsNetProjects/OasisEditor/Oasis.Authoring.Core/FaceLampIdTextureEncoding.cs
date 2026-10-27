namespace OasisEditor;

/// <summary>Maps logical Face lamp IDs to the nonzero byte values used by the runtime lamp texture.</summary>
public static class FaceLampIdTextureEncoding
{
    public const int MinimumLogicalId = 0;
    public const int MaximumLogicalId = 254;

    public static bool IsSupportedLogicalId(int logicalLampId)
        => logicalLampId is >= MinimumLogicalId and <= MaximumLogicalId;

    public static byte Encode(int logicalLampId)
    {
        if (!IsSupportedLogicalId(logicalLampId))
        {
            throw new ArgumentOutOfRangeException(nameof(logicalLampId), logicalLampId,
                $"Logical lamp IDs must be from {MinimumLogicalId} to {MaximumLogicalId} for the byte texture format.");
        }

        return checked((byte)(logicalLampId + 1));
    }

    public static int? Decode(byte encodedValue) => encodedValue == 0 ? null : encodedValue - 1;
}

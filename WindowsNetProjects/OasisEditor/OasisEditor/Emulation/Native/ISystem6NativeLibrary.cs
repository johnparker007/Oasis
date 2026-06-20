namespace OasisEditor;

public interface ISystem6NativeLibrary : INativeCoreLibrary
{
    byte Initialise();

    int LoadRom(IReadOnlyList<string> programRomPaths, bool flashSwitch);

    int LoadSoundRom(IReadOnlyList<string> soundRomPaths);

    void SetSteps(byte reelNum, byte steps);

    void SetOptoStart(byte reelNum, byte start);

    void SetOptoEnd(byte reelNum, byte end);

    void SetOptoInvert(byte reelNum, byte state);

    void Reset();

    int Run(int cycles);

    byte Shutdown();

    bool IsLampsUpdateAvailable { get; }

    string? LampsUpdateExportName { get; }

    void LampsUpdate();

    bool GetLampsOn(ushort lampIndex);

    float GetLampBrightness(ushort lampIndex);

    short GetPosOut(sbyte positionIndex);

    bool IsAlphaSegmentPollingAvailable { get; }

    int GetAlphaSegments(byte index);

    bool IsAlphaBrightnessPollingAvailable { get; }

    byte GetAlphaBrightness();

    bool IsSetPercentAvailable { get; }

    void SetPercent(byte percent);

    void TurnSwitchOn(int switchIndex);

    void TurnSwitchOff(int switchIndex);
}

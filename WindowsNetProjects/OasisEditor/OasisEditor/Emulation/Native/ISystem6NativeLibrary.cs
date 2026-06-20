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

    void SetCoinEnable(byte num, byte coin, byte coinEnable);

    void SetCoinValue(byte num, byte coin, byte coinValue);

    void SetLockoutVal(byte num, byte coin, byte lockoutValue);

    void SetLockoutInvert(byte num, byte coin, byte lockoutInvert);

    void SetEnable(byte num, byte enable);

    void SetCounterIn(byte num, byte counterIn);

    void SetCounterOut(byte num, byte counterOut);

    void SetPortIndex(byte num, byte portIndex);

    void SetCoin(byte num, byte coin);

    void SetLevel(byte num, byte level);

    void SetFullLevel(byte num, byte fullLevel);

    void TurnSwitchOn(int switchIndex);

    bool IsSevenSegmentPollingAvailable { get; }

    void UpdateSegs();

    int GetSegsOn(ushort index);

    byte GetSegsBright(ushort index);

    void TurnSwitchOff(int switchIndex);
}

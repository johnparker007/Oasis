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
    uint GetOutputSnapshotSize();
    System6NativeOutputSnapshot GetOutputSnapshot();
    bool IsSetPercentAvailable { get; }
    void SetPercent(byte percent);
    void SetCoinEnable(byte coin, byte coinEnable);
    void SetCoinValue(byte coin, byte coinValue);
    void SetLockoutVal(byte coin, byte lockoutValue);
    void SetLockoutInvert(byte coin, byte lockoutInvert);
    void SetEnable(byte num, byte enable);
    void SetCounterIn(byte num, byte counterIn);
    void SetCounterOut(byte num, byte counterOut);
    void SetPortIndex(byte num, byte portIndex);
    void SetCoin(byte num, byte coin);
    void SetLevel(byte num, byte level);
    void SetFullLevel(byte num, byte fullLevel);
    void TurnSwitchOn(int switchIndex);
    void TurnSwitchOff(int switchIndex);
}

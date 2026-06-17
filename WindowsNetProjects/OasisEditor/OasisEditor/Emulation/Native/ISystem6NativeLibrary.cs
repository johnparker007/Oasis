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

    bool GetLampsOn(ushort lampIndex);

    float GetLampBrightness(ushort lampIndex);

    short GetPosOut(sbyte positionIndex);

    void TurnSwitchOn(int switchIndex);

    void TurnSwitchOff(int switchIndex);
}

namespace OasisEditor;

public interface ISystem6NativeLibrary : INativeCoreLibrary
{
    int Initialise();

    int LoadRom(string romPath);

    void Reset();

    void Run(int cycles);

    void Shutdown();

    int GetLampsOn();

    int GetLampBrightness(int lampIndex);

    int GetPosOut(int positionIndex);

    void TurnSwitchOn(int switchIndex);

    void TurnSwitchOff(int switchIndex);
}

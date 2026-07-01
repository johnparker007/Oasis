#ifdef JPMCORE_EXPORTS
#define Interface_API __declspec(dllexport)
#else
#define Interface_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C"
{
#endif

	float DLLVersion = 1.0000;
	Interface_API float GetDLLVersion(void);
	//IMPACT Controls
	Interface_API UINT8 Initialise(void);
	Interface_API UINT8 Shutdown(void);
	Interface_API void Reset(void);
	Interface_API INT32 Run(UINT32 Cycles);
	Interface_API INT64	LoadROM(INT8*, INT8*, INT8*, INT8*, INT8);
	//Alpha
	Interface_API int GetAlphaSegments(UINT8);
	Interface_API UINT8 GetAlphaDotComma(UINT8);
	Interface_API UINT8 GetAlphaBright();
	//Reels
	Interface_API INT16 GetPosOut(INT8);
	Interface_API void SetOptoInvert(UINT8 ReelNum, UINT8 State);
	Interface_API void SetOptoStart(UINT8 ReelNum, UINT8 Start);
	Interface_API void SetOptoEnd(UINT8 ReelNum, UINT8 End);
	Interface_API void SetSteps(UINT8 ReelNum, UINT8 State);
	//Lamps
	Interface_API void UpdateLamps(void);
	Interface_API bool GetLampsOn(UINT16);
	//These Floats should be returned in the range 0.f to 1.f
	Interface_API float GetFilamentColourR(UINT16);
	Interface_API float GetFilamentColourG(UINT16);
	Interface_API float GetFilamentColourB(UINT16);
	Interface_API float GetLampBrightness(UINT16);
	//7 Seg Displays + LEDs
	Interface_API void UpdateSegs(void);
	Interface_API UINT8 GetSegOn(UINT16);
	Interface_API UINT8 GetSegBright(UINT16);
	//Meters
	Interface_API UINT32 GetMeterCounter(UINT8);
	Interface_API void TurnSwitchOn(UINT16);
	Interface_API void TurnSwitchOff(UINT16);
	Interface_API UINT8 ReadSwitch(UINT8);
	//Coin Mechs
	Interface_API void SetCommStyle(UINT8 Style);
	Interface_API void SetCommInvert(UINT8 Invert);
	Interface_API void SetCycles(UINT32 Cycles);
	Interface_API void SetEDCEnable(UINT8 Enable);
	Interface_API void SetLockoutVal(UINT8 Coin, UINT8 Value);
	Interface_API void SetLockoutInvert(UINT8 Coin, UINT8 Invert);
	Interface_API UINT8 CoinIn(UINT8 Coin, UINT8 CoinValue);
	Interface_API void SetCoinValue(UINT8 CoinNum, UINT8 Value);
	Interface_API void SetCoinEnable(UINT8 CoinNum, UINT8 Value);
	Interface_API UINT8 GetCoinLampOnOff(UINT8 LampNum);	
	//Coin Tubes - Set
	Interface_API void SetEnable(UINT8 Num, UINT8 Enable);
	Interface_API void SetCounterIn(UINT8 Num, UINT32 Count);
	Interface_API void SetCounterOut(UINT8 Num, UINT32 Count);
	Interface_API void SetPortIndex(UINT8 Num, UINT8 Index);
	Interface_API void SetCoin(UINT8 Num, UINT8 Coin);
	Interface_API void SetLevel(UINT8 Num, UINT8 Level);
	Interface_API void SetFullLevel(UINT8 Num, UINT8 Level);
	Interface_API void SetLoEnable(UINT8 Num, UINT8 Enable);
	Interface_API void SetLoInvert(UINT8 Num, UINT8 Invert);
	Interface_API void SetLoSwitch(UINT8 Num, UINT8 Switch);
	Interface_API void SetLoLevel(UINT8 Num, UINT32 Level);
	Interface_API void SetHiEnable(UINT8 Num, UINT8 Enable);
	Interface_API void SetHiInvert(UINT8 Num, UINT8 Invert);
	Interface_API void SetHiSwitch(UINT8 Num, UINT8 Switch);
	Interface_API void SetHiLevel(UINT8 Num, UINT32 Level);
	//Coin Tubes - Get
	Interface_API UINT8 GetEnable(UINT8 Num);
	Interface_API UINT32 GetCounterIn(UINT8 Num);
	Interface_API UINT32 GetCounterOut(UINT8 Num);
	Interface_API UINT8 GetPortIndex(UINT8 Num);
	Interface_API UINT8 GetCoin(UINT8 Num);
	Interface_API UINT32 GetLevel(UINT8 Num);
	Interface_API UINT32 GetFullLevel(UINT8 Num);
	Interface_API UINT8 GetLoEnable(UINT8 Num);
	Interface_API UINT8 GetLoInvert(UINT8 Num);
	Interface_API UINT8 GetLoSwitch(UINT8 Num);
	Interface_API UINT32 GetLoLevel(UINT8 Num);
	Interface_API UINT8 GetHiEnable(UINT8 Num);
	Interface_API UINT8 GetHiInvert(UINT8 Num);
	Interface_API UINT8 GetHiSwitch(UINT8 Num);
	Interface_API UINT32 GetHiLevel(UINT8 Num);
	
	//Hopper Set
	Interface_API void SetHopperEnable(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperCoin(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperCoinsIn(UINT8 Num, UINT32 Value);
	Interface_API void SetHopperCoinsOut(UINT8 Num, UINT32 Value);
	Interface_API void SetHopperLevel(UINT8 Num, UINT32 Value);
	Interface_API void SetHopperFullLevel(UINT8 Num, UINT32 Value);
	Interface_API void SetHopperLoEnable(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperLoInvert(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperLoSwitch(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperLoLevel(UINT8 Num, UINT32 Value);
	Interface_API void SetHopperHiEnable(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperHiInvert(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperHiSwitch(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperHiLevel(UINT8 Num, UINT32 Value);
	Interface_API void SetHopperOptoEnable(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperOptoReturn(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperMotorEnable(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperLoIndicator(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperHiIndicator(UINT8 Num, UINT8 Value);
	Interface_API void SetHopperCoinsRefilled(UINT8 Num, UINT32 Value);
	//Hoppers Get
	Interface_API UINT8 GetHopperEnable(UINT8 Num);
	Interface_API UINT8 GetHopperCoin(UINT8 Num);
	Interface_API UINT32 GetHopperCoinsIn(UINT8 Num);
	Interface_API UINT32 GetHopperCoinsOut(UINT8 Num);
	Interface_API UINT32 GetHopperLevel(UINT8 Num);
	Interface_API UINT32 GetHopperFullLevel(UINT8 Num);
	Interface_API UINT8 GetHopperLoEnable(UINT8 Num);
	Interface_API UINT8 GetHopperLoInvert(UINT8 Num);
	Interface_API UINT8 GetHopperLoSwitch(UINT8 Num);
	Interface_API UINT32 GetHopperLoLevel(UINT8 Num);
	Interface_API UINT8 GetHopperHiEnable(UINT8 Num);
	Interface_API UINT8 GetHopperHiInvert(UINT8 Num);
	Interface_API UINT8 GetHopperHiSwitch(UINT8 Num);
	Interface_API UINT32 GetHopperHiLevel(UINT8 Num);
	Interface_API UINT8 GetHopperOptoEnable(UINT8 Num);
	Interface_API UINT8 GetHopperOptoReturn(UINT8 Num);
	Interface_API UINT8 GetHopperMotorEnable(UINT8 Num);
	Interface_API UINT32 GetHopperCoinsRefilled(UINT8 Num);
	Interface_API UINT8 GetHopperHiIndicator(UINT8 Num);
	Interface_API UINT8 GetHopperLoIndicator(UINT8 Num);
	//Keys
	Interface_API void SetStake(INT8 Stake);
	Interface_API void SetPrize(INT8 Prize);
	Interface_API void SetPercent(INT8 Percent);
	//State Save
	Interface_API void LoadState(void);
	Interface_API void SaveState(void);
	//DIPS
	Interface_API void SetDIP(UINT8 Num, UINT8 Value);
	//Sound
	Interface_API INT32 LoadSoundROM(INT8 *name1, INT8 *name2, INT8 *name3, INT8 *name4);
	//Status LED
	Interface_API UINT8 GetStatusLED(void);
	//RAM Load/Save
	Interface_API void SaveRAM(INT8 * FileString);
	Interface_API void LoadRAM(INT8 * FileString);
	//Folder/File Name
	Interface_API void SetCFolder(INT8 * Folder);
	Interface_API void SetCFileName(INT8 * FileName);
	//Alpha Display
	Interface_API UINT8 GetAlphaChar(UINT8 Num);
	//EDC Unit
	Interface_API UINT8* GetEDCString();	

#ifdef __cplusplus
}
#endif
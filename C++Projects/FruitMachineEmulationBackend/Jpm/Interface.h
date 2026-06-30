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
	Interface_API unsigned char SYSTEM6Initialise(void);
	Interface_API unsigned char SYSTEM6Shutdown(void);
	Interface_API void SYSTEM6Reset(void);
	Interface_API int SYSTEM6Run(int Cycles);
	Interface_API signed long SYSTEM6LoadROM(char*, char*, char*, char*, char);
	//Alpha
	Interface_API int SYSTEM6GetAlphaSegments(char);
	Interface_API char SYSTEM6GetAlphaDotComma(char);
	Interface_API char SYSTEM6GetAlphaBright();
	//Reels
	Interface_API signed short SYSTEM6GetPosOut(char);
	Interface_API void SetOptoInvert(UINT8 ReelNum, UINT8 State);
	Interface_API void SetOptoStart(UINT8 ReelNum, UINT8 Start);
	Interface_API void SetOptoEnd(UINT8 ReelNum, UINT8 End);
	Interface_API void SetSteps(UINT8 ReelNum, UINT8 State);
	//Lamps
	Interface_API void SYSTEM6UpdateLamps(void);
	Interface_API bool SYSTEM6GetLampsOn(UINT16);
	//These Floats should be returned in the range 0.f to 1.f
	Interface_API float SYSTEM6GetFilamentColourR(UINT16);
	Interface_API float SYSTEM6GetFilamentColourG(UINT16);
	Interface_API float SYSTEM6GetFilamentColourB(UINT16);
	Interface_API float SYSTEM6GetLampBrightness(UINT16);
	//7 Seg Displays + LEDs
	Interface_API void SYSTEM6UpdateSegs(void);
	Interface_API unsigned char SYSTEM6GetSegOn(unsigned short);
	Interface_API unsigned char SYSTEM6GetSegBright(unsigned short);
	//Meters
	Interface_API unsigned int SYSTEM6GetMeterCounter(unsigned char);
	Interface_API void SYSTEM6TurnSwitchOn(int);
	Interface_API void SYSTEM6TurnSwitchOff(int);
	Interface_API unsigned char SYSTEM6ReadSwitch(unsigned char);
	//Coin Mechs
	Interface_API void SYSTEM6SetCommStyle(unsigned char Num, unsigned char Style);
	Interface_API void SYSTEM6SetCommInvert(unsigned char Num, unsigned char Invert);
	Interface_API void SYSTEM6SetCycles(unsigned char Num, unsigned int Cycles);
	Interface_API void SYSTEM6SetEDCEnable(unsigned char Num, unsigned char Enable);
	Interface_API void SYSTEM6SetLockoutVal(unsigned char Num, unsigned char Coin, unsigned char Value);
	Interface_API void SYSTEM6SetLockoutInvert(unsigned char Num, unsigned char Coin, unsigned char Invert);
	Interface_API unsigned char SYSTEM6CoinIn(unsigned char Num, unsigned char Coin, unsigned char CoinValue);
	Interface_API void SYSTEM6SetCoinValue(unsigned char Num, unsigned char CoinNum, unsigned char Value);
	Interface_API void SYSTEM6SetCoinEnable(unsigned char Num, unsigned char CoinNum, unsigned char Value);
	Interface_API unsigned char SYSTEM6GetLampOnOff(unsigned char Num, unsigned char LampNum);	
	//Coin Tubes - Set
	Interface_API void SYSTEM6SetEnable(unsigned char Num, unsigned char Enable);
	Interface_API void SYSTEM6SetCounterIn(unsigned char Num, unsigned long Count);
	Interface_API void SYSTEM6SetCounterOut(unsigned char Num, unsigned long Count);
	Interface_API void SYSTEM6SetPortIndex(unsigned char Num, unsigned char Index);
	Interface_API void SYSTEM6SetCoin(unsigned char Num, unsigned char Coin);
	Interface_API void SYSTEM6SetLevel(unsigned char Num, unsigned char Level);
	Interface_API void SYSTEM6SetFullLevel(unsigned char Num, unsigned char Level);
	Interface_API void SYSTEM6SetLoEnable(unsigned char Num, unsigned char Enable);
	Interface_API void SYSTEM6SetLoInvert(unsigned char Num, unsigned char Invert);
	Interface_API void SYSTEM6SetLoSwitch(unsigned char Num, unsigned char Switch);
	Interface_API void SYSTEM6SetLoLevel(unsigned char Num, signed long Level);
	Interface_API void SYSTEM6SetHiEnable(unsigned char Num, unsigned char Enable);
	Interface_API void SYSTEM6SetHiInvert(unsigned char Num, unsigned char Invert);
	Interface_API void SYSTEM6SetHiSwitch(unsigned char Num, unsigned char Switch);
	Interface_API void SYSTEM6SetHiLevel(unsigned char Num, signed long Level);
	//Coin Tubes - Get
	Interface_API unsigned char SYSTEM6GetEnable(unsigned char Num);
	Interface_API unsigned long SYSTEM6GetCounterIn(unsigned char Num);
	Interface_API unsigned long SYSTEM6GetCounterOut(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetPortIndex(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetCoin(unsigned char Num);
	Interface_API long SYSTEM6GetLevel(unsigned char Num);
	Interface_API long SYSTEM6GetFullLevel(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetLoEnable(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetLoInvert(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetLoSwitch(unsigned char Num);
	Interface_API signed long SYSTEM6GetLoLevel(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetHiEnable(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetHiInvert(unsigned char Num);
	Interface_API unsigned char SYSTEM6GetHiSwitch(unsigned char Num);
	Interface_API signed long SYSTEM6GetHiLevel(unsigned char Num);
	
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
	Interface_API void SetStake(char Stake);
	Interface_API void SetPrize(char Prize);
	Interface_API void SetPercent(char Percent);
	//State Save
	Interface_API void SYSTEM6LoadState(void);
	Interface_API void SYSTEM6SaveState(void);
	//DIPS
	Interface_API void SetDIP(UINT8 Num, UINT8 Value);
	//Sound
	Interface_API signed long SYSTEM6LoadSoundROM(char *name1, char *name2, char *name3, char *name4);
	//Status LED
	Interface_API unsigned char SYSTEM6GetStatusLED(void);
	//RAM Load/Save
	Interface_API void SYSTEM6SaveRAM(char * FileString);
	Interface_API void SYSTEM6LoadRAM(char * FileString);
	//Folder/File Name
	Interface_API void SYSTEM6SetCFolder(char * Folder);
	Interface_API void SYSTEM6SetCFileName(char * FileName);
	//Alpha Display
	Interface_API UINT8 GetAlphaChar(UINT8 Num);
	//EDC Unit
	Interface_API char* GetEDCString();	

#ifdef __cplusplus
}
#endif
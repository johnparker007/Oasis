#ifndef SYS6H
#define SYS6H

#include "SCN68681.h"
#include "DevicePPI8255.h"
#include "AlphaDisplayClass.h"
#include "Reels.h"
#include "Lamps.h"
#include "Segments.h"
#include "Meters.h"
#include "SwitchMatrix.h"
#include "CoinMechs.h"
#include "SoundMain.h"
#include "Solenoids.h"
#include "CashBox.h"
#include "Hoppers.h"
#define NUMMECHS 8

class SYSTEM6 : mc68000 {
protected:

private:
	
	DuartScn68681			DUART;
	DuartScn68681			DUART2;
	DevicePPI8255			PPIO;
	AlphanumericDisplay     Alpha1;
	ReelDrive				Reels;
	Lamping					Lamps;
	Segs					Seg7;
	CoinMeter				Meters;
	SwitchMatrix			Switches;
	CashBoxClass			CashBox;
	ElecronicCoinMech		Mars[NUMMECHS];
	SolenoidPayout			Tubes;
	HopperPayout			Hoppers;

	LoadSaveClass * LSC = NULL;

	int CombineStrings(char * &OutStr, char * In1, char * In2);

	char * CFolder;
	char * CFileName;

	FILE *DebugFile;
	unsigned long			TotalCycles = 0;
	unsigned char			StatusLED = 0;
	unsigned char			RAMEnable = 0;
	unsigned char			IMPACT3 = 0;
	int						SndCnt = 0;	
	int						DivCycles = 0;

public:

	UINT8  					ROM[0x100000];  // Program ROM Storage
	UINT8  					RAM[0x4000];	// Onboard RAM
	
	SampledSound			Sound;

	virtual int __fastcall		cpu_irq_ack(int level);
	virtual void __fastcall		cpu_set_fc(int discard);
	virtual void __fastcall		cpu_inst_hook(int cycles);
	virtual void __fastcall		cpu_pulse_reset(void);
	virtual UINT8 __fastcall 	cpu_read_byte(int address);
	virtual UINT16 __fastcall 	cpu_read_word(int address);
	virtual UINT32 __fastcall 	cpu_read_long(int address);
	virtual void __fastcall 	cpu_write_byte(int address, UINT8 value);
	virtual void __fastcall 	cpu_write_word(int address, UINT16 value);
	virtual void __fastcall 	cpu_write_long(int address, UINT32 value);

	void SetCFolder(char * Folder);
	void SetCFileName(char * FileName);

	//Program Subroutines / Functions	
	void Reset(void);						//Reset Sub		
	void Init(void);						//Init Sub	
	int Run(int);							//Run Sub

	//Alpha
	int GetAlphaSegs(char);
	char GetAlphaDotComma(char);
	char GetAlphaBright();
	UINT8 GetAlphaChar(UINT8 num);
	//Reels
	short GetPosOut(char);
	void SetOptoInvert(UINT8 ReelNum, UINT8 State);
	void SetOptoStart(UINT8 ReelNum, UINT8 Start);
	void SetOptoEnd(UINT8 ReelNum, UINT8 End);
	void SetSteps(UINT8 ReelNum, UINT8 State);
	//Lamps
	void UpdateLamps(void);
	D3DXVECTOR3 GetFilamentColour(UINT16);
	float GetLampBrightness(UINT16);
	bool GetLampsOn(UINT16);
	//7 Segs
	void UpdateSegs(void);
	unsigned char GetSegOn(unsigned short);
	unsigned char GetSegBright(unsigned short);
	//Meters	
	unsigned int GetMeterCounter(unsigned char);
	//Switches
	void TurnSwitchOn(unsigned char);
	void TurnSwitchOff(unsigned char);
	unsigned char ReadSwitch(unsigned char);
	//DIPs
	void SetDIP(UINT8 Num, UINT8 Value);
	//Hoppers

	//Tubes
	void SetEnable(unsigned char Num, unsigned char Enable);
	void SetCounterIn(unsigned char Num, unsigned long Count);
	void SetCounterOut(unsigned char Num, unsigned long Count);
	void SetPortIndex(unsigned char Num, unsigned char Index);
	void SetCoin(unsigned char Num, unsigned char Coin);
	void SetLevel(unsigned char Num, unsigned char Level);
	void SetFullLevel(unsigned char Num, unsigned char Level);
	void SetLoEnable(unsigned char Num, unsigned char Enable);
	void SetLoInvert(unsigned char Num, unsigned char Invert);
	void SetLoSwitch(unsigned char Num, unsigned char Switch);
	void SetLoLevel(unsigned char Num, signed long Level);
	void SetHiEnable(unsigned char Num, unsigned char Enable);
	void SetHiInvert(unsigned char Num, unsigned char Invert);
	void SetHiSwitch(unsigned char Num, unsigned char Switch);
	void SetHiLevel(unsigned char Num, signed long Level);

	unsigned char GetEnable(unsigned char Num);
	unsigned long GetCounterIn(unsigned char Num);
	unsigned long GetCounterOut(unsigned char Num);
	unsigned char GetPortIndex(unsigned char Num);
	unsigned char GetCoin(unsigned char Num);
	signed long GetLevel(unsigned char Num);
	signed long GetFullLevel(unsigned char Num);
	unsigned char GetLoEnable(unsigned char Num);
	unsigned char GetLoInvert(unsigned char Num);
	unsigned char GetLoSwitch(unsigned char Num);
	signed long GetLoLevel(unsigned char Num);
	unsigned char GetHiEnable(unsigned char Num);
	unsigned char GetHiInvert(unsigned char Num);
	unsigned char GetHiSwitch(unsigned char Num);
	signed long GetHiLevel(unsigned char Num);

	//EL Coin Mech
	void SetCommStyle(unsigned char Num, unsigned char Style);
	void SetCommInvert(unsigned char Num, unsigned char Invert);
	void SetCycles(unsigned char Num, unsigned int Cycles);	
	void SetLockoutVal(unsigned char Num, unsigned char Coin, unsigned char Value);
	void SetLockoutInvert(unsigned char Num, unsigned char Coin, unsigned char Invert);
	void SetEDCEnable(unsigned char Num, unsigned char Enable);
	unsigned char CoinIn(unsigned char Num, unsigned char Coin, unsigned char CoinValue);
	void SetCoinValue(unsigned char Num, unsigned char CoinNum, unsigned char Value);
	void SetCoinEnable(unsigned char Num, unsigned char CoinNum, unsigned char Value);

	//Lamps
	unsigned char GetLampOnOff(unsigned char Num, unsigned char LampNum);	
		
	//Status LEDs	
	unsigned char GetStatusLED(void);

	//Hoppers
	void SetHopperEnable(UINT8 Num, UINT8 Value);
	void SetHopperCoin(UINT8 Num, UINT8 Value);
	void SetHopperCoinsIn(UINT8 Num, UINT32 Value);
	void SetHopperCoinsOut(UINT8 Num, UINT32 Value);
	void SetHopperLevel(UINT8 Num, UINT32 Value);
	void SetHopperFullLevel(UINT8 Num, UINT32 Value);
	void SetHopperLoEnable(UINT8 Num, UINT8 Value);
	void SetHopperLoInvert(UINT8 Num, UINT8 Value);
	void SetHopperLoSwitch(UINT8 Num, UINT8 Value);
	void SetHopperLoLevel(UINT8 Num, UINT32 Value);
	void SetHopperHiEnable(UINT8 Num, UINT8 Value);
	void SetHopperHiInvert(UINT8 Num, UINT8 Value);
	void SetHopperHiSwitch(UINT8 Num, UINT8 Value);
	void SetHopperHiLevel(UINT8 Num, UINT32 Value);
	void SetHopperOptoEnable(UINT8 Num, UINT8 Value);
	void SetHopperOptoReturn(UINT8 Num, UINT8 Value);
	void SetHopperMotorEnable(UINT8 Num, UINT8 Value);
	void SetHopperLoIndicator(UINT8 Num, UINT8 Value);
	void SetHopperHiIndicator(UINT8 Num, UINT8 Value);
	void SetHopperCoinsRefilled(UINT8 Num, UINT32 Value);
	UINT8 GetHopperEnable(UINT8 Num);
	UINT8 GetHopperCoin(UINT8 Num);
	UINT32 GetHopperCoinsIn(UINT8 Num);
	UINT32 GetHopperCoinsOut(UINT8 Num);
	UINT32 GetHopperLevel(UINT8 Num);
	UINT32 GetHopperFullLevel(UINT8 Num);
	UINT8 GetHopperLoEnable(UINT8 Num);
	UINT8 GetHopperLoInvert(UINT8 Num);
	UINT8 GetHopperLoSwitch(UINT8 Num);
	UINT32 GetHopperLoLevel(UINT8 Num);
	UINT8 GetHopperHiEnable(UINT8 Num);
	UINT8 GetHopperHiInvert(UINT8 Num);
	UINT8 GetHopperHiSwitch(UINT8 Num);
	UINT32 GetHopperHiLevel(UINT8 Num);
	UINT8 GetHopperOptoEnable(UINT8 Num);
	UINT8 GetHopperOptoReturn(UINT8 Num);
	UINT8 GetHopperMotorEnable(UINT8 Num);
	UINT32 GetHopperCoinsRefilled(UINT8 Num);
	UINT8 GetHopperHiIndicator(UINT8 Num);
	UINT8 GetHopperLoIndicator(UINT8 Num);

	//Stake Prize Keys
	void SetStake(char Stake);
	void SetPrize(char Prize);
	void SetPercent(char Percent);

	//RAM Save/Load
	void SaveRAM(char * FileString);
	void LoadRAM(char * FileString);

	//State Save/Load
	void LoadState(void);
	void SaveState(void);
	void SaveCPUState(void);
	void LoadCPUState(void);

	//EDC
	char* GetEDCString(void);	

	//Con/De structors
	SYSTEM6();
	~SYSTEM6();

};

#endif // SYS6H
#pragma once

#include "SCN68681.h"
#include "DevicePPI8255.h"
#include "AlphaDisplay.h"
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
	ElecronicCoinMech		Mars;
	SolenoidPayout			Tubes;
	HopperPayout			Hoppers;

	LoadSaveClass * LSC = NULL;

	UINT32 __fastcall CombineStrings(UINT8 * &OutStr, UINT8 * In1, UINT8 * In2);

	UINT8* CFolder = NULL;
	UINT8* CFileName = NULL;
	FILE *DebugFile = NULL;

	UINT64			TotalCycles = 0;
	UINT8			StatusLED = 0;
	UINT8			RAMEnable = 0;
	UINT8			IMPACT3 = 0;
	INT32			SndCnt = 0;	
	INT32			DivCycles = 0;

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

	void __fastcall SetCFolder(UINT8 * Folder);
	void __fastcall SetCFileName(UINT8 * FileName);

	//Program Subroutines / Functions	
	void __fastcall Reset(void);						//Reset Sub		
	void __fastcall Init(void);						//Init Sub	
	INT32 __fastcall Run(int);							//Run Sub

	//Alpha
	UINT32 __fastcall GetAlphaSegs(UINT8);
	UINT8 __fastcall GetAlphaDotComma(UINT8);
	UINT8 __fastcall GetAlphaBright();
	UINT8 __fastcall GetAlphaChar(UINT8 num);
	//Reels
	INT16 __fastcall GetPosOut(UINT8);
	void __fastcall SetOptoInvert(UINT8 ReelNum, UINT8 State);
	void __fastcall SetOptoStart(UINT8 ReelNum, UINT8 Start);
	void __fastcall SetOptoEnd(UINT8 ReelNum, UINT8 End);
	void __fastcall SetSteps(UINT8 ReelNum, UINT8 State);
	//Lamps
	void __fastcall UpdateLamps(void);
	float3 __fastcall GetFilamentColour(UINT16);
	float __fastcall GetLampBrightness(UINT16);
	bool __fastcall GetLampsOn(UINT16);
	//7 Segs
	void __fastcall UpdateSegs(void);
	UINT8 __fastcall GetSegOn(unsigned short);
	UINT8 __fastcall GetSegBright(unsigned short);
	
	//Meters	
	UINT32 __fastcall GetMeterCounter(UINT8);
	
	//Switches
	void __fastcall TurnSwitchOn(UINT8);
	void __fastcall TurnSwitchOff(UINT8);
	UINT8 __fastcall ReadSwitch(UINT8);
	
	//DIPs
	void __fastcall SetDIP(UINT8 Num, UINT8 Value);
	
	//Tubes
	void __fastcall SetEnable(UINT8 Num, UINT8 Enable);
	void __fastcall SetCounterIn(UINT8 Num, UINT32 Count);
	void __fastcall SetCounterOut(UINT8 Num, UINT32 Count);
	void __fastcall SetPortIndex(UINT8 Num, UINT8 Index);
	void __fastcall SetCoin(UINT8 Num, UINT8 Coin);
	void __fastcall SetLevel(UINT8 Num, UINT8 Level);
	void __fastcall SetFullLevel(UINT8 Num, UINT8 Level);
	void __fastcall SetLoEnable(UINT8 Num, UINT8 Enable);
	void __fastcall SetLoInvert(UINT8 Num, UINT8 Invert);
	void __fastcall SetLoSwitch(UINT8 Num, UINT8 Switch);
	void __fastcall SetLoLevel(UINT8 Num, UINT32 Level);
	void __fastcall SetHiEnable(UINT8 Num, UINT8 Enable);
	void __fastcall SetHiInvert(UINT8 Num, UINT8 Invert);
	void __fastcall SetHiSwitch(UINT8 Num, UINT8 Switch);
	void __fastcall SetHiLevel(UINT8 Num, UINT32 Level);

	UINT8 __fastcall GetEnable(UINT8 Num);
	UINT32 __fastcall GetCounterIn(UINT8 Num);
	UINT32 __fastcall GetCounterOut(UINT8 Num);
	UINT8 __fastcall GetPortIndex(UINT8 Num);
	UINT8 __fastcall GetCoin(UINT8 Num);
	UINT32 __fastcall GetLevel(UINT8 Num);
	UINT32 __fastcall GetFullLevel(UINT8 Num);
	UINT8 __fastcall GetLoEnable(UINT8 Num);
	UINT8 __fastcall GetLoInvert(UINT8 Num);
	UINT8 __fastcall GetLoSwitch(UINT8 Num);
	UINT32 __fastcall GetLoLevel(UINT8 Num);
	UINT8 __fastcall GetHiEnable(UINT8 Num);
	UINT8 __fastcall GetHiInvert(UINT8 Num);
	UINT8 __fastcall GetHiSwitch(UINT8 Num);
	UINT32 __fastcall GetHiLevel(UINT8 Num);

	//EL Coin Mech
	void __fastcall SetCommStyle(UINT8 Style);
	void __fastcall SetCommInvert(UINT8 Invert);
	void __fastcall SetCycles(UINT32 Cycles);	
	void __fastcall SetLockoutVal(UINT8 Coin, UINT8 Value);
	void __fastcall SetLockoutInvert(UINT8 Coin, UINT8 Invert);
	void __fastcall SetEDCEnable(UINT8 Enable);
	UINT8 CoinIn(UINT8 Coin, UINT8 CoinValue);
	void __fastcall SetCoinValue(UINT8 CoinNum, UINT8 Value);
	void __fastcall SetCoinEnable(UINT8 CoinNum, UINT8 Value);

	//Lamps
	UINT8 __fastcall GetCoinLampOnOff(UINT8 LampNum);	
		
	//Status LEDs	
	UINT8 __fastcall GetStatusLED(void);

	//Hoppers
	void __fastcall SetHopperEnable(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperCoin(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperCoinsIn(UINT8 Num, UINT32 Value);
	void __fastcall SetHopperCoinsOut(UINT8 Num, UINT32 Value);
	void __fastcall SetHopperLevel(UINT8 Num, UINT32 Value);
	void __fastcall SetHopperFullLevel(UINT8 Num, UINT32 Value);
	void __fastcall SetHopperLoEnable(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperLoInvert(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperLoSwitch(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperLoLevel(UINT8 Num, UINT32 Value);
	void __fastcall SetHopperHiEnable(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperHiInvert(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperHiSwitch(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperHiLevel(UINT8 Num, UINT32 Value);
	void __fastcall SetHopperOptoEnable(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperOptoReturn(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperMotorEnable(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperLoIndicator(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperHiIndicator(UINT8 Num, UINT8 Value);
	void __fastcall SetHopperCoinsRefilled(UINT8 Num, UINT32 Value);

	UINT8 __fastcall GetHopperEnable(UINT8 Num);
	UINT8 __fastcall GetHopperCoin(UINT8 Num);
	UINT32 __fastcall GetHopperCoinsIn(UINT8 Num);
	UINT32 __fastcall GetHopperCoinsOut(UINT8 Num);
	UINT32 __fastcall GetHopperLevel(UINT8 Num);
	UINT32 __fastcall GetHopperFullLevel(UINT8 Num);
	UINT8 __fastcall GetHopperLoEnable(UINT8 Num);
	UINT8 __fastcall GetHopperLoInvert(UINT8 Num);
	UINT8 __fastcall GetHopperLoSwitch(UINT8 Num);
	UINT32 __fastcall GetHopperLoLevel(UINT8 Num);
	UINT8 __fastcall GetHopperHiEnable(UINT8 Num);
	UINT8 __fastcall GetHopperHiInvert(UINT8 Num);
	UINT8 __fastcall GetHopperHiSwitch(UINT8 Num);
	UINT32 __fastcall GetHopperHiLevel(UINT8 Num);
	UINT8 __fastcall GetHopperOptoEnable(UINT8 Num);
	UINT8 __fastcall GetHopperOptoReturn(UINT8 Num);
	UINT8 __fastcall GetHopperMotorEnable(UINT8 Num);
	UINT32 __fastcall GetHopperCoinsRefilled(UINT8 Num);
	UINT8 __fastcall GetHopperHiIndicator(UINT8 Num);
	UINT8 __fastcall GetHopperLoIndicator(UINT8 Num);

	//Stake Prize Keys
	void __fastcall SetStake(UINT8 Stake);
	void __fastcall SetPrize(UINT8 Prize);
	void __fastcall SetPercent(UINT8 Percent);

	//RAM Save/Load
	void __fastcall SaveRAM(UINT8 * FileString);
	void __fastcall LoadRAM(UINT8 * FileString);

	//State Save/Load
	void __fastcall LoadState(void);
	void __fastcall SaveState(void);
	void __fastcall SaveCPUState(void);
	void __fastcall LoadCPUState(void);

	//EDC
	UINT8* __fastcall GetEDCString(void);

	//Con/De structors
	SYSTEM6();
	~SYSTEM6();
};
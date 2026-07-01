#pragma once

#include "LoadSave.h"

#define NUMSOLENOIDS 8
class SolenoidPayout {
protected:

private:

	//Solenoids
	UINT8 Pin[NUMSOLENOIDS];	
	UINT8 Enable[NUMSOLENOIDS];	
	UINT8 PrevPin[NUMSOLENOIDS];		
	UINT32 CounterIn[NUMSOLENOIDS];
	UINT32 CounterOut[NUMSOLENOIDS];	
	UINT8 PortIndex[NUMSOLENOIDS];	
	UINT8 Coin[NUMSOLENOIDS];	
	UINT32 Level[NUMSOLENOIDS];
	UINT8 LoEnable[NUMSOLENOIDS];
	UINT8 LoSwitch[NUMSOLENOIDS];
	UINT8 LoState[NUMSOLENOIDS];
	UINT8 LoInvert[NUMSOLENOIDS];
	UINT32 LoLevel[NUMSOLENOIDS];
	UINT8 HiEnable[NUMSOLENOIDS];
	UINT8 HiSwitch[NUMSOLENOIDS];
	UINT32 HiLevel[NUMSOLENOIDS];
	UINT8 HiState[NUMSOLENOIDS];
	UINT8 HiInvert[NUMSOLENOIDS];
	UINT32 FullLevel[NUMSOLENOIDS];

	LoadSaveClass * LSC = NULL;

public:	

	SolenoidPayout();
	~SolenoidPayout();	

	//Set Subs
	void SetEnable(UINT8 Num, UINT8 Enable);
	void SetCounterIn(UINT8 Num, UINT32 Count);
	void SetCounterOut(UINT8 Num, UINT32 Count);
	void SetPortIndex(UINT8 Num, UINT8 Index);
	void SetCoin(UINT8 Num, UINT8 Coin);
	void SetLevel(UINT8 Num, UINT8 Level);
	void SetFullLevel(UINT8 Num, UINT8 Level);
	void SetLoEnable(UINT8 Num, UINT8 Enable);
	void SetLoInvert(UINT8 Num, UINT8 Invert);
	void SetLoSwitch(UINT8 Num, UINT8 Switch);
	void SetLoLevel(UINT8 Num, UINT32 Level);
	void SetHiEnable(UINT8 Num, UINT8 Enable);
	void SetHiInvert(UINT8 Num, UINT8 Invert);
	void SetHiSwitch(UINT8 Num, UINT8 Switch);
	void SetHiLevel(UINT8 Num, UINT32 Level);
	void SetPort(UINT8 Port);

	//Get Functions
	UINT8 GetEnable(UINT8 Num);
	UINT32 GetCounterIn(UINT8 Num);
	UINT32 GetCounterOut(UINT8 Num);
	UINT8 GetPortIndex(UINT8 Num);
	UINT8 GetCoin(UINT8 Num);
	UINT32 GetLevel(UINT8 Num);
	UINT32 GetFullLevel(UINT8 Num);
	UINT8 GetLoEnable(UINT8 Num);
	UINT8 GetLoInvert(UINT8 Num);
	UINT8 GetLoSwitch(UINT8 Num);
	UINT32 GetLoLevel(UINT8 Num);
	UINT8 GetLoState(UINT8 Num);
	UINT8 GetHiEnable(UINT8 Num);
	UINT8 GetHiInvert(UINT8 Num);
	UINT8 GetHiSwitch(UINT8 Num);
	UINT8 GetHiState(UINT8 Num);
	UINT32 GetHiLevel(UINT8 Num);
	UINT8 GetPort(void);

	//Main Subs
	void Write(UINT8 Port);
	UINT8 CoinIn(UINT8 CoinCode);
	void Init(LoadSaveClass * LSCIn);
	void Update(void);

	void SaveState();
	void LoadState();
};
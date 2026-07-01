#pragma once

#include "LoadSave.h"

#define FITTEDMETERS 6
class CoinMeter {
private:

	LoadSaveClass * LSC;

	UINT8 Pin[FITTEDMETERS];	
	UINT8 On[FITTEDMETERS];
	UINT8 Enable[FITTEDMETERS];	
	UINT8 PrevPin[FITTEDMETERS];	
	UINT32 TimeOn[FITTEDMETERS];
	UINT32 Counter[FITTEDMETERS];
	UINT8 PortIndex[FITTEDMETERS];

public:	
	CoinMeter();
	~CoinMeter();
	

	void Write(UINT8 Index, UINT8 PinIn);
	void Run(UINT32 Cycles);
	UINT8 Check(void);
	
	UINT32 GetCounter(UINT8 Num);
	void SetCounter(UINT8 Num, UINT32 Value);

	void Init(LoadSaveClass * LSCIn);

	void SaveState();
	void LoadState();
};
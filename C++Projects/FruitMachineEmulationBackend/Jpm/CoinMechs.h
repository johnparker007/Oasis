#pragma once

#include "LoadSave.h"

#define NUMCOINS 6

class ElecronicCoinMech {
public:

	~ElecronicCoinMech();
	ElecronicCoinMech();

	UINT8 CoinIn(UINT8 Coin);
	UINT8 Run(UINT32 Cycles);
	void Init(LoadSaveClass* LSCIn);
	void SetCommStyle(UINT8 Style);
	void SetCommInvert(UINT8 Invert);
	void SetCycles(UINT32 Cycles);
	void SetEDCEnable(UINT8 Enable);
	void SetLockoutVal(UINT8 Coin, UINT8 Value);
	void SetLockoutInvert(UINT8 Coin, UINT8 Invert);
	void SetLockoutPort(UINT8 Port);
	void SetSelectedCoin(UINT8 Coin);
	void SetCoinValue(UINT8 Num, UINT8 Value);
	void SetCoinEnable(UINT8 Num, UINT8 Value);
	UINT8 GetLampOnOff(UINT8 Num);
	UINT8 GetSelectedCoin();
	UINT8 GetCommStyle();
	UINT8 GetCommInvert();
	UINT8 GetBCD();

	void SaveState();
	void LoadState(); 

private:

	INT32 InputCounter = 0;
	INT32 LockCounter = 0;
	UINT8 CommStyle = 0;
	UINT8 CommInvert = 0;
	UINT32 PulseCycles = 0;
	UINT8 EDCEnable = 0;
	UINT8 LockoutVal[NUMCOINS];
	UINT8 LockoutInvert[NUMCOINS];
	UINT8 CoinValue[NUMCOINS];
	UINT8 CoinEnable[NUMCOINS];
	UINT8 LockoutPort = 0;
	UINT8 SelectedCoin = 0;
	UINT8 LampOnOff[2];
	
	int	CoinsIn2p = 0,
		CoinsIn5p = 0,
		CoinsIn10p = 0,
		CoinsIn20p = 0,
		CoinsIn50p = 0,
		CoinsIn100p = 0,
		CoinsIn200p = 0,
		TokensIn5p = 0,
		TokensIn10p = 0,
		TokensIn20p = 0,
		TokensIn50p = 0,
		TokensIn100p = 0,
		TokensIn200p = 0,
		TokensIn = 0, 
		CoinsIn = 0, 
		TotalIn = 0;

	UINT8 BCD = 0;

	LoadSaveClass * LSC = NULL;
};
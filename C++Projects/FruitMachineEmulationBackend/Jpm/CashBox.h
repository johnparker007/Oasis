#pragma once

#include "LoadSave.h"

class CashBoxClass {
private:	

	UINT32	CoinsIn2p;
	UINT32	CoinsIn5p;
	UINT32	CoinsIn10p;
	UINT32	CoinsIn20p;
	UINT32	CoinsIn50p;
	UINT32	CoinsIn100p;
	UINT32	CoinsIn200p;
	UINT32	TokensIn5p;
	UINT32	TokensIn10p;
	UINT32	TokensIn20p;
	UINT32	TokensIn50p;
	UINT32	TokensIn100p;
	UINT32	TokensIn200p;
	UINT32  TokensIn, CoinsIn, TotalIn;
	
	LoadSaveClass* LSC;

public:	

	~CashBoxClass();
	CashBoxClass();

	void CoinIn(UINT8 Coin);
	UINT32 GetCoinsIn();
	UINT32 GetTokensIn();
	UINT32 GetTotalIn();
	void Init(LoadSaveClass* LSCIn);

	void SaveState();
	void LoadState();
};
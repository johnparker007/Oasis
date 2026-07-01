#include "stdafx.h"
#include "CashBox.h"

void CashBoxClass::SaveState(){
	
	LSC->SaveToBuffer(CoinsIn2p);
	LSC->SaveToBuffer(CoinsIn5p);
	LSC->SaveToBuffer(CoinsIn10p);
	LSC->SaveToBuffer(CoinsIn20p);
	LSC->SaveToBuffer(CoinsIn50p);
	LSC->SaveToBuffer(CoinsIn100p);
	LSC->SaveToBuffer(CoinsIn200p);
	LSC->SaveToBuffer(TokensIn5p);
	LSC->SaveToBuffer(TokensIn10p);
	LSC->SaveToBuffer(TokensIn20p);
	LSC->SaveToBuffer(TokensIn50p);
	LSC->SaveToBuffer(TokensIn100p);
	LSC->SaveToBuffer(TokensIn200p);
	LSC->SaveToBuffer(TokensIn);
	LSC->SaveToBuffer(CoinsIn);
	LSC->SaveToBuffer(TotalIn);
}

void CashBoxClass::LoadState(){
	
	LSC->LoadFromBuffer(CoinsIn2p);
	LSC->LoadFromBuffer(CoinsIn5p);
	LSC->LoadFromBuffer(CoinsIn10p);
	LSC->LoadFromBuffer(CoinsIn20p);
	LSC->LoadFromBuffer(CoinsIn50p);
	LSC->LoadFromBuffer(CoinsIn100p);
	LSC->LoadFromBuffer(CoinsIn200p);
	LSC->LoadFromBuffer(TokensIn5p);
	LSC->LoadFromBuffer(TokensIn10p);
	LSC->LoadFromBuffer(TokensIn20p);
	LSC->LoadFromBuffer(TokensIn50p);
	LSC->LoadFromBuffer(TokensIn100p);
	LSC->LoadFromBuffer(TokensIn200p);
	LSC->LoadFromBuffer(TokensIn);
	LSC->LoadFromBuffer(CoinsIn);
	LSC->LoadFromBuffer(TotalIn);
}

CashBoxClass::CashBoxClass(){

	LSC = NULL;
	CoinsIn2p = 0;
	CoinsIn5p = 0;
	CoinsIn10p = 0;
	CoinsIn20p = 0;
	CoinsIn50p = 0;
	CoinsIn100p = 0;
	CoinsIn200p = 0;
	TokensIn5p = 0;
	TokensIn10p = 0;
	TokensIn20p = 0;
	TokensIn50p = 0;
	TokensIn100p = 0;
	TokensIn200p = 0;
	TokensIn = 0;
	CoinsIn = 0; 
	TotalIn = 0;

}
CashBoxClass::~CashBoxClass(){
}

void CashBoxClass::CoinIn(UINT8 Coin){
	
	switch (Coin){
	case 0: CoinsIn2p += 1; CoinsIn += 2; break;
	case 1: CoinsIn5p += 1; CoinsIn += 5; break;
	case 2: CoinsIn10p += 1; CoinsIn += 10; break;
	case 3: CoinsIn20p += 1; CoinsIn += 20; break;
	case 4: CoinsIn50p += 1; CoinsIn += 50; break;
	case 5: CoinsIn100p += 1; CoinsIn += 100; break;
	case 6: CoinsIn200p += 1; CoinsIn += 200; break;
	case 7: TokensIn5p += 1; TokensIn += 5; break;
	case 8: TokensIn10p += 1; TokensIn += 10; break;
	case 9: TokensIn20p += 1; TokensIn += 20; break;
	case 10: TokensIn50p += 1; TokensIn += 50; break;
	case 11: TokensIn100p += 1; TokensIn += 100; break;
	case 12: TokensIn200p += 1; TokensIn += 200; break;
	}	
	TotalIn = (TokensIn + CoinsIn);
}

UINT32 CashBoxClass::GetCoinsIn(){
	return CoinsIn;
}
UINT32 CashBoxClass::GetTokensIn(){
	return TokensIn;
}
UINT32 CashBoxClass::GetTotalIn(){
	return TotalIn;
}

void CashBoxClass::Init(LoadSaveClass * LSCIn){
	LSC = LSCIn;
}


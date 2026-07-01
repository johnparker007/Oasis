#include "stdafx.h"
#include "CoinMechs.h"

void ElecronicCoinMech::SaveState(){
	UINT32 loop;

	LSC->SaveToBuffer(InputCounter);
	LSC->SaveToBuffer(LockCounter);
	LSC->SaveToBuffer(CommStyle);
	LSC->SaveToBuffer(CommInvert);
	LSC->SaveToBuffer(PulseCycles);
	LSC->SaveToBuffer(EDCEnable);

	for (loop = 0; loop < NUMCOINS; loop++){
		LSC->SaveToBuffer(LockoutVal[loop]);
		LSC->SaveToBuffer(LockoutInvert[loop]);
		LSC->SaveToBuffer(CoinValue[loop]);
		LSC->SaveToBuffer(CoinEnable[loop]);
	}

	LSC->SaveToBuffer(LockoutPort);
	LSC->SaveToBuffer(SelectedCoin);
	LSC->SaveToBuffer(LampOnOff[0]);
	LSC->SaveToBuffer(LampOnOff[1]);
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

void ElecronicCoinMech::LoadState(){
	UINT32 loop;

	LSC->LoadFromBuffer(InputCounter);
	LSC->LoadFromBuffer(LockCounter);
	LSC->LoadFromBuffer(CommStyle);
	LSC->LoadFromBuffer(CommInvert);
	LSC->LoadFromBuffer(PulseCycles);
	LSC->LoadFromBuffer(EDCEnable);

	for (loop = 0; loop < NUMCOINS; loop++){
		LSC->LoadFromBuffer(LockoutVal[loop]);
		LSC->LoadFromBuffer(LockoutInvert[loop]);
		LSC->LoadFromBuffer(CoinValue[loop]);
		LSC->LoadFromBuffer(CoinEnable[loop]);
	}

	LSC->LoadFromBuffer(LockoutPort);
	LSC->LoadFromBuffer(SelectedCoin);
	LSC->LoadFromBuffer(LampOnOff[0]);
	LSC->LoadFromBuffer(LampOnOff[1]);
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
ElecronicCoinMech::ElecronicCoinMech(){
	
	ZeroMemory(LockoutVal, NUMCOINS * sizeof(UINT8));
	ZeroMemory(LockoutInvert, NUMCOINS * sizeof(UINT8));
	ZeroMemory(CoinValue, NUMCOINS * sizeof(UINT8));
	ZeroMemory(CoinEnable, NUMCOINS * sizeof(UINT8));	
	ZeroMemory(LampOnOff, 2 * sizeof(UINT8));
	
}

ElecronicCoinMech::~ElecronicCoinMech(){

}

void ElecronicCoinMech::SetSelectedCoin(UINT8 Coin){
	SelectedCoin = Coin;
}

UINT8 ElecronicCoinMech::GetSelectedCoin(){
	return SelectedCoin;
}

UINT8 ElecronicCoinMech::GetLampOnOff(UINT8 Num){
	UINT8 ret;
	ret = LampOnOff[Num];
	return ret;
}

void ElecronicCoinMech::SetCoinValue(UINT8 Num, UINT8 Value){
	CoinValue[Num] = Value;
}

void ElecronicCoinMech::SetCoinEnable(UINT8 Num, UINT8 Value){
		CoinEnable[Num] = Value;
}
void ElecronicCoinMech::SetLockoutPort(UINT8 Port){
	
	UINT32 tokensEnabled = 0,
		cashEnabled = 0, 
		tokensLocked = 0, 
		cashLocked = 0;

	LockoutPort = Port;

	for (UINT32 i = 0; i < NUMCOINS; i++){
		if (CoinEnable[i]){
			if (CoinValue[i] < 7){
				cashEnabled += 1;
				if (!(LockoutPort & (1 << LockoutVal[i]))){
					cashLocked += 1;
				}
			} else {
				tokensEnabled += 1;
				if (!(LockoutPort & (1 << LockoutVal[i]))){
					tokensLocked += 1;
				}
			}
		}
	}
	
	//Lamp 1
	if (cashLocked == cashEnabled){
		LampOnOff[0] = 0;
	} else {
		LampOnOff[0] = 1;
	}
	//Lamp 2
	if (tokensLocked == tokensEnabled){
		LampOnOff[1] = 0;
	} else {
		LampOnOff[1] = 1;
	}	

}

UINT8 ElecronicCoinMech::CoinIn(UINT8 Coin){
	
	UINT32 LockoutBin;

	if (InputCounter == 0){
		if (LockCounter == 0){
			LockoutBin = (1 << LockoutVal[Coin]);
			if ((LockoutPort & LockoutBin)){
				if (PulseCycles < 1){
					return false;
				}
				InputCounter = PulseCycles;
				switch (CoinValue[Coin]){
				case 0: 
					CoinsIn2p += 1; CoinsIn += 2;
					BCD = 0x02;
					break;
				case 1: 
					CoinsIn5p += 1; CoinsIn += 5; 
					BCD = 0x05;
					break;
				case 2: 
					CoinsIn10p += 1; CoinsIn += 10; 
					BCD = 0x0A;
					break; 
				case 3: 
					CoinsIn20p += 1; CoinsIn += 20;  
					BCD = 0x14;
					break;
				case 4: 
					CoinsIn50p += 1; CoinsIn += 50;  
					BCD = 0x32;
					break;
				case 5: 
					CoinsIn100p += 1; CoinsIn += 100;  
					BCD = 0x3A;
					break;
				case 6: 
					CoinsIn200p += 1; CoinsIn += 200;  
					BCD = 0xC8;
					break;
				case 7: 
					TokensIn5p += 1; TokensIn += 5;  
					BCD = 0x5;
					break;
				case 8: 
					TokensIn10p += 1; TokensIn += 10;  
					BCD = 0xA;
					break;
				case 9: 
					TokensIn20p += 1; TokensIn += 20;  
					BCD = 0x14;
					break;
				case 10: 
					TokensIn50p += 1; TokensIn += 50;  
					BCD = 0x32;
					break;
				case 11: 
					TokensIn100p += 1; TokensIn += 100;  
					BCD = 0x64;
					break;
				case 12: 
					TokensIn200p += 1; TokensIn += 200; 
					BCD = 0xC8;
					break; 
				}
				TotalIn = (CoinsIn + TokensIn);
				return true;//Coin Accepted
			}
		}
	}

	return false; //Coin Rejected

}

UINT8 ElecronicCoinMech::GetBCD(){
	return BCD;
}
UINT8 ElecronicCoinMech::GetCommStyle(){
	return CommStyle;
}
UINT8 ElecronicCoinMech::GetCommInvert(){
	return CommInvert;
}
UINT8 ElecronicCoinMech::Run(UINT32 Cycles){

	UINT8 ret = 0;

	if (InputCounter){
		ret = 1;
		InputCounter -= Cycles;
		if (InputCounter < 0){
			InputCounter = 0;
			LockCounter = 2000000; //Quarter second = 8,000,000 / 4
		}
	}

	if (LockCounter){
		LockCounter -= Cycles;
		if (LockCounter < 0){
			LockCounter = 0;			
		}
	}

	return ret;
}	
void ElecronicCoinMech::Init(LoadSaveClass * LSCIn){
	LSC = LSCIn;
}	
void ElecronicCoinMech::SetCommStyle(UINT8 Style){
	CommStyle = Style;
}
void ElecronicCoinMech::SetCommInvert(UINT8 Invert){
	CommInvert = Invert;
}
void ElecronicCoinMech::SetCycles(UINT32 Cycles){
	PulseCycles = Cycles;
}
void ElecronicCoinMech::SetEDCEnable(UINT8 Enable){
	EDCEnable = Enable;
}
void ElecronicCoinMech::SetLockoutVal(UINT8 Coin, UINT8 Value){
	LockoutVal[Coin] = Value;
}
void ElecronicCoinMech::SetLockoutInvert(UINT8 Coin, UINT8 Invert){
	LockoutInvert[Coin] = Invert;
}
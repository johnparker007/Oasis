#include "stdafx.h"
#include "Meters.h"

CoinMeter::CoinMeter(){

	ZeroMemory(On, FITTEDMETERS * sizeof(UINT8));
	ZeroMemory(TimeOn, FITTEDMETERS * sizeof(UINT32));
	ZeroMemory(Pin, FITTEDMETERS * sizeof(UINT8));
	ZeroMemory(PrevPin, FITTEDMETERS * sizeof(UINT8));
	ZeroMemory(Enable, FITTEDMETERS * sizeof(UINT8));
	ZeroMemory(Counter, FITTEDMETERS * sizeof(UINT32));
	ZeroMemory(PortIndex, FITTEDMETERS * sizeof(UINT8));
}

CoinMeter::~CoinMeter(){

}

UINT32 CoinMeter::GetCounter(UINT8 Num){
	return Counter[Num];	
}
void CoinMeter::SetCounter(UINT8 Num, UINT32 Value){
	Counter[Num] = Value;
}

void CoinMeter::Write(UINT8 Index, UINT8 PinIn){

	if (Enable[Index]){
		if (PinIn){
			Pin[Index] = 1;
		} else {
			Pin[Index] = 0;
		}
	
		if (Pin[Index]){
			if (PrevPin[Index] == 0){
				On[Index] = 1;
			}
		} else {
			if (PrevPin[Index]){
				if (TimeOn[Index] > 80000){
					Counter[Index] += 1;
					if (Counter[Index] > 99999999) {
						Counter[Index] = 0;
					}
				}
				TimeOn[Index] = 0;
				On[Index] = 0;
			}			
		}

		PrevPin[Index] = Pin[Index];

	} else {
		Pin[Index] = 0;
		PrevPin[Index] = 0;
		On[Index] = 0;
		TimeOn[Index] = 0;
	}
	
}

void CoinMeter::Run(UINT32 Cycles){

	int Cnt;

	for (Cnt = 0; Cnt < FITTEDMETERS; Cnt++){
		if (Enable[Cnt]){
			if (On[Cnt]){
				TimeOn[Cnt] += Cycles;
			}
		}
	}

}

UINT8 CoinMeter::Check(void){

	int Cnt;
	UINT8 Ret;

	Ret = 0;

	for (Cnt = 0; Cnt < FITTEDMETERS; Cnt++){
		if (Enable[Cnt]){
			if (On[Cnt]){
				Ret = 1;
			}
		}
	}

	return Ret;
}

void CoinMeter::Init(LoadSaveClass * LSCIn){

	int cnt;

	LSC = LSCIn;

	for (cnt = 0; cnt < FITTEDMETERS; cnt++){
		Enable[cnt] = 1;

	}
}

void CoinMeter::SaveState(){

	int loop;

	for (loop = 0; loop < FITTEDMETERS; loop++){
		LSC->SaveToBuffer(Pin[loop]);
		LSC->SaveToBuffer(Enable[loop]);
		LSC->SaveToBuffer(PrevPin[loop]);
		LSC->SaveToBuffer(TimeOn[loop]);
		LSC->SaveToBuffer(On[loop]);
		LSC->SaveToBuffer(Counter[loop]);
		LSC->SaveToBuffer(PortIndex[loop]);
	}

}

void CoinMeter::LoadState(){

	int loop;

	for (loop = 0; loop < FITTEDMETERS; loop++){
		LSC->LoadFromBuffer(Pin[loop]);
		LSC->LoadFromBuffer(Enable[loop]);
		LSC->LoadFromBuffer(PrevPin[loop]);
		LSC->LoadFromBuffer(TimeOn[loop]);
		LSC->LoadFromBuffer(On[loop]);
		LSC->LoadFromBuffer(Counter[loop]);
		LSC->LoadFromBuffer(PortIndex[loop]);
	}

}
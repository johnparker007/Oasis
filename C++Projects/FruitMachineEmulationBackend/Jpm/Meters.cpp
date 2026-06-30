#include "stdafx.h"
#include "Meters.h"

CoinMeter::CoinMeter(){

	ZeroMemory(On, FITTEDMETERS * sizeof(unsigned char));
	ZeroMemory(TimeOn, FITTEDMETERS * sizeof(unsigned long));
	ZeroMemory(Pin, FITTEDMETERS * sizeof(unsigned char));
	ZeroMemory(PrevPin, FITTEDMETERS * sizeof(unsigned char));
	ZeroMemory(Enable, FITTEDMETERS * sizeof(unsigned char));
	ZeroMemory(Counter, FITTEDMETERS * sizeof(unsigned long));
	ZeroMemory(PortIndex, FITTEDMETERS * sizeof(unsigned char));
}

CoinMeter::~CoinMeter(){

}

unsigned long CoinMeter::GetCounter(unsigned char Num){
	return Counter[Num];	
}
void CoinMeter::SetCounter(unsigned char Num, unsigned long Value){
	Counter[Num] = Value;
}

void CoinMeter::Write(unsigned char Index, unsigned char PinIn){

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

void CoinMeter::Run(unsigned short Cycles){

	int Cnt;

	for (Cnt = 0; Cnt < FITTEDMETERS; Cnt++){
		if (Enable[Cnt]){
			if (On[Cnt]){
				TimeOn[Cnt] += Cycles;
			}
		}
	}

}

unsigned char CoinMeter::Check(void){

	int Cnt;
	unsigned char Ret;

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

void CoinMeter::Init(LoadSaveCompressDLLClass * LSCIn){

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
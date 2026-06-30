#include "stdafx.h"
#include "Solenoids.h"
#include "iostream"

SolenoidPayout::SolenoidPayout(){

	ZeroMemory(Pin, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(Enable, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(PrevPin, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(CounterIn, NUMSOLENOIDS * sizeof(unsigned long));
	ZeroMemory(CounterOut, NUMSOLENOIDS * sizeof(unsigned long));
	ZeroMemory(PortIndex, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(Coin, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(Level, NUMSOLENOIDS * sizeof(signed long));
	ZeroMemory(LoEnable, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(LoSwitch, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(LoState, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(LoInvert, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(LoLevel, NUMSOLENOIDS * sizeof(signed long));
	ZeroMemory(HiEnable, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(HiSwitch, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(HiLevel, NUMSOLENOIDS * sizeof(signed long));
	ZeroMemory(HiState, NUMSOLENOIDS * sizeof(unsigned char));
	ZeroMemory(HiInvert, NUMSOLENOIDS * sizeof(unsigned char));	
	ZeroMemory(FullLevel, NUMSOLENOIDS * sizeof(signed long));
}

SolenoidPayout::~SolenoidPayout(){
}

void SolenoidPayout::Write(unsigned char PinIn){

	int loop;

	for (loop = 0; loop < NUMSOLENOIDS; loop++){
		if (Enable[loop]){		
			if (PinIn & (1 << PortIndex[loop])){
				Pin[loop] = 1;
			} else {
				Pin[loop] = 0;
			}
	
			if (Pin[loop]){
				if (PrevPin[loop] == 0){
					CounterOut[loop] += 1;
					if (CounterOut[loop] > 99999999) {
						CounterOut[loop] = 99999999;
					}

					if (Level[loop] > 0){
						Level[loop] -= 1;
					}
					Update();
				}		
			}
			PrevPin[loop] = Pin[loop];
		}
	}
	
}

void SolenoidPayout::Update(void){

	unsigned char cnt;

	for (cnt = 0; cnt < NUMSOLENOIDS; cnt++){
		//Is Lo Switch Enabled
		if (LoEnable[cnt]){
			//Check level against lo level
			if (Level[cnt] < LoLevel[cnt]){
				//Tube Level Low
				LoState[cnt] = 0;
			} else {
				//Adequate Level
				LoState[cnt] = 1;
			}
		} else {
			//Switch Disabled
			LoState[cnt] = 0;			
		}

		//Is High Switch Enabled
		if (HiEnable[cnt]){
			//Check Level against Hi Level
			if (Level[cnt] < HiLevel[cnt]){
				//Tubes Not Full
				HiState[cnt] = 0;
			} else {
				//Tubes Full
				HiState[cnt] = 1;
			}
		} else {
			//Switch Disabled
			HiState[cnt] = 0;			
		}
	}

}

void SolenoidPayout::Init(LoadSaveCompressDLLClass * LSCIn){

	LSC = LSCIn;

	unsigned char cnt;

	for (cnt = 0; cnt < NUMSOLENOIDS; cnt++){
		Pin[cnt] = 0;
		PrevPin[cnt] = 0;
	}

	Update();

}

unsigned char SolenoidPayout::CoinIn(unsigned char CoinCode){
	
	/*
	Case 0: TStr = "2p Cash In"
	Case 1: TStr = "5p Cash In"
    Case 2: TStr = "10p Cash In"
    Case 3: TStr = "20p Cash In"
    Case 4: TStr = "50p Cash In"
    Case 5: TStr = "£1 Cash In"
    Case 6: TStr = "£2 Cash In"    
    Case 7: TStr = "5p Token In"
    Case 8: TStr = "10p Token In"
    Case 9: TStr = "20p Token In"
    Case 10: TStr = "50p Token In"
    Case 11: TStr = "£1 Token In"
    Case 12: TStr = "£2 Token In"    
	*/

	signed short cnt;
	unsigned char SolIndex;
	unsigned char CoinDrop;
	SolIndex = 0xff;
	CoinDrop = 0;
	
	//Valid Coin Input
	if ((CoinCode >= 0) && (CoinCode < 13)){		
		//Step Through Each Solenoid
		for (cnt = 0; cnt < NUMSOLENOIDS; cnt++){
			//Check Enabled
			if (Enable[cnt]){
				//Set Solenoid Index
				if (CoinCode == Coin[cnt]){
					SolIndex = (cnt & 0xff);				
					//Check Solindex is Valid
					if (SolIndex != 0xff){
						//Check Level against Full Level
						if (Level[SolIndex] < FullLevel[SolIndex]){
							//Not Full
							break;
						} else {
							//Tube Full, set SolIndex to invalid
							SolIndex = 0xff;
							//Increment times coin has dropped
							CoinDrop++;
						}
					}
				}	
			}
		}

		if (SolIndex != 0xff){//We found a tube with this coin
			//Increment tube level
			Level[SolIndex] += 1;	
			//Increment Coins In Counter
			CounterIn[SolIndex] += 1;
			Update();							
		} else {	
			//No Tube found or Tubes full
			//Drop To Cashbox
		}	
	}

	return SolIndex;

}
unsigned char SolenoidPayout::GetLoState(unsigned char Num){
	return LoState[Num];
}
unsigned char SolenoidPayout::GetHiState(unsigned char Num){
	return HiState[Num];
}
void SolenoidPayout::SetEnable(unsigned char Num, unsigned char Enabl){
	Enable[Num] = Enabl;
	Update();
}
void SolenoidPayout::SetCounterIn(unsigned char Num, unsigned long Count){
	CounterIn[Num] = Count;
}
void SolenoidPayout::SetCounterOut(unsigned char Num, unsigned long Count){
	CounterOut[Num] = Count;
}
void SolenoidPayout::SetPortIndex(unsigned char Num, unsigned char Index){
	PortIndex[Num] = Index;
	Update();
}
void SolenoidPayout::SetCoin(unsigned char Num, unsigned char CoinIn){
	Coin[Num] = CoinIn;
}
void SolenoidPayout::SetLevel(unsigned char Num, unsigned char LevelIn){
	Level[Num] = LevelIn;
}
void SolenoidPayout::SetFullLevel(unsigned char Num, unsigned char Level){
	FullLevel[Num] = Level;
}
void SolenoidPayout::SetLoEnable(unsigned char Num, unsigned char Enabl){
	LoEnable[Num] = Enabl;
	Update();
}
void SolenoidPayout::SetLoInvert(unsigned char Num, unsigned char Invert){
	LoInvert[Num] = Invert;
	Update();
}
void SolenoidPayout::SetLoSwitch(unsigned char Num, unsigned char Switch){
	LoSwitch[Num] = Switch;
	Update();
}
void SolenoidPayout::SetLoLevel(unsigned char Num, signed long LevelIn){
	LoLevel[Num] = LevelIn;
	Update();
}
void SolenoidPayout::SetHiEnable(unsigned char Num, unsigned char Enabl){
	HiEnable[Num] = Enabl;
	Update();
}
void SolenoidPayout::SetHiInvert(unsigned char Num, unsigned char Invert){
	HiInvert[Num] = Invert;
	Update();
}
void SolenoidPayout::SetHiSwitch(unsigned char Num, unsigned char Switch){
	HiSwitch[Num] = Switch;
	Update();
}
void SolenoidPayout::SetHiLevel(unsigned char Num, signed long LevelIn){
	HiLevel[Num] = LevelIn;
	Update();
}
void SolenoidPayout::SetPort(unsigned char Port){

}
unsigned char SolenoidPayout::GetEnable(unsigned char Num){
	unsigned char ret;
	ret = Enable[Num];
	return ret;
}
unsigned long SolenoidPayout::GetCounterIn(unsigned char Num){
	unsigned long ret;
	ret = CounterIn[Num];
	return ret;
}
unsigned long SolenoidPayout::GetCounterOut(unsigned char Num){
	unsigned long ret;
	ret = CounterOut[Num];
	return ret;
}
unsigned char SolenoidPayout::GetPortIndex(unsigned char Num){
	unsigned char ret;
	ret = PortIndex[Num];
	return ret;
}
unsigned char SolenoidPayout::GetCoin(unsigned char Num){
	unsigned char ret;
	ret = Coin[Num];
	return ret;
}
signed long SolenoidPayout::GetLevel(unsigned char Num){
	signed long ret;
	ret = Level[Num];
	return ret;
}
signed long SolenoidPayout::GetFullLevel(unsigned char Num){
	signed long ret;
	ret = FullLevel[Num];
	return ret;
}
unsigned char SolenoidPayout::GetLoEnable(unsigned char Num){
	unsigned char ret;
	ret = LoEnable[Num];
	return ret;
}
unsigned char SolenoidPayout::GetLoInvert(unsigned char Num){
	unsigned char ret;
	ret = LoInvert[Num];
	return ret;
}
unsigned char SolenoidPayout::GetLoSwitch(unsigned char Num){
	unsigned char ret;
	ret = LoSwitch[Num];
	return ret;
}
signed long SolenoidPayout::GetLoLevel(unsigned char Num){
	signed long ret;
	ret = LoLevel[Num];
	return ret;
}
unsigned char SolenoidPayout::GetHiEnable(unsigned char Num){
	unsigned char ret;
	ret = HiEnable[Num];
	return ret;
}
unsigned char SolenoidPayout::GetHiInvert(unsigned char Num){
	unsigned char ret;
	ret = HiInvert[Num];
	return ret;
}
unsigned char SolenoidPayout::GetHiSwitch(unsigned char Num){
	unsigned char ret;
	ret = HiSwitch[Num];
	return ret;
}
signed long SolenoidPayout::GetHiLevel(unsigned char Num){
	signed long ret;
	ret = HiLevel[Num];
	return ret;
}

void SolenoidPayout::SaveState(){

	int loop;

	for (loop = 0; loop < NUMSOLENOIDS; loop++){
		LSC->SaveToBuffer(Pin[loop]);	
		LSC->SaveToBuffer(Enable[loop]);	
		LSC->SaveToBuffer(PrevPin[loop]);		
		LSC->SaveToBuffer(CounterIn[loop]);
		LSC->SaveToBuffer(CounterOut[loop]);	
		LSC->SaveToBuffer(PortIndex[loop]);	
		LSC->SaveToBuffer(Coin[loop]);	
		LSC->SaveToBuffer(Level[loop]);
		LSC->SaveToBuffer(LoEnable[loop]);
		LSC->SaveToBuffer(LoSwitch[loop]);
		LSC->SaveToBuffer(LoState[loop]);
		LSC->SaveToBuffer(LoInvert[loop]);
		LSC->SaveToBuffer(LoLevel[loop]);
		LSC->SaveToBuffer(HiEnable[loop]);
		LSC->SaveToBuffer(HiSwitch[loop]);
		LSC->SaveToBuffer(HiLevel[loop]);
		LSC->SaveToBuffer(HiState[loop]);
		LSC->SaveToBuffer(HiInvert[loop]);
		LSC->SaveToBuffer(FullLevel[loop]);
	}

}

void SolenoidPayout::LoadState(){

	int loop;

	for (loop = 0; loop < NUMSOLENOIDS; loop++){
		LSC->LoadFromBuffer(Pin[loop]);	
		LSC->LoadFromBuffer(Enable[loop]);	
		LSC->LoadFromBuffer(PrevPin[loop]);		
		LSC->LoadFromBuffer(CounterIn[loop]);
		LSC->LoadFromBuffer(CounterOut[loop]);	
		LSC->LoadFromBuffer(PortIndex[loop]);	
		LSC->LoadFromBuffer(Coin[loop]);	
		LSC->LoadFromBuffer(Level[loop]);
		LSC->LoadFromBuffer(LoEnable[loop]);
		LSC->LoadFromBuffer(LoSwitch[loop]);
		LSC->LoadFromBuffer(LoState[loop]);
		LSC->LoadFromBuffer(LoInvert[loop]);
		LSC->LoadFromBuffer(LoLevel[loop]);
		LSC->LoadFromBuffer(HiEnable[loop]);
		LSC->LoadFromBuffer(HiSwitch[loop]);
		LSC->LoadFromBuffer(HiLevel[loop]);
		LSC->LoadFromBuffer(HiState[loop]);
		LSC->LoadFromBuffer(HiInvert[loop]);
		LSC->LoadFromBuffer(FullLevel[loop]);
	}

}
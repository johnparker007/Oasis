#include "stdafx.h"
#include "Solenoids.h"
#include "iostream"

SolenoidPayout::SolenoidPayout(){

	ZeroMemory(Pin, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(Enable, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(PrevPin, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(CounterIn, NUMSOLENOIDS * sizeof(UINT32));
	ZeroMemory(CounterOut, NUMSOLENOIDS * sizeof(UINT32));
	ZeroMemory(PortIndex, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(Coin, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(Level, NUMSOLENOIDS * sizeof(UINT32));
	ZeroMemory(LoEnable, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(LoSwitch, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(LoState, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(LoInvert, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(LoLevel, NUMSOLENOIDS * sizeof(UINT32));
	ZeroMemory(HiEnable, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(HiSwitch, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(HiLevel, NUMSOLENOIDS * sizeof(UINT32));
	ZeroMemory(HiState, NUMSOLENOIDS * sizeof(UINT8));
	ZeroMemory(HiInvert, NUMSOLENOIDS * sizeof(UINT8));	
	ZeroMemory(FullLevel, NUMSOLENOIDS * sizeof(UINT32));
}

SolenoidPayout::~SolenoidPayout(){
}

void SolenoidPayout::Write(UINT8 PinIn){

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

	UINT8 cnt;

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

void SolenoidPayout::Init(LoadSaveClass * LSCIn){

	LSC = LSCIn;

	UINT8 cnt;

	for (cnt = 0; cnt < NUMSOLENOIDS; cnt++){
		Pin[cnt] = 0;
		PrevPin[cnt] = 0;
	}

	Update();

}

UINT8 SolenoidPayout::CoinIn(UINT8 CoinCode){
	
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
	UINT8 SolIndex;
	UINT8 CoinDrop;
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
UINT8 SolenoidPayout::GetLoState(UINT8 Num){
	return LoState[Num];
}
UINT8 SolenoidPayout::GetHiState(UINT8 Num){
	return HiState[Num];
}
void SolenoidPayout::SetEnable(UINT8 Num, UINT8 Enabl){
	Enable[Num] = Enabl;
	Update();
}
void SolenoidPayout::SetCounterIn(UINT8 Num, UINT32 Count){
	CounterIn[Num] = Count;
}
void SolenoidPayout::SetCounterOut(UINT8 Num, UINT32 Count){
	CounterOut[Num] = Count;
}
void SolenoidPayout::SetPortIndex(UINT8 Num, UINT8 Index){
	PortIndex[Num] = Index;
	Update();
}
void SolenoidPayout::SetCoin(UINT8 Num, UINT8 CoinIn){
	Coin[Num] = CoinIn;
}
void SolenoidPayout::SetLevel(UINT8 Num, UINT8 LevelIn){
	Level[Num] = LevelIn;
}
void SolenoidPayout::SetFullLevel(UINT8 Num, UINT8 Level){
	FullLevel[Num] = Level;
}
void SolenoidPayout::SetLoEnable(UINT8 Num, UINT8 Enabl){
	LoEnable[Num] = Enabl;
	Update();
}
void SolenoidPayout::SetLoInvert(UINT8 Num, UINT8 Invert){
	LoInvert[Num] = Invert;
	Update();
}
void SolenoidPayout::SetLoSwitch(UINT8 Num, UINT8 Switch){
	LoSwitch[Num] = Switch;
	Update();
}
void SolenoidPayout::SetLoLevel(UINT8 Num, UINT32 LevelIn){
	LoLevel[Num] = LevelIn;
	Update();
}
void SolenoidPayout::SetHiEnable(UINT8 Num, UINT8 Enabl){
	HiEnable[Num] = Enabl;
	Update();
}
void SolenoidPayout::SetHiInvert(UINT8 Num, UINT8 Invert){
	HiInvert[Num] = Invert;
	Update();
}
void SolenoidPayout::SetHiSwitch(UINT8 Num, UINT8 Switch){
	HiSwitch[Num] = Switch;
	Update();
}
void SolenoidPayout::SetHiLevel(UINT8 Num, UINT32 LevelIn){
	HiLevel[Num] = LevelIn;
	Update();
}
void SolenoidPayout::SetPort(UINT8 Port){

}
UINT8 SolenoidPayout::GetEnable(UINT8 Num){
	UINT8 ret;
	ret = Enable[Num];
	return ret;
}
UINT32 SolenoidPayout::GetCounterIn(UINT8 Num){
	UINT32 ret = CounterIn[Num];
	return ret;
}
UINT32 SolenoidPayout::GetCounterOut(UINT8 Num){
	UINT32 ret = CounterOut[Num];
	return ret;
}
UINT8 SolenoidPayout::GetPortIndex(UINT8 Num){
	UINT8 ret = PortIndex[Num];
	return ret;
}
UINT8 SolenoidPayout::GetCoin(UINT8 Num){
	UINT8 ret = Coin[Num];
	return ret;
}
UINT32 SolenoidPayout::GetLevel(UINT8 Num){
	UINT32 ret = Level[Num];
	return ret;
}
UINT32 SolenoidPayout::GetFullLevel(UINT8 Num){
	UINT32 ret = FullLevel[Num];
	return ret;
}
UINT8 SolenoidPayout::GetLoEnable(UINT8 Num){
	UINT8 ret = LoEnable[Num];
	return ret;
}
UINT8 SolenoidPayout::GetLoInvert(UINT8 Num){
	UINT8 ret = LoInvert[Num];
	return ret;
}
UINT8 SolenoidPayout::GetLoSwitch(UINT8 Num){
	UINT8 ret = LoSwitch[Num];
	return ret;
}
UINT32 SolenoidPayout::GetLoLevel(UINT8 Num){
	UINT32 ret = LoLevel[Num];
	return ret;
}
UINT8 SolenoidPayout::GetHiEnable(UINT8 Num){
	UINT8 ret = HiEnable[Num];
	return ret;
}
UINT8 SolenoidPayout::GetHiInvert(UINT8 Num){
	UINT8 ret = HiInvert[Num];
	return ret;
}
UINT8 SolenoidPayout::GetHiSwitch(UINT8 Num){
	UINT8 ret = HiSwitch[Num];
	return ret;
}
UINT32 SolenoidPayout::GetHiLevel(UINT8 Num){
	UINT32 ret = HiLevel[Num];
	return ret;
}

void SolenoidPayout::SaveState(){

	UINT32 loop;

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

	UINT32 loop;

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
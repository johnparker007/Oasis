#include "stdafx.h"
#include "Segments.h"
#include "iostream"

void Segs::Reset(LoadSaveClass * LSCIn){

	INT32 cnt;

	LSC = LSCIn;

	for (cnt = 0; cnt < NUMSEGMENTS; cnt++) {		
		On[cnt] = 0;
		Brightness[cnt] = 0;
		State[cnt] = 0;
		SavedState[cnt] = 0;
		LastState[cnt] = 0;
		MaxOnCount[cnt] = 0;
		OnCount[cnt] = 0;
		OffCount[cnt] = 0;
		LastOnCount[cnt] = 0;
		LastOffCount[cnt] = 0;
		MaxTimeOn[cnt] = 0;
		MaxTimeOff[cnt] = 0;
		MinDimLevel[cnt] = 0;
		//TEST VALUES - REMOVE!
		GlowTime[cnt] = 4651;
		FadeTime[cnt] = 534883;
	}

	//## External Settings NOT Reset ##
	for (cnt = 0; cnt < 16; cnt++) {
		MuxGapCount[cnt] = 0;
		SuperBrightEnable[cnt] = 0;
	}

	for (cnt = 0; cnt < 16; cnt++) {
		SegColumnData[cnt] = 0;
		LastSegColumnData[cnt] = 0;
	}
	
	TimeSinceDataChange = 0;
	MuxValue = 0;
	LastMuxValue = 0;
	MuxStart = 0;
	MuxEnd = 0;
	SuperBrightMux = 0;
	MuxGapStat = 0;
	SuperBrightLevel = 0;
	Toggle = 0;
}

void Segs::WriteJPMSegs(UINT16 data){
//This sub should be called every time data is written to the lamps

	INT32 cnt;
	INT32 cnt2;
	INT32 tval;
	INT32 tval2;

	LastSegColumnData[MuxValue] = SegColumnData[MuxValue];
	SegColumnData[MuxValue] = data;

	tval2 = (MuxValue * 16);

	for (cnt = 0; cnt < 256; cnt++) {
		if ((cnt < tval2) || (cnt > (tval2 + 15))) {
			//## Lamp Unpowered ##
			
			//Update Counts
			OffCount[cnt] += TimeSinceDataChange;
			OnCount[cnt] -= TimeSinceDataChange;
			//Trap Overflows
			if (OnCount[cnt] < -5000000){
				OnCount[cnt] = -5000000;
			}
			if (OffCount[cnt] > 5000000){
				OffCount[cnt] = 5000000;
			}
		} else {
			for (cnt2 = 0; cnt2 < 16; cnt2++) {
				tval = (tval2 + cnt2);
				if (LastSegColumnData[MuxValue] & (1 << cnt2)){
					//## Lamp Powered ##
					if (OnCount[tval] < 0) {
						OnCount[tval] = 0; //Always start counting again from 0
					}
					//Increment On Count
					OnCount[tval] += TimeSinceDataChange;
					//If on an upward trend set new maximum
					if (LastOnCount[tval] < OnCount[tval]) {
						MaxOnCount[tval]  = OnCount[tval];
					}
					//Set The Last On Count
					LastOnCount[tval] = OnCount[tval];
					//Reset the Off Count
					OffCount[tval] = 0;
					//Trap Overflow
					if (OnCount[tval] > 5000000){
						OnCount[tval] = 5000000;
					}
				} else {
					//## Lamp Unpowered ##
					//Update Counts
					OffCount[tval] += TimeSinceDataChange;
					OnCount[tval] -= TimeSinceDataChange;
					//Trap Overflows
					if (OnCount[tval] < -5000000){
						OnCount[tval] = -5000000;
					}
					if (OffCount[tval] > 5000000){
						OffCount[tval] = 5000000;
					}
				}
			}
		}
	}

	TimeSinceDataChange = 0;
}



 INT32 Segs::GetSegState(UINT16 Num, UINT32 OnCycles, UINT32 OffCycles){
//This sub gets the current lamp brightness for a given lamp number.

	INT32 cnt;
	UINT32 Ret;
	UINT32 Ret2;
	UINT32 TMaxOn;
	UINT32 TMaxOff;

	 MaxTimeOff[Num] = (FadeTime[Num] / 63);
	 MaxTimeOn[Num] = (GlowTime[Num] / 63);

	 
	 if (OffCycles == 0) {
		for (cnt = 63; cnt >= 0; cnt--){
			TMaxOn = (MaxTimeOn[Num] * cnt);
			if (OnCycles > TMaxOn){
				Ret = cnt;
				break;
			} else {
				Ret = 0;
			}
		}		 
		 if (LastState[Num] > Ret) {
			 Ret = LastState[Num];
		 }
	 } else {
		for (cnt = 63; cnt >= 0; cnt--){
			TMaxOn = (MaxTimeOn[Num] * cnt);
			if (OnCycles > TMaxOn){
				Ret = cnt;
				break;
			} else {
				Ret = 0;
			}
		}
		for (cnt = 63; cnt >= 0; cnt--){
			TMaxOff = (MaxTimeOff[Num] * cnt);
			if (OffCycles > TMaxOff){
				Ret2 = cnt;
				break;
			} else {
				Ret2 = 0;
			}
		}
		Ret -= Ret2;
		LastState[Num] = Ret;
	 }

	 return Ret;
 }

 void Segs::RunJPMSegs(UINT16 InstructionCycles, UINT64 TotalCycles){
//This sub should be run after every instruction.

	UINT16 cnt;
	INT32 cnt2;
	INT32 tval;
	INT32 tval2;

	TimeSinceDataChange += InstructionCycles;
	
	if (LastMuxValue != MuxValue){ //Check for mux data change, if not no need to run this
		
		LastMuxValue = MuxValue; //Do this here

		MuxEnd = TotalCycles;
		MuxGapStat = (MuxEnd - MuxStart);

		for (cnt = 0; cnt < 16; cnt++) {
			if (LastMuxValue == cnt) {
				MuxGapCount[cnt] += MuxGapStat;
				if (MuxGapCount[cnt] > 10000000) {
					MuxGapCount[cnt] = 0;
				}
				if (MuxGapCount[cnt] > 30000) {
					SuperBrightLevel = MuxGapCount[cnt];
                    SuperBrightEnable[cnt] = 255;
				} else {
					SuperBrightEnable[cnt] = 0;
				}
			} else {
				MuxGapCount[cnt] -= MuxGapStat;
				if (MuxGapCount[cnt] < 0){
					MuxGapCount[cnt] = 0;
				}
				if (MuxGapCount[cnt] > 10000000){
					MuxGapCount[cnt] = 0;
				}
			}
		}

		MuxStart = TotalCycles;

		if (MuxDrive) { //Optional, update lamping on Mux change as well as data change

			tval2 = (MuxValue * 16);

			for (cnt = 0; cnt < 256; cnt++) {
				if ((cnt < tval2) || (cnt > (tval2 + 15))) {
					//## Lamp Unpowered ##
			
					//Update Counts
					OffCount[cnt] += TimeSinceDataChange;
					OnCount[cnt] -= TimeSinceDataChange;
					//Trap Overflows
					if (OnCount[cnt] < -5000000){
						OnCount[cnt] = -5000000;
					}
					if (OffCount[cnt] > 5000000){
						OffCount[cnt] = 5000000;
					}
				} else {
					for (cnt2 = 0; cnt2 < 15; cnt2++) {
						tval = (tval2 + cnt2);
						if (tval < 0) tval = 0;

						if (LastSegColumnData[MuxValue] & (1 << cnt2)){
							//## Lamp Powered ##
							if (OnCount[tval] < 0) {
								OnCount[tval] = 0; //Always start counting again from 0
							}
							
							//Increment On Count
							OnCount[tval] += TimeSinceDataChange;
							//If on an upward trend set new maximum
							if (LastOnCount[tval] < OnCount[tval]) {
								MaxOnCount[tval]  = OnCount[tval];
							}
							//Set The Last On Count
							LastOnCount[tval] = OnCount[tval];
							//Reset the Off Count
							OffCount[tval] = 0;
							//Trap Overflow
							if (OnCount[tval] > 5000000){
								OnCount[tval] = 5000000;
							}
						} else {
							//## Lamp Unpowered ##

							//Update Counts
							OffCount[tval] += TimeSinceDataChange;
							OnCount[tval] -= TimeSinceDataChange;
							//Trap Overflows
							if (OnCount[tval] < -5000000){
								OnCount[tval] = -5000000;
							}
							if (OffCount[tval] > 5000000){
								OffCount[tval] = 5000000;
							}
						}
					}
				}
			}

			TimeSinceDataChange = 0;

		}
	} //Run Mux End
 }

void Segs::Update(){

	signed short cnt;
	INT32 LampTemp;
	float LampIntensity;

	for (cnt = 0; cnt < 256; cnt++){			
		if (OffCount[cnt] < 1){
			State[cnt] = GetSegState(cnt, OnCount[cnt], 0);
		} else {
			State[cnt] = GetSegState(cnt, MaxOnCount[cnt], OffCount[cnt]);
		}

		//if (State[cnt] != SavedState[cnt]){
			if (State[cnt] < 1){
				//if (SavedState[cnt] > 0){
					On[cnt] = 0;
					Brightness[cnt] = 0;
				//}
			} else {
				LampTemp = State[cnt];
				if (LampTemp < 0){
					LampTemp = 0;
				}
				On[cnt] = 1;
				Brightness[cnt] = (((LampTemp * 2) + 129) & 0xff);
				if (Brightness[cnt] < MinDimLevel[cnt]){
					Brightness[cnt] = MinDimLevel[cnt];
				}
				LampIntensity = ((1.0f / 255) * Intensity);	 
				Brightness[cnt] = int(LampIntensity * float(Brightness[cnt]));
			}
			SavedState[cnt] = State[cnt];
		}
	//}


}

void Segs::SetIntensity(UINT8 Intense){
	Intensity = Intense;
}

UINT8 Segs::GetOn(UINT8 segNum)
{
	return On[segNum];
}

UINT8 Segs::GetBrightness(UINT8 segNum)
{
	return Brightness[segNum];
}

void Segs::SetMuxValue(UINT8 value)
{
	MuxValue = value;
}

void Segs::SetLastMuxValue(UINT8 value)
{
	LastMuxValue = value;
}

void Segs::SaveState(){
	
	int loop;

	for (loop = 0; loop < 256; loop++){
		LSC->SaveToBuffer(On[loop]);
		LSC->SaveToBuffer(Brightness[loop]);
		LSC->SaveToBuffer(State[loop]);
		LSC->SaveToBuffer(SavedState[loop]);
		LSC->SaveToBuffer(LastState[loop]);
		LSC->SaveToBuffer(MaxOnCount[loop]);
		LSC->SaveToBuffer(OffCount[loop]);
		LSC->SaveToBuffer(LastOnCount[loop]);
		LSC->SaveToBuffer(LastOffCount[loop]);
		LSC->SaveToBuffer(MaxTimeOn[loop]);
		LSC->SaveToBuffer(MaxTimeOff[loop]);
		LSC->SaveToBuffer(FadeTime[loop]);
		LSC->SaveToBuffer(GlowTime[loop]);
		LSC->SaveToBuffer(FadeTimeStore[loop]);
		LSC->SaveToBuffer(GlowTimeStore[loop]);
		LSC->SaveToBuffer(MinDimLevel[loop]);
	}

	for (loop = 0; loop < 16; loop++){
		LSC->SaveToBuffer(MuxGapCount[loop]);
		LSC->SaveToBuffer(SuperBrightEnable[loop]);
		LSC->SaveToBuffer(SegColumnData[loop]);
		LSC->SaveToBuffer(LastSegColumnData[loop]);
	}

	LSC->SaveToBuffer(MuxValue);
	LSC->SaveToBuffer(LastMuxValue);
	LSC->SaveToBuffer(MuxStart);
	LSC->SaveToBuffer(MuxEnd);
	LSC->SaveToBuffer(SuperBrightMux);
	LSC->SaveToBuffer(MuxGapStat);
	LSC->SaveToBuffer(SuperBrightLevel);
	LSC->SaveToBuffer(TimeSinceDataChange);
	LSC->SaveToBuffer(MuxDrive);
	LSC->SaveToBuffer(Toggle);
	LSC->SaveToBuffer(Intensity);


}

void Segs::LoadState(){

	int loop;

	for (loop = 0; loop < 256; loop++){
		LSC->LoadFromBuffer(On[loop]);
		LSC->LoadFromBuffer(Brightness[loop]);
		LSC->LoadFromBuffer(State[loop]);
		LSC->LoadFromBuffer(SavedState[loop]);
		LSC->LoadFromBuffer(LastState[loop]);
		LSC->LoadFromBuffer(MaxOnCount[loop]);
		LSC->LoadFromBuffer(OffCount[loop]);
		LSC->LoadFromBuffer(LastOnCount[loop]);
		LSC->LoadFromBuffer(LastOffCount[loop]);
		LSC->LoadFromBuffer(MaxTimeOn[loop]);
		LSC->LoadFromBuffer(MaxTimeOff[loop]);
		LSC->LoadFromBuffer(FadeTime[loop]);
		LSC->LoadFromBuffer(GlowTime[loop]);
		LSC->LoadFromBuffer(FadeTimeStore[loop]);
		LSC->LoadFromBuffer(GlowTimeStore[loop]);
		LSC->LoadFromBuffer(MinDimLevel[loop]);
	}

	for (loop = 0; loop < 16; loop++){
		LSC->LoadFromBuffer(MuxGapCount[loop]);
		LSC->LoadFromBuffer(SuperBrightEnable[loop]);
		LSC->LoadFromBuffer(SegColumnData[loop]);
		LSC->LoadFromBuffer(LastSegColumnData[loop]);
	}

	LSC->LoadFromBuffer(MuxValue);
	LSC->LoadFromBuffer(LastMuxValue);
	LSC->LoadFromBuffer(MuxStart);
	LSC->LoadFromBuffer(MuxEnd);
	LSC->LoadFromBuffer(SuperBrightMux);
	LSC->LoadFromBuffer(MuxGapStat);
	LSC->LoadFromBuffer(SuperBrightLevel);
	LSC->LoadFromBuffer(TimeSinceDataChange);
	LSC->LoadFromBuffer(MuxDrive);
	LSC->LoadFromBuffer(Toggle);
	LSC->LoadFromBuffer(Intensity);

}
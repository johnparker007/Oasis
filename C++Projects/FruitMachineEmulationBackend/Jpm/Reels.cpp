#include "stdafx.h"
#include "Reels.h"
#include "IOStream"
#include "Exception"


//Direction Values for Reel Positions
#define Point_NW 0
#define Point_W  1
#define Point_SW 2
#define Point_S  3 
#define Point_SE 4
#define Point_E  5
#define Point_NE 6
#define Point_N	 7

void ReelDrive::Initialise(LoadSaveCompressDLLClass * LSCIn){

	LSC =LSCIn;

	StarpointReel[0][0] = 0;
    StarpointReel[0][1] = 0;
    StarpointReel[0][2] = 0;
    StarpointReel[0][3] = 0;
    StarpointReel[0][4] = 2;
    StarpointReel[0][5] = 1;
    StarpointReel[0][6] = 3;
    StarpointReel[0][7] = 0;
    StarpointReel[0][8] = -2;
    StarpointReel[0][9] = -1;
    StarpointReel[0][10] = -3;
    StarpointReel[0][11] = 0;
    StarpointReel[0][12] = 0;
    StarpointReel[0][13] = 0;
    StarpointReel[0][14] = 0;
    StarpointReel[0][15] = 0;
    
    StarpointReel[1][0] = 0;
    StarpointReel[1][1] = -1;
    StarpointReel[1][2] = 3;
    StarpointReel[1][3] = 0;
    StarpointReel[1][4] = 1;
    StarpointReel[1][5] = 0;
    StarpointReel[1][6] = 2;
    StarpointReel[1][7] = 0;
    StarpointReel[1][8] = -3;
    StarpointReel[1][9] = -2;
    StarpointReel[1][10] = 0;
    StarpointReel[1][11] = 0;
    StarpointReel[1][12] = 0;
    StarpointReel[1][13] = 0;
    StarpointReel[1][14] = 0;
    StarpointReel[1][15] = 0;
    
    StarpointReel[2][0] = 0;
    StarpointReel[2][1] = -2;
    StarpointReel[2][2] = 2;
    StarpointReel[2][3] = 0;
    StarpointReel[2][4] = 0;
    StarpointReel[2][5] = -1;
    StarpointReel[2][6] = 1;
    StarpointReel[2][7] = 0;
    StarpointReel[2][8] = 0;
    StarpointReel[2][9] = -3;
    StarpointReel[2][10] = 3;
    StarpointReel[2][11] = 0;
    StarpointReel[2][12] = 0;
    StarpointReel[2][13] = 0;
    StarpointReel[2][14] = 0;
    StarpointReel[2][15] = 0;
    
    StarpointReel[3][0] = 0;
    StarpointReel[3][1] = -3;
	StarpointReel[3][2] = 1;
    StarpointReel[3][3] = 0;
    StarpointReel[3][4] = -1;
    StarpointReel[3][5] = -2;
    StarpointReel[3][6] = 0;
    StarpointReel[3][7] = 0;
    StarpointReel[3][8] = 3;
    StarpointReel[3][9] = 0;
    StarpointReel[3][10] = 2;
    StarpointReel[3][11] = 0;
    StarpointReel[3][12] = 0;
    StarpointReel[3][13] = 0;
    StarpointReel[3][14] = 0;
    StarpointReel[3][15] = 0;
    
    StarpointReel[4][0] = 0;
    StarpointReel[4][1] = 0;
    StarpointReel[4][2] = 0;
    StarpointReel[4][3] = 0;
    StarpointReel[4][4] = -2;
    StarpointReel[4][5] = -3;
    StarpointReel[4][6] = -1;
    StarpointReel[4][7] = 0;
    StarpointReel[4][8] = 2;
    StarpointReel[4][9] = 3;
    StarpointReel[4][10] = 1;
    StarpointReel[4][11] = 0;
    StarpointReel[4][12] = 0;
    StarpointReel[4][13] = 0;
    StarpointReel[4][14] = 0;
    StarpointReel[4][15] = 0;
    
    StarpointReel[5][0] = 0;
    StarpointReel[5][1] = 3;
    StarpointReel[5][2] = -1;
    StarpointReel[5][3] = 0;
    StarpointReel[5][4] = -3;
    StarpointReel[5][5] = 0;
    StarpointReel[5][6] = -2;
    StarpointReel[5][7] = 0;
    StarpointReel[5][8] = 1;
    StarpointReel[5][9] = 2;
    StarpointReel[5][10] = 0;
    StarpointReel[5][11] = 0;
    StarpointReel[5][12] = 0;
    StarpointReel[5][13] = 0;
    StarpointReel[5][14] = 0;
    StarpointReel[5][15] = 0;
    
    StarpointReel[6][0] = 0;
    StarpointReel[6][1] = 2;
    StarpointReel[6][2]= -2;
    StarpointReel[6][3] = 0;
    StarpointReel[6][4] = 0;
    StarpointReel[6][5] = 3;
    StarpointReel[6][6] = -3;
    StarpointReel[6][7] = 0;
    StarpointReel[6][8] = 0;
    StarpointReel[6][9] = 1;
    StarpointReel[6][10] = -1;
    StarpointReel[6][11] = 0;
    StarpointReel[6][12] = 0;
    StarpointReel[6][13] = 0;
    StarpointReel[6][14] = 0;
    StarpointReel[6][15] = 0;
    
    StarpointReel[7][0] = 0;
    StarpointReel[7][1] = 1;
    StarpointReel[7][2] = -3;
    StarpointReel[7][3] = 0;
    StarpointReel[7][4] = 3;
    StarpointReel[7][5] = 2;
    StarpointReel[7][6] = 0;
    StarpointReel[7][7] = 0;
    StarpointReel[7][8] = -1;
    StarpointReel[7][9] = 0;
    StarpointReel[7][10] = -2;
    StarpointReel[7][11] = 0;
    StarpointReel[7][12] = 0;
    StarpointReel[7][13] = 0;
    StarpointReel[7][14] = 0;
    StarpointReel[7][15] = 0;
	
	signed short num;

	for (num = 0; num < 8; num++) {
		 Position[num] = 0;
		 PrevPosition[num] = 0;
		 Direction[num] = 0;
		 Speed[num] = 0;
		 PrevSpeed[num] = 0;
		 Stopped[num] = 1;
		 PosOut[num] = 0;	
		 Split[num] = 0;
		 UnevenStopOffset[num] = 0;
		 Opto[num] = 0;

		 BounceEnable[num] = 0;
		 BounceStage[num] = 0;
		 BounceCounter[num] = 0;
		 BounceOffset[num] = 0;
		 BounceLevel[num] = 0;
		 BounceTable[15][num] = 0;
		 BounceStart[num] = 0;
		 BouncePointer[num] = 0;
		 		 
		//External Settings	
		Steps[num] = 96;
		MaxBounceOffset[num] = 1;		
		ExtenderType = 0;
		ExtenderPort = 0;
		OptoInvert[num] = 0;
		OptoStart[num] = 0;
		OptoEnd[num] = 0;
		//TBC
		BounceMax[num] = 1;
		BounceMin[num] = 1;
		BounceReelEnable[num] = 0;
		BounceDelay[num] = 2800;

	}

	for (num = 4; num < 8; num++) {
		OptoInvert[num] = 1;
	}
	
}

void ReelDrive::SetOptoInvert(UINT8 ReelNum, UINT8 State){
	OptoInvert[ReelNum] = State;
}
void ReelDrive::SetOptoStart(UINT8 ReelNum, UINT8 Start){
	OptoStart[ReelNum] = Start;
}
void ReelDrive::SetOptoEnd(UINT8 ReelNum, UINT8 End){
	OptoEnd[ReelNum] = End;
}
void ReelDrive::SetSteps(UINT8 ReelNum, UINT8 StepsIn){
	Steps[ReelNum] = StepsIn;
}

void ReelDrive::WriteJPMReel(unsigned char data, unsigned char num){
	
	signed short cnt;
	signed short PhaseVal;
	signed short Offset;
	signed short temp;
	try {
		if (num > 7) { 
			return;
		}

		data &= 15;

		//Disregard zero data
		if (data == 0){
			return;
		}

		//Keep an array of last 7 data writes
		//Array(0) being most recent and Array(7) being least recent
		for (cnt = 6; cnt > -1; cnt--) {
			PrevData[cnt + 1][num] = PrevData[cnt][num];
		}

		//Set Most Recent Previous Data and current data.
		PrevData[0][num] = CurrentData[num];
		CurrentData[num] = data;

		//If the last written value is the same as this one, do not proceed
		if (PrevData[0][num] == CurrentData[num]) {
			return;
		}

		//Update Data Changed Cycles
		PrevDataChangedCycles[num] = DataChangedCycles[num];
		DataChangedCycles[num] = DataChangedCounter[num];
	
		//Reset Counter
		DataChangedCounter[num] = 0;

		PhaseVal = (PosOut[num] % 8);
        Offset = StarpointReel[PhaseVal][data];

		//Set Direction 
		if (Offset < 0){
			Direction[num] = 0;
		}
		if (Offset > 0){
			Direction[num] = 1;
		}

		//Check if cycle distances are similar ~(+/-2000 cycles)
		if ((DataChangedCycles[num] <= (PrevDataChangedCycles[num] + 2000)) && (DataChangedCycles[num] >= (PrevDataChangedCycles[num] - 2000))) {
			//~Same Speed - do nothing
		} else if (DataChangedCycles[num] < PrevDataChangedCycles[num]){
			//Speeding up
			PrevSpeed[num] = Speed[num];
			Speed[num] += 1;
		} else {
			//Slowing down
			PrevSpeed[num] = Speed[num];
			Speed[num] -= 1;
			if ((Speed[num] == 0) && (PrevSpeed[num] > 0)) {
				BounceStart[num] = 1;
			}
		}

		//Is Reel Spinning?
		if ((Speed[num] == 0) || (DataChangedCounter[num] > 120000)){
			Speed[num] = 0;
			PrevSpeed[num] = 0;
			Stopped[num] = 1;
		} else {
			Stopped[num] = 0;
		}

		//Set Pos Out
		PosOut[num] = ((PosOut[num] + Offset + Steps[num]) % Steps[num]);

		//Clamp Split To Bounds
		if (PosOut[num] < 0){
			PosOut[num] += Steps[num];
		}

		temp = (Steps[num] - 1);
		if (PosOut[num] > temp){
			PosOut[num] -= Steps[num];
		}

		//Set Split
		Split[num] = PosOut[num];

		// Reel Opto Handling.
		if (OptoInvert[num]){
			if ((Split[num] >= OptoStart[num]) && (Split[num] < OptoEnd[num])){
				Opto[num] = 0;
			} else {
				Opto[num] = 1;			
			}
		} else {
			if ((Split[num] >= OptoStart[num]) && (Split[num] < OptoEnd[num])){
				Opto[num] = 1;
			} else {
				Opto[num] = 0;			
			}
		}		
	}
	catch (...){
		//ErrHandle.Handler("WriteMPU4Reel");
	}
}


unsigned short ReelDrive::UpdateBounce(unsigned short levelin){

	unsigned short Ret;
	unsigned short Val;

	Val = (levelin - 1);
	
	if (Val < 0) {
		Ret = 0;
	} else {
		Ret = Val;
	}

	return Ret;
}

unsigned char ReelDrive::GetOptos(void){

	unsigned char ret;
	ret = (Opto[7]) << 7 | (Opto[6] << 6) | (Opto[5] << 5) | (Opto[4] << 4) | (Opto[3] << 3) | (Opto[2] << 2) | (Opto[1] << 1) | (Opto[0]) ;
	return ret;
}

signed short ReelDrive::GetPosOut(unsigned char num){

	signed short ret;
	ret = PosOut[num];
	return ret;	
}

void ReelDrive::LoadState(){

	int loop, loop2;

	for (loop = 0; loop < 8; loop++){
		for (loop2 = 0; loop2 < 16; loop2++){
			LSC->LoadFromBuffer(BounceTable[loop2][loop]);
			LSC->LoadFromBuffer(StarpointReel[loop][loop2]);
		}
		for (loop2 = 0; loop2 < 8; loop2++){
			LSC->LoadFromBuffer(PrevData[loop][loop2]);
		}
	}
	for (loop = 0; loop < 8; loop++){
		LSC->LoadFromBuffer(BounceEnable[loop]);
		LSC->LoadFromBuffer(BounceStage[loop]);
		LSC->LoadFromBuffer(BounceCounter[loop]);
		LSC->LoadFromBuffer(BounceOffset[loop]);
		LSC->LoadFromBuffer(BounceLevel[loop]);	  
		LSC->LoadFromBuffer(BounceStart[loop]);
		LSC->LoadFromBuffer(BouncePointer[loop]);
		LSC->LoadFromBuffer(Position[loop]);
		LSC->LoadFromBuffer(PrevPosition[loop]);
		LSC->LoadFromBuffer(Direction[loop]);
		LSC->LoadFromBuffer(Speed[loop]);
		LSC->LoadFromBuffer(PrevSpeed[loop]);
		LSC->LoadFromBuffer(Stopped[loop]);
		LSC->LoadFromBuffer(PosOut[loop]);	
		LSC->LoadFromBuffer(Split[loop]);
		LSC->LoadFromBuffer(UnevenStopOffset[loop]);
		LSC->LoadFromBuffer(Opto[loop]);	  	  
		LSC->LoadFromBuffer(CurrentData[loop]);
		LSC->LoadFromBuffer(DataChangedCycles[loop]);
		LSC->LoadFromBuffer(PrevDataChangedCycles[loop]);
		LSC->LoadFromBuffer(DataChangedCounter[loop]);
	}

}

void ReelDrive::SaveState(){
	
	int loop, loop2;

	for (loop = 0; loop < 8; loop++){
		for (loop2 = 0; loop2 < 16; loop2++){
			LSC->SaveToBuffer(BounceTable[loop2][loop]);
			LSC->SaveToBuffer(StarpointReel[loop][loop2]);
		}
		for (loop2 = 0; loop2 < 8; loop2++){
			LSC->SaveToBuffer(PrevData[loop][loop2]);
		}
	}
	for (loop = 0; loop < 8; loop++){
		LSC->SaveToBuffer(BounceEnable[loop]);
		LSC->SaveToBuffer(BounceStage[loop]);
		LSC->SaveToBuffer(BounceCounter[loop]);
		LSC->SaveToBuffer(BounceOffset[loop]);
		LSC->SaveToBuffer(BounceLevel[loop]);	  
		LSC->SaveToBuffer(BounceStart[loop]);
		LSC->SaveToBuffer(BouncePointer[loop]);
		LSC->SaveToBuffer(Position[loop]);
		LSC->SaveToBuffer(PrevPosition[loop]);
		LSC->SaveToBuffer(Direction[loop]);
		LSC->SaveToBuffer(Speed[loop]);
		LSC->SaveToBuffer(PrevSpeed[loop]);
		LSC->SaveToBuffer(Stopped[loop]);
		LSC->SaveToBuffer(PosOut[loop]);	
		LSC->SaveToBuffer(Split[loop]);
		LSC->SaveToBuffer(UnevenStopOffset[loop]);
		LSC->SaveToBuffer(Opto[loop]);	  	  
		LSC->SaveToBuffer(CurrentData[loop]);
		LSC->SaveToBuffer(DataChangedCycles[loop]);
		LSC->SaveToBuffer(PrevDataChangedCycles[loop]);
		LSC->SaveToBuffer(DataChangedCounter[loop]);
	}

}
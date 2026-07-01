#pragma once

#include "LoadSave.h"

class ReelDrive {
private:
	//Bounce Stuff		
	INT16 BounceEnable[8];
	INT16 BounceStage[8];
	INT16 BounceCounter[8];
	INT16 BounceOffset[8];
	INT16 BounceLevel[8];
	INT16 BounceTable[16][8];
	INT16 BounceStart[8];
	INT16 BouncePointer[8];
	//Internals
	INT16 Position[8];
	INT16 PrevPosition[8];
	INT16 Direction[8];
	INT16 Speed[8];
	INT16 PrevSpeed[8];
	INT16 Stopped[8];
	INT16 PosOut[8];	
	INT16 Split[8];
	INT16 UnevenStopOffset[8];
	INT16 Opto[8];
	//## Variables ##
	INT16 StarpointReel[8][16];
	UINT8 PrevData[8][8];
	UINT8 CurrentData[8];
	UINT32 DataChangedCycles[8];
	UINT32 PrevDataChangedCycles[8];
	UINT32 DataChangedCounter[8];

	LoadSaveClass * LSC;

public:		

	//External Settings	
	INT16 Steps[8];
	INT16 MaxBounceOffset[8];
	INT16 BounceMax[8];
	INT16 BounceMin[8];
	INT16 BounceReelEnable[8];
	INT16 BounceDelay[8];
	INT16 ExtenderType;
	UINT8 ExtenderPort;
	INT16 OptoInvert[8];
	INT16 OptoStart[8];
	INT16 OptoEnd[8];	

	//## Subroutines / Functions ##
	void Initialise(LoadSaveClass * LSCIn);
	void WriteJPMReel(UINT8 data, UINT8 num);
	void RunJPMReel(UINT8 maxreel, UINT16 cycles);
	UINT16 UpdateBounce(UINT16 levelin);
	INT16 GetPosOut(UINT8 num);
	UINT8 GetOptos(void);
	void SetOptoInvert(UINT8 ReelNum, UINT8 State);
	void SetOptoStart(UINT8 ReelNum, UINT8 Start);
	void SetOptoEnd(UINT8 ReelNum, UINT8 End);
	void SetSteps(UINT8 ReelNum, UINT8 State);

	void LoadState();
	void SaveState();

};
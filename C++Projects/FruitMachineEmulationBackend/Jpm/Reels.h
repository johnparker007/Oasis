#ifndef ReelsH
#define ReelsH

#include "LoadSave.h"

class ReelDrive {
private:
	//Bounce Stuff		
	signed short BounceEnable[8];
	signed short BounceStage[8];
	signed short BounceCounter[8];
	signed short BounceOffset[8];
	signed short BounceLevel[8];
	signed short BounceTable[16][8];
	signed short BounceStart[8];
	signed short BouncePointer[8];
	//Internals
	signed short Position[8];
	signed short PrevPosition[8];
	signed short Direction[8];
	signed short Speed[8];
	signed short PrevSpeed[8];
	signed short Stopped[8];
	signed short PosOut[8];	
	signed short Split[8];
	signed short UnevenStopOffset[8];
	signed short Opto[8];
	//## Variables ##
	signed short StarpointReel[8][16];
	unsigned char PrevData[8][8];
	unsigned char CurrentData[8];
	signed long DataChangedCycles[8];
	signed long PrevDataChangedCycles[8];
	signed long DataChangedCounter[8];

	LoadSaveClass * LSC;

public:		

	//External Settings	
	signed short Steps[8];
	signed short MaxBounceOffset[8];
	signed short BounceMax[8];
	signed short BounceMin[8];
	signed short BounceReelEnable[8];
	signed short BounceDelay[8];
	signed short ExtenderType;
	unsigned char ExtenderPort;
	signed short OptoInvert[8];
	signed short OptoStart[8];
	signed short OptoEnd[8];	

	//## Subroutines / Functions ##
	void Initialise(LoadSaveClass * LSCIn);
	void WriteJPMReel(unsigned char data, unsigned char num);
	void RunJPMReel(unsigned char maxreel, unsigned short cycles);
	unsigned short UpdateBounce(unsigned short levelin);
	signed short GetPosOut(unsigned char num);
	unsigned char GetOptos(void);
	void SetOptoInvert(UINT8 ReelNum, UINT8 State);
	void SetOptoStart(UINT8 ReelNum, UINT8 Start);
	void SetOptoEnd(UINT8 ReelNum, UINT8 End);
	void SetSteps(UINT8 ReelNum, UINT8 State);

	void LoadState();
	void SaveState();

};

#endif ReelsH
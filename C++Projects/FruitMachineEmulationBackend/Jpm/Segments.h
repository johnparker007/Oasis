#pragma once

#include "LoadSave.h"

#define NUMSTROBELINES			16										//16 Strobe Lines
#define NUMDATALINES			16										//16 Data Lines
#define NUMSEGMENTS				(NUMSTROBELINES * NUMDATALINES)			//Total Number of Lamps

class Segs {
protected:

private:
	//## Variables ##

	//These 2 Variables will be called externally and displayed
	//All the others are internal to the dll

	UINT8 On[NUMSEGMENTS];				//Lamp On / Off 
	UINT8 Brightness[NUMSEGMENTS];		//Lamp Brightness

	//Dimming Stuff
	UINT8 MuxValue;				//Multiplexer Value 0-7
	UINT8 LastMuxValue;			//Last Multiplexer Value 0-7
	UINT32 State[NUMSEGMENTS];				//Dimming State
	UINT32 SavedState[NUMSEGMENTS];		//Saved Dimming State
	UINT32 LastState[NUMSEGMENTS];			//Last Dimming State
	INT32 MaxOnCount[NUMSEGMENTS];		//Maximum On Count
	INT32 OnCount[NUMSEGMENTS];			//On Count
	INT32 OffCount[NUMSEGMENTS];			//Off Count
	INT32 LastOnCount[NUMSEGMENTS];		//Last On Count
	INT32 LastOffCount[NUMSEGMENTS];		//Last Off Count
	UINT32 MaxTimeOn[NUMSEGMENTS];			//Max Time On
	UINT32 MaxTimeOff[NUMSEGMENTS];		//Max Time Off

	//Options
	UINT32 FadeTime[NUMSEGMENTS];			//Fade Time
	UINT32 GlowTime[NUMSEGMENTS];			//Glow Time	
	UINT32 FadeTimeStore[NUMSEGMENTS];		//Fade Time
	UINT32 GlowTimeStore[NUMSEGMENTS];		//Glow Time	
	UINT8 MinDimLevel[NUMSEGMENTS];		//Minium Dimming Level

	//Super Bright Stuff
	UINT64 MuxStart;				//Mux Start Value
	UINT64 MuxEnd;					//Mux End Value
	UINT32 SuperBrightMux;			//Super Bright Mux Value
	UINT64 MuxGapStat;				//Mux Gap State
	UINT64 MuxGapCount[NUMSTROBELINES];		//Mux Gap Count
	UINT32 SuperBrightEnable[NUMSTROBELINES];	//Super Bright Enable
	UINT64 SuperBrightLevel;		//Super Bright Level

	//General Stuff	
	UINT32 SegColumnData[NUMSTROBELINES];		//Lamp Column Data
	UINT32 LastSegColumnData[NUMSTROBELINES];	//Last Lamp Column Data
	UINT32 TimeSinceDataChange;	//Time Since Data Change
	UINT32 MuxDrive;
	UINT8 Toggle;

	UINT8 Intensity;

	LoadSaveClass * LSC;
public:	

	

	//## Subroutines / Functions ##
	void Reset(LoadSaveClass * LSCIn);							//Reset Subroutine
	void WriteJPMSegs(UINT16 data);	//	
	void RunJPMSegs(UINT16 InstructionCycles, UINT64 TotalCycles);//Run Lamps
	INT32 GetSegState(UINT16 Num, UINT32 OnCycles, UINT32 OffCycles); //Lamp Brightness Calculation Sub
	void Update(void);
	void SetIntensity(UINT8 Intens);
	UINT8 GetOn(UINT8 segNum);
	UINT8 GetBrightness(UINT8 segNum);

	void SetMuxValue(UINT8 value);
	void SetLastMuxValue(UINT8 value);

	void SaveState();
	void LoadState();

};
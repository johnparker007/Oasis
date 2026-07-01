#ifndef SEGMENTSH
#define SEGMENTSH

#include "LoadSave.h"
class Segs {
protected:

private:
	LoadSaveClass * LSC;
public:	

	//## Variables ##

	//These 2 Variables will be called externally and displayed
	//All the others are internal to the dll

	unsigned char On[256];				//Lamp On / Off 
	unsigned char Brightness[256];		//Lamp Brightness
	
	//Dimming Stuff
	unsigned char MuxValue;				//Multiplexer Value 0-7
	unsigned char LastMuxValue;			//Last Multiplexer Value 0-7
	signed long State[256];				//Dimming State
	signed long SavedState[256];		//Saved Dimming State
	signed long LastState[256];			//Last Dimming State
	signed long MaxOnCount[256];		//Maximum On Count
	signed long	OnCount[256];			//On Count
	signed long OffCount[256];			//Off Count
	signed long LastOnCount[256];		//Last On Count
	signed long LastOffCount[256];		//Last Off Count
	signed long MaxTimeOn[256];			//Max Time On
	signed long MaxTimeOff[256];		//Max Time Off

	//Options
	signed long FadeTime[256];			//Fade Time
	signed long GlowTime[256];			//Glow Time	
	signed long FadeTimeStore[256];		//Fade Time
	signed long GlowTimeStore[256];		//Glow Time	
	unsigned char MinDimLevel[256];		//Minium Dimming Level

	//Super Bright Stuff
	signed long MuxStart;				//Mux Start Value
	signed long MuxEnd;					//Mux End Value
	signed long SuperBrightMux;			//Super Bright Mux Value
	signed long MuxGapStat;				//Mux Gap State
	signed long MuxGapCount[16];		//Mux Gap Count
	signed long SuperBrightEnable[16];	//Super Bright Enable
	signed long SuperBrightLevel;		//Super Bright Level

	//General Stuff	
	signed long SegColumnData[16];		//Lamp Column Data
	signed long LastSegColumnData[16];	//Last Lamp Column Data
	signed long TimeSinceDataChange;	//Time Since Data Change
	signed long MuxDrive;	
	unsigned char Toggle;

	unsigned char Intensity;

	//## Subroutines / Functions ##
	void Reset(LoadSaveClass * LSCIn);							//Reset Subroutine
	void WriteJPMSegs(unsigned short data);	//	
	void RunJPMSegs(unsigned short InstructionCycles, unsigned long TotalCycles);//Run Lamps
	signed long GetSegState(unsigned short Num, signed long OnCycles, signed long OffCycles); //Lamp Brightness Calculation Sub
	void Update(void);
	void SetIntensity(unsigned char Intens);


	void SaveState();
	void LoadState();

	//Help
};

#endif SEGMENTSH
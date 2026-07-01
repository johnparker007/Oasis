#pragma once

#include "LoadSave.h"

#define BASSBUFFERSIZE 6000
#define PAGES 8
#define MAXSAMPLES 224 // ???
#define SOUNDMEMORYSIZE 0x800000
#define NUMSPEAKERS 2
#define BASSINTERNALFREQUENCY 64000

class SampledSound {
public:	

	void ExtractROM(void);	
	void NECReset(void);
	void NECInit(LoadSaveClass * LSCIn);
	void NECWriteLatch(UINT8 LatchVal);
	void NECWriteControl(UINT8 ResetIn, UINT8 STIn);
	void NECStop(void);
	void NECPlay(void);
	void NECRun(signed short Cycles);
	void NECSerialWrite(UINT8 Reset, UINT8 Clock, UINT8 Data);
	void NECSerialReset(void);
	void NECSerialDataByte(UINT8 Data);
	void NECSetBank(UINT8 Bank);
	void NECSetTune(UINT8 Tune);
	signed short NECDecodeNibble(UINT8 Nibble);
	bool NECUpdate(void);

	UINT8 GetBusy();
	void SetMemory(UINT32 pos, UINT8 value);
	void SetROMSize(UINT32 size);
	//Con/De structors
	SampledSound(void);
	~SampledSound(void);

	void SaveState();
	void LoadState();

private:

	UINT8 Memory_Space[SOUNDMEMORYSIZE];	 //8MB ROM Space 
	signed short* Sample_Space[MAXSAMPLES];	 //Sample Generation Data 	
	signed short* SampledBuffer = NULL;		 //Playback buffer

	//Sample Data Tables
	float SampleSeconds[MAXSAMPLES];
	UINT8 SampleBank[MAXSAMPLES];
	UINT8 SampleIndex[MAXSAMPLES];
	UINT32 SampleStart[MAXSAMPLES];
	UINT32 SampleEnd[MAXSAMPLES];
	UINT32 SampleRate[MAXSAMPLES];
	UINT32 SampleRateDivisor[MAXSAMPLES];
	UINT32 SampleLengthBytes[MAXSAMPLES];
	UINT32 SampleLengthSamples[MAXSAMPLES];
	UINT8 SampleDummy[MAXSAMPLES];
	UINT32 TotalSamples = 0;
	UINT32 ROMSize = 0;

	//ADPCM Conversion	
	UINT32 ADPCMIndex = 0;
	UINT32 ADPCMLast = 0;

	//NEC Decodes
	INT16 Step7759[16][16];
	INT16 State[16];
	UINT8 TuneLookup[PAGES][128];
	UINT32 SamplesInPage[PAGES];

	//Emulation
	UINT8 BankSwitch = 0;
	//Busy Flag
	UINT8 Busy = 0;
	//Busy Timer
	UINT32 BusyTimer = 0;

	//NEC has 1 Channel	
	UINT8 Looping;
	UINT8 Playing;
	UINT8 Stopped;
	UINT32 PositionL;
	UINT32 PositionR;
	UINT8 EndOfSample;
	UINT8 Restarted;
	UINT8 NowPlaying;
	UINT32 Frequency;
	UINT8 Tune = 0;

	LoadSaveClass* LSC = NULL;
};
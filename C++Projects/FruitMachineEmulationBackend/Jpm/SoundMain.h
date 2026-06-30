#ifndef SoundMainH
#define SoundMainH

#include "Bass.h"
#include "LoadSaveCompressDLLClass_SAFE.h"

#define BASSBUFFERSIZE 6000
#define NUMCHANNELS 1
#define PAGES 8
#define MAXSAMPLES 224 // ???
#define SOUNDMEMORYSIZE 0x800000
#define NUMSPEAKERS 2
#define BASSINTERNALFREQUENCY 64000

class SampledSound {
public:	
	
	unsigned char Memory_Space[SOUNDMEMORYSIZE];	//8MB ROM Space 
	signed short * Sample_Space[MAXSAMPLES];		//Sample Generation Data 
	HSTREAM SampledHandle[NUMCHANNELS];				//Handle to sample playback channel in BASS
	signed short * SampledBuffer;					//Playback buffer

	//Sample Data Tables
	float SampleSeconds[MAXSAMPLES];
	unsigned char SampleBank[MAXSAMPLES];
	unsigned char SampleIndex[MAXSAMPLES];
	signed long SampleStart[MAXSAMPLES];
	signed long SampleEnd[MAXSAMPLES];
	signed long SampleRate[MAXSAMPLES];
	signed long SampleRateDivisor[MAXSAMPLES];
	signed long SampleLengthBytes[MAXSAMPLES];
	signed long SampleLengthSamples[MAXSAMPLES];
	unsigned char SampleDummy[MAXSAMPLES];
	signed long TotalSamples = 0;
	signed long ROMSize = 0;
	
	//ADPCM Conversion	
	signed short StepSizes[49];
	signed long ADPCMIndex = 0;
	signed long ADPCMLast = 0;

	//NEC Decodes
	signed long Step7759[16][16];
	signed long State[16];
	unsigned char TuneLookup[PAGES][128];
	signed long SamplesInPage[PAGES];

	//Emulation
	unsigned char BankSwitch = 0;
	//Busy Flag
	unsigned char Busy = 0;
	//Busy Timer
	signed long BusyTimer = 0;

	//NEC has 1 Channel	
	unsigned char Looping[NUMCHANNELS];
	unsigned char Playing[NUMCHANNELS];
	unsigned char Stopped[NUMCHANNELS];
	signed long PositionL[NUMCHANNELS];
	signed long PositionR[NUMCHANNELS];
	unsigned char EndOfSample[NUMCHANNELS];
	unsigned char Restarted[NUMCHANNELS];
	unsigned char NowPlaying[NUMCHANNELS];
	signed long Frequency[NUMCHANNELS];
	unsigned char Tune = 0;

	void ExtractROM(void);	
	void NECReset(void);
	void NECInit(LoadSaveCompressDLLClass * LSCIn);
	void NECWriteLatch(unsigned char LatchVal);
	void NECWriteControl(unsigned char ResetIn, unsigned char STIn);
	void NECStop(void);
	void NECPlay(void);
	void NECRun(signed short Cycles);
	void NECSerialWrite(unsigned char Reset, unsigned char Clock, unsigned char Data);
	void NECSerialReset(void);
	void NECSerialDataByte(unsigned char Data);
	void NECSetBank(unsigned char Bank);
	void NECSetTune(unsigned char Tune);
	signed short NECDecodeNibble(unsigned char Nibble);
	bool NECUpdate(void);
	//Con/De structors
	SampledSound(void);
	~SampledSound(void);

	void SaveState();
	void LoadState();

private:

	LoadSaveCompressDLLClass* LSC = NULL;


};

#endif SoundMainH
#include "stdafx.h"
#include "SoundMain.h"
#include "iostream"
#include "Bass.h"

SampledSound::SampledSound(void) {

	ZeroMemory(Playing, NUMCHANNELS * sizeof(unsigned char));
	ZeroMemory(Looping, NUMCHANNELS * sizeof(unsigned char));
	ZeroMemory(Stopped, NUMCHANNELS * sizeof(unsigned char));
	ZeroMemory(EndOfSample, NUMCHANNELS * sizeof(unsigned char));
	ZeroMemory(Restarted, NUMCHANNELS * sizeof(unsigned char));
	ZeroMemory(NowPlaying, NUMCHANNELS * sizeof(unsigned char));
	ZeroMemory(PositionL, NUMCHANNELS * sizeof(signed long));
	ZeroMemory(PositionR, NUMCHANNELS * sizeof(signed long));
	ZeroMemory(Frequency, NUMCHANNELS * sizeof(signed long));
	ZeroMemory(SampledHandle, NUMCHANNELS * sizeof(HSTREAM));

	ZeroMemory(SampleBank, MAXSAMPLES * sizeof(unsigned char));
	ZeroMemory(SampleIndex, MAXSAMPLES * sizeof(unsigned char));
	ZeroMemory(SampleDummy, MAXSAMPLES * sizeof(unsigned char));
	ZeroMemory(SampleStart, MAXSAMPLES * sizeof(signed long));
	ZeroMemory(SampleEnd, MAXSAMPLES * sizeof(signed long));
	ZeroMemory(SampleRate, MAXSAMPLES * sizeof(signed long));
	ZeroMemory(SampleRateDivisor, MAXSAMPLES * sizeof(signed long));
	ZeroMemory(SampleLengthBytes, MAXSAMPLES * sizeof(signed long));
	ZeroMemory(SampleLengthSamples, MAXSAMPLES * sizeof(signed long));
	ZeroMemory(SampleSeconds, MAXSAMPLES * sizeof(float));
	ZeroMemory(Sample_Space, MAXSAMPLES * sizeof(signed short));
	
	ZeroMemory(Memory_Space, SOUNDMEMORYSIZE * sizeof(unsigned char));

	ZeroMemory(TuneLookup, PAGES * 128 * sizeof(unsigned char));
	ZeroMemory(SamplesInPage, PAGES * sizeof(signed long));

	

	if (BASS_Init(-1, BASSINTERNALFREQUENCY, 0, 0, NULL) == false) {

	}

	for (int i = 0; i < NUMCHANNELS; i++) {
		EndOfSample[i] = 1;
	}

}

SampledSound::~SampledSound(void) {

	//Stop Channels	
	if (SampledHandle[0]) {
		BASS_ChannelStop(SampledHandle[0]);
	}

	for (int i = 0; i < MAXSAMPLES; i++){
		if (Sample_Space[i]) {
			free(Sample_Space[i]);
		}
	}

	BASS_Free();

}

void SampledSound::ExtractROM(void){

	signed long cnt;
	signed long cnt2;	
	signed long Position;
	signed long ByteCount;
	signed long SampleCount;

	
	//OKI Table Setups
	StepSizes[0] = 16;
	StepSizes[1] = 17;
	StepSizes[2] = 19;
	StepSizes[3] = 21;
	StepSizes[4] = 23;
	StepSizes[5] = 25;
	StepSizes[6] = 28;
	StepSizes[7] = 31;
	StepSizes[8] = 34;
	StepSizes[9] = 37;
        
	StepSizes[10] = 41;
	StepSizes[11] = 45;
	StepSizes[12] = 50;
	StepSizes[13] = 55;
	StepSizes[14] = 60;
	StepSizes[15] = 66;
	StepSizes[16] = 73;
	StepSizes[17] = 80;
	StepSizes[18] = 88;
	StepSizes[19] = 97;
        
	StepSizes[20] = 107;
	StepSizes[21] = 118;
	StepSizes[22] = 130;
	StepSizes[23] = 143;
	StepSizes[24] = 157;
	StepSizes[25] = 173;
	StepSizes[26] = 190;
	StepSizes[27] = 209;
	StepSizes[28] = 230;
	StepSizes[29] = 253;
        
	StepSizes[30] = 279;
	StepSizes[31] = 307;
	StepSizes[32] = 337;
	StepSizes[33] = 371;
	StepSizes[34] = 408;
	StepSizes[35] = 449;
	StepSizes[36] = 494;
	StepSizes[37] = 544;
	StepSizes[38] = 598;
	StepSizes[39] = 658;
        
	StepSizes[40] = 724;
	StepSizes[41] = 796;
	StepSizes[42] = 876;
	StepSizes[43] = 963;
	StepSizes[44] = 1060;
	StepSizes[45] = 1166;
	StepSizes[46] = 1282;
	StepSizes[47] = 1411;
	StepSizes[48] = 1552;


	//NEC Table Setups        
	SamplesInPage[0] = 0;
	SamplesInPage[1] = 0;
	SamplesInPage[2] = 0;
	SamplesInPage[3] = 0;
	SamplesInPage[4] = 0;
	SamplesInPage[5] = 0;
	SamplesInPage[6] = 0;
	SamplesInPage[7] = 0;
        
	Step7759[0][0] = 0;
	Step7759[0][1] = 0;
	Step7759[0][2] = 1;
	Step7759[0][3] = 2;
	Step7759[0][4] = 3;
	Step7759[0][5] = 5;
	Step7759[0][6] = 7;
	Step7759[0][7] = 10;
	Step7759[0][8] = 0;
	Step7759[0][9] = 0;
	Step7759[0][10] = -1;
	Step7759[0][11] = -2;
	Step7759[0][12] = -3;
	Step7759[0][13] = -5;
	Step7759[0][14] = -7;
	Step7759[0][15] = -10;
        
	Step7759[1][0] = 0;
	Step7759[1][1] = 1;
	Step7759[1][2] = 2;
	Step7759[1][3] = 3;
	Step7759[1][4] = 4;
	Step7759[1][5] = 6;
	Step7759[1][6] = 8;
	Step7759[1][7] = 13;
	Step7759[1][8] = 0;
	Step7759[1][9] = -1;
	Step7759[1][10] = -2;
	Step7759[1][11] = -3;
	Step7759[1][12] = -4;
	Step7759[1][13] = -6;
	Step7759[1][14] = -8;
	Step7759[1][15] = -13;
        
	Step7759[2][0] = 0;
	Step7759[2][1] = 1;
	Step7759[2][2] = 2;
	Step7759[2][3] = 4;
	Step7759[2][4] = 5;
	Step7759[2][5] = 7;
	Step7759[2][6] = 10;
	Step7759[2][7] = 15;
	Step7759[2][8] = 0;
	Step7759[2][9] = -1;
	Step7759[2][10] = -2;
	Step7759[2][11] = -4;
	Step7759[2][12] = -5;
	Step7759[2][13] = -7;
	Step7759[2][14] = -10;
	Step7759[2][15] = -15;
        
	Step7759[3][0] = 0;
	Step7759[3][1] = 1;
	Step7759[3][2] = 3;
	Step7759[3][3] = 4;
	Step7759[3][4] = 6;
	Step7759[3][5] = 9;
	Step7759[3][6] = 13;
	Step7759[3][7] = 19;
	Step7759[3][8] = 0;
	Step7759[3][9] = -1;
	Step7759[3][10] = -3;
	Step7759[3][11] = -4;
	Step7759[3][12] = -6;
	Step7759[3][13] = -9;
	Step7759[3][14] = -13;
	Step7759[3][15] = -19;
        
	Step7759[4][0] = 0;
	Step7759[4][1] = 2;
	Step7759[4][2] = 3;
	Step7759[4][3] = 5;
	Step7759[4][4] = 8;
	Step7759[4][5] = 11;
	Step7759[4][6] = 15;
	Step7759[4][7] = 23;
	Step7759[4][8] = 0;
	Step7759[4][9] = -2;
	Step7759[4][10] = -3;
	Step7759[4][11] = -5;
	Step7759[4][12] = -8;
	Step7759[4][13] = -11;
	Step7759[4][14] = -15;
	Step7759[4][15] = -23;
        
	Step7759[5][0] = 0;
	Step7759[5][1] = 2;
	Step7759[5][2] = 4;
	Step7759[5][3] = 7;
	Step7759[5][4] = 10;
	Step7759[5][5] = 14;
	Step7759[5][6] = 19;
	Step7759[5][7] = 29;
	Step7759[5][8] = 0;
	Step7759[5][9] = -2;
	Step7759[5][10] = -4;
	Step7759[5][11] = -7;
	Step7759[5][12] = -10;
	Step7759[5][13] = -14;
	Step7759[5][14] = -19;
	Step7759[5][15] = -29;
        
	Step7759[6][0] = 0;
	Step7759[6][1] = 3;
	Step7759[6][2] = 5;
	Step7759[6][3] = 8;
	Step7759[6][4] = 12;
	Step7759[6][5] = 16;
	Step7759[6][6] = 22;
	Step7759[6][7] = 33;
	Step7759[6][8] = 0;
	Step7759[6][9] = -3;
	Step7759[6][10] = -5;
	Step7759[6][11] = -7;
	Step7759[6][12] = -12;
	Step7759[6][13] = -16;
	Step7759[6][14] = -22;
	Step7759[6][15] = -33;
        
	Step7759[7][0] = 1;
	Step7759[7][1] = 4;
	Step7759[7][2] = 7;
	Step7759[7][3] = 10;
	Step7759[7][4] = 15;
	Step7759[7][5] = 20;
	Step7759[7][6] = 29;
	Step7759[7][7] = 43;
	Step7759[7][8] = -1;
	Step7759[7][9] = -4;
	Step7759[7][10] = -7;
	Step7759[7][11] = -10;
	Step7759[7][12] = -15;
	Step7759[7][13] = -20;
	Step7759[7][14] = -29;
	Step7759[7][15] = -43;
        
	Step7759[8][0] = 1;
	Step7759[8][1] = 4;
	Step7759[8][2] = 8;
	Step7759[8][3] = 13;
	Step7759[8][4] = 18;
	Step7759[8][5] = 25;
	Step7759[8][6] = 35;
	Step7759[8][7] = 53;
	Step7759[8][8] = -1;
	Step7759[8][9] = -4;;
	Step7759[8][10] = -8;
	Step7759[8][11] = -13;
	Step7759[8][12] = -18;
	Step7759[8][13] = -25;
	Step7759[8][14] = -35;
	Step7759[8][15] = -53;
        
	Step7759[9][0] = 1;
	Step7759[9][1] = 6;
	Step7759[9][2] = 10;
	Step7759[9][3] = 16;
	Step7759[9][4] = 22;
	Step7759[9][5] = 31;
	Step7759[9][6] = 43;
	Step7759[9][7] = 64;
	Step7759[9][8] = -1;
	Step7759[9][9] = -6;
	Step7759[9][10] = -10;
	Step7759[9][11] = -16;
	Step7759[9][12] = -22;
	Step7759[9][13] = -31;
	Step7759[9][14] = -43;
	Step7759[9][15] = -64;
        
	Step7759[10][0] = 2;
	Step7759[10][1] = 7;
	Step7759[10][2] = 12;
	Step7759[10][3] = 19;
	Step7759[10][4] = 27;
	Step7759[10][5] = 37;
	Step7759[10][6] = 51;
	Step7759[10][7] = 76;
	Step7759[10][8] = -2;
	Step7759[10][9] = -7;
	Step7759[10][10] = -12;
	Step7759[10][11] = -19;
	Step7759[10][12] = -27;
	Step7759[10][13] = -37;
	Step7759[10][14] = -51;
	Step7759[10][15] = -76;
        
	Step7759[11][0] = 2;
	Step7759[11][1] = 9;
	Step7759[11][2] = 16;
	Step7759[11][3] = 24;
	Step7759[11][4] = 34;
	Step7759[11][5] = 46;
	Step7759[11][6] = 64;
	Step7759[11][7] = 96;
	Step7759[11][8] = -2;
	Step7759[11][9] = -9;
	Step7759[11][10] = -16;
	Step7759[11][11] = -24;
	Step7759[11][12] = -34;
	Step7759[11][13] = -46;
	Step7759[11][14] = -64;
	Step7759[11][15] = -96;
        
	Step7759[12][0] = 3;
	Step7759[12][1] = 11;
	Step7759[12][2] = 19;
	Step7759[12][3] = 29;
	Step7759[12][4] = 41;
	Step7759[12][5] = 57;
	Step7759[12][6] = 79;
	Step7759[12][7] = 117;
	Step7759[12][8] = -3;
	Step7759[12][9] = -11;
	Step7759[12][10] = -19;
	Step7759[12][11] = -29;
	Step7759[12][12] = -41;
	Step7759[12][13] = -57;
	Step7759[12][14] = -79;
	Step7759[12][15] = -117;
        
	Step7759[13][0] = 4;
	Step7759[13][1] = 13;
	Step7759[13][2] = 24;
	Step7759[13][3] = 36;
	Step7759[13][4] = 50;
	Step7759[13][5] = 69;
	Step7759[13][6] = 96;
	Step7759[13][7] = 143;
	Step7759[13][8] = -4;
	Step7759[13][9] = -13;
	Step7759[13][10] = -24;
	Step7759[13][11] = -36;
	Step7759[13][12] = -50;
	Step7759[13][13] = -69;
	Step7759[13][14] = -96;
	Step7759[13][15] = -143;
        
	Step7759[14][0] = 4;
	Step7759[14][1] = 16;
	Step7759[14][2] = 29;
	Step7759[14][3] = 44;
	Step7759[14][4] = 62;
	Step7759[14][5] = 85;
	Step7759[14][6] = 118;
	Step7759[14][7] = 175;
	Step7759[14][8] = -4;
	Step7759[14][9] = -16;
	Step7759[14][10] = -29;
	Step7759[14][11] = -44;
	Step7759[14][12] = -62;
	Step7759[14][13] = -85;
	Step7759[14][14] = -118;
	Step7759[14][15] = -175;
        
	Step7759[15][0] = 6;
	Step7759[15][1] = 20;
	Step7759[15][2] = 36;
	Step7759[15][3] = 54;
	Step7759[15][4] = 76;
	Step7759[15][5] = 104;
	Step7759[15][6] = 144;
	Step7759[15][7] = 214;
	Step7759[15][8] = -6;
	Step7759[15][9] = -20;
	Step7759[15][10] = -36;
	Step7759[15][11] = -54;
	Step7759[15][12] = -76;
	Step7759[15][13] = -104;
	Step7759[15][14] = -144;
	Step7759[15][15] = -214;
        
	State[0] = -1;
	State[1] = -1;
	State[2] = 0;
	State[3] = 0;
	State[4] = 1;
	State[5] = 2;
	State[6] = 2;
	State[7] = 3;
	State[8] = -1;
	State[9] = -1;
	State[10] = 0;
	State[11] = 0;
	State[12] = 1;
	State[13] = 2;
	State[14] = 2;
	State[15] = 3;

//NEC

	unsigned char PageTemp;
	unsigned char Pages;
	signed long Page;		
	unsigned long Header;
	unsigned char Repeat;
	unsigned char Value;
	unsigned char ValidHeader;
	signed long RepeatOffset;
	signed long SilenceLength;
	signed long MyRate = 8000; //Set a reasonable value in case of error
	

	unsigned short Nibbles;
	signed long NibbleCount;

	for (cnt = 0; cnt < 8; cnt++){
		for (cnt2 = 0; cnt2 < 128; cnt2++){
			TuneLookup[cnt][cnt2] = 0;
		}
	}

	PageTemp = ((ROMSize / 131072) & 0xff);

	TotalSamples = 0;

	for (Pages = 0; Pages < PageTemp; Pages++){
		//Set Page Size
		Page = Pages * 131072;			
		//Set Initial Position
		Position = Page;
		//Get Number of Samples
		SamplesInPage[Pages] = Memory_Space[Position];
		Position += 1;
		//Get Header
		Header = Memory_Space[Position];
		Header = (Header << 8);
		Position += 1;
		Header |= Memory_Space[Position];
		Header = (Header << 8);
		Position += 1;
		Header |= Memory_Space[Position];
		Header = (Header << 8);
		Position += 1;
		Header |= Memory_Space[Position];			
		Position += 1;
		//Check we are within a ROM
		if (Page < ROMSize){						
			//Check for Valid Header
			if (Header == 0x5AA56955){
				//Check at least 1 sample
				if (SamplesInPage[Pages] > 0){
					//Decode Samples in Page
					for (cnt = 0; cnt <= SamplesInPage[Pages]; cnt++){						
						TotalSamples++;

						short * SampleTemp = (signed short*)calloc(0x80000, sizeof(signed short)); //Temp
						if (SampleTemp) {
							//Reset Repeat Flag
							Repeat = 0;
							//Set Position
							Position = (Page + 5 + (cnt * 2));
							//Get Start Address
							//Byte 1
							SampleStart[TotalSamples] = Memory_Space[Position];
							SampleStart[TotalSamples] = (SampleStart[TotalSamples] << 8);
							Position += 1;
							//Byte 2
							SampleStart[TotalSamples] |= Memory_Space[Position];
							Position += 1;
							//Double the value
							SampleStart[TotalSamples] = (SampleStart[TotalSamples] << 1);
							//Add Page Offset
							SampleStart[TotalSamples] += Page;

							//Set End Address
							if (cnt < SamplesInPage[Pages]) {
								//Set Position
								Position = (Page + 5 + ((cnt + 1) * 2));
								//Get End Address
								//Byte 1
								SampleEnd[TotalSamples] = Memory_Space[Position];
								SampleEnd[TotalSamples] = (SampleEnd[TotalSamples] << 8);
								Position += 1;
								//Byte 2
								SampleEnd[TotalSamples] |= Memory_Space[Position];
								Position += 1;
								//Double the value
								SampleEnd[TotalSamples] = (SampleEnd[TotalSamples] << 1);
								//Add Page Offset
								SampleEnd[TotalSamples] += Page;
							}
							else {
								//Set End Address
								SampleEnd[TotalSamples] = (Page + 131071);
							}
							//Set Position - Skip Start Byte
							Position = (SampleStart[TotalSamples] + 1);
							//Reset ADPCM
							ADPCMIndex = 0;
							ADPCMLast = 0;
							//Reset Counts
							ByteCount = 0;
							SampleCount = 0;
							//Reset Flag
							ValidHeader = 0;
							//Reset Nibbles
							Nibbles = 0;

							while (Position < SampleEnd[TotalSamples]) {
								//Repeat
								if (Repeat) {
									Repeat -= 1;
									Position = RepeatOffset;
								}
								//Get Data Byte
								Value = Memory_Space[Position];
								Position += 1;
								//Data Switch
								switch (Value & 192) {
								case 0://Silence
									if (((Value & 63) == 0) && (ValidHeader)) {
										Position = SampleEnd[TotalSamples];
									}
									else {
										ValidHeader = 1;
										SilenceLength = ((Value & 63) * 20);
										ADPCMIndex = 0;
										ADPCMLast = 0;
										for (cnt2 = 0; cnt2 < SilenceLength; cnt2++) {
											SampleTemp[SampleCount] = NECDecodeNibble(0);
											SampleCount += 1;
										}

									}
									Nibbles = 0;
									break;
								case 64://256 Nibbles
									MyRate = (160000 / ((Value & 31) + 1));
									Nibbles = 256;
									ValidHeader = 1;
									break;
								case 128://n Nibbles
									MyRate = (160000 / ((Value & 31) + 1));
									Nibbles = (Memory_Space[Position] + 1);
									Position += 1;
									ValidHeader = 1;
									break;
								case 192://Repeat Loop
									Repeat = ((Value & 7) + 1);
									RepeatOffset = Position;
									ValidHeader = 1;
									break;
								}

								if (Nibbles) {
									for (NibbleCount = 0; NibbleCount < Nibbles; NibbleCount++) {
										if (NibbleCount & 1) {
											//Low Nibble
											SampleTemp[SampleCount] = NECDecodeNibble(Value & 0xf);
										}
										else {
											//Next Byte
											Value = Memory_Space[Position];
											Position += 1;
											//High Nibble
											SampleTemp[SampleCount] = NECDecodeNibble((Value & 0xf0) >> 4);
											ByteCount += 1;
										}
										SampleCount += 1;
									}
								}
							}
							if (SampleCount > 0) {
								//Valid Sample Set Data Table
								SampleRate[TotalSamples] = MyRate;
								SampleLengthBytes[TotalSamples] = ByteCount;
								SampleLengthSamples[TotalSamples] = SampleCount;
							}
							else {
								//Not a Valid Sample, set some defaults
								SampleLengthBytes[TotalSamples] = 0;
								SampleLengthSamples[TotalSamples] = 0;
								SampleRate[TotalSamples] = 8000;
							}

							//Update the sample length to its real value
							SampleEnd[TotalSamples] = (SampleStart[TotalSamples] + ByteCount);
							//Bank
							SampleBank[TotalSamples] = Pages;
							//Index
							SampleIndex[TotalSamples] = (cnt & 0xff);
							//Tune Lookup
							TuneLookup[SampleBank[TotalSamples]][SampleIndex[TotalSamples]] = (TotalSamples & 0xff);
							//Seconds
							if (SampleRate[TotalSamples]) {
								SampleSeconds[TotalSamples] = ((float)SampleLengthSamples[TotalSamples] / (float)SampleRate[TotalSamples]);
							}
							else {
								//Prevent Divide By Zero
								SampleSeconds[TotalSamples] = 0;
							}
							//Show The Data
							/*
							fprintf(DebugFile, "Sample Num %d \n", TotalSamples);
							fprintf(DebugFile, "Sample Rate %d \n", SampleRate[TotalSamples] );
							fprintf(DebugFile, "Sample Start %X \n", SampleStart[TotalSamples]);
							fprintf(DebugFile, "Sample End %X \n", SampleEnd[TotalSamples]);
							fprintf(DebugFile, "Sample Bank %d \n", SampleBank[TotalSamples]);
							fprintf(DebugFile, "Sample Index %d \n", SampleIndex[TotalSamples]);
							fprintf(DebugFile, "Sample Length Bytes %d \n", SampleLengthBytes[TotalSamples]);
							fprintf(DebugFile, "Sample Length Samples %d \n", SampleLengthSamples[TotalSamples]);
							fprintf(DebugFile, "Sample Seconds %f \n", SampleSeconds[TotalSamples]);
							*/

							if (SampleLengthSamples[TotalSamples] > 0) {
								Sample_Space[TotalSamples] = (signed short*)calloc(SampleLengthSamples[TotalSamples], sizeof(signed short));
								memcpy_s(Sample_Space[TotalSamples], (SampleLengthSamples[TotalSamples] * 2), SampleTemp, (SampleLengthSamples[TotalSamples] * 2));
							}

							free(SampleTemp);
						}
					}	
					//fprintf(DebugFile, "Samples In Page %d \n", SamplesInPage[Pages]);   
				} else {
					//fprintf(DebugFile, "No Samples In Page %d \n", Pages);
				}
			} else {
				//fprintf(DebugFile, "No Valid Header In Page %d \n", Pages);
			}
		} else {
			//fprintf(DebugFile, "Not Within ROM Page %d \n", Pages);
		}
	}	

	//fprintf(DebugFile, "Extract end %d \n", 0);
	//fclose (DebugFile);

}


signed short SampledSound::NECDecodeNibble(unsigned char Nibble){
	
	signed long Sample;

	Sample = (ADPCMLast + Step7759[ADPCMIndex][Nibble]);
	ADPCMIndex += State[Nibble];
	//Clamp Index
	if (ADPCMIndex < 0){
		ADPCMIndex = 0;
	} else if (ADPCMIndex > 15) {
		ADPCMIndex = 15;
	}
	//Clamp Sample
	if (Sample > 255){
		Sample = 255;
	} else if (Sample < -255){
		Sample = -255;
	}
	ADPCMLast = Sample;
	Sample = (Sample << 7);
	//Clamp Sample
	if (Sample > 32767){
		Sample = 32767;
	} else if (Sample < -32768){
		Sample = -32768;
	}
	return (Sample & 0xffff);
}


void SampledSound::NECReset(void){
	BusyTimer = 15;
	Busy = 0;
	NECStop();
}
void SampledSound::NECInit(LoadSaveCompressDLLClass * LSCIn){
	
	unsigned char cnt;

	LSC = LSCIn;
	BankSwitch = 0;
	Tune = 0;
	Busy = 0;
	BusyTimer = 15;


	//Stop Unused Channels
	for (cnt = 0; cnt < 8; cnt++){
		if (SampledHandle[cnt]){
			BASS_ChannelStop(SampledHandle[cnt]);
			//fprintf(DebugFile, "Channel Stop %d \n", cnt);
		}
	}

	//Ensure Used Channel is started
	if (SampledHandle[0] == 0){
		SampledHandle[0] = BASS_StreamCreate(64000, 2, BASS_SPEAKER_FRONT, STREAMPROC_PUSH, 0);			
		//fprintf(DebugFile, "Channel Create LR %d \n", cnt);
	}
	if (SampledHandle[0]){
		BASS_ChannelPlay(SampledHandle[0], true);
		//fprintf(DebugFile, "Channel Play LR %d \n", cnt);
	}

	//Set this here or the sound goes funny for a few seconds on initial playback
	BASS_ChannelSetAttribute(SampledHandle[0], BASS_ATTRIB_FREQ, 8000.0f);
}


void SampledSound::NECStop(void){

	//Reset Positions
	PositionL[0] = 0;
	PositionR[0] = 0;
	//Clear Playing Flag
	Playing[0] = 0;
	//Clear Looping Flag
	Looping[0] = 0;
	//Set End Of Sample Flag
	EndOfSample[0] = 1;	
}
void SampledSound::NECPlay(void){

	//Set Now Playing Tune
	NowPlaying[0] = Tune;
	//Reset Positions
	PositionL[0] = 0;
	PositionR[0] = 0;
	//Reset End Of Sample Flag
	EndOfSample[0] = 0;	
	//Set Playing Flag
	Playing[0] = 1;
	//Set Busy
	Busy = 0;
	//Clear Looping Flag
	Looping[0] = 0;
	//Frequency
	Frequency[0] = SampleRate[Tune];

}

void SampledSound::NECRun(signed short Cycles){
	
	if (BusyTimer > 0){
		BusyTimer -= Cycles;
		if (BusyTimer <= 0){
			BusyTimer = 0;
			Busy = 1;
		}
	}

}

void SampledSound::NECSetTune(unsigned char TuneIn){
	Tune = TuneLookup[BankSwitch][TuneIn];
}
void SampledSound::NECSetBank(unsigned char BankIn){
	BankSwitch = BankIn;
}


bool SampledSound::NECUpdate(void){

	INT32 BufferUsed, BufferLength, BufferCount, SampleCount;

	//Get Bytes Used
	BufferUsed = (BASS_ChannelGetData(SampledHandle[0], 0, BASS_DATA_AVAILABLE) * NUMSPEAKERS);
	if (BufferUsed < BASSBUFFERSIZE) {
		BufferLength = (BASSBUFFERSIZE - BufferUsed);
	}
	else {
		//fclose (DebugFile);
		return false;
	}
	if (BufferUsed <= 0) {
		int help = BASS_ErrorGetCode();
		if (SampledHandle[0]) {
			BASS_ChannelPlay(SampledHandle[0], TRUE);
		}
	}

	SampledBuffer = (INT16*)malloc((BASSBUFFERSIZE * sizeof(INT16) * NUMSPEAKERS));
	
	if (SampledBuffer) {

		SampleCount = 0;

		if (Playing[0]) {

			for (BufferCount = 0; BufferCount < BufferLength; BufferCount++) {
				//Buffer Loop
				if (PositionL[0] >= SampleLengthSamples[NowPlaying[0]]) {
					//Sample Has Finished
					EndOfSample[0] = 1;
					Busy = 1;
					if (Looping[0]) {
						//Replay
						//Reset Position
						PositionL[0] = 0;
						//Reset End Of Sample Flag
						EndOfSample[0] = 0;
					}
					else {
						//Fill buffer with Zero Data;				
						Playing[0] = 0;
						SampledBuffer[SampleCount] = 0;
						SampleCount++;
						SampledBuffer[SampleCount] = 0;
						SampleCount++;
					}
				}
				else 
				{

					//Sample Playback
					if (Sample_Space[NowPlaying[0]]) {
						//Left Channel
						SampledBuffer[SampleCount] = Sample_Space[NowPlaying[0]][PositionL[0]];
					}

					SampleCount++;

					if (Sample_Space[NowPlaying[0]]) {
						//Right Channel
						SampledBuffer[SampleCount] = Sample_Space[NowPlaying[0]][PositionL[0]];
					}

					SampleCount++;
					PositionL[0]++;
				}
			}

		}
		else {

			//Fill with zero data
			for (BufferCount = 0; BufferCount < (BufferLength); BufferCount++) {
				SampledBuffer[SampleCount] = 0;
				SampleCount++;
				SampledBuffer[SampleCount] = 0;
				SampleCount++;
			}

		}
		BASS_ChannelSetAttribute(SampledHandle[0], BASS_ATTRIB_FREQ, (float)Frequency[0]);
		//Set The Data
		BASS_StreamPutData(SampledHandle[0], SampledBuffer, (BufferLength * sizeof(INT16) * NUMSPEAKERS));

		//Free audio buffer
		free(SampledBuffer);

	}

	return true;
}

void SampledSound::SaveState(){

	for (int i = 0; i < NUMCHANNELS; i++){

		LSC->SaveToBuffer(Playing[i]);
		LSC->SaveToBuffer(Looping[i]);
		LSC->SaveToBuffer(Stopped[i]);	
		LSC->SaveToBuffer(PositionL[i]);
		LSC->SaveToBuffer(PositionR[i]);
		LSC->SaveToBuffer(EndOfSample[i]);
		LSC->SaveToBuffer(Restarted[i]);
		LSC->SaveToBuffer(NowPlaying[i]);	
		LSC->SaveToBuffer(Frequency[i]);
	}

	LSC->SaveToBuffer(BankSwitch);
	LSC->SaveToBuffer(Busy);
	LSC->SaveToBuffer(BusyTimer);
	LSC->SaveToBuffer(Tune);

}

void SampledSound::LoadState(){
	
	for (int i = 0; i < NUMCHANNELS; i++){

		LSC->LoadFromBuffer(Playing[i]);
		LSC->LoadFromBuffer(Looping[i]);
		LSC->LoadFromBuffer(Stopped[i]);	
		LSC->LoadFromBuffer(PositionL[i]);
		LSC->LoadFromBuffer(PositionR[i]);
		LSC->LoadFromBuffer(EndOfSample[i]);
		LSC->LoadFromBuffer(Restarted[i]);
		LSC->LoadFromBuffer(NowPlaying[i]);	
		LSC->LoadFromBuffer(Frequency[i]);
	}

	LSC->LoadFromBuffer(BankSwitch);
	LSC->LoadFromBuffer(Busy);
	LSC->LoadFromBuffer(BusyTimer);
	LSC->LoadFromBuffer(Tune);

}
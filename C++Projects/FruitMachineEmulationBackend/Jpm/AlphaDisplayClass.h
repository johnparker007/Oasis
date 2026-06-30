#ifndef AlphaH
#define AlphaH

#include "LoadSaveCompressDLLClass_SAFE.h"
class AlphanumericDisplay {
public:

	AlphanumericDisplay();
	~AlphanumericDisplay();

	int GetAlphaSegments(char SegNum);
	UINT8 GetAlphaDotComma(char SegNum);
	char GetAlphaBright();

	void WriteAlphaBits(unsigned char Reset, unsigned char Clock, unsigned char Data);
	void WriteAlphaByte(unsigned char Reset, unsigned char Data);

	void Initialise(LoadSaveCompressDLLClass* LSC);

	void SaveState();
	void LoadState();

private:

	//Internal Variables
	unsigned char PrevClock = 0;
	unsigned char PrevPointer = 0;
	unsigned char InCounter = 0;
	unsigned char Buffer = 0;
	unsigned char Character = 0;
	unsigned char Pointer = 0;
	unsigned char DigitCounter = 0;
	unsigned char CharBuffer[16];
	unsigned char CharBuffer2[16];
	unsigned char Brightness = 0;

	//
	unsigned char GetAlphaCharacter(char SegNum);

	LoadSaveCompressDLLClass * LSC = NULL;

};

#endif AlphaH
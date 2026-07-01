#pragma once

#include "LoadSave.h"

#define NUMSEGMENTS 16

class AlphanumericDisplay {
public:

	AlphanumericDisplay();
	~AlphanumericDisplay();

	UINT16 GetAlphaSegments(UINT8 SegNum);
	UINT8 GetAlphaDotComma(UINT8 SegNum);
	UINT8 GetAlphaBright();

	void WriteAlphaBits(UINT8 Reset, UINT8 Clock, UINT8 Data);
	void WriteAlphaByte(UINT8 Reset, UINT8 Data);

	void Initialise(LoadSaveClass* LSC);

	void SaveState();
	void LoadState();

private:

	//Internal Variables
	UINT8 PrevClock = 0;
	UINT8 PrevPointer = 0;
	UINT8 InCounter = 0;
	UINT8 Buffer = 0;
	UINT8 Character = 0;
	UINT8 Pointer = 0;
	UINT8 DigitCounter = 0;
	UINT8 CharBuffer[NUMSEGMENTS];
	UINT8 DotCommaBuffer[NUMSEGMENTS];
	UINT8 Brightness = 0;

	//
	UINT8 GetAlphaCharacter(UINT8 SegNum);

	LoadSaveClass * LSC = NULL;

};
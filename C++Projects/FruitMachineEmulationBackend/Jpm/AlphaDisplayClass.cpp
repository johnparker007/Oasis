#include "stdafx.h"
#include "AlphaDisplayClass.h"


AlphanumericDisplay::AlphanumericDisplay(){

	ZeroMemory(CharBuffer, 16 * sizeof(unsigned char));
	ZeroMemory(CharBuffer2, 16 * sizeof(unsigned char));
	
}

AlphanumericDisplay::~AlphanumericDisplay(){

}

int AlphanumericDisplay::GetAlphaSegments(char SegNum){
	
	char StandardisedChar;
	int ret = 0;

	StandardisedChar = GetAlphaCharacter(SegNum);
	if (StandardisedChar != 31){
		StandardisedChar |= 0;
	}
	//Updated for 16 segment display (MPU4 is 14)
	switch (StandardisedChar){
	case 0: ret = 20607;	break;//@
	case 1: ret = 17615;	break;//A
	case 2: ret = 5439;		break;//B
	case 3: ret = 243;		break;//C
	case 4: ret = 4415;		break;//D
	case 5: ret = 16627;	break;//E
	case 6: ret = 16579;	break;//F
	case 7: ret = 1275;		break;//G
	case 8: ret = 17612;	break;//H
	case 9: ret = 4403;		break;//I
	case 10: ret = 124;		break;//J
	case 11: ret = 19136;	break;//K
	case 12: ret = 240;		break;//L
	case 13: ret = 33484;	break;//M
	case 14: ret = 35020;	break;//N
	case 15: ret = 255;		break;//O
	case 16: ret = 17607;	break;//P
	case 17: ret = 2303;	break;//Q
	case 18: ret = 19655;	break;//R
	case 19: ret = 17595;	break;//S
	case 20: ret = 4355;	break;//T
	case 21: ret = 252;		break;//U
	case 22: ret = 8896;	break;//V
	case 23: ret = 10444;	break;//W
	case 24: ret = 43520;	break;//X
	case 25: ret = 37376;	break;//Y
	case 26: ret = 8755;	break;//Z
	case 27: ret = 225;		break;// SQUARE OPEN BRACKET
	case 28: ret = 34816;	break;// BACKSLASH
	case 29: ret = 30;		break;// SQUARE CLOSED BRACKET
	case 30: ret = 10240;	break;//^
	case 31: ret = 48;		break;//_
	case 32: ret = 0;		break;// SPACE
	case 33: ret = 33057;	break;//!
	case 34: ret = 384;		break;//"
	case 35: ret = 21820;	break;//#
	case 36: ret = 4539;	break;//$
	case 37: ret = 30617;	break;//%
	case 38: ret = 51577;	break;//&
	case 39: ret = 512;		break;//'
	case 40: ret = 2560;	break;//<
	case 41: ret = 40960;	break;//>
	case 42: ret = 65280;	break;//*
	case 43: ret = 21760;	break;//+
	case 44: ret = 0;		break;//;
	case 45: ret = 17408;	break;//-
	case 46: ret = 0;		break;//.
	case 47: ret = 8704;	break;///
	case 48: ret = 8959;	break;//0
	case 49: ret = 4352;	break;//1
	case 50: ret = 17527;	break;//2
	case 51: ret = 17471;	break;//3
	case 52: ret = 17548;	break;//4
	case 53: ret = 17595;	break;//5
	case 54: ret = 17659;	break;//6
	case 55: ret = 15;		break;//7
	case 56: ret = 17663;	break;//8
	case 57: ret = 17599;	break;//9
	case 58: ret = 33;		break;//=
	case 59: ret = 8193;	break;//;
	case 60: ret = 17456;	break;//==
	case 61: ret = 17456;	break;//=
	case 62: ret = 786;		break;//!!
	case 63: ret = 5127;	break;//?
	}
	return ret;

}

unsigned char AlphanumericDisplay::GetAlphaCharacter(char SegNum){

	unsigned char AlphaConvert = 0;
	unsigned char UseChar;

	UseChar = CharBuffer[SegNum];

	switch (UseChar){
    case 0: ;
	case 1: ;
	case 2: ;
	case 3: ;
	case 4: ;
	case 5: ;
	case 6: ;
	case 7: ;
	case 8: ;
	case 9: ;
	case 10: ;
	case 11: ;
	case 12: ;
	case 13: ;
	case 14: ;
	case 15: ;
	case 16: ;
	case 17: ;
	case 18: ;
	case 19: ;
	case 20: ;
	case 21: ;
	case 22: ;
	case 23: ;
	case 24: ;
	case 25: ;
	case 26: AlphaConvert = (UseChar); break;
    case 27: AlphaConvert = 32; break;
    case 28: AlphaConvert = 28; break;
    case 29: AlphaConvert = 32; break;
    case 30: AlphaConvert = 32; break;
    case 31: AlphaConvert = 31; break;
    case 32: AlphaConvert = 32; break;
    case 33: AlphaConvert = 32; break;
    case 34: AlphaConvert = 34; break;
    case 35: AlphaConvert = 35; break;
    case 36: AlphaConvert = 36; break;
    case 37: AlphaConvert = 37; break;
    case 38: AlphaConvert = 38; break;
    case 39: AlphaConvert = 39; break;
    case 40: AlphaConvert = 40; break;
    case 41: AlphaConvert = 41; break;
    case 42: AlphaConvert = 42; break;
    case 43: AlphaConvert = 43; break;
    case 44: AlphaConvert = 44; break;
    case 45: AlphaConvert = 45; break;
    case 46: AlphaConvert = 46; break;
    case 47: AlphaConvert = 47; break;
	case 48:;
	case 49:;
	case 50:;
	case 51:;
	case 52:;
	case 53:;
	case 54:;
	case 55:;
	case 56: AlphaConvert = (UseChar); break;
    case 57: AlphaConvert = 56; break;
    case 58: AlphaConvert = 57; break;
    case 59: AlphaConvert = 58; break;
    case 60: AlphaConvert = 59; break;
    case 61: AlphaConvert = 60; break;
    case 62: AlphaConvert = 61; break;
    case 63: AlphaConvert = 62; break;
	}

	return AlphaConvert;
}

UINT8 AlphanumericDisplay::GetAlphaDotComma(char SegNum){	
	UINT8 ret;
	ret = CharBuffer2[SegNum];
	return ret;
}
char AlphanumericDisplay::GetAlphaBright(){		
	return Brightness;
}
void AlphanumericDisplay::WriteAlphaBits(unsigned char Reset, unsigned char Clock, unsigned char Data)
{
	// Normalise incoming hardware lines.
	// The caller may pass a bitmask, not literal 0/1.
	Reset = Reset ? 1 : 0;
	Clock = Clock ? 1 : 0;
	Data = Data ? 1 : 0;

	// Active-low reset. While reset is asserted, clear the serial receiver state.
	// Do not necessarily blank CharBuffer here; just resync the display controller.
	if (!Reset)
	{
		PrevClock = Clock;
		InCounter = 0;
		Buffer = 0;
		Character = 0;
		Pointer = 0;
		PrevPointer = 0;
		DigitCounter = 16;
		Brightness = 31;
		return;
	}

	// Falling edge trigger.
	if (PrevClock && !Clock)
	{
		Buffer |= Data;
		InCounter++;

		if (InCounter >= 8)
		{
			Character = Buffer;
			InCounter = 0;
			Buffer = 0;

			WriteAlphaByte(1, Character);
		}
		else
		{
			Buffer <<= 1;
		}
	}

	PrevClock = Clock;
}

void AlphanumericDisplay::WriteAlphaByte(unsigned char Reset, unsigned char Data){

	if (Reset){ //check for alpha NOT reset				
		
		Character = Data;
		InCounter = 0;
		Buffer = 0;
		//Interpret Character
		if ((Character & 128) == 0) {
			//Display Character
			switch (Character & 63) {
			case 44: //Comma
			case 46: //Dot							
				CharBuffer2[PrevPointer] = (Character & 63);
				break;
			default://All Other Characters
				CharBuffer[Pointer] = (Character & 63);
				CharBuffer2[Pointer] = 6;
				PrevPointer = Pointer;
				Pointer += 1;				
				if (Pointer > 15) Pointer = 0;
				break;
			}
		} else {
			//Control Character
			switch (Character & 96){
			case 32:
				//Set Pointer
				switch (Character & 15) {
				case 15:
					PrevPointer = Pointer;
					Pointer = 0;
					break;
				default:
					PrevPointer = Pointer;
					Pointer = ((Character & 15) + 1);
					break;
				}
				break;
			case 64:
				//Set Digit Count
				DigitCounter = (Character & 15);
				if (DigitCounter == 0) DigitCounter = 16;
				break;
			case 96:
				//Set Brightness
				Brightness = (Character & 31);
				break;
			case 0:
				//Test Mode, not emulated
				break;
			}
		}			
	} else {
		//Initialise();
	}
}

void AlphanumericDisplay::Initialise(LoadSaveCompressDLLClass * LSCIn){

	unsigned char cnt;

	LSC = LSCIn;

	Buffer = 0;
	Pointer = 0;
	PrevClock = 0;
	InCounter = 0;	
	Character = 0;
	DigitCounter = 16;
	Brightness = 31;
	PrevPointer = 0;

	for (cnt = 0; cnt < 16; cnt++) {
		CharBuffer[cnt] = 32;
		CharBuffer2[cnt] = 0;
	}

}

void AlphanumericDisplay::SaveState(){

	int loop;

	LSC->SaveToBuffer(PrevClock);
	LSC->SaveToBuffer(PrevPointer);
	LSC->SaveToBuffer(InCounter);
	LSC->SaveToBuffer(Buffer);
	LSC->SaveToBuffer(Character);
	LSC->SaveToBuffer(Pointer);
	LSC->SaveToBuffer(DigitCounter);
	LSC->SaveToBuffer(Brightness);
	for (loop = 0; loop < 16; loop++){
		LSC->SaveToBuffer(CharBuffer[loop]);
		LSC->SaveToBuffer(CharBuffer2[loop]);
	}

}
void AlphanumericDisplay::LoadState(){

	int loop;

	LSC->LoadFromBuffer(PrevClock);
	LSC->LoadFromBuffer(PrevPointer);
	LSC->LoadFromBuffer(InCounter);
	LSC->LoadFromBuffer(Buffer);
	LSC->LoadFromBuffer(Character);
	LSC->LoadFromBuffer(Pointer);
	LSC->LoadFromBuffer(DigitCounter);
	LSC->LoadFromBuffer(Brightness);
	for (loop = 0; loop < 16; loop++){
		LSC->LoadFromBuffer(CharBuffer[loop]);
		LSC->LoadFromBuffer(CharBuffer2[loop]);
	}

}
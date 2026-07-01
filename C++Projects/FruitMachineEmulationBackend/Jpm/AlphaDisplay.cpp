#include "stdafx.h"
#include "AlphaDisplay.h"


AlphanumericDisplay::AlphanumericDisplay(){
	ZeroMemory(CharBuffer, NUMSEGMENTS * sizeof(UINT8));
	ZeroMemory(DotCommaBuffer, NUMSEGMENTS * sizeof(UINT8));
}

AlphanumericDisplay::~AlphanumericDisplay(){
}

UINT16 AlphanumericDisplay::GetAlphaSegments(UINT8 SegNum){
		
	UINT8 StandardisedChar;
	UINT16 ret = 0;

	StandardisedChar = GetAlphaCharacter(SegNum);
	if (StandardisedChar != 0x1f){
		StandardisedChar |= 0;
	}
	//16 segment display
	switch (StandardisedChar) {
	case 0: ret = 0x507F;	break;//@
	case 1: ret = 0x44CF;	break;//A
	case 2: ret = 0x153F;	break;//B
	case 3: ret = 0x00F3;	break;//C
	case 4: ret = 0x113F;	break;//D
	case 5: ret = 0x40F3;	break;//E
	case 6: ret = 0x40C3;	break;//F
	case 7: ret = 0x04FB;	break;//G
	case 8: ret = 0x44CC;	break;//H
	case 9: ret = 0x1133;	break;//I
	case 10: ret = 0x007C;	break;//J
	case 11: ret = 0x4AC0;	break;//K
	case 12: ret = 0x00F0;	break;//L
	case 13: ret = 0x82CC;	break;//M
	case 14: ret = 0x88CC;	break;//N
	case 15: ret = 0x00FF;	break;//O
	case 16: ret = 0x44C7;	break;//P
	case 17: ret = 0x08FF;	break;//Q
	case 18: ret = 0x4CC7;	break;//R
	case 19: ret = 0x44BB;	break;//S
	case 20: ret = 0x1103;	break;//T
	case 21: ret = 0x00FC;	break;//U
	case 22: ret = 0x22C0;	break;//V
	case 23: ret = 0x28CC;	break;//W
	case 24: ret = 0xAA00;	break;//X
	case 25: ret = 0x9200;	break;//Y
	case 26: ret = 0x2233;	break;//Z
	case 27: ret = 0x00E1;	break;// SQUARE OPEN BRACKET
	case 28: ret = 0x8800;	break;// BACKSLASH
	case 29: ret = 0x001E;	break;// SQUARE CLOSED BRACKET
	case 30: ret = 0x2800;	break;//^
	case 31: ret = 0x0030;	break;//_
	case 32: ret = 0x0000;	break;// SPACE
	case 33: ret = 0x8121;	break;//!
	case 34: ret = 0x0180;	break;//"
	case 35: ret = 0x553C;	break;//#
	case 36: ret = 0x11BB;	break;//$
	case 37: ret = 0x7799;	break;//%
	case 38: ret = 0xC979;	break;//&
	case 39: ret = 0x0200;	break;//'
	case 40: ret = 0x0A00;	break;//<
	case 41: ret = 0xA000;	break;//>
	case 42: ret = 0xFF00;	break;//*
	case 43: ret = 0x5500;	break;//+
	case 44: ret = 0x0000;	break;//;
	case 45: ret = 0x4400;	break;//-
	case 46: ret = 0x0000;	break;//.
	case 47: ret = 0x2200;	break;///
	case 48: ret = 0x22FF;	break;//0
	case 49: ret = 0x1100;	break;//1
	case 50: ret = 0x4477;	break;//2
	case 51: ret = 0x443F;	break;//3
	case 52: ret = 0x448C;	break;//4
	case 53: ret = 0x44BB;	break;//5
	case 54: ret = 0x44FB;	break;//6
	case 55: ret = 0x000F;	break;//7
	case 56: ret = 0x44FF;	break;//8
	case 57: ret = 0x44BF;	break;//9
	case 58: ret = 0x0021;	break;//=
	case 59: ret = 0x2001;	break;//;
	case 60: ret = 0x4430;	break;//==
	case 61: ret = 0x4430;	break;//=
	case 62: ret = 0x0312;	break;//!!
	case 63: ret = 0x1407;	break;//?
	}
	return ret;

}

UINT8 AlphanumericDisplay::GetAlphaCharacter(UINT8 SegNum){

	UINT8 AlphaConvert;
	UINT8 UseChar = CharBuffer[SegNum];

	switch (UseChar){    
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
    case 57: AlphaConvert = 56; break;
    case 58: AlphaConvert = 57; break;
    case 59: AlphaConvert = 58; break;
    case 60: AlphaConvert = 59; break;
    case 61: AlphaConvert = 60; break;
    case 62: AlphaConvert = 61; break;
    case 63: AlphaConvert = 62; break;
	default:
		AlphaConvert = UseChar;
	}

	return AlphaConvert;
}

UINT8 AlphanumericDisplay::GetAlphaDotComma(UINT8 SegNum){
	UINT8 ret = DotCommaBuffer[SegNum];
	return ret;
}

UINT8 AlphanumericDisplay::GetAlphaBright(){
	return Brightness;
}
void AlphanumericDisplay::WriteAlphaBits(UINT8 Reset, UINT8 Clock, UINT8 Data)
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
		DigitCounter = NUMSEGMENTS;
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

void AlphanumericDisplay::WriteAlphaByte(UINT8 Reset, UINT8 Data){

	if (Reset){ //check for alpha NOT reset				
		
		Character = Data;
		InCounter = 0;
		Buffer = 0;
		//Interpret Character
		if ((Character & 0x80) == 0) {
			//Display Character
			switch (Character & 63) {
			case 44: //Comma
			case 46: //Dot							
				DotCommaBuffer[PrevPointer] = (Character & 63);
				break;
			default://All Other Characters
				CharBuffer[Pointer] = (Character & 63);
				DotCommaBuffer[Pointer] = 6;
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
	}
}

void AlphanumericDisplay::Initialise(LoadSaveClass * LSCIn){

	UINT8 cnt;

	LSC = LSCIn;

	Buffer = 0;
	Pointer = 0;
	PrevClock = 0;
	InCounter = 0;	
	Character = 0;
	DigitCounter = NUMSEGMENTS;
	Brightness = 31;
	PrevPointer = 0;

	for (cnt = 0; cnt < NUMSEGMENTS; cnt++) {
		CharBuffer[cnt] = 32;
		DotCommaBuffer[cnt] = 0;
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
	for (loop = 0; loop < NUMSEGMENTS; loop++){
		LSC->SaveToBuffer(CharBuffer[loop]);
		LSC->SaveToBuffer(DotCommaBuffer[loop]);
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
	for (loop = 0; loop < NUMSEGMENTS; loop++){
		LSC->LoadFromBuffer(CharBuffer[loop]);
		LSC->LoadFromBuffer(DotCommaBuffer[loop]);
	}

}
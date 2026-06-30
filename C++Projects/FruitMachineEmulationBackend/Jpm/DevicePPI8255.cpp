// ###########################################################################
// #
// # DevicePPI8255 - Definition of 8255 PPI Driver
// # Copyright (C) 2002-2012 Tony Friery [DialTone]
// #
// # ALL RIGHTS RESERVED
// #
// ###########################################################################

//#pragma hdrstop
#include "stdafx.h"
#include "DevicePPI8255.h"

///////////////////////////////////////////////////////////////////////
//
//  FUNCTION:	DevicePPI8255::writeA
//
//  PURPOSE:	Internal function used to update the state of port A
//					after considering the latch state and I/O state
//
//  INPUTS:		void
//
//  OUTPUT:		void
//
///////////////////////////////////////////////////////////////////////
void __fastcall DevicePPI8255::_writeA(void) {
	UINT8 temp = PortA;

	PortA = (Latch[0] & ~IO[0]) | (0xff & IO[0]);
	PortAChanged = temp ^ PortA;
	PortAUpdated = true;
}

///////////////////////////////////////////////////////////////////////
//
//  FUNCTION:	DevicePPI8255::writeB
//
//  PURPOSE:	Internal function used to update the state of port B
//					after considering the latch state and I/O state
//
//  INPUTS:		void
//
//  OUTPUT:		void
//
///////////////////////////////////////////////////////////////////////
void __fastcall DevicePPI8255::_writeB(void) {
	UINT8 temp = PortB;

	PortB = (Latch[1] & ~IO[1]) | (0xff & IO[1]);
	PortBChanged = temp ^ PortB;
	PortBUpdated = true;
}

///////////////////////////////////////////////////////////////////////
//
//  FUNCTION:	DevicePPI8255::writeC
//
//  PURPOSE:	Internal function used to update the state of port C
//					after considering the latch state and I/O state
//
//  INPUTS:		void
//
//  OUTPUT:		void
//
///////////////////////////////////////////////////////////////////////
void __fastcall DevicePPI8255::_writeC(void) {
	UINT8 temp = PortC;

	PortC = (Latch[2] & ~IO[2]) | (0xff & IO[2]);
	PortCChanged = temp ^ PortC;
	PortCUpdated = true;
}

///////////////////////////////////////////////////////////////////////
//
//  FUNCTION:	DevicePPI8255::Reset
//
//  PURPOSE:	(POR) Reset the emulated 8255 Hardware
//
//  INPUTS:		void
//
//  OUTPUT:		void
//
///////////////////////////////////////////////////////////////////////
void __fastcall DevicePPI8255::Reset(LoadSaveCompressDLLClass * LSCIn) {
	
	LSC = LSCIn;
	// Reset Port Group Modes
	GroupAMode = 0;
	GroupBMode = 0;

	// Reset DDR Registers and Output Latches
	for (int i = 0; i < 3; i++) {
		IO[i] = 0xff;
		Latch[i] = 0;
	}

	// Reset Input Latches
	PortA		= PortB
				= PortC
				= 0;
}

///////////////////////////////////////////////////////////////////////
//
//  FUNCTION:	DevicePPI8255::Read
//
//  PURPOSE:	Read from PPI Registers
//
//  INPUTS:		UINT16				Offset into the PPI Register Array
//											(Only 0x00 thru 0x03 are valid)
//
//  OUTPUT:		UINT8					Valued read from emulated hardware
//
///////////////////////////////////////////////////////////////////////
UINT8 __fastcall DevicePPI8255::Read(UINT16 offset) {
	UINT8 result = 0;

	if (offset > 3)
		return 0xff;

	switch (offset) {
		case 0:
			// Port A Read
			if (!IO[0])
				result = Latch[0];
			else
				result = PortAIn;
			break;
		case 1:
			// Port B Read
			if (!IO[1])
				result = Latch[1];
			else
				result = PortBIn;
			break;
		case 2:
			// Port C Read
			result = (Latch[2] & ~IO[2]) | (PortCIn & IO[2]);
			break;
		case 3:
			// Control Word (returns 0xff as it cannot be read)
			result = 0xff;
			break;
	 }

	 // Return value read from 8255
	 return result;
}

///////////////////////////////////////////////////////////////////////
//
//  FUNCTION:	DevicePPI8255::Write
//
//  PURPOSE:	Write to PPI Registers
//
//  INPUTS:		UINT16				Offset into the PPI Register Array
//											(Only 0x00 thru 0x03 are valid)
//             UINT8					Valued to be written to emulated hardware
//
//  OUTPUT:		void
//
///////////////////////////////////////////////////////////////////////
void __fastcall DevicePPI8255::Write(UINT16 offset, UINT8 data)
{
	if (offset > 3)
	{
		return;
	}

	PortAChanged = PortBChanged = PortCChanged = 0;
	PortAUpdated = PortBUpdated = PortCUpdated = false;

	switch (offset)
	{
		case 0:
			// Port A Write
			Latch[0] = data;
			_writeA();
			break;
		case 1:
			// Port B Write
			Latch[1] = data;
			_writeB();
			break;
		case 2:
			// Port C Write
			Latch[2] = data;
			_writeC();
			break;
		case 3:
			// Control Word
			if (data & 0x80) {
				GroupAMode = (data >> 5) & 3;
				GroupBMode = (data >> 2) & 1;

				if (GroupAMode || GroupBMode)
					return;

				// Port A Direction
				if (data & 0x10)
					IO[0] = 0xff;
				else
					IO[0] = 0x00;

				// Port B Direction
				if (data & 0x02)
					IO[1] = 0xff;
				else
					IO[1] = 0x00;

				// Port C Upper Direction
				if (data & 0x08)
					IO[2] |= 0xf0;
				else
					IO[2] &= 0x0f;

				// Port C Lower Direction
				if (data & 0x01)
					IO[2] |= 0x0f;
				else
					IO[2] &= 0xf0;

				_writeA();
				_writeB();
				_writeC();

				Latch[0] = Latch[1] = Latch[2] = 0;

				_writeA();
				_writeB();
				_writeC();
			} else {
				// Bit Set/Reset
				int bit = (data >> 1) & 0x07;

				if (data & 1)
					Latch[2] |= (1 << bit);		// Set bit
				else
					Latch[2] &= ~(1 << bit);	// Reset bit

				_writeC();
			}
			break;
	}
}

///////////////////////////////////////////////////////////////////////
//
//  FUNCTION:	DevicePPI8255::DevicePPI8255
//
//  PURPOSE:	Constructor for DevicePPI8255 Class
//
//  INPUTS:		(none)
//
//  OUTPUT:		(none)
//
///////////////////////////////////////////////////////////////////////
DevicePPI8255::DevicePPI8255()
{	
	ZeroMemory(IO, 3 * sizeof(UINT8));
	ZeroMemory(Latch, 3 * sizeof(UINT8));
}

DevicePPI8255::~DevicePPI8255()
{	
}

void DevicePPI8255::SaveState(){
	
	LSC->SaveToBuffer(GroupAMode);
	LSC->SaveToBuffer(GroupBMode);
	for (int i = 0; i < 3; i++){
		LSC->SaveToBuffer(IO[i]);
		LSC->SaveToBuffer(Latch[i]);	
	}
	LSC->SaveToBuffer(PortA);
	LSC->SaveToBuffer(PortB);
	LSC->SaveToBuffer(PortC);
	LSC->SaveToBuffer(PortAIn);
	LSC->SaveToBuffer(PortBIn);
	LSC->SaveToBuffer(PortCIn);
	LSC->SaveToBuffer(PortAChanged);
	LSC->SaveToBuffer(PortBChanged);
	LSC->SaveToBuffer(PortCChanged);
	LSC->SaveToBuffer(PortAUpdated);
	LSC->SaveToBuffer(PortBUpdated);
	LSC->SaveToBuffer(PortCUpdated);
}
void DevicePPI8255::LoadState(){
	int loop;

	LSC->LoadFromBuffer(GroupAMode);
	LSC->LoadFromBuffer(GroupBMode);
	for (loop = 0; loop < 3; loop++){
		LSC->LoadFromBuffer(IO[loop]);
		LSC->LoadFromBuffer(Latch[loop]);	
	}
	LSC->LoadFromBuffer(PortA);
	LSC->LoadFromBuffer(PortB);
	LSC->LoadFromBuffer(PortC);
	LSC->LoadFromBuffer(PortAIn);
	LSC->LoadFromBuffer(PortBIn);
	LSC->LoadFromBuffer(PortCIn);
	LSC->LoadFromBuffer(PortAChanged);
	LSC->LoadFromBuffer(PortBChanged);
	LSC->LoadFromBuffer(PortCChanged);
	LSC->LoadFromBuffer(PortAUpdated);
	LSC->LoadFromBuffer(PortBUpdated);
	LSC->LoadFromBuffer(PortCUpdated);
}
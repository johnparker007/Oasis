// ###########################################################################
// #
// # DevicePPI8255 - Definition of 8255 PPI Driver
// # Copyright (C) 2002-2012 Tony Friery [DialTone]
// #
// # ALL RIGHTS RESERVED
// #
// ###########################################################################
#pragma once

#include "LoadSave.h"

class DevicePPI8255 {
public:

	UINT8	
		GroupAMode = 0,
		GroupBMode = 0,
		IO[3],
		Latch[3],
		PortA = 0,
		PortB = 0,
		PortC = 0,
		PortAIn = 0,
		PortBIn = 0,
		PortCIn = 0,
		PortAChanged = 0,
		PortBChanged = 0,
		PortCChanged = 0;

	bool
		PortAUpdated = false,
		PortBUpdated = false,
		PortCUpdated = false;

	UINT8 __fastcall	Read(UINT16 offset);
	void __fastcall		Write(UINT16 offset, UINT8 val);
	void __fastcall		Reset(LoadSaveClass* LSCIn);

	void SaveState();
	void LoadState();

	DevicePPI8255();
	~DevicePPI8255();

private:

	void __fastcall		_writeA(void);
	void __fastcall		_writeB(void);
	void __fastcall		_writeC(void);

	LoadSaveClass * LSC = NULL;

};
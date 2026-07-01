#ifndef DEVICEEDCUNIT
#define DEVICEEDCUNIT

#include "LoadSave.h"

#define EDCBUFFERSIZE 256

class EDCUNIT {
public:

	void __fastcall		Write(UINT8 ByteIn);
	void __fastcall		Reset(LoadSaveClass* LSCIn);

	char* __fastcall	getEDCString();

	void SaveState();
	void LoadState();

	EDCUNIT();
	~EDCUNIT();

private:

	char EDCBuf[EDCBUFFERSIZE];
	UINT16 EDCLength = 0;
	UINT16 EDCSaveLength = 0;

	LoadSaveClass* LSC = NULL;

};

#endif // DEVICEEDCUNIT
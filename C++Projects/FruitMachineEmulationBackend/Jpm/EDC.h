#ifndef DEVICEEDCUNIT
#define DEVICEEDCUNIT

#include "LoadSaveCompressDLLClass_SAFE.h"

#define EDCBUFFERSIZE 256

class EDCUNIT {
public:

	void __fastcall		Write(UINT8 ByteIn);
	void __fastcall		Reset(LoadSaveCompressDLLClass* LSCIn);

	char* __fastcall	getEDCString();

	void SaveState();
	void LoadState();

	EDCUNIT();
	~EDCUNIT();

private:

	char EDCBuf[EDCBUFFERSIZE];
	UINT16 EDCLength = 0;
	UINT16 EDCSaveLength = 0;

	LoadSaveCompressDLLClass* LSC = NULL;

};

#endif // DEVICEEDCUNIT
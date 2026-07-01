// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "Interface.h"
#include "System6.h"
#include <iostream>

using namespace std;

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
					 )
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}
//Instance
 SYSTEM6 *sys6_board;

Interface_API float GetDLLVersion(void){		
	return DLLVersion;
}

Interface_API void LoadState(void){
	sys6_board->LoadState();
}
Interface_API void SaveState(void){
	sys6_board->SaveState();
}
Interface_API void SetCFolder(UINT8 * Folder){
	sys6_board->SetCFolder(Folder);
}
Interface_API void SetCFileName(UINT8 * FileName){
	sys6_board->SetCFileName(FileName);
}

Interface_API UINT8 Shutdown(void)
{
	UINT8 ret = 0;

	if (sys6_board)
	{
		delete sys6_board;
		ret = 1;
	}
	
	return ret;
}

// Controls
 Interface_API UINT8 Initialise(void)
{
	UINT8 ret = 0;

	if (!sys6_board)
	{		
		sys6_board = new SYSTEM6();
		
	}
	
	if (sys6_board){
		sys6_board->Init();
		ret = 1;
	}

	return ret;
}
 Interface_API signed long LoadROM(UINT8 *name1, UINT8*name2, UINT8*name3, UINT8*name4){
	
	FILE *file1;
	FILE *file2;

	UINT8 *buffer1, *buffer2;	
	UINT8 Enable1,Enable2;

	UINT32 TotalSize, fileLen1, fileLen2, cnt;	

	if (name1 == NULL){
		Enable1 = 0;
	} else {
		Enable1 = 1;
	}
	if (name2 == NULL){
		Enable2 = 0;
	} else {
		Enable2 = 1;
	}
	
	buffer1 = NULL;
	buffer2 = NULL;

	//ROM File 1
	if (Enable1){
		//Open file
		fopen_s(&file1, (char*)name1, "rb");
		if (!file1){			
			return 0;
		}
	
		//Get file length
		fseek(file1, 0L, SEEK_END);
		fileLen1 = ftell(file1);
		fseek(file1, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen1 < 0) return 0;

		//Allocate memory
		buffer1 = new UINT8[fileLen1];
		if (!buffer1)
		{		
			fclose(file1);
			return 0;
		}

		//Read file contents into buffer
		fread(buffer1, fileLen1, 1, file1);
		fclose(file1);
	} else {
		return 0;
	}

	//ROM File 2
	if (Enable2){
		//Open file
		fopen_s(&file2, (char*)name2, "rb");
		if (!file2){			
			return 0;
		}
	
		//Get file length
		fseek(file2, 0L, SEEK_END);
		fileLen2 = ftell(file2);
		fseek(file2, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen2 < 0) return 0;

		//Allocate memory
		buffer2 = new UINT8[fileLen2];
		if (!buffer2)
		{		
			fclose(file2);
			return 0;
		}

		//Read file contents into buffer
		fread(buffer2, fileLen2, 1, file2);
		fclose(file2);
	} else {
		return 0;
	}

	//Clear ROM Space
	ZeroMemory(sys6_board->ROM, 0x100000);

	TotalSize = (fileLen1 + fileLen2);	

	if (TotalSize > 0x100000) return 0;
	if (fileLen1 < 0) return 0;
	if (fileLen2 < 0) return 0;

	INT32 size1 = sizeof(buffer1[fileLen1]);
	INT32 size2 = sizeof(buffer2[fileLen2]);

	//ROM1	
	if (Enable1){		
		for (cnt = 0; (cnt < (fileLen1)); cnt++) {
				sys6_board->ROM[cnt * 2] = (buffer1[cnt] & 255);
		}
		delete (buffer1);
	}
	//ROM2
	if (Enable2){		
		for (cnt = 0; (cnt < fileLen2); cnt ++) {
			sys6_board->ROM[cnt * 2 + 1] = (buffer2[cnt] & 255);
		}
		delete (buffer2);	
	}

	return TotalSize;
}

 Interface_API void Reset(void)
{

	sys6_board->Reset();

}

 Interface_API INT32 Run(UINT32 Cycles)
 {
	INT32 ret = sys6_board->Run(Cycles);
	return ret;
 } 
 Interface_API UINT8 GetAlphaChar(UINT8 Num){
	UINT8 ret = sys6_board->GetAlphaChar(Num);
	return ret;
 }
 Interface_API int GetAlphaSegments(UINT8 CharIn)
 {	 
	 UINT32 ret = sys6_board->GetAlphaSegs(CharIn);
	 return ret;
 }
 Interface_API UINT8 GetAlphaDotComma(UINT8 SegIn){
	 UINT8 ret = sys6_board->GetAlphaDotComma(SegIn);
	 return ret;
 }
 Interface_API UINT8 GetAlphaBright(){
	 UINT8 ret = sys6_board->GetAlphaBright();
	 return ret;
 }
 Interface_API INT16 GetPosOut(UINT8 num)
 {	 
	 INT16 ret = sys6_board->GetPosOut(num);
	 return ret;
 }
Interface_API void SetOptoInvert(UINT8 ReelNum, UINT8 State){
	sys6_board->SetOptoInvert(ReelNum, State);
}
Interface_API void SetOptoStart(UINT8 ReelNum, UINT8 Start){
	sys6_board->SetOptoStart(ReelNum, Start);
}
Interface_API void SetOptoEnd(UINT8 ReelNum, UINT8 End){
	sys6_board->SetOptoEnd(ReelNum, End);
}
Interface_API void SetSteps(UINT8 ReelNum, UINT8 Steps){
	sys6_board->SetSteps(ReelNum, Steps);
}
Interface_API void UpdateLamps(void){
	sys6_board->UpdateLamps();
}

Interface_API float GetLampBrightness(UINT16 num) {
	return sys6_board->GetLampBrightness(num);
}

Interface_API bool GetLampsOn(UINT16 num) {
	return sys6_board->GetLampsOn(num);
}

Interface_API float GetFilamentColourR(UINT16 num){
	return sys6_board->GetFilamentColour(num).x;
}

Interface_API float GetFilamentColourG(UINT16 num){
	return sys6_board->GetFilamentColour(num).y;
}

Interface_API float GetFilamentColourB(UINT16 num){
	return sys6_board->GetFilamentColour(num).z;
}
Interface_API void UpdateSegs(void){
	sys6_board->UpdateSegs();
}
Interface_API UINT8 GetSegOn(UINT16 num){
	UINT8 ret = sys6_board->GetSegOn(num);
	return ret;
}
Interface_API UINT8 GetSegBright(UINT16 num){
	UINT8 ret = sys6_board->GetSegBright(num);
	return ret;
}
Interface_API UINT32 GetMeterCounter(UINT8 num){	
	UINT32 ret = sys6_board->GetMeterCounter(num);
	return ret;
}
Interface_API void TurnSwitchOn(int num){
	sys6_board->TurnSwitchOn(num & 0xff);
}
Interface_API void TurnSwitchOff(int num){
	sys6_board->TurnSwitchOff(num & 0xff);
}
Interface_API UINT8 ReadSwitch(UINT8 num){
	UINT8 ret = sys6_board->ReadSwitch(num);
	return ret;
}
Interface_API UINT8 CoinIn(UINT8 Coin, UINT8 CoinValue){
	UINT8 ret = sys6_board->CoinIn(Coin, CoinValue);
	return ret;
}
Interface_API void SetCommStyle(UINT8 Style){
	sys6_board->SetCommStyle(Style);
}
Interface_API void SetCommInvert(UINT8 Invert){
	sys6_board->SetCommInvert(Invert);
}
Interface_API void SetCycles(UINT32 Cycles){
	sys6_board->SetCycles(Cycles);
}
Interface_API void SetEDCEnable(UINT8 Enable){
	sys6_board->SetEDCEnable(Enable);
}
Interface_API void SetLockoutVal(UINT8 Coin, UINT8 Value){
	sys6_board->SetLockoutVal(Coin, Value);
}
Interface_API void SetLockoutInvert(UINT8 Coin, UINT8 Invert){
	sys6_board->SetLockoutInvert(Coin, Invert);
}
Interface_API void SetCoinValue(UINT8 CoinNum, UINT8 Value)
{
	sys6_board->SetCoinValue(CoinNum, Value);
}
Interface_API void SetCoinEnable(UINT8 CoinNum, UINT8 Value)
{
	sys6_board->SetCoinEnable(CoinNum, Value);
}
Interface_API UINT8 GetCoinLampOnOff(UINT8 LampNum)
{
	UINT8 ret = sys6_board->GetCoinLampOnOff(LampNum);
	return ret;
}	
Interface_API signed long LoadSoundROM(UINT8*name1, UINT8*name2, UINT8*name3, UINT8*name4){

	FILE *file1;
	FILE *file2;
	FILE *file3;
	FILE *file4;
	
	UINT8 *buffer1;
	UINT8*buffer2;
	UINT8*buffer3;
	UINT8*buffer4;
	
	UINT8 Enable1;
	UINT8 Enable2;
	UINT8 Enable3;
	UINT8 Enable4;


	UINT32 Offset1;
	UINT32 Offset2;
	UINT32 Offset3;
	UINT32 Offset4;

	UINT32 TotalSize;
	UINT32 fileLen1;
	UINT32 fileLen2;
	UINT32 fileLen3;
	UINT32 fileLen4;

	UINT32 cnt;
	UINT32 offset;
	UINT32 Position;
	UINT32 NextPos;

	buffer1 = 0;
	buffer2 = 0;
	buffer3 = 0;
	buffer4 = 0;

	if (name1 == NULL){
		Enable1 = 0;
	} else {
		Enable1 = 1;
	}
	if (name2 == NULL){
		Enable2 = 0;
	} else {
		Enable2 = 1;
	}
	if (name3 == NULL){
		Enable3 = 0;
	} else {
		Enable3 = 1;
	}
	if (name4 == NULL){
		Enable4 = 0;
	} else {
		Enable4 = 1;
	}
	//ROM File 1
	if (Enable1){
		//Open file
		file1 = fopen((char*)name1, "rb");
		if (!file1){
			return 0;
		}
	
		//Get file length
		fseek(file1, 0L, SEEK_END);
		fileLen1 = ftell(file1);
		fseek(file1, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen1 < 0) return 0;

		//Allocate memory
		buffer1 = new UINT8[fileLen1 + 1];
		if (!buffer1)
		{		
			fclose(file1);
			return 0;
		}

		//Read file contents into buffer
		fread(buffer1, fileLen1, 1, file1);
		fclose(file1);

	} else {
		fileLen1 = 0;
	}
	//ROM File 2
	if (Enable2){
		//Open file
		file2 = fopen((char*)name2, "rb");
		if (!file2){
			return 0;
		}
	
		//Get file length
		fseek(file2, 0L, SEEK_END);
		fileLen2 = ftell(file2);
		fseek(file2, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen2 < 0) return 0;

		//Allocate memory
		buffer2 = new UINT8[fileLen2 + 1];
		if (!buffer2)
		{		
			fclose(file2);
			//fclose (DebugFile2);
			return 0;
		}

		//Read file contents into buffer
		fread(buffer2, fileLen2, 1, file2);
		fclose(file2);		
	} else {
		fileLen2 = 0;
	}
	//ROM File 3
	if (Enable3){
		//Open file
		file3 = fopen((char*)name3, "rb");
		if (!file3){
			return 0;
		}
		
		//Get file length
		fseek(file3, 0L, SEEK_END);
		fileLen3 = ftell(file3);
		fseek(file3, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen3 < 0) return 0;

		//Allocate memory
		buffer3 = new UINT8[fileLen3 + 1];
		if (!buffer3)
		{		
			fclose(file3);			
			return 0;
		}

		//Read file contents into buffer
		fread(buffer3, fileLen3, 1, file3);
		fclose(file3);

	} else {
		fileLen3 = 0;
	}
	//ROM File 4
	if (Enable4){
		//Open file
		file4 = fopen((char*)name4, "rb");
		if (!file4){			
			return 0;
		}
	
		//Get file length
		fseek(file4, 0L, SEEK_END);
		fileLen4 = ftell(file4);
		fseek(file4, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen4 < 0) return 0;

		//Allocate memory
		buffer4 = new UINT8[fileLen4 + 1];
		if (!buffer4)
		{		
			fclose(file4);
			return 0;
		}

		//Read file contents into buffer
		fread(buffer4, fileLen4, 1, file4);
		fclose(file4);

	} else {
		fileLen4 = 0;
	}

	TotalSize = (fileLen1 + fileLen2 + fileLen3 + fileLen4);

	NextPos = (TotalSize - 1);

	//Clear ROM Space
	for (cnt = 0; (cnt < (1048576 * 8)); cnt++) {
		sys6_board->Sound.SetMemory(cnt, 0);
	}

	//Load ROMs to Memory Space
	//ROM1	
	if (Enable1){
		Position = NextPos;
		NextPos = (Position - fileLen1);
		offset = (NextPos + 1);	
		Offset1 = offset;

		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.SetMemory(cnt ,buffer1[cnt - offset] & 0xff);
		}		
	}
	//ROM2
	if (Enable2){
		Position = NextPos;
		NextPos = (Position - fileLen2);
		offset = (NextPos + 1);	
		Offset2 = offset;

		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.SetMemory(cnt, buffer2[cnt - offset] & 0xff);
		}		
	}
	//ROM3
	if (Enable3){
		Position = NextPos;
		NextPos = (Position - fileLen3);
		offset = (NextPos + 1);	
		Offset3 = offset;

		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.SetMemory(cnt, buffer3[cnt - offset] & 0xff);
		}		
	}
	//ROM4
	if (Enable4){
		Position = NextPos;
		NextPos = (Position - fileLen4);
		offset = (NextPos + 1);	
		Offset4 = offset;

		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.SetMemory(cnt, buffer4[cnt - offset] & 0xff);
		}		
	}	

	//Delete Buffers
	if (Enable1){ delete(buffer1);}
	if (Enable2){ delete(buffer2);}
	if (Enable3){ delete(buffer3);}
	if (Enable4){ delete(buffer4);}

	sys6_board->Sound.SetROMSize(TotalSize);
	sys6_board->Sound.ExtractROM();

	return TotalSize;
}

Interface_API UINT8 GetStatusLED(void){
	UINT8 ret = sys6_board->GetStatusLED();
	return ret;
}

Interface_API void SetEnable(UINT8 Num, UINT8 Enabl){
	sys6_board->SetEnable(Num, Enabl);
}
Interface_API void SetCounterIn(UINT8 Num, UINT32 Count){
	sys6_board->SetCounterIn(Num, Count);
}
Interface_API void SetCounterOut(UINT8 Num, UINT32 Count){
	sys6_board->SetCounterOut(Num, Count);
}
Interface_API void SetPortIndex(UINT8 Num, UINT8 Index){
	sys6_board->SetPortIndex(Num, Index);
}
Interface_API void SetCoin(UINT8 Num, UINT8 CoinIn){
	sys6_board->SetCoin(Num, CoinIn);
}
Interface_API void SetLevel(UINT8 Num, UINT8 LevelIn){
	sys6_board->SetLevel(Num, LevelIn);
}
Interface_API void SetFullLevel(UINT8 Num, UINT8 LevelIn){
	sys6_board->SetFullLevel(Num, LevelIn);
}
Interface_API void SetLoEnable(UINT8 Num, UINT8 Enabl){
	sys6_board->SetLoEnable(Num, Enabl);
}
Interface_API void SetLoInvert(UINT8 Num, UINT8 Invert){
	sys6_board->SetLoInvert(Num, Invert);
}
Interface_API void SetLoSwitch(UINT8 Num, UINT8 Switch){
	sys6_board->SetLoSwitch(Num, Switch);
}
Interface_API void SetLoLevel(UINT8 Num, UINT32 LevelIn){
	sys6_board->SetLoLevel(Num, LevelIn);
}
Interface_API void SetHiEnable(UINT8 Num, UINT8 Enabl){
	sys6_board->SetHiEnable(Num, Enabl);
}
Interface_API void SetHiInvert(UINT8 Num, UINT8 Invert){
	sys6_board->SetHiInvert(Num, Invert);
}
Interface_API void SetHiSwitch(UINT8 Num, UINT8 Switch){
	sys6_board->SetHiSwitch(Num, Switch);
}
Interface_API void SetHiLevel(UINT8 Num, UINT32 LevelIn){
	sys6_board->SetHiLevel(Num, LevelIn);
}

Interface_API UINT8 GetEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetEnable(Num);
	return ret;
}
Interface_API UINT32 GetCounterIn(UINT8 Num){
	UINT32 ret = sys6_board->GetCounterIn(Num);
	return ret;
}
Interface_API UINT32 GetCounterOut(UINT8 Num){
	UINT32 ret = sys6_board->GetCounterOut(Num);
	return ret;
}
Interface_API UINT8 GetPortIndex(UINT8 Num){
	UINT8 ret = sys6_board->GetPortIndex(Num);
	return ret;
}
Interface_API UINT8 GetCoin(UINT8 Num){
	UINT8 ret = sys6_board->GetCoin(Num);
	return ret;
}
Interface_API UINT32 GetLevel(UINT8 Num){	
	UINT32 ret = sys6_board->GetLevel(Num);
	return ret;
}
Interface_API UINT32 GetFullLevel(UINT8 Num){
	UINT32 ret = sys6_board->GetFullLevel(Num);
	return ret;
}
Interface_API UINT8 GetLoEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetLoEnable(Num);
	return ret;
}
Interface_API UINT8 GetLoInvert(UINT8 Num){
	UINT8 ret = sys6_board->GetLoInvert(Num);
	return ret;
}
Interface_API UINT8 GetLoSwitch(UINT8 Num){
	UINT8 ret = sys6_board->GetLoSwitch(Num);
	return ret;
}
Interface_API UINT32 GetLoLevel(UINT8 Num){	
	UINT32 ret = sys6_board->GetLoLevel(Num);
	return ret;
}
Interface_API UINT8 GetHiEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetHiEnable(Num);
	return ret;
}
Interface_API UINT8 GetHiInvert(UINT8 Num){
	UINT8 ret = sys6_board->GetHiInvert(Num);
	return ret;
}
Interface_API UINT8 GetHiSwitch(UINT8 Num){
	UINT8 ret = sys6_board->GetHiSwitch(Num);
	return ret;
}
Interface_API UINT32 GetHiLevel(UINT8 Num){	
	UINT32 ret = sys6_board->GetHiLevel(Num);
	return ret;
}
Interface_API void SaveRAM(UINT8 * FileString){
	sys6_board->SaveRAM(FileString);
}
Interface_API void LoadRAM(UINT8 * FileString){
	sys6_board->LoadRAM(FileString);
}
Interface_API void SetDIP(UINT8 Num, UINT8 Value){
	sys6_board->SetDIP(Num, Value);
}
Interface_API void SetHopperEnable(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperEnable(Num, Value);
}
Interface_API void SetHopperCoin(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperCoin(Num, Value);
}
Interface_API void SetHopperCoinsIn(UINT8 Num, UINT32 Value){
	sys6_board->SetHopperCoinsIn(Num, Value);
}
Interface_API void SetHopperCoinsOut(UINT8 Num, UINT32 Value){
	sys6_board->SetHopperCoinsOut(Num, Value);
}
Interface_API void SetHopperLevel(UINT8 Num, UINT32 Value){
	sys6_board->SetHopperLevel(Num, Value);
}
Interface_API void SetHopperFullLevel(UINT8 Num, UINT32 Value){
	sys6_board->SetHopperFullLevel(Num, Value);
}
Interface_API void SetHopperLoEnable(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperLoEnable(Num, Value);
}
Interface_API void SetHopperLoInvert(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperLoInvert(Num, Value);
}
Interface_API void SetHopperLoSwitch(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperLoSwitch(Num, Value);
}
Interface_API void SetHopperLoLevel(UINT8 Num, UINT32 Value){
	sys6_board->SetHopperLoLevel(Num, Value);
}
Interface_API void SetHopperHiEnable(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperHiEnable(Num, Value);
}
Interface_API void SetHopperHiInvert(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperHiInvert(Num, Value);
}
Interface_API void SetHopperHiSwitch(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperHiSwitch(Num, Value);
}
Interface_API void SetHopperHiLevel(UINT8 Num, UINT32 Value){
	sys6_board->SetHopperHiLevel(Num, Value);
}
Interface_API void SetHopperOptoEnable(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperOptoEnable(Num, Value);
}
Interface_API void SetHopperOptoReturn(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperOptoReturn(Num, Value);
}
Interface_API void SetHopperMotorEnable(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperMotorEnable(Num, Value);
}
Interface_API void SetHopperLoIndicator(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperLoIndicator(Num, Value);
}
Interface_API void SetHopperHiIndicator(UINT8 Num, UINT8 Value){
	sys6_board->SetHopperHiIndicator(Num, Value);
}
Interface_API void SetHopperCoinsRefilled(UINT8 Num, UINT32 Value){
	sys6_board->SetHopperCoinsRefilled(Num, Value);
}

Interface_API UINT8 GetHopperEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperCoin(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperCoin(Num);
	return ret;
}
Interface_API UINT32 GetHopperCoinsIn(UINT8 Num){
	UINT32 ret = sys6_board->GetHopperCoinsIn(Num);
	return ret;
}
Interface_API UINT32 GetHopperCoinsOut(UINT8 Num){
	UINT32 ret = sys6_board->GetHopperCoinsOut(Num);
	return ret;
}
Interface_API UINT32 GetHopperLevel(UINT8 Num){
	UINT32 ret = sys6_board->GetHopperLevel(Num);
	return ret;
}
Interface_API UINT32 GetHopperFullLevel(UINT8 Num){
	UINT32 ret = sys6_board->GetHopperFullLevel(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperLoEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoInvert(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperLoInvert(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoSwitch(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperLoSwitch(Num);
	return ret;
}
Interface_API UINT32 GetHopperLoLevel(UINT8 Num){
	UINT32 ret = sys6_board->GetHopperLoLevel(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperHiEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiInvert(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperHiInvert(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiSwitch(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperHiSwitch(Num);
	return ret;
}
Interface_API UINT32 GetHopperHiLevel(UINT8 Num){
	UINT32 ret = sys6_board->GetHopperHiLevel(Num);
	return ret;
}
Interface_API UINT8 GetHopperOptoEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperOptoEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperOptoReturn(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperOptoReturn(Num);
	return ret;
}
Interface_API UINT8 GetHopperMotorEnable(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperMotorEnable(Num);
	return ret;
}
Interface_API UINT32 GetHopperCoinsRefilled(UINT8 Num){
	UINT32 ret = sys6_board->GetHopperCoinsRefilled(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiIndicator(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperHiIndicator(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoIndicator(UINT8 Num){
	UINT8 ret = sys6_board->GetHopperLoIndicator(Num);
	return ret;
}

Interface_API void SetStake(UINT8 Stake){
	sys6_board->SetStake(Stake);
}
Interface_API void SetPrize(UINT8 Prize){
	sys6_board->SetPrize(Prize);
}
Interface_API void SetPercent(UINT8 Percent){
	sys6_board->SetPercent(Percent);
}
Interface_API UINT8* GetEDCString(){
	return sys6_board->GetEDCString();
}

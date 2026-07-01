// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "Interface.h"
#include "System6.h"
#include <iostream>
//#include <fstream>
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

Interface_API void SYSTEM6LoadState(void){
	sys6_board->LoadState();
}
Interface_API void SYSTEM6SaveState(void){
	sys6_board->SaveState();
}
Interface_API void SYSTEM6SetCFolder(char * Folder){
	sys6_board->SetCFolder(Folder);
}
Interface_API void SYSTEM6SetCFileName(char * FileName){
	sys6_board->SetCFileName(FileName);
}

Interface_API unsigned char SYSTEM6Shutdown(void)
{
	unsigned char ret = 0;

	if (sys6_board)
	{
		delete sys6_board;
		ret = 1;
	}
	
	return ret;
}

//SYSTEM6 Controls
 Interface_API unsigned char SYSTEM6Initialise(void)
{
	unsigned char ret = 0;

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
 Interface_API signed long SYSTEM6LoadROM(char *name1, char *name2, char *name3, char *name4, char FlashSw){
	
	//FlashSw is ignored
	FILE *file1;
	FILE *file2;
	//FILE *DebugFile;

	char *buffer1, *buffer2;	
	char Enable1,Enable2;
	signed long TotalSize, fileLen1, fileLen2, cnt;	

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
	
	buffer1 = 0;
	buffer2 = 0;
	
	//fopen_s(&DebugFile, "Debug.txt","a");   	   
   	   
	
	//ROM File 1
	if (Enable1){
		//Open file
		//fprintf(DebugFile, "ROM1: %s \n", name1);
		fopen_s(&file1, name1, "rb");
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
		buffer1 = (char *)malloc(fileLen1);
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
		//fprintf(DebugFile, "ROM2: %s \n", name2);
		fopen_s(&file2, name2, "rb");
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
		buffer2 = (char *)malloc(fileLen2);
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
	

	//Clear ROM SPace
	ZeroMemory(sys6_board->ROM, 0x100000);

	//fprintf(DebugFile, "ROM1 Size: %X \n", fileLen1);
	//fprintf(DebugFile, "ROM2 Size: %X \n", fileLen2);
	TotalSize = (fileLen1 + fileLen2);	
	//fprintf(DebugFile, "Total ROM Size: %X \n", TotalSize);
	
	if (TotalSize > 0x100000) return 0;
	if (fileLen1 < 0) return 0;
	if (fileLen2 < 0) return 0;

	int size1 = sizeof(buffer1[fileLen1]);
	int size2 = sizeof(buffer2[fileLen1]);

	//ROM1	
	if (Enable1){		
		for (cnt = 0; (cnt < (fileLen1)); cnt++) {
			//if (cnt < size1) {
				sys6_board->ROM[cnt * 2] = (buffer1[cnt] & 255);
			//}
		}
		free(buffer1);
	}
	//ROM2
	if (Enable2){		
		for (cnt = 0; (cnt < fileLen2); cnt ++) {
			//if (cnt < size2) {
				sys6_board->ROM[cnt * 2 + 1] = (buffer2[cnt] & 255);
			//}
		}
		free(buffer2);	
	}
		
	//fclose (DebugFile);

	return TotalSize;
}

 Interface_API void SYSTEM6Reset(void)
{

	sys6_board->Reset();

}

 Interface_API int SYSTEM6Run(int Cycles)
{
	int ret = 0;	

	ret = sys6_board->Run(Cycles);

	return ret;
}
Interface_API UINT8 GetAlphaChar(UINT8 Num){
	UINT8 ret;
	ret = sys6_board->GetAlphaChar(Num);
	return ret;
}
 Interface_API int SYSTEM6GetAlphaSegments(char CharIn){
	 
	 int ret;
	 ret = sys6_board->GetAlphaSegs(CharIn);
	 return ret;
}
 Interface_API char SYSTEM6GetAlphaDotComma(char SegIn){
	 char ret;
	 ret = sys6_board->GetAlphaDotComma(SegIn);
	 return ret;
 }
 Interface_API char SYSTEM6GetAlphaBright(){
	 char ret;
	 ret = sys6_board->GetAlphaBright();
	 return ret;
 }
 Interface_API signed short SYSTEM6GetPosOut(char num){

	 short ret;
	 ret = sys6_board->GetPosOut(num);
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
Interface_API void SYSTEM6UpdateLamps(void){
	sys6_board->UpdateLamps();
}

Interface_API float SYSTEM6GetLampBrightness(UINT16 num) {
	return sys6_board->GetLampBrightness(num);
}

Interface_API bool SYSTEM6GetLampsOn(UINT16 num) {
	return sys6_board->GetLampsOn(num);
}

Interface_API float SYSTEM6GetFilamentColourR(UINT16 num){
	return sys6_board->GetFilamentColour(num).x;
}

Interface_API float SYSTEM6GetFilamentColourG(UINT16 num){
	return sys6_board->GetFilamentColour(num).y;
}

Interface_API float SYSTEM6GetFilamentColourB(UINT16 num){
	return sys6_board->GetFilamentColour(num).z;
}

Interface_API void SYSTEM6UpdateSegs(void){
	sys6_board->UpdateSegs();
}
Interface_API unsigned char SYSTEM6GetSegOn(unsigned short num){
	unsigned char ret;
	ret = sys6_board->GetSegOn(num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetSegBright(unsigned short num){
	unsigned char ret;
	ret = sys6_board->GetSegBright(num);
	return ret;
}
Interface_API unsigned int SYSTEM6GetMeterCounter(unsigned char num){
	unsigned int ret;
	ret = sys6_board->GetMeterCounter(num);
	return ret;
}
Interface_API void SYSTEM6TurnSwitchOn(int num){
	sys6_board->TurnSwitchOn(num & 0xff);
}
Interface_API void SYSTEM6TurnSwitchOff(int num){
	sys6_board->TurnSwitchOff(num & 0xff);
}
Interface_API unsigned char SYSTEM6ReadSwitch(unsigned char num){
	unsigned char ret;
	ret = sys6_board->ReadSwitch(num);
	return ret;
}
Interface_API unsigned char SYSTEM6CoinIn(unsigned char Num, unsigned char Coin, unsigned char CoinValue){
	unsigned char ret;
	ret = sys6_board->CoinIn(Num, Coin, CoinValue);
	return ret;
}
Interface_API void SYSTEM6SetCommStyle(unsigned char Num, unsigned char Style){
	sys6_board->SetCommStyle(Num, Style);
}
Interface_API void SYSTEM6SetCommInvert(unsigned char Num, unsigned char Invert){
	sys6_board->SetCommInvert(Num, Invert);
}
Interface_API void SYSTEM6SetCycles(unsigned char Num, unsigned int Cycles){
	sys6_board->SetCycles(Num, Cycles);
}
Interface_API void SYSTEM6SetEDCEnable(unsigned char Num, unsigned char Enable){
	sys6_board->SetEDCEnable(Num, Enable);
}
Interface_API void SYSTEM6SetLockoutVal(unsigned char Num, unsigned char Coin, unsigned char Value){
	sys6_board->SetLockoutVal(Num, Coin, Value);
}
Interface_API void SYSTEM6SetLockoutInvert(unsigned char Num, unsigned char Coin, unsigned char Invert){
	sys6_board->SetLockoutInvert(Num, Coin, Invert);
}
Interface_API void SYSTEM6SetCoinValue(unsigned char Num, unsigned char CoinNum, unsigned char Value)
{
	sys6_board->SetCoinValue(Num, CoinNum, Value);
}
Interface_API void SYSTEM6SetCoinEnable(unsigned char Num, unsigned char CoinNum, unsigned char Value)
{
	sys6_board->SetCoinEnable(Num, CoinNum, Value);
}
Interface_API unsigned char SYSTEM6GetLampOnOff(unsigned char Num, unsigned char LampNum)
{
	unsigned char ret;
	ret = sys6_board->GetLampOnOff(Num, LampNum);
	return ret;
}	
Interface_API signed long SYSTEM6LoadSoundROM(char *name1, char *name2, char *name3, char *name4){

	FILE *file1;
	FILE *file2;
	FILE *file3;
	FILE *file4;
	
	char *buffer1;
	char *buffer2;
	char *buffer3;
	char *buffer4;
	
	unsigned char Enable1;
	unsigned char Enable2;
	unsigned char Enable3;
	unsigned char Enable4;


	signed long Offset1;
	signed long Offset2;
	signed long Offset3;
	signed long Offset4;

	signed long TotalSize;
	signed long fileLen1;
	signed long fileLen2;
	signed long fileLen3;
	signed long fileLen4;

	signed long cnt;	
    signed long offset;
	signed long Position;
	signed long NextPos;

	//FILE *DebugFile2;
	//DebugFile2 = fopen ("Sound Debug.txt","a");     
	//fprintf(DebugFile2, "LOAD SOUND BEGIN: %d \n", 0); 
	
	//fprintf(DebugFile2, "Name1: %s \n", name1);
	//fprintf(DebugFile2, "Name2: %s \n", name2);
	//fprintf(DebugFile2, "Name3: %s \n", name3);
	//(DebugFile2, "Name4: %s \n", name4);

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
		file1 = fopen(name1, "rb");
		if (!file1){
			fclose(file1);
			//fclose(DebugFile2);
			return 0;
		}
	
		//Get file length
		fseek(file1, 0L, SEEK_END);
		fileLen1 = ftell(file1);
		fseek(file1, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen1 < 0) return 0;

		//Allocate memory
		buffer1 = (char *)malloc(fileLen1 + 1);
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
		file2 = fopen(name2, "rb");
		if (!file2){
			fclose(file2);
			//fclose (DebugFile2);
			return 0;
		}
	
		//Get file length
		fseek(file2, 0L, SEEK_END);
		fileLen2 = ftell(file2);
		fseek(file2, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen2 < 0) return 0;

		//Allocate memory
		buffer2 = (char *)malloc(fileLen2 + 1);
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
		file3 = fopen(name3, "rb");
		if (!file3){
			fclose(file3);
			//fclose (DebugFile2);
			return 0;
		}
		
		//Get file length
		fseek(file3, 0L, SEEK_END);
		fileLen3 = ftell(file3);
		fseek(file3, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen3 < 0) return 0;

		//Allocate memory
		buffer3 = (char *)malloc(fileLen3 + 1);
		if (!buffer3)
		{		
			fclose(file3);
			//fclose (DebugFile2);
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
		file4 = fopen(name4, "rb");
		if (!file4){
			fclose(file4);
			//fclose (DebugFile2);
			return 0;
		}
	
		//Get file length
		fseek(file4, 0L, SEEK_END);
		fileLen4 = ftell(file4);
		fseek(file4, 0L, SEEK_SET);	

		//Check File Len didn't error
		if (fileLen4 < 0) return 0;

		//Allocate memory
		buffer4 = (char *)malloc(fileLen4 + 1);
		if (!buffer4)
		{		
			fclose(file4);
			//fclose (DebugFile2);
			return 0;
		}

		//Read file contents into buffer
		fread(buffer4, fileLen4, 1, file4);
		fclose(file4);

	} else {
		fileLen4 = 0;
	}

	//fprintf(DebugFile2, "Size1: %X \n", fileLen1);
	//fprintf(DebugFile2, "Size2: %X \n", fileLen2);
	//fprintf(DebugFile2, "Size3: %X \n", fileLen3);
	//fprintf(DebugFile2, "Size4: %X \n", fileLen4);

	TotalSize = (fileLen1 + fileLen2 + fileLen3 + fileLen4);
	
	//fprintf(DebugFile2, "Total Sound Size: %X \n", TotalSize);

	NextPos = (TotalSize - 1);

	//Clear ROM SPace
	for (cnt = 0; (cnt < (1048576 * 8)); cnt++) {
		sys6_board->Sound.Memory_Space[cnt] = 0;
	}
	//Load ROMs to Memory Space
	//ROM1	
	if (Enable1){
		Position = NextPos;
		NextPos = (Position - fileLen1);
		offset = (NextPos + 1);	
		Offset1 = offset;
		//fprintf(DebugFile2, "Offset1: %X \n", Offset1);
		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.Memory_Space[cnt] = (buffer1[cnt - offset] & 255);
		}		
	}
	//ROM2
	if (Enable2){
		Position = NextPos;
		NextPos = (Position - fileLen2);
		offset = (NextPos + 1);	
		Offset2 = offset;
		//fprintf(DebugFile2, "Offset2: %X \n", Offset2);
		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.Memory_Space[cnt] = (buffer2[cnt - offset] & 255);
		}		
	}
	//ROM3
	if (Enable3){
		Position = NextPos;
		NextPos = (Position - fileLen3);
		offset = (NextPos + 1);	
		Offset3 = offset;
		//fprintf(DebugFile2, "Offset3: %X \n", Offset3);
		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.Memory_Space[cnt] = (buffer3[cnt - offset] & 255);
		}		
	}
	//ROM4
	if (Enable4){
		Position = NextPos;
		NextPos = (Position - fileLen4);
		offset = (NextPos + 1);	
		Offset4 = offset;
		//fprintf(DebugFile2, "Offset4: %X \n", Offset4);
		for (cnt = Position; (cnt > NextPos); cnt--) {
			sys6_board->Sound.Memory_Space[cnt] = (buffer4[cnt - offset] & 255);
		}		
	}	

	//Free Buffers
	if (Enable1){ free(buffer1);}
	if (Enable2){ free(buffer2);}
	if (Enable3){ free(buffer3);}
	if (Enable4){ free(buffer4);}
	//fprintf(DebugFile2, "LOAD SOUND END: %d \n", 0);  
	//fclose (DebugFile2);
	sys6_board->Sound.ROMSize = TotalSize;
	sys6_board->Sound.ExtractROM();

	return TotalSize;
}

Interface_API unsigned char SYSTEM6GetStatusLED(void){
	unsigned char ret;
	ret = sys6_board->GetStatusLED();
	return ret;
}

Interface_API void SYSTEM6SetEnable(unsigned char Num, unsigned char Enabl){
	sys6_board->SetEnable(Num, Enabl);
}
Interface_API void SYSTEM6SetCounterIn(unsigned char Num, unsigned long Count){
	sys6_board->SetCounterIn(Num, Count);
}
Interface_API void SYSTEM6SetCounterOut(unsigned char Num, unsigned long Count){
	sys6_board->SetCounterOut(Num, Count);
}
Interface_API void SYSTEM6SetPortIndex(unsigned char Num, unsigned char Index){
	sys6_board->SetPortIndex(Num, Index);
}
Interface_API void SYSTEM6SetCoin(unsigned char Num, unsigned char CoinIn){
	sys6_board->SetCoin(Num, CoinIn);
}
Interface_API void SYSTEM6SetLevel(unsigned char Num, unsigned char LevelIn){
	sys6_board->SetLevel(Num, LevelIn);
}
Interface_API void SYSTEM6SetFullLevel(unsigned char Num, unsigned char LevelIn){
	sys6_board->SetFullLevel(Num, LevelIn);
}
Interface_API void SYSTEM6SetLoEnable(unsigned char Num, unsigned char Enabl){
	sys6_board->SetLoEnable(Num, Enabl);
}
Interface_API void SYSTEM6SetLoInvert(unsigned char Num, unsigned char Invert){
	sys6_board->SetLoInvert(Num, Invert);
}
Interface_API void SYSTEM6SetLoSwitch(unsigned char Num, unsigned char Switch){
	sys6_board->SetLoSwitch(Num, Switch);
}
Interface_API void SYSTEM6SetLoLevel(unsigned char Num, signed long LevelIn){
	sys6_board->SetLoLevel(Num, LevelIn);
}
Interface_API void SYSTEM6SetHiEnable(unsigned char Num, unsigned char Enabl){
	sys6_board->SetHiEnable(Num, Enabl);
}
Interface_API void SYSTEM6SetHiInvert(unsigned char Num, unsigned char Invert){
	sys6_board->SetHiInvert(Num, Invert);
}
Interface_API void SYSTEM6SetHiSwitch(unsigned char Num, unsigned char Switch){
	sys6_board->SetHiSwitch(Num, Switch);
}
Interface_API void SYSTEM6SetHiLevel(unsigned char Num, signed long LevelIn){
	sys6_board->SetHiLevel(Num, LevelIn);
}

Interface_API unsigned char SYSTEM6GetEnable(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetEnable(Num);
	return ret;
}
Interface_API unsigned long SYSTEM6GetCounterIn(unsigned char Num){
	unsigned long ret;
	ret = sys6_board->GetCounterIn(Num);
	return ret;
}
Interface_API unsigned long SYSTEM6GetCounterOut(unsigned char Num){
	unsigned long ret;
	ret = sys6_board->GetCounterOut(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetPortIndex(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetPortIndex(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetCoin(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetCoin(Num);
	return ret;
}
Interface_API long SYSTEM6GetLevel(unsigned char Num){
	long ret;
	ret = sys6_board->GetLevel(Num);
	return ret;
}
Interface_API long SYSTEM6GetFullLevel(unsigned char Num){
	long ret;
	ret = sys6_board->GetFullLevel(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetLoEnable(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetLoEnable(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetLoInvert(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetLoInvert(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetLoSwitch(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetLoSwitch(Num);
	return ret;
}
Interface_API signed long SYSTEM6GetLoLevel(unsigned char Num){
	signed long ret;
	ret = sys6_board->GetLoLevel(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetHiEnable(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetHiEnable(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetHiInvert(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetHiInvert(Num);
	return ret;
}
Interface_API unsigned char SYSTEM6GetHiSwitch(unsigned char Num){
	unsigned char ret;
	ret = sys6_board->GetHiSwitch(Num);
	return ret;
}
Interface_API signed long SYSTEM6GetHiLevel(unsigned char Num){
	signed long ret;
	ret = sys6_board->GetHiLevel(Num);
	return ret;
}
Interface_API void SYSTEM6SaveRAM(char * FileString){
	sys6_board->SaveRAM(FileString);
}
Interface_API void SYSTEM6LoadRAM(char * FileString){
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
	UINT8 ret = 0;
	ret = sys6_board->GetHopperEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperCoin(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperCoin(Num);
	return ret;
}
Interface_API UINT32 GetHopperCoinsIn(UINT8 Num){
	UINT32 ret = 0;
	ret = sys6_board->GetHopperCoinsIn(Num);
	return ret;
}
Interface_API UINT32 GetHopperCoinsOut(UINT8 Num){
	UINT32 ret = 0;
	ret = sys6_board->GetHopperCoinsOut(Num);
	return ret;
}
Interface_API UINT32 GetHopperLevel(UINT8 Num){
	UINT32 ret = 0;
	ret = sys6_board->GetHopperLevel(Num);
	return ret;
}
Interface_API UINT32 GetHopperFullLevel(UINT8 Num){
	UINT32 ret = 0;
	ret = sys6_board->GetHopperFullLevel(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoEnable(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperLoEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoInvert(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperLoInvert(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoSwitch(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperLoSwitch(Num);
	return ret;
}
Interface_API UINT32 GetHopperLoLevel(UINT8 Num){
	UINT32 ret = 0;
	ret = sys6_board->GetHopperLoLevel(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiEnable(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperHiEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiInvert(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperHiInvert(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiSwitch(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperHiSwitch(Num);
	return ret;
}
Interface_API UINT32 GetHopperHiLevel(UINT8 Num){
	UINT32 ret = 0;
	ret = sys6_board->GetHopperHiLevel(Num);
	return ret;
}
Interface_API UINT8 GetHopperOptoEnable(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperOptoEnable(Num);
	return ret;
}
Interface_API UINT8 GetHopperOptoReturn(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperOptoReturn(Num);
	return ret;
}
Interface_API UINT8 GetHopperMotorEnable(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperMotorEnable(Num);
	return ret;
}
Interface_API UINT32 GetHopperCoinsRefilled(UINT8 Num){
	UINT32 ret = 0;
	ret = sys6_board->GetHopperCoinsRefilled(Num);
	return ret;
}
Interface_API UINT8 GetHopperHiIndicator(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperHiIndicator(Num);
	return ret;
}
Interface_API UINT8 GetHopperLoIndicator(UINT8 Num){
	UINT8 ret = 0;
	ret = sys6_board->GetHopperLoIndicator(Num);
	return ret;
}

Interface_API void SetStake(char Stake){
	sys6_board->SetStake(Stake);
}
Interface_API void SetPrize(char Prize){
	sys6_board->SetPrize(Prize);
}
Interface_API void SetPercent(char Percent){
	sys6_board->SetPercent(Percent);
}
Interface_API char* GetEDCString(){
	return sys6_board->GetEDCString();
}

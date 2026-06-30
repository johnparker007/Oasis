#ifndef SolenoidsH
#define SolenoidsH

#include "LoadSaveCompressDLLClass_SAFE.h"

#define NUMSOLENOIDS 8
class SolenoidPayout {
protected:

private:

	//Solenoids
	unsigned char Pin[NUMSOLENOIDS];	
	unsigned char Enable[NUMSOLENOIDS];	
	unsigned char PrevPin[NUMSOLENOIDS];		
	unsigned long CounterIn[NUMSOLENOIDS];
	unsigned long CounterOut[NUMSOLENOIDS];	
	unsigned char PortIndex[NUMSOLENOIDS];	
	unsigned char Coin[NUMSOLENOIDS];	
	signed long Level[NUMSOLENOIDS];
	unsigned char LoEnable[NUMSOLENOIDS];
	unsigned char LoSwitch[NUMSOLENOIDS];
	unsigned char LoState[NUMSOLENOIDS];
	unsigned char LoInvert[NUMSOLENOIDS];
	signed long LoLevel[NUMSOLENOIDS];
	unsigned char HiEnable[NUMSOLENOIDS];
	unsigned char HiSwitch[NUMSOLENOIDS];
	signed long HiLevel[NUMSOLENOIDS];
	unsigned char HiState[NUMSOLENOIDS];
	unsigned char HiInvert[NUMSOLENOIDS];
	signed long FullLevel[NUMSOLENOIDS];

	LoadSaveCompressDLLClass * LSC = NULL;

public:	

	SolenoidPayout();
	~SolenoidPayout();	

	//Set Subs
	void SetEnable(unsigned char Num, unsigned char Enable);
	void SetCounterIn(unsigned char Num, unsigned long Count);
	void SetCounterOut(unsigned char Num, unsigned long Count);
	void SetPortIndex(unsigned char Num, unsigned char Index);
	void SetCoin(unsigned char Num, unsigned char Coin);
	void SetLevel(unsigned char Num, unsigned char Level);
	void SetFullLevel(unsigned char Num, unsigned char Level);
	void SetLoEnable(unsigned char Num, unsigned char Enable);
	void SetLoInvert(unsigned char Num, unsigned char Invert);
	void SetLoSwitch(unsigned char Num, unsigned char Switch);
	void SetLoLevel(unsigned char Num, signed long Level);
	void SetHiEnable(unsigned char Num, unsigned char Enable);
	void SetHiInvert(unsigned char Num, unsigned char Invert);
	void SetHiSwitch(unsigned char Num, unsigned char Switch);
	void SetHiLevel(unsigned char Num, signed long Level);
	void SetPort(unsigned char Port);

	//Get Functions
	unsigned char GetEnable(unsigned char Num);
	unsigned long GetCounterIn(unsigned char Num);
	unsigned long GetCounterOut(unsigned char Num);
	unsigned char GetPortIndex(unsigned char Num);
	unsigned char GetCoin(unsigned char Num);
	signed long GetLevel(unsigned char Num);
	signed long GetFullLevel(unsigned char Num);
	unsigned char GetLoEnable(unsigned char Num);
	unsigned char GetLoInvert(unsigned char Num);
	unsigned char GetLoSwitch(unsigned char Num);
	signed long GetLoLevel(unsigned char Num);
	unsigned char GetLoState(unsigned char Num);
	unsigned char GetHiEnable(unsigned char Num);
	unsigned char GetHiInvert(unsigned char Num);
	unsigned char GetHiSwitch(unsigned char Num);
	unsigned char GetHiState(unsigned char Num);
	signed long GetHiLevel(unsigned char Num);
	unsigned char GetPort(void);

	//Main Subs
	void Write(unsigned char Port);
	unsigned char CoinIn(unsigned char CoinCode);
	void Init(LoadSaveCompressDLLClass * LSCIn);
	void Update(void);

	void SaveState();
	void LoadState();
};

#endif SolenoidsH
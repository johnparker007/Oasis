#ifndef MetersH
#define MetersH

#include "LoadSaveCompressDLLClass_SAFE.h"


#define FITTEDMETERS 6
class CoinMeter {
private:

	LoadSaveCompressDLLClass * LSC;

	unsigned char Pin[FITTEDMETERS];	
	unsigned char On[FITTEDMETERS];	
	unsigned char Enable[FITTEDMETERS];	
	unsigned char PrevPin[FITTEDMETERS];	
	unsigned long TimeOn[FITTEDMETERS];
	unsigned long Counter[FITTEDMETERS];
	unsigned char PortIndex[FITTEDMETERS];	
public:	
	CoinMeter();
	~CoinMeter();
	

	void Write(unsigned char Index, unsigned char PinIn);
	void Run(unsigned short Cycles);
	unsigned char Check(void);
	
	unsigned long GetCounter(unsigned char Num);
	void SetCounter(unsigned char Num, unsigned long Value);

	void Init(LoadSaveCompressDLLClass * LSCIn);

	void SaveState();
	void LoadState();
};

#endif MetersH
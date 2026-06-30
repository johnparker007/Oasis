#ifndef CoinMechsH
#define CoinMechsH

#include "LoadSaveCompressDLLClass_SAFE.h"

#define NUMCOINS 6

class ElecronicCoinMech {
public:

	~ElecronicCoinMech();
	ElecronicCoinMech();

	unsigned char CoinIn(unsigned char Coin);
	unsigned char Run(unsigned short Cycles);
	void Init(LoadSaveCompressDLLClass* LSCIn);
	void SetCommStyle(unsigned char Style);
	void SetCommInvert(unsigned char Invert);
	void SetCycles(unsigned int Cycles);
	void SetEDCEnable(unsigned char Enable);
	void SetLockoutVal(unsigned char Coin, unsigned char Value);
	void SetLockoutInvert(unsigned char Coin, unsigned char Invert);
	void SetLockoutPort(unsigned char Port);
	void SetSelectedCoin(unsigned char Coin);
	void SetCoinValue(unsigned char Num, unsigned char Value);
	void SetCoinEnable(unsigned char Num, unsigned char Value);
	unsigned char GetLampOnOff(unsigned char Num);
	unsigned char GetSelectedCoin();
	UINT8 GetCommStyle();
	UINT8 GetCommInvert();
	UINT8 GetBCD();

	void SaveState();
	void LoadState(); 

private:

	int InputCounter = 0;
	int LockCounter = 0;
	unsigned char CommStyle = 0;
	unsigned char CommInvert = 0;
	unsigned int PulseCycles = 0;
	unsigned char EDCEnable = 0;
	unsigned char LockoutVal[NUMCOINS];
	unsigned char LockoutInvert[NUMCOINS];
	unsigned char CoinValue[NUMCOINS];
	unsigned char CoinEnable[NUMCOINS];
	unsigned char LockoutPort = 0;
	unsigned char SelectedCoin = 0;
	unsigned char LampOnOff[2];
	
	int	CoinsIn2p = 0,
		CoinsIn5p = 0,
		CoinsIn10p = 0,
		CoinsIn20p = 0,
		CoinsIn50p = 0,
		CoinsIn100p = 0,
		CoinsIn200p = 0,
		TokensIn5p = 0,
		TokensIn10p = 0,
		TokensIn20p = 0,
		TokensIn50p = 0,
		TokensIn100p = 0,
		TokensIn200p = 0,
		TokensIn = 0, 
		CoinsIn = 0, 
		TotalIn = 0;

	UINT8 BCD = 0;

	LoadSaveCompressDLLClass * LSC = NULL;
};

class ElecronicNoteMech {
private:
	
public:	
	
	//char CoinIn(unsigned char Coin);	
	//void Run(unsigned short Cycles);	
	//void Init(void);
};

class ElectroMechanicalCoin {
private:
	
public:	

	//char CoinIn(unsigned char Coin);
	//void Run(unsigned short Cycles);	
	//void Init(void);
};

#endif CoinMechsH
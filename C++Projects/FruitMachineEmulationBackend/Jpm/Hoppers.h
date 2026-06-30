#ifndef HoppersH
#define HoppersH

#define FITTEDHOPPERS 2
#define STAGE1TIME 3000000
#define STAGE2TIME 40000

class HopperPayout{
protected:

private:	
		
	//Internal Variables		
	UINT8	Motor[FITTEDHOPPERS];
	UINT8	PrevMotor[FITTEDHOPPERS];
	UINT8	OptoEnable[FITTEDHOPPERS];
	UINT8	OptoFlag[FITTEDHOPPERS];
	long	Timer[FITTEDHOPPERS];
	UINT8	State[FITTEDHOPPERS];
	
	UINT8	CoinSelect[FITTEDHOPPERS];
	UINT8	LowSwitch[FITTEDHOPPERS];
	UINT8	LowEnable[FITTEDHOPPERS];
	UINT8	LowInvert[FITTEDHOPPERS];
	UINT8	LowIndicator[FITTEDHOPPERS];
	UINT32	LowLevel[FITTEDHOPPERS];	
	UINT32	Level[FITTEDHOPPERS];
	UINT8	HiSwitch[FITTEDHOPPERS];
	UINT8	HiEnable[FITTEDHOPPERS];
	UINT8	HiInvert[FITTEDHOPPERS];
	UINT8	HiIndicator[FITTEDHOPPERS];
	UINT32	HiLevel[FITTEDHOPPERS];	
	UINT32	FullLevel[FITTEDHOPPERS];
	UINT32	CounterIn[FITTEDHOPPERS];
	UINT32	CounterOut[FITTEDHOPPERS];
	UINT32	CounterRefill[FITTEDHOPPERS];
	UINT8	OptoEnablePort[FITTEDHOPPERS];
	UINT8	OptoReturnPort[FITTEDHOPPERS];
	UINT8	MotorEnablePort[FITTEDHOPPERS];

	UINT8 CoinInputMode = 0;

public:	

	UINT8	Select = 0;
	UINT8	Enable[FITTEDHOPPERS];
	//Motor On/Off
	void WriteMotor(UINT8 HNum, UINT8 MotorOn);
	//Opto Enable
	void WriteOptoEnable(UINT8 HNum, UINT8 OptoIn);
	//Read Opto Flag
	UINT8 ReadOpto(UINT8 HNum);
	//Reset Sub
	void Reset(UINT8 HNum);
	//Update The Hopper
	void Update(UINT8 HNum, unsigned short Cycles);
	//Insert A Coin
	UINT8 CoinIn(UINT8 CoinCode);

	void SetHopperEnable(UINT8 Num, UINT8 Value);
	void SetHopperCoin(UINT8 Num, UINT8 Value);
	void SetHopperCoinsIn(UINT8 Num, UINT32 Value);
	void SetHopperCoinsOut(UINT8 Num, UINT32 Value);	
	void SetHopperLevel(UINT8 Num, UINT32 Value);
	void SetHopperFullLevel(UINT8 Num, UINT32 Value);	
	void SetHopperLoEnable(UINT8 Num, UINT8 Value);
	void SetHopperLoInvert(UINT8 Num, UINT8 Value);
	void SetHopperLoSwitch(UINT8 Num, UINT8 Value);	
	void SetHopperLoLevel(UINT8 Num, UINT32 Value);
	void SetHopperHiEnable(UINT8 Num, UINT8 Value);
	void SetHopperHiInvert(UINT8 Num, UINT8 Value);
	void SetHopperHiSwitch(UINT8 Num, UINT8 Value);	
	void SetHopperHiLevel(UINT8 Num, UINT32 Value);
	void SetHopperOptoEnable(UINT8 Num, UINT8 Value);
	void SetHopperOptoReturn(UINT8 Num, UINT8 Value);
	void SetHopperMotorEnable(UINT8 Num, UINT8 Value);
	void SetHopperLoIndicator(UINT8 Num, UINT8 Value);
	void SetHopperHiIndicator(UINT8 Num, UINT8 Value);
	void SetHopperCoinsRefilled(UINT8 Num, UINT32 Value);

	UINT8 GetHopperEnable(UINT8 Num);
	UINT8 GetHopperCoin(UINT8 Num);
	UINT32 GetHopperCoinsIn(UINT8 Num);
	UINT32 GetHopperCoinsOut(UINT8 Num);	
	UINT32 GetHopperLevel(UINT8 Num);
	UINT32 GetHopperFullLevel(UINT8 Num);
	UINT8 GetHopperLoEnable(UINT8 Num);
	UINT8 GetHopperLoInvert(UINT8 Num);
	UINT8 GetHopperLoSwitch(UINT8 Num);	
	UINT32 GetHopperLoLevel(UINT8 Num);
	UINT8 GetHopperHiEnable(UINT8 Num);
	UINT8 GetHopperHiInvert(UINT8 Num);
	UINT8 GetHopperHiSwitch(UINT8 Num);	
	UINT32 GetHopperHiLevel(UINT8 Num);
	UINT8 GetHopperOptoEnable(UINT8 Num);
	UINT8 GetHopperOptoReturn(UINT8 Num);
	UINT8 GetHopperMotorEnable(UINT8 Num);
	UINT32 GetHopperCoinsRefilled(UINT8 Num);
	UINT8 GetHopperHiIndicator(UINT8 Num);
	UINT8 GetHopperLoIndicator(UINT8 Num);

	HopperPayout();
	~HopperPayout();
};

#endif HoppersH
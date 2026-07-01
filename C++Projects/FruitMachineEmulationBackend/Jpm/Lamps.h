#pragma once

#include "LoadSave.h"

#define EMULATEDCYCLESPERSECOND 8000000.0f								//8 Mhz Main CPU
#define NUMSTROBELINES			16										//16 Strobe Lines
#define NUMDATALINES			16										//16 Data Lines
#define NUMLAMPS				(NUMSTROBELINES * NUMDATALINES)			//Total Number of Lamps
#define INPUTVOLTAGEAC			50.0f									//50 Volts AC Lamp Supply
#define M_PI					3.1415926535897932384626433832795f		//Pi Constant

struct float3 {
	float x, y, z;

	// Constructors
	float3() : x(0.0f), y(0.0f), z(0.0f) {}
	float3(float scalar) : x(scalar), y(scalar), z(scalar) {}
	float3(float x, float y, float z) : x(x), y(y), z(z) {}

	// Compound division assignment operator
	float3& operator/=(const float3& rhs) {
		x /= rhs.x;
		y /= rhs.y;
		z /= rhs.z;
		return *this;
	}
};

class Lamping {
private:
	
	LoadSaveClass * LSC = NULL;

	typedef struct Filament {

		bool Lit = false;
		UINT8	Powered = 0;
		UINT32	OnCycles = 0, OffCycles = 0, DutyCycles = 0, Period = 0;
		float3 Colour = float3();
		float  Brightness = 0.f;

	} Filament;

	Filament Bulbs[NUMLAMPS];
	
	float InputRMSVoltage = 0.f;

	//General Stuff	
	UINT8 StrobeVal = 0;							//Current Strobe Value
	UINT8 PrevStrobe = 0;							//Previous Strobe Value
	UINT16 DataVal[NUMSTROBELINES];					//Lamp Data

	//Internal Values
	UINT8 Intensity = 0;							//Internal value part of IMPACT board
	UINT8 IntensityEnable = 0;						//Enable Intensity

public:	
	
	Lamping();	
	~Lamping();
	
	//Subroutines / Functions
	void __fastcall Reset(LoadSaveClass * LSCIn);			//Reset Subroutine
	void __fastcall WriteData(UINT16 data);					//Matrix Data	
	void __fastcall WriteStrobe(UINT8 strobe);				//Matrix Strobe
	void __fastcall Run(UINT16 InstructionCycles);			//Run Lamps		
	void __fastcall Update(void);							//Update
	
	//Input Functions
	void __fastcall SetIntensity(UINT8);					//Sets internal value
	
	//Output Functions
	float __fastcall GetLampBrightness(UINT16 Num);			//Returns brightness of requested lamp 
	bool __fastcall GetLampsOn(UINT16 Num);					//Returns status of requested lamp (On or Off)
	float3 __fastcall GetFilamentColour(UINT16 Num);		//Returns colour of requested lamp filament
	UINT8 __fastcall GetStrobeVal();
	UINT8 __fastcall GetIntensityEnable();
	void __fastcall SetIntensityEnable(UINT8 value);

	//State Save & Load
	void __fastcall SaveState();
	void __fastcall LoadState();

};
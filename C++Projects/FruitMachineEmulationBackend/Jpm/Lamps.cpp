#include "stdafx.h"
#include "Lamps.h"
#include "iostream"

Lamping::~Lamping(){
	
}

Lamping::Lamping(){

	ZeroMemory(DataVal, NUMSTROBELINES * sizeof(UINT16));

	//Calculate RMS voltage from AC voltage
	InputRMSVoltage = (INPUTVOLTAGEAC / 1.414213562373095f); // voltage/sqrt(2)

	//The values in the data sheet for these bulbs (10mm 12v 1.2W Wedge Bulb) @ 12v (google "CML 1112LF" for info)
	//			Current	Lumens		
	//Max		110mA	6.25
	//Nominal	100mA	5.0
	//Min		90mA	3.8
	// 4.166666666666667 lumens per watt
}

void Lamping::SetIntensity(UINT8 Intens) {
	Intensity = Intens;
}

void Lamping::Reset(LoadSaveClass * LSCIn){

	ZeroMemory(Bulbs, sizeof(Bulbs));
	LSC = LSCIn;
}

void Lamping::WriteStrobe(UINT8 strobe){

	if (strobe != StrobeVal) {

		//Collect Pulse Width Data
		for (int cnt = 0; cnt < NUMDATALINES; cnt++) {
			int sel = ((strobe * NUMDATALINES) + cnt);
			Bulbs[sel].Period = Bulbs[sel].OnCycles + Bulbs[sel].OffCycles;
			Bulbs[sel].DutyCycles = Bulbs[sel].OnCycles;
			//Reset Cycles
			Bulbs[sel].OffCycles = 0;
			Bulbs[sel].OnCycles = 0;
		}

		StrobeVal = strobe;

	}

}

void Lamping::WriteData(UINT16 data){

	DataVal[StrobeVal] = data;
}

 void Lamping::Run(UINT16 InstructionCycles){

	 //Called every instruction

	int cnt, cnt2, sel;

	for (cnt = 0; cnt < NUMSTROBELINES; cnt++){
		if (cnt == StrobeVal){
			for (cnt2 = 0; cnt2 < NUMDATALINES; cnt2++){
				sel = ((cnt * NUMDATALINES) + cnt2);
				if (DataVal[StrobeVal] & (1 << cnt2)){
					Bulbs[sel].Powered = 1;					
					Bulbs[sel].OnCycles += InstructionCycles;			
				} else {										
					Bulbs[sel].Powered = 0;
					Bulbs[sel].OffCycles += InstructionCycles;					
				}				
			}
		} else {
			for (cnt2 = 0; cnt2 < NUMDATALINES; cnt2++){
				sel = ((cnt * NUMDATALINES) + cnt2);				
				Bulbs[sel].Powered = 0;
				Bulbs[sel].OffCycles += InstructionCycles;			
			}
		}
	}

}

void Lamping::Update(){

	//Called once per frame
	int cnt;
	double Temperature;
	float3 TempColor;


	//Inputs
	float I = 0.1f;							//Amps
	float V = 12.f;							//Volts

	//Bulb Characteristics
	float coldR = 14.2f;					//Resistance Ohms when Cold
	float T0 = 21.5f;						//Reference Temperature Celcius

	//Resistivity Constants
	float P0 = float(5.65 * pow(10, -8));	//Resistivity of Tungsten @ 300k (near enough 21.5c)
	float delta = float(4.5 * pow(10, -3));	//Resistivity constant for Tungsten

	//Calculation Variables
	float R;								//Resistance When Operating
	float LA;								// L / A value
	float P;								//Resistivity when operating

	//Output Temperature
	float TempCelcius;						//Output in Celcius
	float TempKelvin;						//Outout in Kelvin

	//Power / Lumens Quadratic
	float Power, Lumens;



	



	// *** Quadratic for Lumens / Power

	// y = ax^2+bx+c
	


	/*
	Vectors
	x     y
	1.08, 3.80
	1.20, 5.00
	1.32, 6.25
	*/

	//Starting Equations
	//3.80 = a(1.08)^2+b(1.08)+c
	//5.00 = a(1.20)^2+b(1.20)+c
	//6.25 = a(1.32)^2+b(1.32)+c

	//Simplify (multiply out)
	//3.80 = 1.1664*a+1.08*b+c
	//5.00 = 1.4400*a+1.20*b+c
	//6.25 = 1.7424*a+1.32*b+c

	//Compare top 2 and eliminate C
	//3.80 = 1.1664*a+1.08*b+c
	//flip signs on y,a,b,c
	//-5.00 = -1.4400*a+-1.20*b+-c
	//Add together
	//3.8+-5.0 = -1.2
	//1.1664+-1.4400=-0.2736
	//1.08+-1.20 = -0.12
	//Result 1 = C cancelled out
	//-1.2= -0.2736a-0.12b


	//Compare bottom 2 and eliminate C
	//5.00 = 1.4400*a+1.20*b+c	
	//flip signs on y,a,b,c
	//-6.25=+-1.7424*a+-1.32*b+-c	
	//Add together
	//5.0+-6.25=-1.25
	//1.44+-1.7424=-0.3024
	//1.2+-1.32=-0.12
	//Result 2 = C cancelled out
	//-1.25=-0.3024a-0.12b


	//Remove Variable B
	//Result 1     -1.2= -0.2736a-0.12b
	//Result 2     -1.25=-0.3024a-0.12b
	//Multiply Result 2 by (n)b

	//-1.25*0.12 = -0.15
	//-0.3024*0.12 = -0.036288
	//-0.12*0.12 = -0.0144

	float Voltage, VRMS, Frequency, Duration, Smoothing = 12.f;

	for (cnt = 0; cnt < NUMLAMPS; cnt++){
		
		//Calculate VRMS = Voltage Peak * sqrt(frequency (Hz) * Duration (Seconds))
		
		//Frequency Calculation
		Frequency = (EMULATEDCYCLESPERSECOND / Bulbs[cnt].Period);
		
		//Duration Calculation
		Duration = (1.f / EMULATEDCYCLESPERSECOND * Bulbs[cnt].DutyCycles);
		
		//Input voltage is AC so get VRMS voltage of input supply
		//VRMS is based on a full rectified wave, however the Power Supply may have smoothing capacitors which will increase the voltage.
		// Smoothing is an arbritrary number in Volts to boost the input by.
		VRMS = float((INPUTVOLTAGEAC / sqrt(2)) + Smoothing); 

		//Final voltage to bulb using AC Input and Square Wave from Matrix
		Voltage = VRMS * sqrt(Frequency * Duration);
		
			   
		// Determine whether bulb is lit
		if (Voltage > 0.f) {
			Bulbs[cnt].Lit = true;
		}
		else {
			Voltage = 0.f;
			Bulbs[cnt].Lit = false;
		}

		if (Bulbs[cnt].Lit) {



			//Where X is Power Y is Lumens
			//Power = 1.20f;
			Power = Voltage * I; //TODO: get the correct Amperage this will do for now
			Lumens = (5.0680415263749f * (Power * Power)) - (1.9549663299663f * Power);

			Bulbs[cnt].Brightness = (Lumens / 6.25f); //Divide by max value to get value between 0 and 1
			

			//Find R when operating
			R = (Voltage / I);

			//Find L/A
			LA = (coldR / P0);

			//Find P when operating
			P = (R / LA);

			//Temperature when operating Calculation
			TempCelcius = (((P - P0) / P0) + (T0 * delta)) / (delta);

			//Convert to Kelvin
			TempKelvin = TempCelcius + 273.15f;

			//Red/Blue Temperature Shift Algorithm (Kelvin Colours)
			if (TempKelvin > 3300.f) {
				TempKelvin = 3300; //Clamp value below melting point of Tungsten (3695.15 Kelvin)			
			}
			Temperature = (TempKelvin / 100.0);
			if (Temperature >= 10.0) {
				if (Temperature <= 66) {
					TempColor.x = 255;
				}
				else {
					TempColor.x = float(Temperature - 60.f);
					TempColor.x = 329.698727446f * (float)pow(TempColor.x, -0.1332047592f);
				}
				if (Temperature <= 66) {
					TempColor.y = (float)Temperature;
					TempColor.y = 99.4708025861f * (float)log(TempColor.y) - 161.1195681661f;
				}
				else {
					TempColor.y = (float)Temperature - 60.f;
					TempColor.y = 288.1221695283f * (float)pow(TempColor.y, -0.0755148492f);
				}
				if (Temperature >= 66) {
					TempColor.z = 255;
				}
				else {
					if (Temperature <= 19.0) {
						TempColor.z = 0.0;
					}
					else {
						TempColor.z = ((float)Temperature - 10.f);
						TempColor.z = 138.5177312231f * (float)log(TempColor.z) - 305.0447927307f;
					}
				}
				TempColor /= 255;

				
				Bulbs[cnt].Colour = TempColor;
			}
			else {
				Bulbs[cnt].Colour = float3();
			}

		}
		else {
			Bulbs[cnt].Colour = float3();
			Bulbs[cnt].Brightness = (0.0f);
		}

	}
}

float3 Lamping::GetFilamentColour(UINT16 Num){	
	if (Num >= NUMLAMPS) return float3();
	return Bulbs[(Num & 0xff)].Colour;
}

float Lamping::GetLampBrightness(UINT16 Num) {
	if (Num >= NUMLAMPS) return 0.f;
	return Bulbs[(Num & 0xff)].Brightness;
}

bool Lamping::GetLampsOn(UINT16 Num) {
	if (Num >= NUMLAMPS) return false;
	return Bulbs[(Num & 0xff)].Lit;
}


void Lamping::SaveState(){
	
	int loop;

	for (loop = 0; loop < NUMLAMPS; loop++){
		//LSC->SaveToBuffer(Bulbs[loop].Temperature);	
	}

}

void Lamping::LoadState(){

	int loop;

	for (loop = 0; loop < NUMLAMPS; loop++){
		//LSC->LoadFromBuffer(On[loop]);

	}

}
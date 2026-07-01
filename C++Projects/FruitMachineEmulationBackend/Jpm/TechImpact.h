// ###########################################################################
// #
// # TechImpact - Definition of main IMPACT Emulation Classes
// # Copyright (C) 2002-2012 Tony Friery [DialTone]
// #
// # ALL RIGHTS RESERVED
// #
// ###########################################################################

#ifndef TechImpactH
#define TechImpactH

#include <vector>
#include <string>
#include <time.h>
#include "FormDebug.h"
#include "FruitBoard.h"
#include "FruitSoundUPD7759.h"
#include "DeviceAlpha.h"
#include "DeviceJPMReels.h"
#include "DeviceLamp.h"
#include "DeviceLed.h"
#include "DevicePPI8255.h"
#include "DeviceSCN68681b.h"
#include "m68kcpu.h"

///////////////////////////////////////////////////////////////////////
//
// 	CPU Emulation Class Definition
//
///////////////////////////////////////////////////////////////////////

class CPUImpact : public mc68000
{
	friend class BoardImpact;

	private:
		UINT8  					ROM[0x100000];    // Program ROM Storage
		UINT8  					RAM[0x4000];		// Onboard RAM
		BoardImpact 			*fOwner;				// Board which this CPU belongs to

		DevicePPI8255			fPPI8255;			// PPI8255 Device
		DeviceSCN68681b		fDUART;				// DUART Device
		DeviceLED				f7Seg;				// 7-Seg Hardware

		UINT8						fPayEn;				// Payout Enable
		UINT8						fPaySlide;			// PaySlide Pulse
		bool						fSndBusy;			// Sound Chip Busy

		int						refresh;

	protected:
		lamps						fLamps;				// Lamps Hardware
		DeviceAlpha				fAlphaDisplay;		// Alphanumeric Device
		jpmreels					fReels;				// Reels Hardware

		UINT8						fStatusLED;			// Status LED
		UINT16					fLampSource;		// Lamp Sink
		UINT16					fLampValue;			// Lamp Source
		UINT16					fReel1;				// Reels 1, 2, 3, 4
		UINT16					fReel2;				// Reels 5, 6, 7, 8
		UINT8						fSndTune;			// Sound Chip Tune Number
		UINT8						fSndPage;			// Sound Chip Bank Number

	public:
		int __fastcall 		cpu_irq_ack(int level);
		void 	__fastcall 		cpu_set_fc(int discard) { };
		void 	__fastcall 		cpu_inst_hook(int cycles);
		void 	__fastcall 		cpu_pulse_reset(void);
		UINT8 __fastcall		cpu_read_byte(int address);
		UINT16 __fastcall		cpu_read_word(int address);
		UINT32 __fastcall 	cpu_read_long(int address);
		void __fastcall		cpu_write_byte(int address, UINT8 value);
		void __fastcall		cpu_write_word(int address, UINT16 value);
		void __fastcall		cpu_write_long(int address, UINT32 value);

		__fastcall				CPUImpact();
		__fastcall 				~CPUImpact();
};

///////////////////////////////////////////////////////////////////////
//
// 	IMPACT System Board Class Definition
//
///////////////////////////////////////////////////////////////////////

class BoardImpact : public FruitBoard
{
	friend class CPUImpact;

	private:
		CPUImpact 				*fMainCPU;
		FruitSoundUPD7759		*fSampleChip;
		TMaskEdit				*fDebugRegs[23];
		TDebugForm				*fMainDebug;
		bool						fProgramLoaded;
		UINT32					fFrameCyclesElapsed;
		UINT32					fThrottleAdjust;
		UINT32					fThrottleValue;
		UINT32					fThrottleTarget;

		clock_t					debugDelay;
		clock_t					clocksPerFrame;
		int						cyclesPerFrame;
		bool						fAutoThrottle;
		UINT32					execBP;

		TShape 					*fStatusIndicator;
		TShape 					*fReelIndicators[6][5];
		TLabel					*fReelPositions[6];

		void __fastcall 		DebugResetClick(TObject *Sender);
		void __fastcall 		DebugRunClick(TObject *Sender);
		void __fastcall 		DebugWalkClick(TObject *Sender);
		void __fastcall 		DebugStopClick(TObject *Sender);
		void __fastcall 		DebugStepIntoClick(TObject *Sender);
		void __fastcall 		DebugStepOverClick(TObject *Sender);
		void __fastcall 		DebugExecBPChange(TObject *Sender);
		void __fastcall 		DebugExecBPClick(TObject *Sender);

	public:
		void __fastcall		UpdateStatusLED(void);

		void __fastcall		UpdateDebug(void);
		void __fastcall		UpdateIOExplorer(void);
		int __fastcall 		Dasm(char* str_buff, UINT32 pc);
		void __fastcall		SwitchRunMode(ExecutionModes mode);
		void __fastcall 		BuildDebugger(DebugID debugid, TDebugForm *debugform);
		void __fastcall		BuildStatusPanel(TPanel *panel);
		void __fastcall 		BuildConfig(void);
		void __fastcall 		BuildIOExplorer(void);
		bool __fastcall		OpenProgramROMList(TStringList *ROMNames);
		bool __fastcall		OpenSoundROMList(TStringList *ROMNames);
		bool __fastcall		IsProgramLoaded(void);
		void __fastcall		LoadRAM(void);
		void __fastcall		SaveRAM(void);
		void __fastcall		ClearRAM(void);
		void __fastcall		PowerOnReset(void);
		UINT32 __fastcall 	RunBoard(void);
		void __fastcall		SetSwitchMatrix(int which, bool state);

		__fastcall 				BoardImpact(TForm *ParentForm);
		virtual __fastcall 	~BoardImpact(void);
};

#endif

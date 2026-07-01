// ###########################################################################
// #
// # TechImpact - Definition of main IMPACT Emulation Classes
// # Copyright (C) 2002-2012 Tony Friery [DialTone]
// #
// # ALL RIGHTS RESERVED
// #
// ###########################################################################

#pragma hdrstop

#include "TechImpact.h"
#include "FormDiag.h"
#include "FormIOExplorer.h"
#include "Utilities.h"

#include <sys/stat.h>

///////////////////////////////////////////////////////////////////////
//
//		IMPACT CPU Class Implementation
//
///////////////////////////////////////////////////////////////////////

int __fastcall CPUImpact::cpu_irq_ack(int level)
{
	int res = M68K_INT_ACK_AUTOVECTOR;	// Default is Auto-Vector

	switch (level)
	{
		case 5:
			m68k_set_irq(M68K_IRQ_NONE);
			// UART supplies Vector
			res = fDUART.IVR;
			break;
		default:
			m68k_set_irq(M68K_IRQ_NONE);
			break;
	}

	return res;
}

void __fastcall CPUImpact::cpu_inst_hook(int cycles)
{
//	fDUART.Tick(cycles);
	fDUART.Tick(2);

	refresh +=cycles ;

	// Find out how many cycles processed in current block
	fOwner->fFrameCyclesElapsed = m68k_cycles_run();

	if ((fOwner->fFrameCyclesElapsed % 0x1000)==0)
	{
		fOwner->fThrottleAdjust += 0x1000;
	}

	if (m68ki_cpu.pc == fOwner->execBP)
	{
		fOwner->fNextMode = RM_STOP;
	}

	if (refresh >= 1000)
	{
		refresh -= 1000;
		fLamps.updatespecial(fLampValue, fLampSource);
		f7Seg.Update();
	}

	if (fDUART.ISR & fDUART.IMR)
	{
		m68k_set_irq(M68K_IRQ_5);
	}
}

void __fastcall CPUImpact::cpu_pulse_reset(void)
{
	refresh = 0;

//	fLamps.reset();
	f7Seg.Reset();
	fPPI8255.Reset();
	fDUART.Reset();
}

UINT8 __fastcall CPUImpact::cpu_read_byte(int address)
{
	UINT8 value = 0;

	if ( address < 0x100000 )
	{
		value = ROM[address];
	}
	else if ((address >= 0x400000) && (address < 0x404000))
	{
		value = RAM[address - 0x400000];
	}
	else if ((address >= 0x480060) && (address < 0x480068))
	{
//		fPPI8255.PortBIn = 0xe4 + hopper1.opto() + (hopper2.opto() << 3);
		fPPI8255.PortCIn = 0xf0;//(fPPI8255.PortCIn & 0x20) | Hoppers;
		value = fPPI8255.Read((address - 0x480060) >> 1);
	}
	else if ( address >= 0x480000 && address < 0x480020 )
	{
		value = fDUART.Read((address - 0x480000) >> 1);
	}
	else if ( address >= 0x480020 && address < 0x480034 )
	{
		switch (address)
		{
			case 0x480021:
				value = 0xff;
//	        value = ~( DIP1 | matrix[0]);
				break;
			case 0x480023:
				value = 0xf6;
//	        value = ~(PCkey | matrix[1]);
				break;
			case 0x480025:
				value = 0xb5;
//	        value = ~((STkey << 4) | JPkey | matrix[2]) & 0xff;
				break;
			case 0x480033:
				value = 0xff;
//	        value = ~(matrix[9] | (MechType << 5));
				break;
			default:
				value = 0xff;
//	        value = ~matrix[(address-0x480020)>>1];
				break;
		}
	}
	else if ( address == 0x480041 )
	{
		value = fReels.optos;
	}
	else if ( address == 0x480085 )
	{
//    	value = 1 - fSndBusy;

		if (fSndTune != 0)
		{
			value = 1;// - Sound->Playing(tune);

			if (value == 1)
			{
				fSndTune = 0;
				fSndBusy = false;
			}
		}
		else
		{
			value = 1;
		}

		if (value == 1)
		{
			fSndBusy = false;
		}
	}
	else if ( address == 0x480035 )
	{
		value = 0xff;
	}
	else if ( address >= 0x480085 )
	{
		value = 1;
	}
	else if ( address >= 0x480000 )
	{
		value = 0x0;
	}
	else
	{
		// Memory Access Error
		DiagForm->AddMessage(DIAG_CPU, Format(
			"8-Bit Unhandled Read from %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (address, m68ki_cpu.pc))
		));
	}

	return value;
}

UINT16 __fastcall CPUImpact::cpu_read_word(int address)
{
	 UINT16 value = 0;

	 if (address < 0x100000)
	 {
		value = ROM[address];
		value = ROM[address + 1] + (value << 8);
	 }
	 else if ((address >= 0x400000) && (address < 0x404000))
	 {
		address -= 0x400000;
		value = RAM[address];
		value = RAM[address + 1] + (value << 8);
	 }
	 else
	 {
		// Memory Access Error
		DiagForm->AddMessage(DIAG_CPU, Format(
			"16-Bit Unhandled Read from %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (address, m68ki_cpu.pc))
		));
	 }

	 return value;
}

UINT32 __fastcall CPUImpact::cpu_read_long(int address)
{
	UINT32 value = 0;

	if (address < 0x100000)
	{
		value = ROM[address];
		value = ROM[address + 1] + (value << 8);
		value = ROM[address + 2] + (value << 8);
		value = ROM[address + 3] + (value << 8);
	}
	else if ((address >= 0x400000) && (address < 0x404000))
	{
		address -= 0x400000;
		value = RAM[address];
		value = RAM[address + 1] + (value << 8);
		value = RAM[address + 2] + (value << 8);
		value = RAM[address + 3] + (value << 8);
	}
	else
	{
		// Memory Access Error
		DiagForm->AddMessage(DIAG_CPU, Format(
			"32-Bit Unhandled Read from %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (address, m68ki_cpu.pc))
		));
	}

	return value;
}

void __fastcall CPUImpact::cpu_write_byte(int address, UINT8 value)
{
	if ((address >= 0x400000) && (address < 0x404000))
	{
		RAM[address - 0x400000] = value;
	}
	else if ((address >= 0x480060) && (address < 0x480068))
	{
		fPPI8255.Write((address - 0x480060) >> 1, value);

		if (fPPI8255.PortCChanged)
		{
			fAlphaDisplay.WriteJPM(fPPI8255.PortC);

			if ((fAlphaDisplay.DisplayChanged) || (fAlphaDisplay.IntensityChanged))
			{
//			owner->fDebugDirty = true;
			}
		}

		if (fPPI8255.PortAChanged)
		{
			if (fPPI8255.PortA & 0x10)
			{
				fPayEn = 1;
				DiagForm->AddMessage(DIAG_PAYOUTS, "Payout Enable (PAYEN) = TRUE");
			}
			else
			{
				fPayEn = 0;
				DiagForm->AddMessage(DIAG_PAYOUTS, "Payout Enable (PAYEN) = FALSE");
			}

//		    hopper1.enable( (fPPI8255.PortA & 0x11) == 0x11, fPPI8255.PortA & 0x80, fPPI8255.PortA & 0x20);
//		    hopper2.enable( (fPPI8255.PortA & 0x50) == 0x50, fPPI8255.PortA & 0x80, fPPI8255.PortA & 0x20);
		}
	}
	else if ((address >= 0x480000) && (address < 0x480020))
	{
		fDUART.Write(((address - 0x480000) >> 1), value);
	}
	else if (address == 0x480081)
	{
		if (fSndTune != 0)
		{
//		    if ( !Sound->Playing(tune) ) {
			fSndTune = 0;
			fSndBusy = false;
//		    }
		}
		else
		{
			 fSndBusy = false;
		}

		if (!fSndBusy)
		{
//			fprintf(stderr, "Playing tune: %d\n", value & 0xff);
//		    sndTune = lookup[page][value & 0xff];
			 fSndBusy = true;
//		    parent->do_wave();
		}
	 }
	 else if (address == 0x480083)
	 {
		fSndPage = (value >> 1) & 7;
//		fprintf(stderr, "Sound Page: %d\n", sndPage);

		if (value & 1)
		{
			// Reset
//			fprintf(stderr, "Reset Sound Chip\n");
			 fSndTune = 0;
//		    parent->do_wave();
		}
	 }
	 else if ( address == 0x4800ab )
	 {
		f7Seg.writejpm(value, fLampSource);
	 }
	 else if ( address == 0x4800af )
	 {
		if (value & 0x10)
		{
//			owner->fDebugDirty = true;
			 fLampSource = (value + 1) & 0xf;
	//	    if ( fLampSource == 0 )
	//		parent->do_lamp();
		}
	 }
	 else if (address == 0x4800ad)
	 {
	 }
	 else
	 {
		// Memory Access Error
		DiagForm->AddMessage(DIAG_CPU, Format(
			"8-Bit Unhandled Write %2.2x to %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (value, address, m68ki_cpu.pc))
		));
	 }
}

void __fastcall CPUImpact::cpu_write_word(int address, UINT16 value)
{
#if 0
	 if (address & 1)
	 {
		DiagForm->AddMessage(DIAG_CPU, Format(
			"16-Bit Address Exception Writing %4.4x to %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (value, address, m68ki_cpu.pc))
		));
	 }
#endif

	 if ((address >= 0x400000) && (address < 0x404000))
	 {
		address -= 0x400000;
		RAM[address] = value >> 8;
		RAM[address + 1] = value & 0xff;
	 }
	 else if (address == 0x4800a0)
	 {
		value = (value & 0x200) >> 9;

		if (value != fStatusLED)
		{
			fStatusLED = value;
			if (fOwner->fStatusIndicator)
			{
				fOwner->UpdateStatusLED();
			}
		}
	}
	else if (address == 0x4800a2)
	{
//		owner->fDebugDirty = true;
		fReel1 = value;
		fReels.write1(value);
		//if (reel.changed[0] || reel.changed[1] || reel.changed[2] || reel.changed[3])
		//	parent->do_reel();
	}
	else if (address == 0x4800a4)
	{
//		owner->fDebugDirty = true;
		fReel2 = value;
		fReels.write2(value);
		//if ( reel.changed[4] || reel.changed[5] )
			//parent->do_reel();
	}
	else if (address == 0x4800a6)
	{
		if (value & 0x10)
		{   // PAYEN ?
			if (value & 0xf)
			{
				fPaySlide = 1;
			}
			else
			{
				fPaySlide = 0;
			}

			DiagForm->AddMessage(DIAG_PAYOUTS, Format(
				"Payslide %s",
				OPENARRAY(TVarRec, (fPaySlide ? "Enabled" : "Disabled"))
			));
		}
		else
		{
			fPaySlide = 0;
			DiagForm->AddMessage(DIAG_PAYOUTS, Format(
				"Payslide %s",
				OPENARRAY(TVarRec, (fPaySlide ? "Enabled" : "Disabled"))
			));
		}

		DiagForm->AddMessage(DIAG_OTHERIO, Format(
			"Meter Write %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (value >> 10, m68ki_cpu.pc))
		));
//		meter.write(value >> 10);
//		owner->fDebugDirty = true;

//		if ( meter.meter_on )
//      		fDUART.IP &= ~0x10;
//    	else
//      		fDUART.IP |= 0x10;
	}
	else if ( address == 0x4800a8 )
	{
		fLampValue = value;
//		fLamps.writejpm(fLampValue, fLampSource, 40);
//		owner->fDebugDirty = true;
	}
	else
	{
		// Memory Write Error
		DiagForm->AddMessage(DIAG_CPU, Format(
			"16-Bit Unhandled Write %4.4x to %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (value, address, m68ki_cpu.pc))
		));
	}
}

void __fastcall CPUImpact::cpu_write_long(int address, UINT32 value)
{
#if 0
	if (address & 3)
	{
		DiagForm->AddMessage(DIAG_CPU, Format(
			"32-Bit Address Exception Writing %8.8x to %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (value, address, m68ki_cpu.pc))
		));
	}
#endif

	if ((address >= 0x400000) && (address < 0x404000))
	{
		address -= 0x400000;
		RAM[address] = value >> 24;
		RAM[address + 1] = (value >> 16) & 0xff;
		RAM[address + 2] = (value >> 8) & 0xff;
		RAM[address + 3] = value & 0xff;
	}
	else
	{
		// Memory Write Error
		DiagForm->AddMessage(DIAG_CPU, Format(
			"32-Bit Unhandled Write %8.8x to %8.8x @ %8.8x",
			OPENARRAY(TVarRec, (value, address, m68ki_cpu.pc))
		));
	}
}

__fastcall CPUImpact::CPUImpact()
{
	fStatusLED = 0xff;

	jpmreels::rstruct *qreel = &fReels.reels[0];

	for (int i = 0; i < 10; i++)
	{
		qreel->startopto = 6;
		qreel->endopto = 8;
		qreel->adjust = 7;
		qreel->stops = 16;
		qreel->steps = 96;
		qreel->offset = 0;
		qreel->inverted = false;
		qreel->flag = 0;
		qreel++;
	}
}

__fastcall CPUImpact::~CPUImpact()
{
}

///////////////////////////////////////////////////////////////////////
//
//		IMPACT MainBoard Implementation
//
///////////////////////////////////////////////////////////////////////

void __fastcall BoardImpact::DebugRunClick(TObject *)
{
	fNextMode = RM_RUN;
}

void __fastcall BoardImpact::DebugResetClick(TObject *)
{
	PowerOnReset();
}

void __fastcall BoardImpact::DebugWalkClick(TObject *)
{
	fNextMode = RM_WALK;
}

void __fastcall BoardImpact::DebugStopClick(TObject *)
{
	fNextMode = RM_STOP;
}

void __fastcall BoardImpact::DebugStepIntoClick(TObject *)
{
	fNextMode = RM_STEPIN;
}

void __fastcall BoardImpact::DebugStepOverClick(TObject *)
{
	fNextMode = RM_STEPOVER;
}

void __fastcall BoardImpact::DebugExecBPChange(TObject *)
{
	if (fMainDebug)
	{
		fMainDebug->chkExecBP->Checked = false;
	}
}

void __fastcall BoardImpact::DebugExecBPClick(TObject *)
{
	if (fMainDebug->chkExecBP->Checked)
	{
		execBP = HToI(fMainDebug->EditExecBP->Text);
	}
	else
	{
		execBP = 0xffffffff;
	}
}

void __fastcall BoardImpact::UpdateDebug(void)
{
	UINT32 addr;
	int bytCnt;
	char dbuf[100];

	if (fMainDebug)
	{
		if (fMainDebug->ChkUpdate->Checked)
		{
			for (int i = 0; i < 16; i++)
			{
				fDebugRegs[i]->Text = Format("%8.8X", OPENARRAY(TVarRec, (fMainCPU->m68ki_cpu.dar[i])));
			}

			fDebugRegs[16]->Text = Format("%8.8X", OPENARRAY(TVarRec, (fMainCPU->m68ki_cpu.pc)));
			fDebugRegs[18]->Text = Format("%1.1X", OPENARRAY(TVarRec, (fMainCPU->m68ki_cpu.sfc)));
			fDebugRegs[19]->Text = Format("%1.1X", OPENARRAY(TVarRec, (fMainCPU->m68ki_cpu.dfc)));
			fDebugRegs[20]->Text = Format("%8.8X", OPENARRAY(TVarRec, (fMainCPU->m68ki_cpu.vbr)));
			fDebugRegs[21]->Text = Format("%4.4X", OPENARRAY(TVarRec, (fMainCPU->m68ki_cpu.cacr)));
			fDebugRegs[22]->Text = Format("%4.4X", OPENARRAY(TVarRec, (fMainCPU->m68ki_cpu.caar)));

			UINT8 SR =	fMainCPU->m68ki_cpu.t1_flag 							|
							fMainCPU->m68ki_cpu.t0_flag 							|
							(fMainCPU->m68ki_cpu.s_flag << 11)					|
							(fMainCPU->m68ki_cpu.m_flag << 11)					|
							fMainCPU->m68ki_cpu.int_mask							|
							((fMainCPU->m68ki_cpu.x_flag & XFLAG_SET) >> 4)	|
							((fMainCPU->m68ki_cpu.n_flag & NFLAG_SET) >> 4)	|
							((!fMainCPU->m68ki_cpu.not_z_flag) << 2)			|
							((fMainCPU->m68ki_cpu.v_flag & VFLAG_SET) >> 6)	|
							((fMainCPU->m68ki_cpu.c_flag & CFLAG_SET) >> 8);

			fDebugRegs[17]->Text = Format("%1.1x%1.1x%1.1x%1.1x%1.1x%1.1x%1.1x%1.1x",
				OPENARRAY(TVarRec, (
					((SR >> 7) & 1),
					((SR >> 6) & 1),
					((SR >> 5) & 1),
					((SR >> 4) & 1),
					((SR >> 3) & 1),
					((SR >> 2) & 1),
					((SR >> 1) & 1),
					(SR & 1)
			)));

			bytCnt = 0;
			addr = fMainCPU->m68ki_cpu.pc;
			fMainDebug->ListAssembler->Items->Clear();

			for (int i = 0; i < 14; i++)
			{
				bytCnt = Dasm(&dbuf[0], addr);
				fMainDebug->ListAssembler->Items->Add(AnsiString(dbuf));
				addr += bytCnt;
			}
		}
	}
}

void __fastcall BoardImpact::UpdateIOExplorer(void)
{
	int bit;

	if (IOForm->Showing)
	{
		for (int reel = 0; reel < 4; reel++)
		{
			for (int phase = 0; phase < 4; phase++)
			{
				bit = (reel * 4) + (3 - phase);
				fReelIndicators[reel][phase]->Brush->Color = (fMainCPU->fReel1 & (1 << bit)) ? clLime : clMaroon;

				if (reel < 2)
				{
					fReelIndicators[reel + 4][phase]->Brush->Color = (fMainCPU->fReel2 & (1 << bit)) ? clLime : clMaroon;
				}
			}

			fReelIndicators[reel][4]->Brush->Color = (fMainCPU->fReels.optos & (1 << reel)) ? clLime : clMaroon;
			fReelPositions[reel]->Caption = IntToStr(fMainCPU->fReels.reels[reel].pos);

			if (reel < 2)
			{
				fReelIndicators[reel + 4][4]->Brush->Color = (fMainCPU->fReels.optos & (1 << (reel + 4))) ? clLime : clMaroon;
				fReelPositions[reel + 4]->Caption = IntToStr(fMainCPU->fReels.reels[reel + 4].pos);
			};
		}

		IOForm->PaintBoxOutputs->Buffer->Clear(clBlack32);

		for (int i = 0; i < 256; i++)
		{
			int x = ((i % 16) * 20);
			int y = ((i / 16) * 20);
			int lampVal = fMainCPU->fLamps.lamp[i];
//			int lampCol = (lampVal > 255) ? 255 : lampVal;
			int lampCol = lampVal >> 2;

			if (lampVal > 0)
			{
				for (int sq = 0; sq < 16; sq++)
				{
					IOForm->PaintBoxOutputs->Buffer->HorzLineTS(x, (y + sq), x + 15, Color32(lampCol, lampCol, (lampCol > 200) ? (lampCol - 200) : 0, 255));
				}
			}
		}

		IOForm->PaintBoxOutputs->Refresh();
	}
}

int __fastcall BoardImpact::Dasm(char* str_buff, UINT32 pc)
{
	return fMainCPU->m68k_disassemble(str_buff, pc, CPU_TYPE_000);
}

void __fastcall BoardImpact::SwitchRunMode(ExecutionModes mode)
{
	fExecMode = mode;
	fNextMode = mode;

	if (fMainDebug)
	{
		fMainDebug->BtnReset->Enabled = (fExecMode == RM_STOP);
		fMainDebug->BtnRun->Enabled = (fExecMode == RM_STOP);
		fMainDebug->BtnWalk->Enabled = (fExecMode == RM_STOP);
		fMainDebug->BtnStop->Enabled = ((fExecMode == RM_RUN) || (fExecMode == RM_WALK));
		fMainDebug->BtnInto->Enabled = (fExecMode == RM_STOP);
		fMainDebug->BtnOver->Enabled = (fExecMode == RM_STOP);
	}

	if ((fNextMode == RM_RUN) || (fNextMode == RM_WALK))
	{
		debugDelay = clock();
		cyclesPerFrame = 0;
	}
}

bool __fastcall BoardImpact::OpenProgramROMList(TStringList *ROMNames)
{
	TMemoryStream *HiRom, *LoRom;
	bool res = true;
	UINT8 data;
	int count, count2 = 0;

	for (int count = 0; count < ROMNames->Count; count++)
	{
		DiagForm->AddMessage(DIAG_MISC, "IMPACT: ROM Added: " + ROMNames->Strings[count]);
	}

	DiagForm->AddMessage(DIAG_MISC, "IMPACT: Loading ROMs");

	switch (ROMNames->Count)
	{
		case 1:
			HiRom = new TMemoryStream();
			HiRom->LoadFromFile(ROMNames->Strings[0]);
			count = HiRom->Size;

			while (count)
			{
				HiRom->Read(&data, 1);
				fMainCPU->ROM[count2++] = data;
				count--;
			}

			delete HiRom;
			break;
		case 2:
			HiRom = new TMemoryStream();
			LoRom = new TMemoryStream();
			HiRom->LoadFromFile(ROMNames->Strings[1]);
			LoRom->LoadFromFile(ROMNames->Strings[0]);

			if (LoRom->Size != HiRom->Size)
			{
				DiagForm->AddMessage(DIAG_MISC, "IMPACT: ROM Sizes are Mismatched");
				Application->MessageBox(L"ROM Files must be identical size", L"Invalid ROMS", MB_ICONSTOP | MB_OK);
				res = false;
			}
			else
			{
				count = HiRom->Size;

				while (count)
				{
					LoRom->Read(&data, 1);
					fMainCPU->ROM[count2++] = data;
					HiRom->Read(&data, 1);
					fMainCPU->ROM[count2++] = data;
					count--;
				}
			}

			delete LoRom;
			delete HiRom;
			break;
		default:
			DiagForm->AddMessage(DIAG_MISC, "IMPACT: Too many ROMs specified");
			Application->MessageBox(L"Sorry, don't know how to\rHandle that many ROMS", L"Incorrect ROM Count", MB_ICONSTOP | MB_OK);
			res = false;
			break;
	}

	if (res)
	{
		DiagForm->AddMessage(DIAG_MISC, "IMPACT: ROM Load Success (" + AnsiString(count2) + " bytes)");
		fProgramLoaded = true;
	}
	else
	{
		DiagForm->AddMessage(DIAG_MISC, "IMPACT: ROM Load Failed");
		fProgramLoaded = false;
	}

	return res;
}

bool __fastcall BoardImpact::OpenSoundROMList(TStringList *ROMNames)
{
	if (fSampleChip)
	{
		return fSampleChip->OpenROMList(ROMNames);
	}

	return false;
}

void __fastcall BoardImpact::PowerOnReset(void)
{
	DiagForm->AddMessage(DIAG_MISC, "IMPACT: Power-On Reset");
	fMainCPU->m68k_set_cpu_type(M68K_CPU_TYPE_68000);
	fMainCPU->m68k_pulse_reset();
	UpdateDebug();
	fThrottleTarget = 0x8000;
	fThrottleValue = 10000;
}

bool __fastcall BoardImpact::IsProgramLoaded(void)
{
	return fProgramLoaded;
}

void __fastcall BoardImpact::LoadRAM(void)
{
}

void __fastcall BoardImpact::SaveRAM(void)
{
}

void __fastcall BoardImpact::ClearRAM(void)
{
	for (int i = 0; i < 0x4000; i++)
	{
		fMainCPU->RAM[i] = 0x00;
	}
}

UINT32 __fastcall BoardImpact::RunBoard(void)
{
	UINT32 cyclesHandled = 0;

	if (fNextMode != fExecMode)
	{
		SwitchRunMode(fNextMode);
	}

	switch (fExecMode)
	{
		case RM_STOP:
			break;
		case RM_STEPIN:
		case RM_STEPOVER:
			fNextMode = RM_STOP;
		case RM_RUN:
		case RM_WALK:
			if (fMainCPU)
			{
				// Walk animates 4 instructions per second
				if (fExecMode != RM_WALK)
				{
					if (fThrottleAdjust > fThrottleTarget)
						fThrottleValue -= (fThrottleAdjust - fThrottleTarget);
					else
						fThrottleValue += (fThrottleTarget - fThrottleAdjust);

					if (fThrottleValue < 10000)
						fThrottleValue = 10000;

					if (fThrottleValue > 2500000)
						fThrottleValue = 2500000;

					fThrottleAdjust = 0;
	//					DebugForm->Caption = IntToStr((int)fThrottleValue);

					// Execute a number of cycles, based on the delay
					if (fAutoThrottle)
					{
						cyclesHandled = fMainCPU->m68k_execute(fThrottleValue);
					}
					else
					{
						cyclesHandled = fMainCPU->m68k_execute(16000000);
					}
				}
				else
				{
					// Walking pace...
					cyclesHandled = fMainCPU->m68k_execute(1);
				}

#if 0
				if ((fMainDebug) && (fMainCPU->h8.h8err))
				{
					DiagForm->AddMessage(DIAG_CPU, Format(
						"!!! ERROR FLAG SET !!! @ %8.8x",
						OPENARRAY(TVarRec, (fMainCPU->h8.pc))
					));

					if (fMainDebug->ChkErrors->Checked)
					{
						fNextMode == RM_STOP;
					}
				}
#endif

				UpdateDebug();
				UpdateIOExplorer();
			}
			break;
	}

//				if ((fMainDebug) && ((fNextMode == RM_STOP) || (fExecMode == RM_WALK))) {
//					UpdateDebug();
//				}

	return cyclesHandled;
}

void __fastcall BoardImpact::BuildDebugger(DebugID debugid, TDebugForm *debugform)
{
	static char *debugLabels[] =
	{
		"D0 :", "D1 :", "D2 :", "D3 :",
		"D4 :", "D5 :", "D6 :", "D7 :",
		"A0 :", "A1 :", "A2 :", "A3 :",
		"A4 :", "A5 :", "A6 :", "A7 :"
	};

	int loop;

	switch (debugid)
	{
		case DEBUG_MAIN:
			fMainDebug = debugform;
			debugform->Caption = "Main Debugger Window";

			for (loop = 0; loop < 8; loop++)
			{
				debugform->MakeRegisterLabel(10, (16 + (loop * 25)), 20, 13, AnsiString(debugLabels[loop]));
				debugform->MakeRegisterLabel(118, (16 + (loop * 25)), 20, 13, AnsiString(debugLabels[loop + 8]));

				fDebugRegs[loop] = debugform->MakeRegisterEdit(36, (13 + (loop * 25)), 67, 22, 8);
				fDebugRegs[loop]->TabOrder = loop;
				fDebugRegs[loop + 8] = debugform->MakeRegisterEdit(144, (13 + (loop * 25)), 67, 22, 8);
				fDebugRegs[loop + 8]->TabOrder = (loop + 8);
			}

			TLabel *lbl = debugform->MakeRegisterLabel(254, 62, 56, 14, "---XNZVC");
			lbl->Font->Name = "Courier New";
			lbl->Font->Charset = DEFAULT_CHARSET;
			lbl->Font->Color = clWindowText;
			lbl->Font->Height = -11;
			lbl->Font->Style.Clear();

			debugform->MakeRegisterLabel(225, 16, 20, 13, "PC :");
			fDebugRegs[16] = debugform->MakeRegisterEdit(251, 13, 67, 22, 8);
			fDebugRegs[16]->TabOrder = 16;

			debugform->MakeRegisterLabel(225, 41, 20, 13, "SR :");
			fDebugRegs[17] = debugform->MakeRegisterEdit(251, 38, 67, 22, 8);
			fDebugRegs[17]->TabOrder = 17;

			debugform->MakeRegisterLabel(225, 91, 24, 13, "SFC:");
			fDebugRegs[18] = debugform->MakeRegisterEdit(251, 88, 18, 22, 1);
			fDebugRegs[18]->TabOrder = 18;

			debugform->MakeRegisterLabel(275, 91, 24, 13, "DFC:");
			fDebugRegs[19] = debugform->MakeRegisterEdit(301, 88, 18, 22, 1);
			fDebugRegs[19]->TabOrder = 19;

			debugform->MakeRegisterLabel(225, 116, 24, 13, "VBR:");
			fDebugRegs[20] = debugform->MakeRegisterEdit(251, 113, 67, 22, 8);
			fDebugRegs[20]->TabOrder = 20;

			debugform->MakeRegisterLabel(225, 141, 35, 13, "CACR :");
			fDebugRegs[21] = debugform->MakeRegisterEdit(280, 138, 38, 22, 4);
			fDebugRegs[21]->TabOrder = 21;

			debugform->MakeRegisterLabel(225, 166, 35, 13, "CAAR :");
			fDebugRegs[22] = debugform->MakeRegisterEdit(280, 163, 38, 22, 4);
			fDebugRegs[22]->TabOrder = 22;

			debugform->BtnReset->Enabled = true;
			debugform->BtnReset->OnClick = &this->DebugResetClick;
			debugform->BtnRun->Enabled = true;
			debugform->BtnRun->OnClick = &this->DebugRunClick;
			debugform->BtnWalk->Enabled = true;
			debugform->BtnWalk->OnClick = &this->DebugWalkClick;
			debugform->BtnStop->Enabled = false;
			debugform->BtnStop->OnClick = &this->DebugStopClick;
			debugform->BtnInto->Enabled = true;
			debugform->BtnInto->OnClick = &this->DebugStepIntoClick;
			debugform->BtnOver->Enabled = true;
			debugform->BtnOver->OnClick = &this->DebugStepOverClick;
			debugform->EditExecBP->OnChange = &this->DebugExecBPChange;
			debugform->chkExecBP->OnClick = &this->DebugExecBPClick;
			break;
	}
}

void __fastcall BoardImpact::UpdateStatusLED(void)
{
	fStatusIndicator->Brush->Color = fMainCPU->fStatusLED ? clRed : clMaroon;

	if (fMainCPU->fStatusLED)
	{
		fAutoThrottle = true;
	}
}

void __fastcall BoardImpact::SetSwitchMatrix(int which, bool state)
{
}

void __fastcall BoardImpact::BuildStatusPanel(TPanel *panel)
{
	panel->Caption = "(IMPACT)";
	fStatusIndicator = new TShape(((TComponent *)(NULL)));
	fStatusIndicator->Brush->Color = clMaroon;
	fStatusIndicator->Shape = stCircle;
	fStatusIndicator->Height = 15;
	fStatusIndicator->Width = 15;
	fStatusIndicator->Top = 4;
	fStatusIndicator->Left = (panel->Width - 25);
	fStatusIndicator->ControlStyle.Clear();
	fStatusIndicator->ControlStyle << csOpaque;
	fStatusIndicator->Parent = panel;
}

void __fastcall BoardImpact::BuildConfig(void)
{

}

void __fastcall BoardImpact::BuildIOExplorer(void)
{
	IOForm->ClientWidth = 556;
	IOForm->ClientHeight = 370;

	TGroupBox *reelGrp = new TGroupBox(((TComponent *)(NULL)));
	reelGrp->Left = 363;
	reelGrp->Top = 8;
	reelGrp->Width = 185;
	reelGrp->Height = 181;
	reelGrp->Caption = " Reels ";
	reelGrp->TabOrder = 1;
	reelGrp->Parent = IOForm;

	TLabel *lbl = NULL;
	TShape *shape = NULL;

	// Headers
	lbl = new TLabel(NULL);
	lbl->Caption = "OPTO";
	lbl->Width = 28;
	lbl->Height = 13;
	lbl->Left = 150;
	lbl->Top = 19;
	lbl->Parent = reelGrp;

	lbl = new TLabel(NULL);
	lbl->Caption = "DRIVE";
	lbl->Width = 30;
	lbl->Height = 13;
	lbl->Left = 73;
	lbl->Top = 19;
	lbl->Parent = reelGrp;

	for (int reel = 0; reel < 6; reel++)
	{
		// Reel Labels
		lbl = new TLabel(NULL);
		lbl->Caption = "Reel " + IntToStr(reel + 1) + ":";
		lbl->Width = 34;
		lbl->Height = 13;
		lbl->Left = 12;
		lbl->Top = 38 + (reel * 22);
		lbl->Parent = reelGrp;

		// Reel Motor Phases
		for (int phase = 0; phase < 4; phase++)
		{
			shape = new TShape(((TComponent *)(NULL)));
			shape->Left = 52 + (18 * phase);
			shape->Top = 38 + (reel * 22);
			shape->Width = 16;
			shape->Height = 16;
			shape->Shape = stRoundSquare;
			shape->Brush->Color = clMaroon;
			shape->Parent = reelGrp;
			shape->ControlStyle.Clear();
			shape->ControlStyle << csOpaque;
			fReelIndicators[reel][phase] = shape;
		}

		// Reel Opto Indicator
		shape = new TShape(((TComponent *)(NULL)));
		shape->Left = 155;
		shape->Top = 38 + (reel * 22);
		shape->Width = 16;
		shape->Height = 16;
		shape->Shape = stCircle;
		shape->Brush->Color = clMaroon;
		shape->Parent = reelGrp;
		shape->ControlStyle.Clear();
		shape->ControlStyle << csOpaque;
		fReelIndicators[reel][4] = shape;

		// Reel Positions
		lbl = new TLabel(NULL);
		lbl->Caption = "0";
		lbl->Width = 21;
		lbl->Height = 13;
		lbl->Left = 128;
		lbl->Top = 38 + (reel * 22);
		lbl->Alignment = taCenter;
		lbl->AutoSize = false;
		lbl->ParentFont = false;
		lbl->Font->Name = "Tahoma";
		lbl->Font->Style.Clear();
		lbl->Font->Style << fsBold;
		lbl->Parent = reelGrp;
		fReelPositions[reel] = lbl;
	}
}

__fastcall BoardImpact::BoardImpact(TForm *ParentForm) :
	FruitBoard(ParentForm), fProgramLoaded(false)
{
	DiagForm->AddMessage(DIAG_MISC, "IMPACT: Creating IMPACT Board");

	fMainCPU = new CPUImpact();
	fMainCPU->fOwner = this;

	fBoardType = TECH_IMPACT;
	fMainDebug = NULL;
	fHasSound = true;

	fSampleChip = new FruitSoundUPD7759(fSoundController);

	clocksPerFrame = CLOCKS_PER_SEC / 25;
	debugDelay = clock();
	cyclesPerFrame = 0;
	fAutoThrottle = false;

	BuildIOExplorer();
}

__fastcall BoardImpact::~BoardImpact()
{
	 DiagForm->AddMessage(DIAG_MISC, "IMPACT: Shutdown of IMPACT");
	 // SaveRAM();
	 delete fSampleChip;
	 delete fMainCPU;
}

#pragma package(smart_init)

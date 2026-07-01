#include "stdafx.h"
#include "System6.h"
#include <iostream>
#include <fstream>
#include <stdio.h>
#include <stdint.h>

using namespace std;
FILE* IOFile;

static const unsigned int IMPACT_TRACE_ROM_LIMIT = 0x100000;
static const unsigned int IMPACT_TRACE_SLOT_COUNT = IMPACT_TRACE_ROM_LIMIT / 2;

static bool g_impactFunctionDiscoveryEnabled = true;
static unsigned long long g_impactInstructionCount = 0;
static unsigned long long g_impactLastDumpInstruction = 0;

static unsigned int g_impactPcHits[IMPACT_TRACE_SLOT_COUNT];
static unsigned int g_impactFunctionTargets[IMPACT_TRACE_SLOT_COUNT];
static unsigned int g_impactCallSites[IMPACT_TRACE_SLOT_COUNT];
static unsigned int g_impactBranchTargets[IMPACT_TRACE_SLOT_COUNT];
static unsigned int g_impactBranchSites[IMPACT_TRACE_SLOT_COUNT];
static unsigned int g_impactReturnSites[IMPACT_TRACE_SLOT_COUNT];
static unsigned int g_impactIndirectCallSites[IMPACT_TRACE_SLOT_COUNT];

static int ImpactTraceSlot(unsigned int pc)
{
	if (pc >= IMPACT_TRACE_ROM_LIMIT)
		return -1;
	return (pc & 0xFFFFFE) >> 1;
}

static unsigned short ImpactTraceReadWord(const UINT8* rom, unsigned int pc)
{
	if (pc + 1 >= IMPACT_TRACE_ROM_LIMIT)
		return 0;
	return ((unsigned short)rom[pc] << 8) | rom[pc + 1];
}

static unsigned int ImpactTraceReadLong(const UINT8* rom, unsigned int pc)
{
	unsigned int hi = ImpactTraceReadWord(rom, pc);
	unsigned int lo = ImpactTraceReadWord(rom, pc + 2);
	return (hi << 16) | lo;
}

static int ImpactTraceSign8(unsigned int v)
{
	v &= 0xFF;
	return (v & 0x80) ? ((int)v - 0x100) : (int)v;
}

static int ImpactTraceSign16(unsigned int v)
{
	v &= 0xFFFF;
	return (v & 0x8000) ? ((int)v - 0x10000) : (int)v;
}

static void ImpactTraceMark(unsigned int* table, unsigned int pc)
{
	int slot = ImpactTraceSlot(pc);
	if (slot >= 0)
		table[slot]++;
}

static void ImpactTraceDumpFiles(void)
{
	FILE* f = NULL;
	fopen_s(&f, "ImpactFunctionHints.csv", "w");
	if (f)
	{
		fprintf(f, "kind,address,count\n");
		for (unsigned int slot = 0; slot < IMPACT_TRACE_SLOT_COUNT; ++slot)
		{
			unsigned int pc = slot << 1;
			if (g_impactPcHits[slot])
				fprintf(f, "EXEC_PC,%06X,%u\n", pc, g_impactPcHits[slot]);
			if (g_impactFunctionTargets[slot])
				fprintf(f, "FUNCTION_TARGET,%06X,%u\n", pc, g_impactFunctionTargets[slot]);
			if (g_impactCallSites[slot])
				fprintf(f, "CALL_SITE,%06X,%u\n", pc, g_impactCallSites[slot]);
			if (g_impactBranchTargets[slot])
				fprintf(f, "BRANCH_TARGET,%06X,%u\n", pc, g_impactBranchTargets[slot]);
			if (g_impactBranchSites[slot])
				fprintf(f, "BRANCH_SITE,%06X,%u\n", pc, g_impactBranchSites[slot]);
			if (g_impactReturnSites[slot])
				fprintf(f, "RETURN_SITE,%06X,%u\n", pc, g_impactReturnSites[slot]);
			if (g_impactIndirectCallSites[slot])
				fprintf(f, "INDIRECT_CALL_OR_JUMP_SITE,%06X,%u\n", pc, g_impactIndirectCallSites[slot]);
		}
		fclose(f);
	}

	FILE* j = NULL;
	fopen_s(&j, "JPM_System6_TraceDiscoveredLabels.java", "w");
	if (j)
	{
		fprintf(j, "import ghidra.app.script.GhidraScript;\n");
		fprintf(j, "import ghidra.program.model.address.Address;\n");
		fprintf(j, "import ghidra.program.model.listing.Function;\n");
		fprintf(j, "import ghidra.program.model.symbol.SourceType;\n\n");
		fprintf(j, "public class JPM_System6_TraceDiscoveredLabels extends GhidraScript {\n");
		fprintf(j, "  private void label(String a, String n) throws Exception { createLabel(toAddr(a), n, true); }\n");
		fprintf(j, "  private void func(String a, String n) throws Exception { Address ad=toAddr(a); disassemble(ad); Function f=getFunctionAt(ad); if(f==null) f=createFunction(ad,n); else f.setName(n, SourceType.USER_DEFINED); }\n");
		fprintf(j, "  public void run() throws Exception {\n");
		fprintf(j, "    println(\"Adding trace-discovered JPM System 6 labels...\");\n");

		for (unsigned int slot = 0; slot < IMPACT_TRACE_SLOT_COUNT; ++slot)
		{
			unsigned int pc = slot << 1;
			if (g_impactFunctionTargets[slot])
				fprintf(j, "    func(\"%06X\", \"trace_func_%06X\");\n", pc, pc);
		}
		for (unsigned int slot = 0; slot < IMPACT_TRACE_SLOT_COUNT; ++slot)
		{
			unsigned int pc = slot << 1;
			if (g_impactCallSites[slot])
				fprintf(j, "    label(\"%06X\", \"trace_callsite_%06X\");\n", pc, pc);
			if (g_impactBranchTargets[slot])
				fprintf(j, "    label(\"%06X\", \"trace_branch_target_%06X\");\n", pc, pc);
			if (g_impactReturnSites[slot])
				fprintf(j, "    label(\"%06X\", \"trace_return_%06X\");\n", pc, pc);
			if (g_impactIndirectCallSites[slot])
				fprintf(j, "    label(\"%06X\", \"trace_indirect_call_or_jump_%06X\");\n", pc, pc);
		}

		fprintf(j, "    println(\"Done.\");\n");
		fprintf(j, "  }\n");
		fprintf(j, "}\n");
		fclose(j);
	}
}

static void ImpactTraceInstruction(unsigned int pc, const UINT8* rom)
{
	if (!g_impactFunctionDiscoveryEnabled)
		return;

	pc &= 0xFFFFFF;
	if (pc >= IMPACT_TRACE_ROM_LIMIT)
		return;

	ImpactTraceMark(g_impactPcHits, pc);

	unsigned short op = ImpactTraceReadWord(rom, pc);

	// Direct absolute JSR/JMP long.
	if (op == 0x4EB9 || op == 0x4EF9)
	{
		unsigned int target = ImpactTraceReadLong(rom, pc + 2) & 0xFFFFFF;
		if (op == 0x4EB9)
		{
			ImpactTraceMark(g_impactCallSites, pc);
			ImpactTraceMark(g_impactFunctionTargets, target);
		}
		else
		{
			ImpactTraceMark(g_impactBranchSites, pc);
			ImpactTraceMark(g_impactBranchTargets, target);
		}
	}
	// Direct absolute JSR/JMP word.
	else if (op == 0x4EB8 || op == 0x4EF8)
	{
		unsigned int target = (unsigned short)ImpactTraceReadWord(rom, pc + 2);
		if (op == 0x4EB8)
		{
			ImpactTraceMark(g_impactCallSites, pc);
			ImpactTraceMark(g_impactFunctionTargets, target);
		}
		else
		{
			ImpactTraceMark(g_impactBranchSites, pc);
			ImpactTraceMark(g_impactBranchTargets, target);
		}
	}
	// Indirect JSR/JMP forms: JSR <ea> = 4E90-4EAF, JMP <ea> = 4ED0-4EEF.
	else if ((op >= 0x4E90 && op <= 0x4EAF) || (op >= 0x4ED0 && op <= 0x4EEF))
	{
		ImpactTraceMark(g_impactIndirectCallSites, pc);
	}
	// BSR / BRA / Bcc. 0x61xx = BSR, 0x60xx = BRA, 0x62xx-0x6Fxx = Bcc.
	else if ((op & 0xF000) == 0x6000)
	{
		unsigned int cc = (op >> 8) & 0x0F;
		unsigned int disp8 = op & 0xFF;
		unsigned int target = 0;

		if (disp8 == 0)
			target = (pc + 4 + ImpactTraceSign16(ImpactTraceReadWord(rom, pc + 2))) & 0xFFFFFF;
		else
			target = (pc + 2 + ImpactTraceSign8(disp8)) & 0xFFFFFF;

		if (cc == 1) // BSR
		{
			ImpactTraceMark(g_impactCallSites, pc);
			ImpactTraceMark(g_impactFunctionTargets, target);
		}
		else
		{
			ImpactTraceMark(g_impactBranchSites, pc);
			ImpactTraceMark(g_impactBranchTargets, target);
		}
	}
	// RTS/RTE/RTR.
	else if (op == 0x4E75 || op == 0x4E73 || op == 0x4E77)
	{
		ImpactTraceMark(g_impactReturnSites, pc);
	}

	g_impactInstructionCount++;
	if (g_impactInstructionCount - g_impactLastDumpInstruction >= 200000)
	{
		g_impactLastDumpInstruction = g_impactInstructionCount;
		ImpactTraceDumpFiles();
	}
}

// ============================================================================
// IMPACT / System 6 decompilation trace helpers
// ============================================================================
// Set this to false to disable IO tracing without removing the instrumentation.
static bool g_ImpactTraceIo = true;

static const char* ImpactIoName(unsigned int address)
{
	switch (address & 0x00FFFFFE)
	{
	case 0x480000: return "DUART1_BASE";
	case 0x480020: return "SWITCH_MATRIX_0";
	case 0x480022: return "SWITCH_MATRIX_1";
	case 0x480024: return "SWITCH_MATRIX_2";
	case 0x480026: return "SWITCH_MATRIX_3";
	case 0x480028: return "SWITCH_MATRIX_4";
	case 0x48002A: return "SWITCH_MATRIX_5";
	case 0x48002C: return "SWITCH_MATRIX_6";
	case 0x48002E: return "SWITCH_MATRIX_7";
	case 0x480030: return "SWITCH_MATRIX_8";
	case 0x480032: return "SWITCH_MATRIX_9";
	case 0x480034: return "VALID_COIN_SWITCHES";
	case 0x480040: return "REEL_OPTOS";

	case 0x480060: return "PPI_PORT_A_HOPPER_OUT";
	case 0x480062: return "PPI_PORT_B_HOPPER_SEC";
	case 0x480064: return "PPI_PORT_C_ALPHA_HOPPER";
	case 0x480066: return "PPI_CONTROL";

	case 0x480080: return "SOUND_SAMPLE_NUMBER";
	case 0x480082: return "SOUND_CONTROL";
	case 0x480084: return "SOUND_STATUS";

	case 0x4800A0: return "WATCHDOG_LED_RAMGATE";
	case 0x4800A2: return "REELS_ABCD";
	case 0x4800A4: return "REELS_EF";
	case 0x4800A6: return "METERS_PAYOUTS";
	case 0x4800A8: return "LAMP_DATA";
	case 0x4800AA: return "LED_DATA";
	case 0x4800AC: return "LAMP_INTENSITY";
	case 0x4800AE: return "LAMP_SINKS_CTRL";

	case 0x4800E0: return "SEC_ADDRESS";
	case 0x4801DC: return "LAMP_MUX_READY_UNKNOWN";
	case 0x4801E0: return "DUART2_BASE";
	default: return NULL;
	}
}

// ============================================================================


SYSTEM6::SYSTEM6()
{
	ZeroMemory(ROM, 0x100000 * sizeof(UINT8));
	ZeroMemory(RAM, 0x4000 * sizeof(UINT8));

	LSC = new LoadSaveClass();
}

SYSTEM6::~SYSTEM6()
{
	delete LSC;
}

void SYSTEM6::SetCFolder(UINT8* Folder) {

	CFolder = Folder;

}

void SYSTEM6::SetCFileName(UINT8* FileName) {

	CFileName = FileName;

}


void SYSTEM6::LoadState(void) {
	
	UINT8* OutStr = nullptr;
	int OutLen;
	
	//Load State
	OutLen = CombineStrings(OutStr, CFolder, (UINT8*)"STATE");
	LSC->LoadInit(OutStr);
	if (OutLen > 0) {
		delete(OutStr);
	}

	//Retrieve from buffer
	Lamps.LoadState();	
	Mars.LoadState();	
	Alpha1.LoadState();
	PPIO.LoadState();
	LoadCPUState();
	Meters.LoadState();
	Reels.LoadState();
	DUART.LoadState();
	DUART2.LoadState();
	Seg7.LoadState();
	Tubes.LoadState();
	Sound.LoadState();
	CashBox.LoadState();

	//SYSTEM6 stuff
	LSC->LoadFromBuffer(StatusLED);
	LSC->LoadFromBuffer(RAMEnable);
	LSC->LoadFromBuffer(IMPACT3);
	LSC->LoadFromBuffer(SndCnt);
	LSC->LoadFromBuffer(DivCycles);

	//Memory Cleanup
	LSC->LoadEnd();

}

UINT32 SYSTEM6::CombineStrings(UINT8*& OutStr, UINT8* In1, UINT8* In2) {

	UINT32 len1, len2, outlen, loop;

	//Get input string lengths
	len1 = strlen((char*)In1);
	len2 = strlen((char*)In2);

	if (len1 <= 0) return 0;
	if (len2 <= 0) return 0;

	//Set output length
	outlen = len1 + len2;
	//allocate memory
	OutStr = new UINT8[outlen + 1];

	if (OutStr) {
		//Clear Memory
		ZeroMemory(OutStr, outlen + 1);
		//Combine Strings
		for (loop = 0; loop < outlen; loop++) {

			if (loop < len1)
				OutStr[loop] = In1[loop];
			else
				OutStr[loop] = In2[loop - len1];
		}
	}

	//Return Length
	return int(outlen);

}

void SYSTEM6::SaveState(void) {

	UINT8* OutStr = nullptr;
	UINT32 OutLen;

	//Initialize
	LSC->SaveInit(0x100000);

	//Dump to buffer
	Lamps.SaveState();	
	Mars.SaveState();	
	Alpha1.SaveState();
	PPIO.SaveState();
	SaveCPUState();
	Meters.SaveState();
	Reels.SaveState();
	DUART.SaveState();
	DUART2.SaveState();
	Seg7.SaveState();
	Tubes.SaveState();
	Sound.SaveState();
	CashBox.SaveState();

	//SYSTEM6 stuff
	LSC->SaveToBuffer(StatusLED);
	LSC->SaveToBuffer(RAMEnable);
	LSC->SaveToBuffer(IMPACT3);
	LSC->SaveToBuffer(SndCnt);
	LSC->SaveToBuffer(DivCycles);


	//Save Layout Info
	OutLen = CombineStrings(OutStr, CFolder, (UINT8 *)"STATE");
	LSC->SaveToFile((UINT8*)OutStr);
	if (OutLen > 0) {
		delete(OutStr);
	}	
}

void SYSTEM6::SaveCPUState(void) {

	UINT32 loop;

	//Do Switches Here
	for (loop = 0; loop < 256; loop++) {
		LSC->SaveToBuffer(Switches.ReadSwitch(loop));
	}
	//RAM
	for (loop = 0; loop < 0x4000; loop++) {
		LSC->SaveToBuffer(RAM[loop]);
	}
	//CPU
	for (loop = 0; loop < 16; loop++) {
		LSC->SaveToBuffer(m68ki_cpu.dar[loop]);
	}
	for (loop = 0; loop < 6; loop++) {
		LSC->SaveToBuffer(m68ki_cpu.sp[loop]);
	}
	LSC->SaveToBuffer(m68ki_cpu.cpu_type);
	LSC->SaveToBuffer(m68ki_cpu.ppc);
	LSC->SaveToBuffer(m68ki_cpu.pc);
	LSC->SaveToBuffer(m68ki_cpu.vbr);
	LSC->SaveToBuffer(m68ki_cpu.sfc);
	LSC->SaveToBuffer(m68ki_cpu.dfc);
	LSC->SaveToBuffer(m68ki_cpu.cacr);
	LSC->SaveToBuffer(m68ki_cpu.caar);
	LSC->SaveToBuffer(m68ki_cpu.ir);
	LSC->SaveToBuffer(m68ki_cpu.t1_flag);
	LSC->SaveToBuffer(m68ki_cpu.t0_flag);
	LSC->SaveToBuffer(m68ki_cpu.s_flag);
	LSC->SaveToBuffer(m68ki_cpu.m_flag);
	LSC->SaveToBuffer(m68ki_cpu.x_flag);
	LSC->SaveToBuffer(m68ki_cpu.n_flag);
	LSC->SaveToBuffer(m68ki_cpu.not_z_flag);
	LSC->SaveToBuffer(m68ki_cpu.v_flag);
	LSC->SaveToBuffer(m68ki_cpu.c_flag);
	LSC->SaveToBuffer(m68ki_cpu.int_mask);
	LSC->SaveToBuffer(m68ki_cpu.int_level);
	LSC->SaveToBuffer(m68ki_cpu.int_cycles);
	LSC->SaveToBuffer(m68ki_cpu.stopped);
	LSC->SaveToBuffer(m68ki_cpu.pref_addr);
	LSC->SaveToBuffer(m68ki_cpu.pref_data);
	LSC->SaveToBuffer(m68ki_cpu.address_mask);
	LSC->SaveToBuffer(m68ki_cpu.sr_mask);
	LSC->SaveToBuffer(m68ki_cpu.cyc_bcc_notake_b);
	LSC->SaveToBuffer(m68ki_cpu.cyc_bcc_notake_w);
	LSC->SaveToBuffer(m68ki_cpu.cyc_dbcc_f_noexp);
	LSC->SaveToBuffer(m68ki_cpu.cyc_dbcc_f_exp);
	LSC->SaveToBuffer(m68ki_cpu.cyc_scc_r_false);
	LSC->SaveToBuffer(m68ki_cpu.cyc_movem_w);
	LSC->SaveToBuffer(m68ki_cpu.cyc_movem_l);
	LSC->SaveToBuffer(m68ki_cpu.cyc_shift);
	LSC->SaveToBuffer(m68ki_cpu.cyc_reset);

}
void SYSTEM6::LoadCPUState(void) {

	int loop;
	UINT8 temp;

	//Do Switches Here
	for (loop = 0; loop < 256; loop++) {
		LSC->LoadFromBuffer(temp);
		if (temp) {
			TurnSwitchOn(loop);
		}
		else {
			TurnSwitchOff(loop);
		}
	}
	//RAM
	for (loop = 0; loop < 0x4000; loop++) {
		LSC->LoadFromBuffer(RAM[loop]);
	}
	//CPU
	for (loop = 0; loop < 16; loop++) {
		LSC->LoadFromBuffer(m68ki_cpu.dar[loop]);
	}
	for (loop = 0; loop < 6; loop++) {
		LSC->LoadFromBuffer(m68ki_cpu.sp[loop]);
	}
	LSC->LoadFromBuffer(m68ki_cpu.cpu_type);
	LSC->LoadFromBuffer(m68ki_cpu.ppc);
	LSC->LoadFromBuffer(m68ki_cpu.pc);
	LSC->LoadFromBuffer(m68ki_cpu.vbr);
	LSC->LoadFromBuffer(m68ki_cpu.sfc);
	LSC->LoadFromBuffer(m68ki_cpu.dfc);
	LSC->LoadFromBuffer(m68ki_cpu.cacr);
	LSC->LoadFromBuffer(m68ki_cpu.caar);
	LSC->LoadFromBuffer(m68ki_cpu.ir);
	LSC->LoadFromBuffer(m68ki_cpu.t1_flag);
	LSC->LoadFromBuffer(m68ki_cpu.t0_flag);
	LSC->LoadFromBuffer(m68ki_cpu.s_flag);
	LSC->LoadFromBuffer(m68ki_cpu.m_flag);
	LSC->LoadFromBuffer(m68ki_cpu.x_flag);
	LSC->LoadFromBuffer(m68ki_cpu.n_flag);
	LSC->LoadFromBuffer(m68ki_cpu.not_z_flag);
	LSC->LoadFromBuffer(m68ki_cpu.v_flag);
	LSC->LoadFromBuffer(m68ki_cpu.c_flag);
	LSC->LoadFromBuffer(m68ki_cpu.int_mask);
	LSC->LoadFromBuffer(m68ki_cpu.int_level);
	LSC->LoadFromBuffer(m68ki_cpu.int_cycles);
	LSC->LoadFromBuffer(m68ki_cpu.stopped);
	LSC->LoadFromBuffer(m68ki_cpu.pref_addr);
	LSC->LoadFromBuffer(m68ki_cpu.pref_data);
	LSC->LoadFromBuffer(m68ki_cpu.address_mask);
	LSC->LoadFromBuffer(m68ki_cpu.sr_mask);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_bcc_notake_b);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_bcc_notake_w);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_dbcc_f_noexp);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_dbcc_f_exp);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_scc_r_false);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_movem_w);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_movem_l);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_shift);
	LSC->LoadFromBuffer(m68ki_cpu.cyc_reset);

}

UINT8 SYSTEM6::GetStatusLED(void) {
	return StatusLED;
}

void SYSTEM6::Init(void)
{
	Mars.Init(LSC);
	Alpha1.Initialise(LSC);
	Reels.Initialise(LSC);
	Meters.Init(LSC);
	DUART.reset(LSC);
	DUART2.reset(LSC);
	PPIO.Reset(LSC);
	Lamps.Reset(LSC);
	Seg7.Reset(LSC);
	Tubes.Init(LSC);
	Sound.NECInit(LSC);
	CashBox.Init(LSC);
}

int SYSTEM6::Run(int Cycles) {
	int ret;

	ret = m68k_execute(Cycles);

	return ret;
}

void SYSTEM6::Reset() {

	this->m68k_set_cpu_type(M68K_CPU_TYPE_68000);
	this->m68k_pulse_reset();

}
void SYSTEM6::SaveRAM(UINT8* FileString) {

	unsigned long Cnt;
	streampos size;
	UINT8* memblock;

	ofstream file((char*)FileString, ios::out | ios::binary | ios::trunc);
	if (file.is_open()) {
		size = 0x4000;
		memblock = new UINT8[0x4000];
		for (Cnt = 0; Cnt < size; Cnt++) {
			memblock[Cnt] = (RAM[Cnt] & 0xff);
		}
		file.write((char*)memblock, size);
		file.close();

		delete[] memblock;
	}

}

void SYSTEM6::LoadRAM(UINT8* FileString) {

	unsigned long Cnt;
	streampos size;
	UINT8* memblock;

	ifstream file((char*)FileString, ios::in | ios::binary | ios::ate);
	if (file.is_open()) {
		size = file.tellg();
		memblock = new UINT8[0x4000];
		file.seekg(0, ios::beg);
		file.read((char*)memblock, size);
		file.close();
		for (Cnt = 0; Cnt < 0x4000; Cnt++) {
			RAM[Cnt] = (memblock[Cnt] & 0xff);
		}
		delete[] memblock;
	}

}
UINT8 SYSTEM6::GetAlphaChar(UINT8 num) {
	return 0;
}
UINT32 SYSTEM6::GetAlphaSegs(UINT8 CharIn) {	
	UINT32 ret = Alpha1.GetAlphaSegments(CharIn);
	return ret;
}
UINT8 SYSTEM6::GetAlphaDotComma(UINT8 SegIn) {
	char ret;
	ret = Alpha1.GetAlphaDotComma(SegIn);
	return ret;
}
UINT8 SYSTEM6::GetAlphaBright() {
	char ret;
	ret = Alpha1.GetAlphaBright();
	return ret;
}
INT16 SYSTEM6::GetPosOut(UINT8 num) {
	INT16 ret = Reels.GetPosOut(num);
	return ret;
}

void SYSTEM6::UpdateLamps(void) {

	Lamps.Update();

}

float SYSTEM6::GetLampBrightness(UINT16 num) {
	return Lamps.GetLampBrightness(num);
}
bool SYSTEM6::GetLampsOn(UINT16 num) {
	return Lamps.GetLampsOn(num);
}
float3 SYSTEM6::GetFilamentColour(UINT16 num) {
	return Lamps.GetFilamentColour(num);
}
void SYSTEM6::UpdateSegs(void) {

	Seg7.Update();

}
UINT8 SYSTEM6::GetSegOn(unsigned short num) {	
	if (num > 255) return 0;
	UINT8 ret = Seg7.GetOn(num & 0xff);
	return ret;
}
UINT8 SYSTEM6::GetSegBright(unsigned short num) {	
	if (num > 255) return 0;
	UINT8 ret = Seg7.GetBrightness(num & 0xff);
	return ret;
}
unsigned int SYSTEM6::GetMeterCounter(UINT8 num) {
	unsigned int ret;
	ret = Meters.GetCounter(num);
	return ret;
}
void SYSTEM6::TurnSwitchOn(UINT8 num) {
	Switches.TurnSwitchOn(num);
}

void SYSTEM6::TurnSwitchOff(UINT8 num) {
	Switches.TurnSwitchOff(num);
}

UINT8 SYSTEM6::ReadSwitch(UINT8 num) {
	UINT8 ret;
	ret = Switches.ReadSwitch(num);
	return ret;
}
UINT8 SYSTEM6::CoinIn(UINT8 Coin, UINT8 CoinValue) {
	Mars.SetSelectedCoin(Coin);
	if (Mars.CoinIn(Coin)) {
		if (Tubes.CoinIn(CoinValue) == 0xff) {
			if (Hoppers.CoinIn(CoinValue) == 0xff) {
				CashBox.CoinIn(CoinValue);
			}
		}
		return 1;//Coin Accepted
	}
	return 0;//Coin Rejected
}
void SYSTEM6::SetCommStyle(UINT8 Style) {
	Mars.SetCommStyle(Style);
}
void SYSTEM6::SetCommInvert(UINT8 Invert) {
	Mars.SetCommInvert(Invert);
}
void SYSTEM6::SetCycles(UINT32 Cycles) {
	Mars.SetCycles(Cycles);
}
void SYSTEM6::SetEDCEnable(UINT8 Enable) {
	Mars.SetEDCEnable(Enable);
}
void SYSTEM6::SetLockoutVal(UINT8 Coin, UINT8 Value) {
	Mars.SetLockoutVal(Coin, Value);
}
void SYSTEM6::SetLockoutInvert(UINT8 Coin, UINT8 Invert) {
	Mars.SetLockoutInvert(Coin, Invert);
}
void SYSTEM6::SetCoinValue(UINT8 CoinNum, UINT8 Value)
{
	Mars.SetCoinValue(CoinNum, Value);
}
void SYSTEM6::SetCoinEnable(UINT8 CoinNum, UINT8 Value)
{
	Mars.SetCoinEnable(CoinNum, Value);
}
UINT8 SYSTEM6::GetCoinLampOnOff(UINT8 LampNum)
{
	UINT8 ret = Mars.GetLampOnOff(LampNum);
	return ret;
}

int __fastcall  SYSTEM6::cpu_irq_ack(int level)
{
	int res = M68K_INT_ACK_AUTOVECTOR; // Default is Auto-Vector

	switch (level)
	{
	case 5:
		m68k_set_irq(M68K_IRQ_NONE);
		// UART supplies Vector
		res = DUART.ivr;
		break;
	default:
		m68k_set_irq(M68K_IRQ_NONE);
		break;
	}

	return res;
}

void __fastcall	SYSTEM6::cpu_set_fc(int discard)
{

}

void __fastcall  SYSTEM6::cpu_inst_hook(int cycles)
{

	int TickCycles = 0;
	int loop;

	//Update Total Cycles
	TotalCycles += cycles;
	//ImpactTraceInstruction(m68ki_cpu.ppc, ROM);

	//Divide Cycles by 4, account for remainders.
	DivCycles += cycles;
	while (DivCycles > 4) {
		DivCycles -= 4;
		TickCycles += 1;
	}

	//Tick Duarts
	DUART.tick(TickCycles);
	DUART2.tick(TickCycles);

	//Interrupts from DUARTs
	if (DUART.isr & DUART.imr)
	{
		m68k_set_irq(M68K_IRQ_5);
	}
	/*if (DUART2.isr & DUART2.imr)
	 {
	  m68k_set_irq(M68K_IRQ_5);
	 }*/


	 //Run Components
	Lamps.Run(cycles);
	Seg7.RunJPMSegs(cycles, TotalCycles);
	Meters.Run(cycles);

	UINT8 SelCoin;

	//Coin Mech
	SelCoin = Mars.GetSelectedCoin();
	if (Mars.GetCommStyle() == 0) {
		//Parallel
		if (Mars.Run(cycles)) {
			TurnSwitchOn(72 + SelCoin);
		}
		else {
			TurnSwitchOff(72 + SelCoin);
		}
	}
	else if (Mars.GetCommStyle() == 1) {
		//BCD
		//Parallel		
		if (Mars.Run(cycles)) {
			UINT8 TBCD = Mars.GetBCD();
			for (loop = 0; loop < 8; loop++) {
				if (TBCD & (1 << loop)) {
					TurnSwitchOn(72 + loop);
				}
				else {
					TurnSwitchOff(72 + loop);
				}
			}
		}
		else {
			for (loop = 0; loop < 8; loop++) {
				if (0x20 & (1 << loop)) {
					TurnSwitchOn(72 + loop);
				}
				else {
					TurnSwitchOff(72 + loop);
				}
			}
		}
	}
	//Hoppers
	Hoppers.Update(0, cycles);
	Hoppers.Update(1, cycles);
	for (loop = 0; loop < 2; loop++) {
		if (Hoppers.GetHopperHiEnable(loop)) {
			if (Hoppers.GetHopperHiIndicator(loop)) {
				if (Hoppers.GetHopperHiInvert(loop)) {
					TurnSwitchOff(Hoppers.GetHopperHiSwitch(loop));
				}
				else {
					TurnSwitchOn(Hoppers.GetHopperHiSwitch(loop));
				}
			}
			else {
				if (Hoppers.GetHopperHiInvert(loop)) {
					TurnSwitchOn(Hoppers.GetHopperHiSwitch(loop));
				}
				else {
					TurnSwitchOff(Hoppers.GetHopperHiSwitch(loop));
				}
			}
		}

		if (Hoppers.GetHopperLoEnable(loop)) {
			if (Hoppers.GetHopperLoIndicator(loop)) {
				if (Hoppers.GetHopperLoInvert(loop)) {
					TurnSwitchOff(Hoppers.GetHopperLoSwitch(loop));
				}
				else {
					TurnSwitchOn(Hoppers.GetHopperLoSwitch(loop));
				}
			}
			else {
				if (Hoppers.GetHopperLoInvert(loop)) {
					TurnSwitchOn(Hoppers.GetHopperLoSwitch(loop));
				}
				else {
					TurnSwitchOff(Hoppers.GetHopperLoSwitch(loop));
				}
			}
		}
	}
	//Sound
	Sound.NECRun(cycles);
	SndCnt += cycles;
	if (SndCnt > 2000) {
		SndCnt -= 2000;// 1/4000 seconds
		Sound.NECUpdate();

		//Coin Tubes
		Tubes.Update();
		for (loop = 0; loop < 4; loop++) {
			if (Tubes.GetHiEnable(loop)) {
				if (Tubes.GetHiState(loop)) {
					if (Tubes.GetHiInvert(loop)) {
						TurnSwitchOff(Tubes.GetHiSwitch(loop));
					}
					else {
						TurnSwitchOn(Tubes.GetHiSwitch(loop));
					}
				}
				else {
					if (Tubes.GetHiInvert(loop)) {
						TurnSwitchOn(Tubes.GetHiSwitch(loop));
					}
					else {
						TurnSwitchOff(Tubes.GetHiSwitch(loop));
					}
				}
			}
			else {
				TurnSwitchOff(Tubes.GetHiSwitch(loop));
			}

			if (Tubes.GetLoEnable(loop)) {
				if (Tubes.GetLoState(loop)) {
					if (Tubes.GetLoInvert(loop)) {
						TurnSwitchOff(Tubes.GetLoSwitch(loop));
					}
					else {
						TurnSwitchOn(Tubes.GetLoSwitch(loop));
					}
				}
				else {
					if (Tubes.GetLoInvert(loop)) {
						TurnSwitchOn(Tubes.GetLoSwitch(loop));
					}
					else {
						TurnSwitchOff(Tubes.GetLoSwitch(loop));
					}
				}
			}
			else {
				TurnSwitchOff(Tubes.GetLoSwitch(loop));
			}
		}
	}

}

void __fastcall		SYSTEM6::cpu_pulse_reset(void)
{

}

/*
0x400000	RAM
0x480000	DUART 1

0x480021	Switch Start
0x480035	Valid Switches
0x480041	Reel Optos
0x480060	PIA
0x480061	PIA Port A - Hopper Outputs
0x480063	PIA Port B - Hopper Inputs, SEC Data
0x480065	PIA Port C - Hopper Inputs, Alpha Display Outputs
0x480067	PIA Control
0x480081	Sample Num
0x480083	Sample Control (Sample Stop 0x1, Page 0x2 - 0x8, Volume Step 0x10, Volume Dir 0x20, Vol Control 0x40, Volume Disable 0x40 should be 0x80?)
0x480085	Sample Status (Busy 0x1, Feedback 0x10 - 0x80)

0x4800a0	0x1 Watchdog / 0x200 LED / 0x100 RAM
0x4800a2	Reels A - D
0x4800a4	Reels E - F
0x4800a6	Meters (bits 9 - 15)
0x4800a8	Lamp Data
0x4800ab	LED Data
0x4800ad	Lamp Intesnity
0x4800af	Sinks + Ctrl (Strobe Enable 0x10, Intensity Enable 0x20)
0x4800e0	SEC Address (SEC Meters or Security?)
0x4801e0	DUART 2
*/

UINT8 __fastcall 	SYSTEM6::cpu_read_byte(int address)
{
	UINT8 value = 0;
	int Val;
	if (address < 0x100000)//ROM
	{
		value = ROM[address];
	}
	else if ((address >= 0x400000) && (address < 0x404000))//RAM
	{
		if (RAMEnable) {
			Val = 1;
		}
		value = RAM[address - 0x400000];
	}
	else if ((address >= 0x480060) && (address < 0x480068))//PIA
	{
		//Port B	- Hoppers
		//0x1		- Opto Input (Both Hoppers)
		//0x2		- Low Switch Hopper 0 (Switches 64+72 Hopper 1 Low)
		//0x4		- SEC Input (SEC Meters)
		//0x8		- 
		//0x10		- 
		//0x20		- Hopper Detect
		//0x40		- Hopper Detect
		//0x80		- 
		PPIO.PortBIn = 0xff;
		if (Hoppers.Enable[0]) {
			PPIO.PortBIn &= ~0x21;
		}
		if (Hoppers.Enable[1]) {
			PPIO.PortBIn &= ~0x41;
		}
		UINT8 Opto1, Opto2;
		static UINT8 LastOpto = 0;
		Opto1 = Hoppers.ReadOpto(0);
		Opto2 = Hoppers.ReadOpto(1);
		if ((Opto1 | (Opto2 << 1)) != LastOpto) {
			UINT8 Stop = 0;
		}

		PPIO.PortBIn |= (Opto1);
		LastOpto = (Opto1 | (Opto2 << 1));
		//Port C	- Upper 4 Bits		(Lower 4 Bits are output and drive Alpha).
		//0x80		- Vogue Cab Games wont pay 20p if this is high. (Hopper 1)
		//0x40		- Vogue Cab Games wont pay �1 if this is high (Hopper 0)
		//0x20		- Unknown
		//0x10		- Unknown
		PPIO.PortCIn = 0xf0;//0x30


		value = PPIO.Read((address - 0x480060) >> 1);
	}
	else if (address >= 0x480000 && address < 0x480020)//DUART 1
	{

		//Input Port 0x10 - Meters Return
		if (Meters.Check()) {
			DUART.ip &= ~0x10;
		}
		else {
			DUART.ip |= 0x10;
		}
		//Input Port 0x20 - Test Button		
		if (Switches.ReadSwitch(255)) {
			DUART.ip &= ~0x20;
		}
		else {
			DUART.ip |= 0x20;
		}
		//DUART.ip |= 0xff;
		value = DUART.read((address - 0x480000) >> 1);
	}
	else if (address >= 0x480020 && address < 0x480034)//Switch Matrix
	{
		UINT8 index = ((address - 0x480020) >> 1);

		value = ~(Switches.ReadMatrix(index));
	}
	else if (address == 0x480041)//Reel Optos
	{
		value = Reels.GetOptos();
	}
	else if (address == 0x480085)//Sample Status
	{
		value = Sound.GetBusy();
	}
	else if (address == 0x480035)//Valid Coin Switches
	{
		value = 0xff;
	}
	else if (address >= 0x4801e0 && address < 0x480200)//DUART 2
	{
		value = DUART2.read((address - 0x4801e0) >> 1);
	}
	else if (address == 0x4801DD) //Not Sure but game locks up without it (Lamp MUX Ready or OK?)
	{
		//Bit 0 is checked specifically
		value = 1;
	}

	else
	{
		// Memory Access Error
		fopen_s(&DebugFile, "UnknownReads.txt", "a");
		if (DebugFile) {
			fprintf(DebugFile, "Unknown Read8 @ Addr: %X \n", address);
			fprintf(DebugFile, "Unknown Value: %X \n", value);
			fclose(DebugFile);
		}
	}

	return value;
}

UINT16 __fastcall 	SYSTEM6::cpu_read_word(int address)
{
	UINT16 value = 0;
	int Val;
	if (address < 0x100000)//ROM
	{
		if (address < 0xffffd) {
			value = ROM[address];
			value = ROM[address + 1] + (value << 8);
		}
	}
	else if ((address >= 0x400000) && (address < 0x404000))//RAM
	{
		if (RAMEnable) {
			Val = 1;
		}
		address -= 0x400000;
		value = RAM[address];
		value = RAM[address + 1] + (value << 8);
	}
	else
	{
		// Memory Access Error
		fopen_s(&DebugFile, "UnknownReads.txt", "a");
		if (DebugFile) {
			fprintf(DebugFile, "Unknown Read16 @ Addr: %X \n", address);
			fprintf(DebugFile, "Unknown Value: %X \n", value);
			fclose(DebugFile);
		}
	}

	return value;
}

UINT32 __fastcall 	SYSTEM6::cpu_read_long(int address)
{
	UINT32 value = 0;
	int Val;
	if (address < 0x100000)//ROM
	{
		if (address < 0xffffd) {
			value = ROM[address];
			value = ROM[address + 1] + (value << 8);
			value = ROM[address + 2] + (value << 8);
			value = ROM[address + 3] + (value << 8);
		}
	}
	else if ((address >= 0x400000) && (address < 0x404000))
	{
		if (RAMEnable) {
			Val = 1;
		}
		address -= 0x400000;//RAM
		value = RAM[address];
		value = RAM[address + 1] + (value << 8);
		value = RAM[address + 2] + (value << 8);
		value = RAM[address + 3] + (value << 8);
	}
	else
	{
		// Memory Access Error		
		fopen_s(&DebugFile, "UnknownReads.txt", "a");
		if (DebugFile) {
			fprintf(DebugFile, "Unknown Read32 @ Addr: %X \n", address);
			fprintf(DebugFile, "Unknown Value: %X \n", value);
			fclose(DebugFile);
		}
	}

	return value;
}

void __fastcall 	SYSTEM6::cpu_write_byte(int address, UINT8 value)
{

	int Sec;

	if ((address >= 0x400000) && (address < 0x404000))//RAM
	{
		RAM[address - 0x400000] = value;
	}
	else if ((address >= 0x480060) && (address < 0x480068))//PIA
	{
		PPIO.Write(((address - 0x480060) >> 1), value);

		if (PPIO.PortCChanged)//Alpha Displays
		{
			Alpha1.WriteAlphaBits((PPIO.PortC & 4) >> 2, (PPIO.PortC & 1), (PPIO.PortC & 2) >> 1);
		}
		if (PPIO.PortAChanged)//Hoppers
		{
			//0x80	- 
			//0x40	- 
			//0x20	- Opto Enable?			
			//0x10	- 50Hz Motor Supply Enable
			//0x2	- 
			//0x2	- 
			//0x2	- Opto Enable?
			//0x1	- Motor Select
			Hoppers.WriteOptoEnable(0, PPIO.PortA & 0x2); //�1
			Hoppers.WriteOptoEnable(1, PPIO.PortA & 0x2); //10p

			switch (PPIO.PortA & 0x33) {
			case 0x2:
			case 0x22:
				//Hoppers Off
				Hoppers.WriteMotor(0, 0);
				Hoppers.WriteMotor(1, 0);
				break;
			case 0x12:
			case 0x32:
				//Paying 20p
				Hoppers.WriteMotor(1, 1);
				//Hopper 0 Off
				Hoppers.WriteMotor(0, 0);
				break;
			case 0x13:
			case 0x33:
				//Paying �1
				Hoppers.WriteMotor(0, 1);
				//Hopper 1 Off
				Hoppers.WriteMotor(1, 0);
				break;
			case 0x20:
			case 0x0:
				//Hoppers Off
				Hoppers.WriteMotor(0, 0);
				Hoppers.WriteMotor(1, 0);
				break;
			default:
				UINT8 Stop = 1;
				break;
			}
		}
	}
	else if ((address >= 0x480000) && (address < 0x480020))//DUART
	{
		DUART.write(((address - 0x480000) >> 1), value);
		if (DUART.op_changed) {
			Mars.SetLockoutPort(value);
		}
	}
	else if (address == 0x480081)//Sound Sample Value + Play
	{
		Sound.NECSetTune(value);
		if (Sound.GetBusy()) {//Busy is inverted
			Sound.NECPlay();
		}
	}
	else if (address == 0x480083)//Sound Control
	{
		Sound.NECSetBank((value >> 1) & 7);
		if (value & 1)
		{
			// Reset
			Sound.NECReset();
		}

	}
	else if (address == 0x4800ab)//LED Data
	{
		Seg7.WriteJPMSegs(value);
	}
	else if (address == 0x4800af)//Sinks + Ctrl (Strobe Enable 0x10, Intensity Enable 0x20)
	{

		if (value & 0x10)//Strobe Enable
		{
			Lamps.WriteStrobe((value + 1) & 0xf);

			//7 Segs
			Seg7.SetLastMuxValue(Lamps.StrobeVal);
			Seg7.SetMuxValue((value + 1) & 0xf);
		}
		if (value & 0x20)//Intensity Enable
		{
			//If any value written at all then this is IMPACT3?
			IMPACT3 = 1;
			Lamps.IntensityEnable = (value & 0xf);
		}
	}
	else if (address == 0x4800ad)//Lamp Intensity
	{
		if (IMPACT3) {
			if (Lamps.IntensityEnable) {
				Lamps.SetIntensity(value);
				Seg7.SetIntensity(value);
			}
		}
		else {
			Lamps.SetIntensity(value);
			Seg7.SetIntensity(value);
		}
	}
	else if (address == 0x4800e0)//Sec
	{
		Sec = value;
	}
	else if ((address >= 0x4801e0) && (address < 0x480200))//DUART 2
	{
		DUART2.write(((address - 0x4801e0) >> 1), value);
		if (DUART2.op_changed) {

		}
	}
	else
	{
		//Memory Access Error	 
		fopen_s(&DebugFile, "UnknownWrites.txt", "a");
		if (DebugFile) {
			fprintf(DebugFile, "Unknown Write8 @ Addr: %X \n", address);
			fprintf(DebugFile, "Unknown Value: %X \n", value);
			fclose(DebugFile);
		}
	}
}

void __fastcall 	SYSTEM6::cpu_write_word(int address, UINT16 value)
{
	if ((address >= 0x400000) && (address < 0x404000))
	{
		address -= 0x400000;
		RAM[address] = value >> 8;
		RAM[address + 1] = value & 0xff;
	}
	else if (address == 0x4800a0)//Watchdog
	{
		//0x100 = RAM, 0x01 = Watchdog, 0x200 = Status LED		
		StatusLED = (value & 0x200) >> 9;
		RAMEnable = (value & 0x100) >> 8;
	}
	else if (address == 0x4800a2)//Reels A - D
	{
		Reels.WriteJPMReel(value & 0xf, 0);
		Reels.WriteJPMReel((value & 0xf0) >> 4, 1);
		Reels.WriteJPMReel((value & 0xf00) >> 8, 2);
		Reels.WriteJPMReel((value & 0xf000) >> 12, 3);
	}
	else if (address == 0x4800a4)//Reels D - E
	{
		Reels.WriteJPMReel(value & 0xf, 4);
		Reels.WriteJPMReel((value & 0xf0) >> 4, 5);
	}
	else if (address == 0x4800a6)//Meters + Payouts	
	{
		//Meter Writes
		Meters.Write(0, ((value >> 10) & 1));
		Meters.Write(1, ((value >> 11) & 1));
		Meters.Write(2, ((value >> 12) & 1));
		Meters.Write(3, ((value >> 13) & 1));
		Meters.Write(4, ((value >> 14) & 1));
		Meters.Write(5, ((value >> 15) & 1));

		//Triac Writes
		if (value & 0x10) {//50v AC Circuit Enable
			Tubes.Write(value & 0x0f);
		}

	}
	else if (address == 0x4800a8)//Lamp Data
	{
		Lamps.WriteData(value);
	}
	else
	{
		fopen_s(&DebugFile, "UnknownWrites.txt", "a");
		if (DebugFile) {
			fprintf(DebugFile, "Unknown Write16 @ Addr: %X \n", address);
			fprintf(DebugFile, "Unknown Value: %X \n", value);
			fclose(DebugFile);
		}
	}
}

void __fastcall 	SYSTEM6::cpu_write_long(int address, UINT32 value)
{
	if ((address >= 0x400000) && (address < 0x404000)) //RAM
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
		fopen_s(&DebugFile, "UnknownWrites.txt", "a");
		if (DebugFile) {
			fprintf(DebugFile, "Unknown Write32 @ Addr: %X \n", address);
			fprintf(DebugFile, "Unknown Value: %X \n", value);
			fclose(DebugFile);
		}
	}
}

void SYSTEM6::SetEnable(UINT8 Num, UINT8 Enabl) {
	Tubes.SetEnable(Num, Enabl);
}
void SYSTEM6::SetCounterIn(UINT8 Num, UINT32 Count) {
	Tubes.SetCounterIn(Num, Count);
}
void SYSTEM6::SetCounterOut(UINT8 Num, UINT32 Count) {
	Tubes.SetCounterOut(Num, Count);
}
void SYSTEM6::SetPortIndex(UINT8 Num, UINT8 Index) {
	Tubes.SetPortIndex(Num, Index);
}
void SYSTEM6::SetCoin(UINT8 Num, UINT8 CoinIn) {
	Tubes.SetCoin(Num, CoinIn);
}
void SYSTEM6::SetLevel(UINT8 Num, UINT8 LevelIn) {
	Tubes.SetLevel(Num, LevelIn);
}
void SYSTEM6::SetFullLevel(UINT8 Num, UINT8 LevelIn) {
	Tubes.SetFullLevel(Num, LevelIn);
}
void SYSTEM6::SetLoEnable(UINT8 Num, UINT8 Enabl) {
	Tubes.SetLoEnable(Num, Enabl);
}
void SYSTEM6::SetLoInvert(UINT8 Num, UINT8 Invert) {
	Tubes.SetLoInvert(Num, Invert);
}
void SYSTEM6::SetLoSwitch(UINT8 Num, UINT8 Switch) {
	Tubes.SetLoSwitch(Num, Switch);
}
void SYSTEM6::SetLoLevel(UINT8 Num, UINT32 LevelIn) {
	Tubes.SetLoLevel(Num, LevelIn);
}
void SYSTEM6::SetHiEnable(UINT8 Num, UINT8 Enabl) {
	Tubes.SetHiEnable(Num, Enabl);
}
void SYSTEM6::SetHiInvert(UINT8 Num, UINT8 Invert) {
	Tubes.SetHiInvert(Num, Invert);
}
void SYSTEM6::SetHiSwitch(UINT8 Num, UINT8 Switch) {
	Tubes.SetHiSwitch(Num, Switch);
}
void SYSTEM6::SetHiLevel(UINT8 Num, UINT32 LevelIn) {
	Tubes.SetHiLevel(Num, LevelIn);
}

UINT8 SYSTEM6::GetEnable(UINT8 Num) {
	UINT8 ret = Tubes.GetEnable(Num);
	return ret;
}
UINT32 SYSTEM6::GetCounterIn(UINT8 Num) {	
	UINT32 ret = Tubes.GetCounterIn(Num);
	return ret;
}
UINT32 SYSTEM6::GetCounterOut(UINT8 Num) {
	UINT32 ret = Tubes.GetCounterOut(Num);
	return ret;
}
UINT8 SYSTEM6::GetPortIndex(UINT8 Num) {
	UINT8 ret = Tubes.GetPortIndex(Num);
	return ret;
}
UINT8 SYSTEM6::GetCoin(UINT8 Num) {
	UINT8 ret = Tubes.GetCoin(Num);
	return ret;
}
UINT32 SYSTEM6::GetLevel(UINT8 Num) {
	UINT32 ret = Tubes.GetLevel(Num);
	return ret;
}
UINT32 SYSTEM6::GetFullLevel(UINT8 Num) {	
	UINT32 ret = Tubes.GetFullLevel(Num);
	return ret;
}
UINT8 SYSTEM6::GetLoEnable(UINT8 Num) {
	UINT8 ret = Tubes.GetLoEnable(Num);
	return ret;
}
UINT8 SYSTEM6::GetLoInvert(UINT8 Num) {
	UINT8 ret = Tubes.GetLoInvert(Num);
	return ret;
}
UINT8 SYSTEM6::GetLoSwitch(UINT8 Num) {
	UINT8 ret = Tubes.GetLoSwitch(Num);
	return ret;
}
UINT32 SYSTEM6::GetLoLevel(UINT8 Num) {
	UINT32 ret = Tubes.GetLoLevel(Num);
	return ret;
}
UINT8 SYSTEM6::GetHiEnable(UINT8 Num) {
	UINT8 ret = Tubes.GetHiEnable(Num);
	return ret;
}
UINT8 SYSTEM6::GetHiInvert(UINT8 Num) {
	UINT8 ret = Tubes.GetHiInvert(Num);
	return ret;
}
UINT8 SYSTEM6::GetHiSwitch(UINT8 Num) {
	UINT8 ret = Tubes.GetHiSwitch(Num);
	return ret;
}
UINT32 SYSTEM6::GetHiLevel(UINT8 Num) {
	UINT32 ret = Tubes.GetHiLevel(Num);
	return ret;
}
void SYSTEM6::SetOptoInvert(UINT8 ReelNum, UINT8 State) {
	Reels.SetOptoInvert(ReelNum, State);
}
void SYSTEM6::SetOptoStart(UINT8 ReelNum, UINT8 Start) {
	Reels.SetOptoStart(ReelNum, Start);
}
void SYSTEM6::SetOptoEnd(UINT8 ReelNum, UINT8 End) {
	Reels.SetOptoEnd(ReelNum, End);
}
void SYSTEM6::SetSteps(UINT8 ReelNum, UINT8 Steps) {
	Reels.SetSteps(ReelNum, Steps);
}


void SYSTEM6::SetDIP(UINT8 Num, UINT8 Value) {

	if (Value) {
		Switches.TurnSwitchOn(Num);
	}
	else {
		Switches.TurnSwitchOff(Num);
	}
}

void SYSTEM6::SetHopperEnable(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperEnable(Num, Value);
}
void SYSTEM6::SetHopperCoin(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperCoin(Num, Value);
}
void SYSTEM6::SetHopperCoinsIn(UINT8 Num, UINT32 Value) {
	Hoppers.SetHopperCoinsIn(Num, Value);
}
void SYSTEM6::SetHopperCoinsOut(UINT8 Num, UINT32 Value) {
	Hoppers.SetHopperCoinsOut(Num, Value);
}
void SYSTEM6::SetHopperLevel(UINT8 Num, UINT32 Value) {
	Hoppers.SetHopperLevel(Num, Value);
}
void SYSTEM6::SetHopperFullLevel(UINT8 Num, UINT32 Value) {
	Hoppers.SetHopperFullLevel(Num, Value);
}
void SYSTEM6::SetHopperLoEnable(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperLoEnable(Num, Value);
}
void SYSTEM6::SetHopperLoInvert(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperLoInvert(Num, Value);
}
void SYSTEM6::SetHopperLoSwitch(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperLoSwitch(Num, Value);
}
void SYSTEM6::SetHopperLoLevel(UINT8 Num, UINT32 Value) {
	Hoppers.SetHopperLoLevel(Num, Value);
}
void SYSTEM6::SetHopperHiEnable(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperHiEnable(Num, Value);
}
void SYSTEM6::SetHopperHiInvert(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperHiInvert(Num, Value);
}
void SYSTEM6::SetHopperHiSwitch(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperHiSwitch(Num, Value);
}
void SYSTEM6::SetHopperHiLevel(UINT8 Num, UINT32 Value) {
	Hoppers.SetHopperHiLevel(Num, Value);
}
void SYSTEM6::SetHopperOptoEnable(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperOptoEnable(Num, Value);
}
void SYSTEM6::SetHopperOptoReturn(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperOptoReturn(Num, Value);
}
void SYSTEM6::SetHopperMotorEnable(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperMotorEnable(Num, Value);
}
void SYSTEM6::SetHopperLoIndicator(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperLoIndicator(Num, Value);
}
void SYSTEM6::SetHopperHiIndicator(UINT8 Num, UINT8 Value) {
	Hoppers.SetHopperHiIndicator(Num, Value);
}
void SYSTEM6::SetHopperCoinsRefilled(UINT8 Num, UINT32 Value) {
	Hoppers.SetHopperCoinsRefilled(Num, Value);
}

UINT8 SYSTEM6::GetHopperEnable(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperEnable(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperCoin(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperCoin(Num);
	return ret;
}
UINT32 SYSTEM6::GetHopperCoinsIn(UINT8 Num) {
	UINT32 ret = 0;
	ret = Hoppers.GetHopperCoinsIn(Num);
	return ret;
}
UINT32 SYSTEM6::GetHopperCoinsOut(UINT8 Num) {
	UINT32 ret = 0;
	ret = Hoppers.GetHopperCoinsOut(Num);
	return ret;
}
UINT32 SYSTEM6::GetHopperLevel(UINT8 Num) {
	UINT32 ret = 0;
	ret = Hoppers.GetHopperLevel(Num);
	return ret;
}
UINT32 SYSTEM6::GetHopperFullLevel(UINT8 Num) {
	UINT32 ret = 0;
	ret = Hoppers.GetHopperFullLevel(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperLoEnable(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperLoEnable(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperLoInvert(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperLoInvert(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperLoSwitch(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperLoSwitch(Num);
	return ret;
}
UINT32 SYSTEM6::GetHopperLoLevel(UINT8 Num) {
	UINT32 ret = 0;
	ret = Hoppers.GetHopperLoLevel(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperHiEnable(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperHiEnable(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperHiInvert(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperHiInvert(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperHiSwitch(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperHiSwitch(Num);
	return ret;
}
UINT32 SYSTEM6::GetHopperHiLevel(UINT8 Num) {
	UINT32 ret = 0;
	ret = Hoppers.GetHopperHiLevel(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperOptoEnable(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperOptoEnable(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperOptoReturn(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperOptoReturn(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperMotorEnable(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperMotorEnable(Num);
	return ret;
}

UINT32 SYSTEM6::GetHopperCoinsRefilled(UINT8 Num) {
	UINT32 ret = 0;
	ret = Hoppers.GetHopperCoinsRefilled(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperHiIndicator(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperHiIndicator(Num);
	return ret;
}
UINT8 SYSTEM6::GetHopperLoIndicator(UINT8 Num) {
	UINT8 ret = 0;
	ret = Hoppers.GetHopperLoIndicator(Num);
	return ret;
}

void SYSTEM6::SetStake(UINT8 StakeIn) {

	UINT8 loop;

	for (loop = 0; loop < 4; loop++) {
		if (StakeIn & (1 << loop)) {
			TurnSwitchOn(20 + loop);
		}
		else {
			TurnSwitchOff(20 + loop);
		}
	}
}

void SYSTEM6::SetPrize(UINT8 PrizeIn) {

	UINT8 loop;

	for (loop = 0; loop < 4; loop++) {
		if (PrizeIn & (1 << loop)) {
			TurnSwitchOn(16 + loop);
		}
		else {
			TurnSwitchOff(16 + loop);
		}
	}
}

void SYSTEM6::SetPercent(UINT8 PercentIn) {

	UINT8 loop;

	for (loop = 0; loop < 4; loop++) {
		if (PercentIn & (1 << loop)) {
			TurnSwitchOn(8 + loop);
		}
		else {
			TurnSwitchOff(8 + loop);
		}
	}
}

UINT8* SYSTEM6::GetEDCString(void) {

	return DUART.GetEDCString();

}

/* ======================================================================== */
/* ========================= LICENSING & COPYRIGHT ======================== */
/* ======================================================================== */
/*
 *                                  MUSASHI
 *                                Version 3.3
 *
 * A portable Motorola M680x0 processor emulation engine.
 * Copyright 1998-2001 Karl Stenerud.  All rights reserved.
 *
 * This code may be freely used for non-commercial purposes as long as this
 * copyright notice remains unaltered in the source code and any binary files
 * containing this code in compiled form.
 *
 * All other lisencing terms must be negotiated with the author
 * (Karl Stenerud).
 *
 * The latest version of this code can be obtained at:
 * http://kstenerud.cjb.net
 */




#ifndef m68kcpuH
#define m68kcpuH

//#include <Classes.hpp>
#include <stdio.h>
#include <limits.h>
#include "m68k.h"

#if M68K_EMULATE_ADDRESS_ERROR
#include <setjmp.h>
#endif // M68K_EMULATE_ADDRESS_ERROR

/* ======================================================================== */
/* ==================== ARCHITECTURE-DEPENDANT DEFINES ==================== */
/* ======================================================================== */

/* Check for > 32bit sizes */
#if UINT_MAX > 0xffffffff
	#define M68K_INT_GT_32_BIT  1
#endif

/* Data types used in this emulation core */
#undef sint8
#undef sint16
#undef sint32
#undef sint64
#undef uint8
#undef uint16
#undef uint32
#undef uint64
#undef sint
#undef uint

#define sint8  signed   char			/* ASG: changed from char to signed char */
#define sint16 signed   short
#define sint32 signed   long
#define uint8  unsigned char
#define uint16 unsigned short
#define uint32 unsigned long

/* signed and unsigned int must be at least 32 bits wide */
#define sint   signed   int
#define uint   unsigned int


#if M68K_USE_64_BIT
#define sint64 signed   long long
#define uint64 unsigned long long
#else
#define sint64 sint32
#define uint64 uint32
#endif /* M68K_USE_64_BIT */



/* Allow for architectures that don't have 8-bit sizes */
#if UCHAR_MAX == 0xff
	#define MAKE_INT_8(A) (sint8)(A)
#else
	#undef  sint8
	#define sint8  signed   int
	#undef  uint8
	#define uint8  unsigned int
	INLINE sint MAKE_INT_8(uint value)
	{
		return (value & 0x80) ? value | ~0xff : value & 0xff;
	}
#endif /* UCHAR_MAX == 0xff */


/* Allow for architectures that don't have 16-bit sizes */
#if USHRT_MAX == 0xffff
	#define MAKE_INT_16(A) (sint16)(A)
#else
	#undef  sint16
	#define sint16 signed   int
	#undef  uint16
	#define uint16 unsigned int
	INLINE sint MAKE_INT_16(uint value)
	{
		return (value & 0x8000) ? value | ~0xffff : value & 0xffff;
	}
#endif /* USHRT_MAX == 0xffff */


/* Allow for architectures that don't have 32-bit sizes */
#if ULONG_MAX == 0xffffffff
	#define MAKE_INT_32(A) (sint32)(A)
#else
	#undef  sint32
	#define sint32  signed   int
	#undef  uint32
	#define uint32  unsigned int
	INLINE sint MAKE_INT_32(uint value)
	{
		return (value & 0x80000000) ? value | ~0xffffffff : value & 0xffffffff;
	}
#endif /* ULONG_MAX == 0xffffffff */




/* ======================================================================== */
/* ============================ GENERAL DEFINES =========================== */
/* ======================================================================== */

#define NUM_CPU_TYPES 3

/* Exception Vectors handled by emulation */
#define EXCEPTION_BUS_ERROR                2 /* This one is not emulated! */
#define EXCEPTION_ADDRESS_ERROR            3 /* This one is partially emulated (doesn't stack a proper frame yet) */
#define EXCEPTION_ILLEGAL_INSTR     	 	 4
#define EXCEPTION_ZERO_DIVIDE              5
#define EXCEPTION_CHK                      6
#define EXCEPTION_TRAPV                    7
#define EXCEPTION_PRIVILEGE_VIOLATION      8
#define EXCEPTION_TRACE                    9
#define EXCEPTION_1010                    10
#define EXCEPTION_1111                    11
#define EXCEPTION_FORMAT_ERROR            14
#define EXCEPTION_UNINITIALIZED_INTERRUPT 15
#define EXCEPTION_SPURIOUS_INTERRUPT      24
#define EXCEPTION_INTERRUPT_AUTOVECTOR    24
#define EXCEPTION_TRAP_BASE               32

/* Function codes set by CPU during data/address bus activity */
#define FUNCTION_CODE_USER_DATA          1
#define FUNCTION_CODE_USER_PROGRAM       2
#define FUNCTION_CODE_SUPERVISOR_DATA    5
#define FUNCTION_CODE_SUPERVISOR_PROGRAM 6
#define FUNCTION_CODE_CPU_SPACE          7

/* CPU types for deciding what to emulate */
#define CPU_TYPE_000   1
#define CPU_TYPE_010   2
#define CPU_TYPE_EC020 4
#define CPU_TYPE_020   8

/* Different ways to stop the CPU */
#define STOP_LEVEL_STOP 1
#define STOP_LEVEL_HALT 2

#ifndef NULL
#define NULL ((void*)0)
#endif

/* ======================================================================== */
/* ================================ MACROS ================================ */
/* ======================================================================== */


/* ---------------------------- General Macros ---------------------------- */

/* Bit Isolation Macros */
#define BIT_0(A)  ((A) & 0x00000001)
#define BIT_1(A)  ((A) & 0x00000002)
#define BIT_2(A)  ((A) & 0x00000004)
#define BIT_3(A)  ((A) & 0x00000008)
#define BIT_4(A)  ((A) & 0x00000010)
#define BIT_5(A)  ((A) & 0x00000020)
#define BIT_6(A)  ((A) & 0x00000040)
#define BIT_7(A)  ((A) & 0x00000080)
#define BIT_8(A)  ((A) & 0x00000100)
#define BIT_9(A)  ((A) & 0x00000200)
#define BIT_A(A)  ((A) & 0x00000400)
#define BIT_B(A)  ((A) & 0x00000800)
#define BIT_C(A)  ((A) & 0x00001000)
#define BIT_D(A)  ((A) & 0x00002000)
#define BIT_E(A)  ((A) & 0x00004000)
#define BIT_F(A)  ((A) & 0x00008000)
#define BIT_10(A) ((A) & 0x00010000)
#define BIT_11(A) ((A) & 0x00020000)
#define BIT_12(A) ((A) & 0x00040000)
#define BIT_13(A) ((A) & 0x00080000)
#define BIT_14(A) ((A) & 0x00100000)
#define BIT_15(A) ((A) & 0x00200000)
#define BIT_16(A) ((A) & 0x00400000)
#define BIT_17(A) ((A) & 0x00800000)
#define BIT_18(A) ((A) & 0x01000000)
#define BIT_19(A) ((A) & 0x02000000)
#define BIT_1A(A) ((A) & 0x04000000)
#define BIT_1B(A) ((A) & 0x08000000)
#define BIT_1C(A) ((A) & 0x10000000)
#define BIT_1D(A) ((A) & 0x20000000)
#define BIT_1E(A) ((A) & 0x40000000)
#define BIT_1F(A) ((A) & 0x80000000)

/* Get the most significant bit for specific sizes */
#define GET_MSB_8(A)  ((A) & 0x80)
#define GET_MSB_9(A)  ((A) & 0x100)
#define GET_MSB_16(A) ((A) & 0x8000)
#define GET_MSB_17(A) ((A) & 0x10000)
#define GET_MSB_32(A) ((A) & 0x80000000)
#if M68K_USE_64_BIT
#define GET_MSB_33(A) ((A) & 0x100000000)
#endif /* M68K_USE_64_BIT */

/* Isolate nibbles */
#define LOW_NIBBLE(A)  ((A) & 0x0f)
#define HIGH_NIBBLE(A) ((A) & 0xf0)

/* These are used to isolate 8, 16, and 32 bit sizes */
#define MASK_OUT_ABOVE_2(A)  ((A) & 3)
#define MASK_OUT_ABOVE_8(A)  ((A) & 0xff)
#define MASK_OUT_ABOVE_16(A) ((A) & 0xffff)
#define MASK_OUT_BELOW_2(A)  ((A) & ~3)
#define MASK_OUT_BELOW_8(A)  ((A) & ~0xff)
#define MASK_OUT_BELOW_16(A) ((A) & ~0xffff)

/* No need to mask if we are 32 bit */
#if M68K_INT_GT_32BIT || M68K_USE_64_BIT
	#define MASK_OUT_ABOVE_32(A) ((A) & 0xffffffff)
	#define MASK_OUT_BELOW_32(A) ((A) & ~0xffffffff)
#else
	#define MASK_OUT_ABOVE_32(A) (A)
	#define MASK_OUT_BELOW_32(A) 0
#endif /* M68K_INT_GT_32BIT || M68K_USE_64_BIT */

/* Simulate address lines of 68k family */
#define ADDRESS_68K(A) ((A)&CPU_ADDRESS_MASK)


/* Shift & Rotate Macros. */
#define LSL(A, C) ((A) << (C))
#define LSR(A, C) ((A) >> (C))

/* Some > 32-bit optimizations */
#if M68K_INT_GT_32BIT
	/* Shift left and right */
	#define LSR_32(A, C) ((A) >> (C))
	#define LSL_32(A, C) ((A) << (C))
#else
	/* We have to do this because the morons at ANSI decided that shifts
	 * by >= data size are undefined.
	 */
	#define LSR_32(A, C) ((C) < 32 ? (A) >> (C) : 0)
	#define LSL_32(A, C) ((C) < 32 ? (A) << (C) : 0)
#endif /* M68K_INT_GT_32BIT */

#if M68K_USE_64_BIT
	#define LSL_32_64(A, C) ((A) << (C))
	#define LSR_32_64(A, C) ((A) >> (C))
	#define ROL_33_64(A, C) (LSL_32_64(A, C) | LSR_32_64(A, 33-(C)))
	#define ROR_33_64(A, C) (LSR_32_64(A, C) | LSL_32_64(A, 33-(C)))
#endif /* M68K_USE_64_BIT */

#define ROL_8(A, C)      MASK_OUT_ABOVE_8(LSL(A, C) | LSR(A, 8-(C)))
#define ROL_9(A, C)                      (LSL(A, C) | LSR(A, 9-(C)))
#define ROL_16(A, C)    MASK_OUT_ABOVE_16(LSL(A, C) | LSR(A, 16-(C)))
#define ROL_17(A, C)                     (LSL(A, C) | LSR(A, 17-(C)))
#define ROL_32(A, C)    MASK_OUT_ABOVE_32(LSL_32(A, C) | LSR_32(A, 32-(C)))
#define ROL_33(A, C)                     (LSL_32(A, C) | LSR_32(A, 33-(C)))

#define ROR_8(A, C)      MASK_OUT_ABOVE_8(LSR(A, C) | LSL(A, 8-(C)))
#define ROR_9(A, C)                      (LSR(A, C) | LSL(A, 9-(C)))
#define ROR_16(A, C)    MASK_OUT_ABOVE_16(LSR(A, C) | LSL(A, 16-(C)))
#define ROR_17(A, C)                     (LSR(A, C) | LSL(A, 17-(C)))
#define ROR_32(A, C)    MASK_OUT_ABOVE_32(LSR_32(A, C) | LSL_32(A, 32-(C)))
#define ROR_33(A, C)                     (LSR_32(A, C) | LSL_32(A, 33-(C)))



/* ------------------------------ CPU Access ------------------------------ */

/* Access the CPU registers */
#define CPU_TYPE         m68ki_cpu.cpu_type

#define REG_DA           m68ki_cpu.dar /* easy access to data and address regs */
#define REG_D            m68ki_cpu.dar
#define REG_A            (m68ki_cpu.dar+8)
#define REG_PPC 		 m68ki_cpu.ppc
#define REG_PC           m68ki_cpu.pc
#define REG_SP_BASE      m68ki_cpu.sp
#define REG_USP          m68ki_cpu.sp[0]
#define REG_ISP          m68ki_cpu.sp[4]
#define REG_MSP          m68ki_cpu.sp[6]
#define REG_SP           m68ki_cpu.dar[15]
#define REG_VBR          m68ki_cpu.vbr
#define REG_SFC          m68ki_cpu.sfc
#define REG_DFC          m68ki_cpu.dfc
#define REG_CACR         m68ki_cpu.cacr
#define REG_CAAR         m68ki_cpu.caar
#define REG_IR           m68ki_cpu.ir

#define FLAG_T1          m68ki_cpu.t1_flag
#define FLAG_T0          m68ki_cpu.t0_flag
#define FLAG_S           m68ki_cpu.s_flag
#define FLAG_M           m68ki_cpu.m_flag
#define FLAG_X           m68ki_cpu.x_flag
#define FLAG_N           m68ki_cpu.n_flag
#define FLAG_Z           m68ki_cpu.not_z_flag
#define FLAG_V           m68ki_cpu.v_flag
#define FLAG_C           m68ki_cpu.c_flag
#define FLAG_INT_MASK    m68ki_cpu.int_mask

#define CPU_INT_LEVEL    m68ki_cpu.int_level /* ASG: changed from CPU_INTS_PENDING */
#define CPU_INT_CYCLES   m68ki_cpu.int_cycles /* ASG */
#define CPU_STOPPED      m68ki_cpu.stopped
#define CPU_PREF_ADDR    m68ki_cpu.pref_addr
#define CPU_PREF_DATA    m68ki_cpu.pref_data
#define CPU_ADDRESS_MASK m68ki_cpu.address_mask
#define CPU_SR_MASK      m68ki_cpu.sr_mask

#define CYC_INSTRUCTION  m68ki_cpu.cyc_instruction
#define CYC_EXCEPTION    m68ki_cpu.cyc_exception
#define CYC_BCC_NOTAKE_B m68ki_cpu.cyc_bcc_notake_b
#define CYC_BCC_NOTAKE_W m68ki_cpu.cyc_bcc_notake_w
#define CYC_DBCC_F_NOEXP m68ki_cpu.cyc_dbcc_f_noexp
#define CYC_DBCC_F_EXP   m68ki_cpu.cyc_dbcc_f_exp
#define CYC_SCC_R_FALSE  m68ki_cpu.cyc_scc_r_false
#define CYC_MOVEM_W      m68ki_cpu.cyc_movem_w
#define CYC_MOVEM_L      m68ki_cpu.cyc_movem_l
#define CYC_SHIFT        m68ki_cpu.cyc_shift
#define CYC_RESET        m68ki_cpu.cyc_reset


#define CALLBACK_INT_ACK     m68ki_cpu.int_ack_callback
#define CALLBACK_BKPT_ACK    m68ki_cpu.bkpt_ack_callback
#define CALLBACK_RESET_INSTR m68ki_cpu.reset_instr_callback
#define CALLBACK_PC_CHANGED  m68ki_cpu.pc_changed_callback
#define CALLBACK_SET_FC      m68ki_cpu.set_fc_callback
#define CALLBACK_INSTR_HOOK  m68ki_cpu.instr_hook_callback



/* ----------------------------- Configuration ---------------------------- */

/* These defines are dependant on the configuration defines in m68kconf.h */

/* Disable certain comparisons if we're not using all CPU types */
#if M68K_EMULATE_020
	#define CPU_TYPE_IS_020_PLUS(A)    ((A) & CPU_TYPE_020)
	#define CPU_TYPE_IS_020_LESS(A)    1
#else
	#define CPU_TYPE_IS_020_PLUS(A)    0
	#define CPU_TYPE_IS_020_LESS(A)    1
#endif

#if M68K_EMULATE_EC020
	#define CPU_TYPE_IS_EC020_PLUS(A)  ((A) & (CPU_TYPE_EC020 | CPU_TYPE_020))
	#define CPU_TYPE_IS_EC020_LESS(A)  ((A) & (CPU_TYPE_000 | CPU_TYPE_010 | CPU_TYPE_EC020))
#else
	#define CPU_TYPE_IS_EC020_PLUS(A)  CPU_TYPE_IS_020_PLUS(A)
	#define CPU_TYPE_IS_EC020_LESS(A)  CPU_TYPE_IS_020_LESS(A)
#endif

#if M68K_EMULATE_010
	#define CPU_TYPE_IS_010(A)         ((A) == CPU_TYPE_010)
	#define CPU_TYPE_IS_010_PLUS(A)    ((A) & (CPU_TYPE_010 | CPU_TYPE_EC020 | CPU_TYPE_020))
	#define CPU_TYPE_IS_010_LESS(A)    ((A) & (CPU_TYPE_000 | CPU_TYPE_010))
#else
	#define CPU_TYPE_IS_010(A)         0
	#define CPU_TYPE_IS_010_PLUS(A)    CPU_TYPE_IS_EC020_PLUS(A)
	#define CPU_TYPE_IS_010_LESS(A)    CPU_TYPE_IS_EC020_LESS(A)
#endif

#if M68K_EMULATE_020 || M68K_EMULATE_EC020
	#define CPU_TYPE_IS_020_VARIANT(A) ((A) & (CPU_TYPE_EC020 | CPU_TYPE_020))
#else
	#define CPU_TYPE_IS_020_VARIANT(A) 0
#endif

#if M68K_EMULATE_020 || M68K_EMULATE_EC020 || M68K_EMULATE_010
	#define CPU_TYPE_IS_000(A)         ((A) == CPU_TYPE_000)
#else
	#define CPU_TYPE_IS_000(A)         1
#endif


#if !M68K_SEPARATE_READS
#define m68k_read_immediate_16(A) m68ki_read_program_16(A)
#define m68k_read_immediate_32(A) m68ki_read_program_32(A)

#define m68k_read_pcrelative_8(A) m68ki_read_program_8(A)
#define m68k_read_pcrelative_16(A) m68ki_read_program_16(A)
#define m68k_read_pcrelative_32(A) m68ki_read_program_32(A)
#endif /* M68K_SEPARATE_READS */


/* Enable or disable callback functions */
#if M68K_EMULATE_INT_ACK
	#if M68K_EMULATE_INT_ACK == OPT_SPECIFY_HANDLER
		#define m68ki_int_ack(A) M68K_INT_ACK_CALLBACK(A)
	#else
		#define m68ki_int_ack(A) CALLBACK_INT_ACK(A)
	#endif
#else
	/* Default action is to used autovector mode, which is most common */
	#define m68ki_int_ack(A) M68K_INT_ACK_AUTOVECTOR
#endif /* M68K_EMULATE_INT_ACK */

#if M68K_EMULATE_BKPT_ACK
	#if M68K_EMULATE_BKPT_ACK == OPT_SPECIFY_HANDLER
		#define m68ki_bkpt_ack(A) M68K_BKPT_ACK_CALLBACK(A)
	#else
		#define m68ki_bkpt_ack(A) CALLBACK_BKPT_ACK(A)
	#endif
#else
	#define m68ki_bkpt_ack(A)
#endif /* M68K_EMULATE_BKPT_ACK */

#if M68K_EMULATE_RESET
	#if M68K_EMULATE_RESET == OPT_SPECIFY_HANDLER
		#define m68ki_output_reset() M68K_RESET_CALLBACK()
	#else
		#define m68ki_output_reset() CALLBACK_RESET_INSTR()
	#endif
#else
	#define m68ki_output_reset()
#endif /* M68K_EMULATE_RESET */

#if M68K_INSTRUCTION_HOOK
	#if M68K_INSTRUCTION_HOOK == OPT_SPECIFY_HANDLER
		#define m68ki_instr_hook(A) M68K_INSTRUCTION_CALLBACK(A)
	#else
		#define m68ki_instr_hook(A) CALLBACK_INSTR_HOOK(A)
	#endif
#else
	#define m68ki_instr_hook(A)
#endif /* M68K_INSTRUCTION_HOOK */

#if M68K_MONITOR_PC
	#if M68K_MONITOR_PC == OPT_SPECIFY_HANDLER
		#define m68ki_pc_changed(A) M68K_SET_PC_CALLBACK(ADDRESS_68K(A))
	#else
		#define m68ki_pc_changed(A) CALLBACK_PC_CHANGED(ADDRESS_68K(A))
	#endif
#else
	#define m68ki_pc_changed(A)
#endif /* M68K_MONITOR_PC */


/* Enable or disable function code emulation */
#if M68K_EMULATE_FC
	#if M68K_EMULATE_FC == OPT_SPECIFY_HANDLER
		#define m68ki_set_fc(A) M68K_SET_FC_CALLBACK(A)
	#else
		#define m68ki_set_fc(A) CALLBACK_SET_FC(A)
	#endif
	#define m68ki_use_data_space() m68ki_address_space = FUNCTION_CODE_USER_DATA
	#define m68ki_use_program_space() m68ki_address_space = FUNCTION_CODE_USER_PROGRAM
	#define m68ki_get_address_space() m68ki_address_space
#else
	#define m68ki_set_fc(A)
	#define m68ki_use_data_space()
	#define m68ki_use_program_space()
	#define m68ki_get_address_space() FUNCTION_CODE_USER_DATA
#endif /* M68K_EMULATE_FC */


/* Enable or disable trace emulation */
#if M68K_EMULATE_TRACE
	/* Initiates trace checking before each instruction (t1) */
	#define m68ki_trace_t1() m68ki_tracing = FLAG_T1
	/* adds t0 to trace checking if we encounter change of flow */
	#define m68ki_trace_t0() m68ki_tracing |= FLAG_T0
	/* Clear all tracing */
	#define m68ki_clear_trace() m68ki_tracing = 0
	/* Cause a trace exception if we are tracing */
	#define m68ki_exception_if_trace() if(m68ki_tracing) m68ki_exception_trace()
#else
	#define m68ki_trace_t1()
	#define m68ki_trace_t0()
	#define m68ki_clear_trace()
	#define m68ki_exception_if_trace()
#endif /* M68K_EMULATE_TRACE */



/* Address error */
#if M68K_EMULATE_ADDRESS_ERROR
	extern jmp_buf m68ki_address_error_trap;
	#define m68ki_set_address_error_trap() if(setjmp(m68ki_address_error_trap)) m68ki_exception_address_error();
	#define m68ki_check_address_error(A) if((A)&1) longjmp(m68ki_address_error_jump, 1);
#else
	#define m68ki_set_address_error_trap()
	#define m68ki_check_address_error(A)
#endif /* M68K_ADDRESS_ERROR */

/* Logging */
#if M68K_LOG_ENABLE
	#include <stdio.h>
	extern FILE* M68K_LOG_FILEHANDLE
	extern char* m68ki_cpu_names[];

	#define M68K_DO_LOG(A) if(M68K_LOG_FILEHANDLE) fprintf A
	#if M68K_LOG_1010_1111
		#define M68K_DO_LOG_EMU(A) if(M68K_LOG_FILEHANDLE) fprintf A
	#else
		#define M68K_DO_LOG_EMU(A)
	#endif
#else
	#define M68K_DO_LOG(A)
	#define M68K_DO_LOG_EMU(A)
#endif



/* -------------------------- EA / Operand Access ------------------------- */

/*
 * The general instruction format follows this pattern:
 * .... XXX. .... .YYY
 * where XXX is register X and YYY is register Y
 */
/* Data Register Isolation */
#define DX (REG_D[(REG_IR >> 9) & 7])
#define DY (REG_D[REG_IR & 7])
/* Address Register Isolation */
#define AX (REG_A[(REG_IR >> 9) & 7])
#define AY (REG_A[REG_IR & 7])


/* Effective Address Calculations */
#define EA_AY_AI_8()   AY                                    /* address register indirect */
#define EA_AY_AI_16()  EA_AY_AI_8()
#define EA_AY_AI_32()  EA_AY_AI_8()
#define EA_AY_PI_8()   (AY++)                                /* postincrement (size = byte) */
#define EA_AY_PI_16()  ((AY+=2)-2)                           /* postincrement (size = word) */
#define EA_AY_PI_32()  ((AY+=4)-4)                           /* postincrement (size = long) */
#define EA_AY_PD_8()   (--AY)                                /* predecrement (size = byte) */
#define EA_AY_PD_16()  (AY-=2)                               /* predecrement (size = word) */
#define EA_AY_PD_32()  (AY-=4)                               /* predecrement (size = long) */
#define EA_AY_DI_8()   (AY+MAKE_INT_16(m68ki_read_imm_16())) /* displacement */
#define EA_AY_DI_16()  EA_AY_DI_8()
#define EA_AY_DI_32()  EA_AY_DI_8()
#define EA_AY_IX_8()   m68ki_get_ea_ix(AY)                   /* indirect + index */
#define EA_AY_IX_16()  EA_AY_IX_8()
#define EA_AY_IX_32()  EA_AY_IX_8()

#define EA_AX_AI_8()   AX
#define EA_AX_AI_16()  EA_AX_AI_8()
#define EA_AX_AI_32()  EA_AX_AI_8()
#define EA_AX_PI_8()   (AX++)
#define EA_AX_PI_16()  ((AX+=2)-2)
#define EA_AX_PI_32()  ((AX+=4)-4)
#define EA_AX_PD_8()   (--AX)
#define EA_AX_PD_16()  (AX-=2)
#define EA_AX_PD_32()  (AX-=4)
#define EA_AX_DI_8()   (AX+MAKE_INT_16(m68ki_read_imm_16()))
#define EA_AX_DI_16()  EA_AX_DI_8()
#define EA_AX_DI_32()  EA_AX_DI_8()
#define EA_AX_IX_8()   m68ki_get_ea_ix(AX)
#define EA_AX_IX_16()  EA_AX_IX_8()
#define EA_AX_IX_32()  EA_AX_IX_8()

#define EA_A7_PI_8()   ((REG_A[7]+=2)-2)
#define EA_A7_PD_8()   (REG_A[7]-=2)

#define EA_AW_8()      MAKE_INT_16(m68ki_read_imm_16())      /* absolute word */
#define EA_AW_16()     EA_AW_8()
#define EA_AW_32()     EA_AW_8()
#define EA_AL_8()      m68ki_read_imm_32()                   /* absolute long */
#define EA_AL_16()     EA_AL_8()
#define EA_AL_32()     EA_AL_8()
#define EA_PCDI_8()    m68ki_get_ea_pcdi()                   /* pc indirect + displacement */
#define EA_PCDI_16()   EA_PCDI_8()
#define EA_PCDI_32()   EA_PCDI_8()
#define EA_PCIX_8()    m68ki_get_ea_pcix()                   /* pc indirect + index */
#define EA_PCIX_16()   EA_PCIX_8()
#define EA_PCIX_32()   EA_PCIX_8()


#define OPER_I_8()     m68ki_read_imm_8()
#define OPER_I_16()    m68ki_read_imm_16()
#define OPER_I_32()    m68ki_read_imm_32()



/* --------------------------- Status Register ---------------------------- */

/* Flag Calculation Macros */
#define CFLAG_8(A) (A)
#define CFLAG_16(A) ((A)>>8)

#if M68K_INT_GT_32_BIT
	#define CFLAG_ADD_32(S, D, R) ((R)>>24)
	#define CFLAG_SUB_32(S, D, R) ((R)>>24)
#else
	#define CFLAG_ADD_32(S, D, R) (((S & D) | (~R & (S | D)))>>23)
	#define CFLAG_SUB_32(S, D, R) (((S & R) | (~D & (S | R)))>>23)
#endif /* M68K_INT_GT_32_BIT */

#define VFLAG_ADD_8(S, D, R) ((S^R) & (D^R))
#define VFLAG_ADD_16(S, D, R) (((S^R) & (D^R))>>8)
#define VFLAG_ADD_32(S, D, R) (((S^R) & (D^R))>>24)

#define VFLAG_SUB_8(S, D, R) ((S^D) & (R^D))
#define VFLAG_SUB_16(S, D, R) (((S^D) & (R^D))>>8)
#define VFLAG_SUB_32(S, D, R) (((S^D) & (R^D))>>24)

#define NFLAG_8(A) (A)
#define NFLAG_16(A) ((A)>>8)
#define NFLAG_32(A) ((A)>>24)
#define NFLAG_64(A) ((A)>>56)

#define ZFLAG_8(A) MASK_OUT_ABOVE_8(A)
#define ZFLAG_16(A) MASK_OUT_ABOVE_16(A)
#define ZFLAG_32(A) MASK_OUT_ABOVE_32(A)


/* Flag values */
#define NFLAG_SET   0x80
#define NFLAG_CLEAR 0
#define CFLAG_SET   0x100
#define CFLAG_CLEAR 0
#define XFLAG_SET   0x100
#define XFLAG_CLEAR 0
#define VFLAG_SET   0x80
#define VFLAG_CLEAR 0
#define ZFLAG_SET   0
#define ZFLAG_CLEAR 0xffffffff

#define SFLAG_SET   4
#define SFLAG_CLEAR 0
#define MFLAG_SET   2
#define MFLAG_CLEAR 0

/* Turn flag values into 1 or 0 */
#define XFLAG_AS_1() ((FLAG_X>>8)&1)
#define NFLAG_AS_1() ((FLAG_N>>7)&1)
#define VFLAG_AS_1() ((FLAG_V>>7)&1)
#define ZFLAG_AS_1() (!FLAG_Z)
#define CFLAG_AS_1() ((FLAG_C>>8)&1)


/* Conditions */
#define COND_CS() (FLAG_C&0x100)
#define COND_CC() (!COND_CS())
#define COND_VS() (FLAG_V&0x80)
#define COND_VC() (!COND_VS())
#define COND_NE() FLAG_Z
#define COND_EQ() (!COND_NE())
#define COND_MI() (FLAG_N&0x80)
#define COND_PL() (!COND_MI())
#define COND_LT() ((FLAG_N^FLAG_V)&0x80)
#define COND_GE() (!COND_LT())
#define COND_HI() (COND_CC() && COND_NE())
#define COND_LS() (COND_CS() || COND_EQ())
#define COND_GT() (COND_GE() && COND_NE())
#define COND_LE() (COND_LT() || COND_EQ())

/* Reversed conditions */
#define COND_NOT_CS() COND_CC()
#define COND_NOT_CC() COND_CS()
#define COND_NOT_VS() COND_VC()
#define COND_NOT_VC() COND_VS()
#define COND_NOT_NE() COND_EQ()
#define COND_NOT_EQ() COND_NE()
#define COND_NOT_MI() COND_PL()
#define COND_NOT_PL() COND_MI()
#define COND_NOT_LT() COND_GE()
#define COND_NOT_GE() COND_LT()
#define COND_NOT_HI() COND_LS()
#define COND_NOT_LS() COND_HI()
#define COND_NOT_GT() COND_LE()
#define COND_NOT_LE() COND_GT()

/* Not real conditions, but here for convenience */
#define COND_XS() (FLAG_X&0x100)
#define COND_XC() (!COND_XS)


/* Get the condition code register */
#define m68ki_get_ccr() ((COND_XS() >> 4) | \
						 (COND_MI() >> 4) | \
						 (COND_EQ() << 2) | \
						 (COND_VS() >> 6) | \
						 (COND_CS() >> 8))

/* Get the status register */
#define m68ki_get_sr() ( FLAG_T1              | \
						 FLAG_T0              | \
						(FLAG_S        << 11) | \
						(FLAG_M        << 11) | \
						 FLAG_INT_MASK        | \
						 m68ki_get_ccr())



/* ---------------------------- Cycle Counting ---------------------------- */

#define ADD_CYCLES(A)    m68ki_remaining_cycles += (A)
#define USE_CYCLES(A)    m68ki_remaining_cycles -= (A)
#define SET_CYCLES(A)    m68ki_remaining_cycles = A
#define GET_CYCLES()     m68ki_remaining_cycles
#define USE_ALL_CYCLES() m68ki_remaining_cycles = 0



/* ----------------------------- Read / Write ----------------------------- */

/* Read from the current address space */
#define m68ki_read_8(A)  m68ki_read_8_fc (A, FLAG_S | m68ki_get_address_space())
#define m68ki_read_16(A) m68ki_read_16_fc(A, FLAG_S | m68ki_get_address_space())
#define m68ki_read_32(A) m68ki_read_32_fc(A, FLAG_S | m68ki_get_address_space())

/* Write to the current data space */
#define m68ki_write_8(A, V)  m68ki_write_8_fc (A, FLAG_S | FUNCTION_CODE_USER_DATA, V)
#define m68ki_write_16(A, V) m68ki_write_16_fc(A, FLAG_S | FUNCTION_CODE_USER_DATA, V)
#define m68ki_write_32(A, V) m68ki_write_32_fc(A, FLAG_S | FUNCTION_CODE_USER_DATA, V)

/* map read immediate 8 to read immediate 16 */
#define m68ki_read_imm_8() MASK_OUT_ABOVE_8(m68ki_read_imm_16())

/* Map PC-relative reads */
#define m68ki_read_pcrel_8(A) m68k_read_pcrelative_8(A)
#define m68ki_read_pcrel_16(A) m68k_read_pcrelative_16(A)
#define m68ki_read_pcrel_32(A) m68k_read_pcrelative_32(A)

/* Read from the program space */
#define m68ki_read_program_8(A) 	m68ki_read_8_fc(A, FLAG_S | FUNCTION_CODE_USER_PROGRAM)
#define m68ki_read_program_16(A) 	m68ki_read_16_fc(A, FLAG_S | FUNCTION_CODE_USER_PROGRAM)
#define m68ki_read_program_32(A) 	m68ki_read_32_fc(A, FLAG_S | FUNCTION_CODE_USER_PROGRAM)

/* Read from the data space */
#define m68ki_read_data_8(A) 	m68ki_read_8_fc(A, FLAG_S | FUNCTION_CODE_USER_DATA)
#define m68ki_read_data_16(A) 	m68ki_read_16_fc(A, FLAG_S | FUNCTION_CODE_USER_DATA)
#define m68ki_read_data_32(A) 	m68ki_read_32_fc(A, FLAG_S | FUNCTION_CODE_USER_DATA)


class mc68000
{
	private:
		/* Read data immediately after the program counter */
		uint m68ki_read_imm_16(void);
		uint m68ki_read_imm_32(void);
		
		/* Read data with specific function code */
		uint m68ki_read_8_fc  (uint address, uint fc);
		uint m68ki_read_16_fc (uint address, uint fc);
		uint m68ki_read_32_fc (uint address, uint fc);
		
		/* Write data with specific function code */
		void m68ki_write_8_fc (uint address, uint fc, uint value);
		void m68ki_write_16_fc(uint address, uint fc, uint value);
		void m68ki_write_32_fc(uint address, uint fc, uint value);
		
		/* Indexed and PC-relative ea fetching */
		uint m68ki_get_ea_pcdi(void);
		uint m68ki_get_ea_pcix(void);
		uint m68ki_get_ea_ix(uint An);
		
		/* Operand fetching */
		uint OPER_AY_AI_8(void);
		uint OPER_AY_AI_16(void);
		uint OPER_AY_AI_32(void);
		uint OPER_AY_PI_8(void);
		uint OPER_AY_PI_16(void);
		uint OPER_AY_PI_32(void);
		uint OPER_AY_PD_8(void);
		uint OPER_AY_PD_16(void);
		uint OPER_AY_PD_32(void);
		uint OPER_AY_DI_8(void);
		uint OPER_AY_DI_16(void);
		uint OPER_AY_DI_32(void);
		uint OPER_AY_IX_8(void);
		uint OPER_AY_IX_16(void);
		uint OPER_AY_IX_32(void);
		
		uint OPER_AX_AI_8(void);
		uint OPER_AX_AI_16(void);
		uint OPER_AX_AI_32(void);
		uint OPER_AX_PI_8(void);
		uint OPER_AX_PI_16(void);
		uint OPER_AX_PI_32(void);
		uint OPER_AX_PD_8(void);
		uint OPER_AX_PD_16(void);
		uint OPER_AX_PD_32(void);
		uint OPER_AX_DI_8(void);
		uint OPER_AX_DI_16(void);
		uint OPER_AX_DI_32(void);
		uint OPER_AX_IX_8(void);
		uint OPER_AX_IX_16(void);
		uint OPER_AX_IX_32(void);
		
		uint OPER_A7_PI_8(void);
		uint OPER_A7_PD_8(void);
		
		uint OPER_AW_8(void);
		uint OPER_AW_16(void);
		uint OPER_AW_32(void);
		uint OPER_AL_8(void);
		uint OPER_AL_16(void);
		uint OPER_AL_32(void);
		uint OPER_PCDI_8(void);
		uint OPER_PCDI_16(void);
		uint OPER_PCDI_32(void);
		uint OPER_PCIX_8(void);
		uint OPER_PCIX_16(void);
		uint OPER_PCIX_32(void);
		
		/* Stack operations */
		void m68ki_push_16(uint value);
		void m68ki_push_32(uint value);
		uint m68ki_pull_16(void);
		uint m68ki_pull_32(void);
		
		/* Program flow operations */
		void m68ki_jump(uint new_pc);
		void m68ki_jump_vector(uint vector);
		void m68ki_branch_8(uint offset);
		void m68ki_branch_16(uint offset);
		void m68ki_branch_32(uint offset);
		
		/* Status register operations. */
		void m68ki_set_s_flag(uint value);            /* Only bit 2 of value should be set (i.e. 4 or 0) */
		void m68ki_set_sm_flag(uint value);           /* only bits 1 and 2 of value should be set */
		void m68ki_set_ccr(uint value);               /* set the condition code register */
		void m68ki_set_sr(uint value);                /* set the status register */
		void m68ki_set_sr_noint(uint value);          /* set the status register */
		
		/* Exception processing */
		uint m68ki_init_exception(void);              /* Initial exception processing */
		
		void m68ki_stack_frame_3word(uint pc, uint sr); /* Stack various frame types */
		void m68ki_stack_frame_buserr(uint pc, uint sr, uint address, uint write, uint instruction, uint fc);
		
		void m68ki_stack_frame_0000(uint pc, uint sr, uint vector);
		void m68ki_stack_frame_0001(uint pc, uint sr, uint vector);
		void m68ki_stack_frame_0010(uint sr, uint vector);
		void m68ki_stack_frame_1000(uint pc, uint sr, uint vector);
		void m68ki_stack_frame_1010(uint sr, uint vector, uint pc);
		void m68ki_stack_frame_1011(uint sr, uint vector, uint pc);
		
		void m68ki_exception_trap(uint vector);
		void m68ki_exception_trapN(uint vector);
		void m68ki_exception_trace(void);
		void m68ki_exception_privilege_violation(void);
		void m68ki_exception_1010(void);
		void m68ki_exception_1111(void);
		void m68ki_exception_illegal(void);
		void m68ki_exception_format_error(void);
		void m68ki_exception_address_error(void);
		void m68ki_exception_interrupt(uint int_level);
		void m68ki_check_interrupts(void);            /* ASG: check for interrupts */

		void m68ki_fake_push_16(void);
		void m68ki_fake_push_32(void);
		void m68ki_fake_pull_16(void);
		void m68ki_fake_pull_32(void);

		void build_opcode_table(void);
		void m68ki_build_opcode_table(void);
		char* m68ki_disassemble_quick(unsigned int pc, unsigned int cpu_type);
		unsigned int m68k_is_valid_instruction(unsigned int instruction, unsigned int cpu_type);

		// Opcodes from file: m68kopac
		void m68k_op_1010(void);
		void m68k_op_1111(void);
		void m68k_op_abcd_8_rr(void);
		void m68k_op_abcd_8_mm_ax7(void);
		void m68k_op_abcd_8_mm_ay7(void);
		void m68k_op_abcd_8_mm_axy7(void);
		void m68k_op_abcd_8_mm(void);
		void m68k_op_add_8_er_d(void);
		void m68k_op_add_8_er_ai(void);
		void m68k_op_add_8_er_pi(void);
		void m68k_op_add_8_er_pi7(void);
		void m68k_op_add_8_er_pd(void);
		void m68k_op_add_8_er_pd7(void);
		void m68k_op_add_8_er_di(void);
		void m68k_op_add_8_er_ix(void);
		void m68k_op_add_8_er_aw(void);
		void m68k_op_add_8_er_al(void);
		void m68k_op_add_8_er_pcdi(void);
		void m68k_op_add_8_er_pcix(void);
		void m68k_op_add_8_er_i(void);
		void m68k_op_add_16_er_d(void);
		void m68k_op_add_16_er_a(void);
		void m68k_op_add_16_er_ai(void);
		void m68k_op_add_16_er_pi(void);
		void m68k_op_add_16_er_pd(void);
		void m68k_op_add_16_er_di(void);
		void m68k_op_add_16_er_ix(void);
		void m68k_op_add_16_er_aw(void);
		void m68k_op_add_16_er_al(void);
		void m68k_op_add_16_er_pcdi(void);
		void m68k_op_add_16_er_pcix(void);
		void m68k_op_add_16_er_i(void);
		void m68k_op_add_32_er_d(void);
		void m68k_op_add_32_er_a(void);
		void m68k_op_add_32_er_ai(void);
		void m68k_op_add_32_er_pi(void);
		void m68k_op_add_32_er_pd(void);
		void m68k_op_add_32_er_di(void);
		void m68k_op_add_32_er_ix(void);
		void m68k_op_add_32_er_aw(void);
		void m68k_op_add_32_er_al(void);
		void m68k_op_add_32_er_pcdi(void);
		void m68k_op_add_32_er_pcix(void);
		void m68k_op_add_32_er_i(void);
		void m68k_op_add_8_re_ai(void);
		void m68k_op_add_8_re_pi(void);
		void m68k_op_add_8_re_pi7(void);
		void m68k_op_add_8_re_pd(void);
		void m68k_op_add_8_re_pd7(void);
		void m68k_op_add_8_re_di(void);
		void m68k_op_add_8_re_ix(void);
		void m68k_op_add_8_re_aw(void);
		void m68k_op_add_8_re_al(void);
		void m68k_op_add_16_re_ai(void);
		void m68k_op_add_16_re_pi(void);
		void m68k_op_add_16_re_pd(void);
		void m68k_op_add_16_re_di(void);
		void m68k_op_add_16_re_ix(void);
		void m68k_op_add_16_re_aw(void);
		void m68k_op_add_16_re_al(void);
		void m68k_op_add_32_re_ai(void);
		void m68k_op_add_32_re_pi(void);
		void m68k_op_add_32_re_pd(void);
		void m68k_op_add_32_re_di(void);
		void m68k_op_add_32_re_ix(void);
		void m68k_op_add_32_re_aw(void);
		void m68k_op_add_32_re_al(void);
		void m68k_op_adda_16_d(void);
		void m68k_op_adda_16_a(void);
		void m68k_op_adda_16_ai(void);
		void m68k_op_adda_16_pi(void);
		void m68k_op_adda_16_pd(void);
		void m68k_op_adda_16_di(void);
		void m68k_op_adda_16_ix(void);
		void m68k_op_adda_16_aw(void);
		void m68k_op_adda_16_al(void);
		void m68k_op_adda_16_pcdi(void);
		void m68k_op_adda_16_pcix(void);
		void m68k_op_adda_16_i(void);
		void m68k_op_adda_32_d(void);
		void m68k_op_adda_32_a(void);
		void m68k_op_adda_32_ai(void);
		void m68k_op_adda_32_pi(void);
		void m68k_op_adda_32_pd(void);
		void m68k_op_adda_32_di(void);
		void m68k_op_adda_32_ix(void);
		void m68k_op_adda_32_aw(void);
		void m68k_op_adda_32_al(void);
		void m68k_op_adda_32_pcdi(void);
		void m68k_op_adda_32_pcix(void);
		void m68k_op_adda_32_i(void);
		void m68k_op_addi_8_d(void);
		void m68k_op_addi_8_ai(void);
		void m68k_op_addi_8_pi(void);
		void m68k_op_addi_8_pi7(void);
		void m68k_op_addi_8_pd(void);
		void m68k_op_addi_8_pd7(void);
		void m68k_op_addi_8_di(void);
		void m68k_op_addi_8_ix(void);
		void m68k_op_addi_8_aw(void);
		void m68k_op_addi_8_al(void);
		void m68k_op_addi_16_d(void);
		void m68k_op_addi_16_ai(void);
		void m68k_op_addi_16_pi(void);
		void m68k_op_addi_16_pd(void);
		void m68k_op_addi_16_di(void);
		void m68k_op_addi_16_ix(void);
		void m68k_op_addi_16_aw(void);
		void m68k_op_addi_16_al(void);
		void m68k_op_addi_32_d(void);
		void m68k_op_addi_32_ai(void);
		void m68k_op_addi_32_pi(void);
		void m68k_op_addi_32_pd(void);
		void m68k_op_addi_32_di(void);
		void m68k_op_addi_32_ix(void);
		void m68k_op_addi_32_aw(void);
		void m68k_op_addi_32_al(void);
		void m68k_op_addq_8_d(void);
		void m68k_op_addq_8_ai(void);
		void m68k_op_addq_8_pi(void);
		void m68k_op_addq_8_pi7(void);
		void m68k_op_addq_8_pd(void);
		void m68k_op_addq_8_pd7(void);
		void m68k_op_addq_8_di(void);
		void m68k_op_addq_8_ix(void);
		void m68k_op_addq_8_aw(void);
		void m68k_op_addq_8_al(void);
		void m68k_op_addq_16_d(void);
		void m68k_op_addq_16_a(void);
		void m68k_op_addq_16_ai(void);
		void m68k_op_addq_16_pi(void);
		void m68k_op_addq_16_pd(void);
		void m68k_op_addq_16_di(void);
		void m68k_op_addq_16_ix(void);
		void m68k_op_addq_16_aw(void);
		void m68k_op_addq_16_al(void);
		void m68k_op_addq_32_d(void);
		void m68k_op_addq_32_a(void);
		void m68k_op_addq_32_ai(void);
		void m68k_op_addq_32_pi(void);
		void m68k_op_addq_32_pd(void);
		void m68k_op_addq_32_di(void);
		void m68k_op_addq_32_ix(void);
		void m68k_op_addq_32_aw(void);
		void m68k_op_addq_32_al(void);
		void m68k_op_addx_8_rr(void);
		void m68k_op_addx_16_rr(void);
		void m68k_op_addx_32_rr(void);
		void m68k_op_addx_8_mm_ax7(void);
		void m68k_op_addx_8_mm_ay7(void);
		void m68k_op_addx_8_mm_axy7(void);
		void m68k_op_addx_8_mm(void);
		void m68k_op_addx_16_mm(void);
		void m68k_op_addx_32_mm(void);
		void m68k_op_and_8_er_d(void);
		void m68k_op_and_8_er_ai(void);
		void m68k_op_and_8_er_pi(void);
		void m68k_op_and_8_er_pi7(void);
		void m68k_op_and_8_er_pd(void);
		void m68k_op_and_8_er_pd7(void);
		void m68k_op_and_8_er_di(void);
		void m68k_op_and_8_er_ix(void);
		void m68k_op_and_8_er_aw(void);
		void m68k_op_and_8_er_al(void);
		void m68k_op_and_8_er_pcdi(void);
		void m68k_op_and_8_er_pcix(void);
		void m68k_op_and_8_er_i(void);
		void m68k_op_and_16_er_d(void);
		void m68k_op_and_16_er_ai(void);
		void m68k_op_and_16_er_pi(void);
		void m68k_op_and_16_er_pd(void);
		void m68k_op_and_16_er_di(void);
		void m68k_op_and_16_er_ix(void);
		void m68k_op_and_16_er_aw(void);
		void m68k_op_and_16_er_al(void);
		void m68k_op_and_16_er_pcdi(void);
		void m68k_op_and_16_er_pcix(void);
		void m68k_op_and_16_er_i(void);
		void m68k_op_and_32_er_d(void);
		void m68k_op_and_32_er_ai(void);
		void m68k_op_and_32_er_pi(void);
		void m68k_op_and_32_er_pd(void);
		void m68k_op_and_32_er_di(void);
		void m68k_op_and_32_er_ix(void);
		void m68k_op_and_32_er_aw(void);
		void m68k_op_and_32_er_al(void);
		void m68k_op_and_32_er_pcdi(void);
		void m68k_op_and_32_er_pcix(void);
		void m68k_op_and_32_er_i(void);
		void m68k_op_and_8_re_ai(void);
		void m68k_op_and_8_re_pi(void);
		void m68k_op_and_8_re_pi7(void);
		void m68k_op_and_8_re_pd(void);
		void m68k_op_and_8_re_pd7(void);
		void m68k_op_and_8_re_di(void);
		void m68k_op_and_8_re_ix(void);
		void m68k_op_and_8_re_aw(void);
		void m68k_op_and_8_re_al(void);
		void m68k_op_and_16_re_ai(void);
		void m68k_op_and_16_re_pi(void);
		void m68k_op_and_16_re_pd(void);
		void m68k_op_and_16_re_di(void);
		void m68k_op_and_16_re_ix(void);
		void m68k_op_and_16_re_aw(void);
		void m68k_op_and_16_re_al(void);
		void m68k_op_and_32_re_ai(void);
		void m68k_op_and_32_re_pi(void);
		void m68k_op_and_32_re_pd(void);
		void m68k_op_and_32_re_di(void);
		void m68k_op_and_32_re_ix(void);
		void m68k_op_and_32_re_aw(void);
		void m68k_op_and_32_re_al(void);
		void m68k_op_andi_8_d(void);
		void m68k_op_andi_8_ai(void);
		void m68k_op_andi_8_pi(void);
		void m68k_op_andi_8_pi7(void);
		void m68k_op_andi_8_pd(void);
		void m68k_op_andi_8_pd7(void);
		void m68k_op_andi_8_di(void);
		void m68k_op_andi_8_ix(void);
		void m68k_op_andi_8_aw(void);
		void m68k_op_andi_8_al(void);
		void m68k_op_andi_16_d(void);
		void m68k_op_andi_16_ai(void);
		void m68k_op_andi_16_pi(void);
		void m68k_op_andi_16_pd(void);
		void m68k_op_andi_16_di(void);
		void m68k_op_andi_16_ix(void);
		void m68k_op_andi_16_aw(void);
		void m68k_op_andi_16_al(void);
		void m68k_op_andi_32_d(void);
		void m68k_op_andi_32_ai(void);
		void m68k_op_andi_32_pi(void);
		void m68k_op_andi_32_pd(void);
		void m68k_op_andi_32_di(void);
		void m68k_op_andi_32_ix(void);
		void m68k_op_andi_32_aw(void);
		void m68k_op_andi_32_al(void);
		void m68k_op_andi_16_toc(void);
		void m68k_op_andi_16_tos(void);
		void m68k_op_asr_8_s(void);
		void m68k_op_asr_16_s(void);
		void m68k_op_asr_32_s(void);
		void m68k_op_asr_8_r(void);
		void m68k_op_asr_16_r(void);
		void m68k_op_asr_32_r(void);
		void m68k_op_asr_16_ai(void);
		void m68k_op_asr_16_pi(void);
		void m68k_op_asr_16_pd(void);
		void m68k_op_asr_16_di(void);
		void m68k_op_asr_16_ix(void);
		void m68k_op_asr_16_aw(void);
		void m68k_op_asr_16_al(void);
		void m68k_op_asl_8_s(void);
		void m68k_op_asl_16_s(void);
		void m68k_op_asl_32_s(void);
		void m68k_op_asl_8_r(void);
		void m68k_op_asl_16_r(void);
		void m68k_op_asl_32_r(void);
		void m68k_op_asl_16_ai(void);
		void m68k_op_asl_16_pi(void);
		void m68k_op_asl_16_pd(void);
		void m68k_op_asl_16_di(void);
		void m68k_op_asl_16_ix(void);
		void m68k_op_asl_16_aw(void);
		void m68k_op_asl_16_al(void);
		void m68k_op_bhi_8(void);
		void m68k_op_bls_8(void);
		void m68k_op_bcc_8(void);
		void m68k_op_bcs_8(void);
		void m68k_op_bne_8(void);
		void m68k_op_beq_8(void);
		void m68k_op_bvc_8(void);
		void m68k_op_bvs_8(void);
		void m68k_op_bpl_8(void);
		void m68k_op_bmi_8(void);
		void m68k_op_bge_8(void);
		void m68k_op_blt_8(void);
		void m68k_op_bgt_8(void);
		void m68k_op_ble_8(void);
		void m68k_op_bhi_16(void);
		void m68k_op_bls_16(void);
		void m68k_op_bcc_16(void);
		void m68k_op_bcs_16(void);
		void m68k_op_bne_16(void);
		void m68k_op_beq_16(void);
		void m68k_op_bvc_16(void);
		void m68k_op_bvs_16(void);
		void m68k_op_bpl_16(void);
		void m68k_op_bmi_16(void);
		void m68k_op_bge_16(void);
		void m68k_op_blt_16(void);
		void m68k_op_bgt_16(void);
		void m68k_op_ble_16(void);
		void m68k_op_bhi_32(void);
		void m68k_op_bls_32(void);
		void m68k_op_bcc_32(void);
		void m68k_op_bcs_32(void);
		void m68k_op_bne_32(void);
		void m68k_op_beq_32(void);
		void m68k_op_bvc_32(void);
		void m68k_op_bvs_32(void);
		void m68k_op_bpl_32(void);
		void m68k_op_bmi_32(void);
		void m68k_op_bge_32(void);
		void m68k_op_blt_32(void);
		void m68k_op_bgt_32(void);
		void m68k_op_ble_32(void);
		void m68k_op_bchg_32_r_d(void);
		void m68k_op_bchg_8_r_ai(void);
		void m68k_op_bchg_8_r_pi(void);
		void m68k_op_bchg_8_r_pi7(void);
		void m68k_op_bchg_8_r_pd(void);
		void m68k_op_bchg_8_r_pd7(void);
		void m68k_op_bchg_8_r_di(void);
		void m68k_op_bchg_8_r_ix(void);
		void m68k_op_bchg_8_r_aw(void);
		void m68k_op_bchg_8_r_al(void);
		void m68k_op_bchg_32_s_d(void);
		void m68k_op_bchg_8_s_ai(void);
		void m68k_op_bchg_8_s_pi(void);
		void m68k_op_bchg_8_s_pi7(void);
		void m68k_op_bchg_8_s_pd(void);
		void m68k_op_bchg_8_s_pd7(void);
		void m68k_op_bchg_8_s_di(void);
		void m68k_op_bchg_8_s_ix(void);
		void m68k_op_bchg_8_s_aw(void);
		void m68k_op_bchg_8_s_al(void);
		void m68k_op_bclr_32_r_d(void);
		void m68k_op_bclr_8_r_ai(void);
		void m68k_op_bclr_8_r_pi(void);
		void m68k_op_bclr_8_r_pi7(void);
		void m68k_op_bclr_8_r_pd(void);
		void m68k_op_bclr_8_r_pd7(void);
		void m68k_op_bclr_8_r_di(void);
		void m68k_op_bclr_8_r_ix(void);
		void m68k_op_bclr_8_r_aw(void);
		void m68k_op_bclr_8_r_al(void);
		void m68k_op_bclr_32_s_d(void);
		void m68k_op_bclr_8_s_ai(void);
		void m68k_op_bclr_8_s_pi(void);
		void m68k_op_bclr_8_s_pi7(void);
		void m68k_op_bclr_8_s_pd(void);
		void m68k_op_bclr_8_s_pd7(void);
		void m68k_op_bclr_8_s_di(void);
		void m68k_op_bclr_8_s_ix(void);
		void m68k_op_bclr_8_s_aw(void);
		void m68k_op_bclr_8_s_al(void);
		void m68k_op_bfchg_32_d(void);
		void m68k_op_bfchg_32_ai(void);
		void m68k_op_bfchg_32_di(void);
		void m68k_op_bfchg_32_ix(void);
		void m68k_op_bfchg_32_aw(void);
		void m68k_op_bfchg_32_al(void);
		void m68k_op_bfclr_32_d(void);
		void m68k_op_bfclr_32_ai(void);
		void m68k_op_bfclr_32_di(void);
		void m68k_op_bfclr_32_ix(void);
		void m68k_op_bfclr_32_aw(void);
		void m68k_op_bfclr_32_al(void);
		void m68k_op_bfexts_32_d(void);
		void m68k_op_bfexts_32_ai(void);
		void m68k_op_bfexts_32_di(void);
		void m68k_op_bfexts_32_ix(void);
		void m68k_op_bfexts_32_aw(void);
		void m68k_op_bfexts_32_al(void);
		void m68k_op_bfexts_32_pcdi(void);
		void m68k_op_bfexts_32_pcix(void);
		void m68k_op_bfextu_32_d(void);
		void m68k_op_bfextu_32_ai(void);
		void m68k_op_bfextu_32_di(void);
		void m68k_op_bfextu_32_ix(void);
		void m68k_op_bfextu_32_aw(void);
		void m68k_op_bfextu_32_al(void);
		void m68k_op_bfextu_32_pcdi(void);
		void m68k_op_bfextu_32_pcix(void);
		void m68k_op_bfffo_32_d(void);
		void m68k_op_bfffo_32_ai(void);
		void m68k_op_bfffo_32_di(void);
		void m68k_op_bfffo_32_ix(void);
		void m68k_op_bfffo_32_aw(void);
		void m68k_op_bfffo_32_al(void);
		void m68k_op_bfffo_32_pcdi(void);
		void m68k_op_bfffo_32_pcix(void);
		void m68k_op_bfins_32_d(void);
		void m68k_op_bfins_32_ai(void);
		void m68k_op_bfins_32_di(void);
		void m68k_op_bfins_32_ix(void);
		void m68k_op_bfins_32_aw(void);
		void m68k_op_bfins_32_al(void);
		void m68k_op_bfset_32_d(void);
		void m68k_op_bfset_32_ai(void);
		void m68k_op_bfset_32_di(void);
		void m68k_op_bfset_32_ix(void);
		void m68k_op_bfset_32_aw(void);
		void m68k_op_bfset_32_al(void);
		void m68k_op_bftst_32_d(void);
		void m68k_op_bftst_32_ai(void);
		void m68k_op_bftst_32_di(void);
		void m68k_op_bftst_32_ix(void);
		void m68k_op_bftst_32_aw(void);
		void m68k_op_bftst_32_al(void);
		void m68k_op_bftst_32_pcdi(void);
		void m68k_op_bftst_32_pcix(void);
		void m68k_op_bkpt(void);
		void m68k_op_bra_8(void);
		void m68k_op_bra_16(void);
		void m68k_op_bra_32(void);
		void m68k_op_bset_32_r_d(void);
		void m68k_op_bset_8_r_ai(void);
		void m68k_op_bset_8_r_pi(void);
		void m68k_op_bset_8_r_pi7(void);
		void m68k_op_bset_8_r_pd(void);
		void m68k_op_bset_8_r_pd7(void);
		void m68k_op_bset_8_r_di(void);
		void m68k_op_bset_8_r_ix(void);
		void m68k_op_bset_8_r_aw(void);
		void m68k_op_bset_8_r_al(void);
		void m68k_op_bset_32_s_d(void);
		void m68k_op_bset_8_s_ai(void);
		void m68k_op_bset_8_s_pi(void);
		void m68k_op_bset_8_s_pi7(void);
		void m68k_op_bset_8_s_pd(void);
		void m68k_op_bset_8_s_pd7(void);
		void m68k_op_bset_8_s_di(void);
		void m68k_op_bset_8_s_ix(void);
		void m68k_op_bset_8_s_aw(void);
		void m68k_op_bset_8_s_al(void);
		void m68k_op_bsr_8(void);
		void m68k_op_bsr_16(void);
		void m68k_op_bsr_32(void);
		void m68k_op_btst_32_r_d(void);
		void m68k_op_btst_8_r_ai(void);
		void m68k_op_btst_8_r_pi(void);
		void m68k_op_btst_8_r_pi7(void);
		void m68k_op_btst_8_r_pd(void);
		void m68k_op_btst_8_r_pd7(void);
		void m68k_op_btst_8_r_di(void);
		void m68k_op_btst_8_r_ix(void);
		void m68k_op_btst_8_r_aw(void);
		void m68k_op_btst_8_r_al(void);
		void m68k_op_btst_8_r_pcdi(void);
		void m68k_op_btst_8_r_pcix(void);
		void m68k_op_btst_8_r_i(void);
		void m68k_op_btst_32_s_d(void);
		void m68k_op_btst_8_s_ai(void);
		void m68k_op_btst_8_s_pi(void);
		void m68k_op_btst_8_s_pi7(void);
		void m68k_op_btst_8_s_pd(void);
		void m68k_op_btst_8_s_pd7(void);
		void m68k_op_btst_8_s_di(void);
		void m68k_op_btst_8_s_ix(void);
		void m68k_op_btst_8_s_aw(void);
		void m68k_op_btst_8_s_al(void);
		void m68k_op_btst_8_s_pcdi(void);
		void m68k_op_btst_8_s_pcix(void);
		void m68k_op_callm_32_ai(void);
		void m68k_op_callm_32_di(void);
		void m68k_op_callm_32_ix(void);
		void m68k_op_callm_32_aw(void);
		void m68k_op_callm_32_al(void);
		void m68k_op_callm_32_pcdi(void);
		void m68k_op_callm_32_pcix(void);
		void m68k_op_cas_8_ai(void);
		void m68k_op_cas_8_pi(void);
		void m68k_op_cas_8_pi7(void);
		void m68k_op_cas_8_pd(void);
		void m68k_op_cas_8_pd7(void);
		void m68k_op_cas_8_di(void);
		void m68k_op_cas_8_ix(void);
		void m68k_op_cas_8_aw(void);
		void m68k_op_cas_8_al(void);
		void m68k_op_cas_16_ai(void);
		void m68k_op_cas_16_pi(void);
		void m68k_op_cas_16_pd(void);
		void m68k_op_cas_16_di(void);
		void m68k_op_cas_16_ix(void);
		void m68k_op_cas_16_aw(void);
		void m68k_op_cas_16_al(void);
		void m68k_op_cas_32_ai(void);
		void m68k_op_cas_32_pi(void);
		void m68k_op_cas_32_pd(void);
		void m68k_op_cas_32_di(void);
		void m68k_op_cas_32_ix(void);
		void m68k_op_cas_32_aw(void);
		void m68k_op_cas_32_al(void);
		void m68k_op_cas2_16(void);
		void m68k_op_cas2_32(void);
		void m68k_op_chk_16_d(void);
		void m68k_op_chk_16_ai(void);
		void m68k_op_chk_16_pi(void);
		void m68k_op_chk_16_pd(void);
		void m68k_op_chk_16_di(void);
		void m68k_op_chk_16_ix(void);
		void m68k_op_chk_16_aw(void);
		void m68k_op_chk_16_al(void);
		void m68k_op_chk_16_pcdi(void);
		void m68k_op_chk_16_pcix(void);
		void m68k_op_chk_16_i(void);
		void m68k_op_chk_32_d(void);
		void m68k_op_chk_32_ai(void);
		void m68k_op_chk_32_pi(void);
		void m68k_op_chk_32_pd(void);
		void m68k_op_chk_32_di(void);
		void m68k_op_chk_32_ix(void);
		void m68k_op_chk_32_aw(void);
		void m68k_op_chk_32_al(void);
		void m68k_op_chk_32_pcdi(void);
		void m68k_op_chk_32_pcix(void);
		void m68k_op_chk_32_i(void);
		void m68k_op_chk2cmp2_8_ai(void);
		void m68k_op_chk2cmp2_8_di(void);
		void m68k_op_chk2cmp2_8_ix(void);
		void m68k_op_chk2cmp2_8_aw(void);
		void m68k_op_chk2cmp2_8_al(void);
		void m68k_op_chk2cmp2_8_pcdi(void);
		void m68k_op_chk2cmp2_8_pcix(void);
		void m68k_op_chk2cmp2_16_ai(void);
		void m68k_op_chk2cmp2_16_di(void);
		void m68k_op_chk2cmp2_16_ix(void);
		void m68k_op_chk2cmp2_16_aw(void);
		void m68k_op_chk2cmp2_16_al(void);
		void m68k_op_chk2cmp2_16_pcdi(void);
		void m68k_op_chk2cmp2_16_pcix(void);
		void m68k_op_chk2cmp2_32_ai(void);
		void m68k_op_chk2cmp2_32_di(void);
		void m68k_op_chk2cmp2_32_ix(void);
		void m68k_op_chk2cmp2_32_aw(void);
		void m68k_op_chk2cmp2_32_al(void);
		void m68k_op_chk2cmp2_32_pcdi(void);
		void m68k_op_chk2cmp2_32_pcix(void);
		void m68k_op_clr_8_d(void);
		void m68k_op_clr_8_ai(void);
		void m68k_op_clr_8_pi(void);
		void m68k_op_clr_8_pi7(void);
		void m68k_op_clr_8_pd(void);
		void m68k_op_clr_8_pd7(void);
		void m68k_op_clr_8_di(void);
		void m68k_op_clr_8_ix(void);
		void m68k_op_clr_8_aw(void);
		void m68k_op_clr_8_al(void);
		void m68k_op_clr_16_d(void);
		void m68k_op_clr_16_ai(void);
		void m68k_op_clr_16_pi(void);
		void m68k_op_clr_16_pd(void);
		void m68k_op_clr_16_di(void);
		void m68k_op_clr_16_ix(void);
		void m68k_op_clr_16_aw(void);
		void m68k_op_clr_16_al(void);
		void m68k_op_clr_32_d(void);
		void m68k_op_clr_32_ai(void);
		void m68k_op_clr_32_pi(void);
		void m68k_op_clr_32_pd(void);
		void m68k_op_clr_32_di(void);
		void m68k_op_clr_32_ix(void);
		void m68k_op_clr_32_aw(void);
		void m68k_op_clr_32_al(void);
		void m68k_op_cmp_8_d(void);
		void m68k_op_cmp_8_ai(void);
		void m68k_op_cmp_8_pi(void);
		void m68k_op_cmp_8_pi7(void);
		void m68k_op_cmp_8_pd(void);
		void m68k_op_cmp_8_pd7(void);
		void m68k_op_cmp_8_di(void);
		void m68k_op_cmp_8_ix(void);
		void m68k_op_cmp_8_aw(void);
		void m68k_op_cmp_8_al(void);
		void m68k_op_cmp_8_pcdi(void);
		void m68k_op_cmp_8_pcix(void);
		void m68k_op_cmp_8_i(void);
		void m68k_op_cmp_16_d(void);
		void m68k_op_cmp_16_a(void);
		void m68k_op_cmp_16_ai(void);
		void m68k_op_cmp_16_pi(void);
		void m68k_op_cmp_16_pd(void);
		void m68k_op_cmp_16_di(void);
		void m68k_op_cmp_16_ix(void);
		void m68k_op_cmp_16_aw(void);
		void m68k_op_cmp_16_al(void);
		void m68k_op_cmp_16_pcdi(void);
		void m68k_op_cmp_16_pcix(void);
		void m68k_op_cmp_16_i(void);
		void m68k_op_cmp_32_d(void);
		void m68k_op_cmp_32_a(void);
		void m68k_op_cmp_32_ai(void);
		void m68k_op_cmp_32_pi(void);
		void m68k_op_cmp_32_pd(void);
		void m68k_op_cmp_32_di(void);
		void m68k_op_cmp_32_ix(void);
		void m68k_op_cmp_32_aw(void);
		void m68k_op_cmp_32_al(void);
		void m68k_op_cmp_32_pcdi(void);
		void m68k_op_cmp_32_pcix(void);
		void m68k_op_cmp_32_i(void);
		void m68k_op_cmpa_16_d(void);
		void m68k_op_cmpa_16_a(void);
		void m68k_op_cmpa_16_ai(void);
		void m68k_op_cmpa_16_pi(void);
		void m68k_op_cmpa_16_pd(void);
		void m68k_op_cmpa_16_di(void);
		void m68k_op_cmpa_16_ix(void);
		void m68k_op_cmpa_16_aw(void);
		void m68k_op_cmpa_16_al(void);
		void m68k_op_cmpa_16_pcdi(void);
		void m68k_op_cmpa_16_pcix(void);
		void m68k_op_cmpa_16_i(void);
		void m68k_op_cmpa_32_d(void);
		void m68k_op_cmpa_32_a(void);
		void m68k_op_cmpa_32_ai(void);
		void m68k_op_cmpa_32_pi(void);
		void m68k_op_cmpa_32_pd(void);
		void m68k_op_cmpa_32_di(void);
		void m68k_op_cmpa_32_ix(void);
		void m68k_op_cmpa_32_aw(void);
		void m68k_op_cmpa_32_al(void);
		void m68k_op_cmpa_32_pcdi(void);
		void m68k_op_cmpa_32_pcix(void);
		void m68k_op_cmpa_32_i(void);
		void m68k_op_cmpi_8_d(void);
		void m68k_op_cmpi_8_ai(void);
		void m68k_op_cmpi_8_pi(void);
		void m68k_op_cmpi_8_pi7(void);
		void m68k_op_cmpi_8_pd(void);
		void m68k_op_cmpi_8_pd7(void);
		void m68k_op_cmpi_8_di(void);
		void m68k_op_cmpi_8_ix(void);
		void m68k_op_cmpi_8_aw(void);
		void m68k_op_cmpi_8_al(void);
		void m68k_op_cmpi_8_pcdi(void);
		void m68k_op_cmpi_8_pcix(void);
		void m68k_op_cmpi_16_d(void);
		void m68k_op_cmpi_16_ai(void);
		void m68k_op_cmpi_16_pi(void);
		void m68k_op_cmpi_16_pd(void);
		void m68k_op_cmpi_16_di(void);
		void m68k_op_cmpi_16_ix(void);
		void m68k_op_cmpi_16_aw(void);
		void m68k_op_cmpi_16_al(void);
		void m68k_op_cmpi_16_pcdi(void);
		void m68k_op_cmpi_16_pcix(void);
		void m68k_op_cmpi_32_d(void);
		void m68k_op_cmpi_32_ai(void);
		void m68k_op_cmpi_32_pi(void);
		void m68k_op_cmpi_32_pd(void);
		void m68k_op_cmpi_32_di(void);
		void m68k_op_cmpi_32_ix(void);
		void m68k_op_cmpi_32_aw(void);
		void m68k_op_cmpi_32_al(void);
		void m68k_op_cmpi_32_pcdi(void);
		void m68k_op_cmpi_32_pcix(void);
		void m68k_op_cmpm_8_ax7(void);
		void m68k_op_cmpm_8_ay7(void);
		void m68k_op_cmpm_8_axy7(void);
		void m68k_op_cmpm_8(void);
		void m68k_op_cmpm_16(void);
		void m68k_op_cmpm_32(void);
		void m68k_op_cpbcc_32(void);
		void m68k_op_cpdbcc_32(void);
		void m68k_op_cpgen_32(void);
		void m68k_op_cpscc_32(void);
		void m68k_op_cptrapcc_32(void);

		// Opcodes from file: m68kopdm
		void m68k_op_dbt_16(void);
		void m68k_op_dbf_16(void);
		void m68k_op_dbhi_16(void);
		void m68k_op_dbls_16(void);
		void m68k_op_dbcc_16(void);
		void m68k_op_dbcs_16(void);
		void m68k_op_dbne_16(void);
		void m68k_op_dbeq_16(void);
		void m68k_op_dbvc_16(void);
		void m68k_op_dbvs_16(void);
		void m68k_op_dbpl_16(void);
		void m68k_op_dbmi_16(void);
		void m68k_op_dbge_16(void);
		void m68k_op_dblt_16(void);
		void m68k_op_dbgt_16(void);
		void m68k_op_dble_16(void);
		void m68k_op_divs_16_d(void);
		void m68k_op_divs_16_ai(void);
		void m68k_op_divs_16_pi(void);
		void m68k_op_divs_16_pd(void);
		void m68k_op_divs_16_di(void);
		void m68k_op_divs_16_ix(void);
		void m68k_op_divs_16_aw(void);
		void m68k_op_divs_16_al(void);
		void m68k_op_divs_16_pcdi(void);
		void m68k_op_divs_16_pcix(void);
		void m68k_op_divs_16_i(void);
		void m68k_op_divu_16_d(void);
		void m68k_op_divu_16_ai(void);
		void m68k_op_divu_16_pi(void);
		void m68k_op_divu_16_pd(void);
		void m68k_op_divu_16_di(void);
		void m68k_op_divu_16_ix(void);
		void m68k_op_divu_16_aw(void);
		void m68k_op_divu_16_al(void);
		void m68k_op_divu_16_pcdi(void);
		void m68k_op_divu_16_pcix(void);
		void m68k_op_divu_16_i(void);
		void m68k_op_divl_32_d(void);
		void m68k_op_divl_32_ai(void);
		void m68k_op_divl_32_pi(void);
		void m68k_op_divl_32_pd(void);
		void m68k_op_divl_32_di(void);
		void m68k_op_divl_32_ix(void);
		void m68k_op_divl_32_aw(void);
		void m68k_op_divl_32_al(void);
		void m68k_op_divl_32_pcdi(void);
		void m68k_op_divl_32_pcix(void);
		void m68k_op_divl_32_i(void);
		void m68k_op_eor_8_d(void);
		void m68k_op_eor_8_ai(void);
		void m68k_op_eor_8_pi(void);
		void m68k_op_eor_8_pi7(void);
		void m68k_op_eor_8_pd(void);
		void m68k_op_eor_8_pd7(void);
		void m68k_op_eor_8_di(void);
		void m68k_op_eor_8_ix(void);
		void m68k_op_eor_8_aw(void);
		void m68k_op_eor_8_al(void);
		void m68k_op_eor_16_d(void);
		void m68k_op_eor_16_ai(void);
		void m68k_op_eor_16_pi(void);
		void m68k_op_eor_16_pd(void);
		void m68k_op_eor_16_di(void);
		void m68k_op_eor_16_ix(void);
		void m68k_op_eor_16_aw(void);
		void m68k_op_eor_16_al(void);
		void m68k_op_eor_32_d(void);
		void m68k_op_eor_32_ai(void);
		void m68k_op_eor_32_pi(void);
		void m68k_op_eor_32_pd(void);
		void m68k_op_eor_32_di(void);
		void m68k_op_eor_32_ix(void);
		void m68k_op_eor_32_aw(void);
		void m68k_op_eor_32_al(void);
		void m68k_op_eori_8_d(void);
		void m68k_op_eori_8_ai(void);
		void m68k_op_eori_8_pi(void);
		void m68k_op_eori_8_pi7(void);
		void m68k_op_eori_8_pd(void);
		void m68k_op_eori_8_pd7(void);
		void m68k_op_eori_8_di(void);
		void m68k_op_eori_8_ix(void);
		void m68k_op_eori_8_aw(void);
		void m68k_op_eori_8_al(void);
		void m68k_op_eori_16_d(void);
		void m68k_op_eori_16_ai(void);
		void m68k_op_eori_16_pi(void);
		void m68k_op_eori_16_pd(void);
		void m68k_op_eori_16_di(void);
		void m68k_op_eori_16_ix(void);
		void m68k_op_eori_16_aw(void);
		void m68k_op_eori_16_al(void);
		void m68k_op_eori_32_d(void);
		void m68k_op_eori_32_ai(void);
		void m68k_op_eori_32_pi(void);
		void m68k_op_eori_32_pd(void);
		void m68k_op_eori_32_di(void);
		void m68k_op_eori_32_ix(void);
		void m68k_op_eori_32_aw(void);
		void m68k_op_eori_32_al(void);
		void m68k_op_eori_16_toc(void);
		void m68k_op_eori_16_tos(void);
		void m68k_op_exg_32_dd(void);
		void m68k_op_exg_32_aa(void);
		void m68k_op_exg_32_da(void);
		void m68k_op_ext_16(void);
		void m68k_op_ext_32(void);
		void m68k_op_extb_32(void);
		void m68k_op_illegal(void);
		void m68k_op_jmp_32_ai(void);
		void m68k_op_jmp_32_di(void);
		void m68k_op_jmp_32_ix(void);
		void m68k_op_jmp_32_aw(void);
		void m68k_op_jmp_32_al(void);
		void m68k_op_jmp_32_pcdi(void);
		void m68k_op_jmp_32_pcix(void);
		void m68k_op_jsr_32_ai(void);
		void m68k_op_jsr_32_di(void);
		void m68k_op_jsr_32_ix(void);
		void m68k_op_jsr_32_aw(void);
		void m68k_op_jsr_32_al(void);
		void m68k_op_jsr_32_pcdi(void);
		void m68k_op_jsr_32_pcix(void);
		void m68k_op_lea_32_ai(void);
		void m68k_op_lea_32_di(void);
		void m68k_op_lea_32_ix(void);
		void m68k_op_lea_32_aw(void);
		void m68k_op_lea_32_al(void);
		void m68k_op_lea_32_pcdi(void);
		void m68k_op_lea_32_pcix(void);
		void m68k_op_link_16_a7(void);
		void m68k_op_link_16(void);
		void m68k_op_link_32_a7(void);
		void m68k_op_link_32(void);
		void m68k_op_lsr_8_s(void);
		void m68k_op_lsr_16_s(void);
		void m68k_op_lsr_32_s(void);
		void m68k_op_lsr_8_r(void);
		void m68k_op_lsr_16_r(void);
		void m68k_op_lsr_32_r(void);
		void m68k_op_lsr_16_ai(void);
		void m68k_op_lsr_16_pi(void);
		void m68k_op_lsr_16_pd(void);
		void m68k_op_lsr_16_di(void);
		void m68k_op_lsr_16_ix(void);
		void m68k_op_lsr_16_aw(void);
		void m68k_op_lsr_16_al(void);
		void m68k_op_lsl_8_s(void);
		void m68k_op_lsl_16_s(void);
		void m68k_op_lsl_32_s(void);
		void m68k_op_lsl_8_r(void);
		void m68k_op_lsl_16_r(void);
		void m68k_op_lsl_32_r(void);
		void m68k_op_lsl_16_ai(void);
		void m68k_op_lsl_16_pi(void);
		void m68k_op_lsl_16_pd(void);
		void m68k_op_lsl_16_di(void);
		void m68k_op_lsl_16_ix(void);
		void m68k_op_lsl_16_aw(void);
		void m68k_op_lsl_16_al(void);
		void m68k_op_move_8_d_d(void);
		void m68k_op_move_8_d_ai(void);
		void m68k_op_move_8_d_pi(void);
		void m68k_op_move_8_d_pi7(void);
		void m68k_op_move_8_d_pd(void);
		void m68k_op_move_8_d_pd7(void);
		void m68k_op_move_8_d_di(void);
		void m68k_op_move_8_d_ix(void);
		void m68k_op_move_8_d_aw(void);
		void m68k_op_move_8_d_al(void);
		void m68k_op_move_8_d_pcdi(void);
		void m68k_op_move_8_d_pcix(void);
		void m68k_op_move_8_d_i(void);
		void m68k_op_move_8_ai_d(void);
		void m68k_op_move_8_ai_ai(void);
		void m68k_op_move_8_ai_pi(void);
		void m68k_op_move_8_ai_pi7(void);
		void m68k_op_move_8_ai_pd(void);
		void m68k_op_move_8_ai_pd7(void);
		void m68k_op_move_8_ai_di(void);
		void m68k_op_move_8_ai_ix(void);
		void m68k_op_move_8_ai_aw(void);
		void m68k_op_move_8_ai_al(void);
		void m68k_op_move_8_ai_pcdi(void);
		void m68k_op_move_8_ai_pcix(void);
		void m68k_op_move_8_ai_i(void);
		void m68k_op_move_8_pi7_d(void);
		void m68k_op_move_8_pi_d(void);
		void m68k_op_move_8_pi7_ai(void);
		void m68k_op_move_8_pi7_pi(void);
		void m68k_op_move_8_pi7_pi7(void);
		void m68k_op_move_8_pi7_pd(void);
		void m68k_op_move_8_pi7_pd7(void);
		void m68k_op_move_8_pi7_di(void);
		void m68k_op_move_8_pi7_ix(void);
		void m68k_op_move_8_pi7_aw(void);
		void m68k_op_move_8_pi7_al(void);
		void m68k_op_move_8_pi7_pcdi(void);
		void m68k_op_move_8_pi7_pcix(void);
		void m68k_op_move_8_pi7_i(void);
		void m68k_op_move_8_pi_ai(void);
		void m68k_op_move_8_pi_pi(void);
		void m68k_op_move_8_pi_pi7(void);
		void m68k_op_move_8_pi_pd(void);
		void m68k_op_move_8_pi_pd7(void);
		void m68k_op_move_8_pi_di(void);
		void m68k_op_move_8_pi_ix(void);
		void m68k_op_move_8_pi_aw(void);
		void m68k_op_move_8_pi_al(void);
		void m68k_op_move_8_pi_pcdi(void);
		void m68k_op_move_8_pi_pcix(void);
		void m68k_op_move_8_pi_i(void);
		void m68k_op_move_8_pd7_d(void);
		void m68k_op_move_8_pd_d(void);
		void m68k_op_move_8_pd7_ai(void);
		void m68k_op_move_8_pd7_pi(void);
		void m68k_op_move_8_pd7_pi7(void);
		void m68k_op_move_8_pd7_pd(void);
		void m68k_op_move_8_pd7_pd7(void);
		void m68k_op_move_8_pd7_di(void);
		void m68k_op_move_8_pd7_ix(void);
		void m68k_op_move_8_pd7_aw(void);
		void m68k_op_move_8_pd7_al(void);
		void m68k_op_move_8_pd7_pcdi(void);
		void m68k_op_move_8_pd7_pcix(void);
		void m68k_op_move_8_pd7_i(void);
		void m68k_op_move_8_pd_ai(void);
		void m68k_op_move_8_pd_pi(void);
		void m68k_op_move_8_pd_pi7(void);
		void m68k_op_move_8_pd_pd(void);
		void m68k_op_move_8_pd_pd7(void);
		void m68k_op_move_8_pd_di(void);
		void m68k_op_move_8_pd_ix(void);
		void m68k_op_move_8_pd_aw(void);
		void m68k_op_move_8_pd_al(void);
		void m68k_op_move_8_pd_pcdi(void);
		void m68k_op_move_8_pd_pcix(void);
		void m68k_op_move_8_pd_i(void);
		void m68k_op_move_8_di_d(void);
		void m68k_op_move_8_di_ai(void);
		void m68k_op_move_8_di_pi(void);
		void m68k_op_move_8_di_pi7(void);
		void m68k_op_move_8_di_pd(void);
		void m68k_op_move_8_di_pd7(void);
		void m68k_op_move_8_di_di(void);
		void m68k_op_move_8_di_ix(void);
		void m68k_op_move_8_di_aw(void);
		void m68k_op_move_8_di_al(void);
		void m68k_op_move_8_di_pcdi(void);
		void m68k_op_move_8_di_pcix(void);
		void m68k_op_move_8_di_i(void);
		void m68k_op_move_8_ix_d(void);
		void m68k_op_move_8_ix_ai(void);
		void m68k_op_move_8_ix_pi(void);
		void m68k_op_move_8_ix_pi7(void);
		void m68k_op_move_8_ix_pd(void);
		void m68k_op_move_8_ix_pd7(void);
		void m68k_op_move_8_ix_di(void);
		void m68k_op_move_8_ix_ix(void);
		void m68k_op_move_8_ix_aw(void);
		void m68k_op_move_8_ix_al(void);
		void m68k_op_move_8_ix_pcdi(void);
		void m68k_op_move_8_ix_pcix(void);
		void m68k_op_move_8_ix_i(void);
		void m68k_op_move_8_aw_d(void);
		void m68k_op_move_8_aw_ai(void);
		void m68k_op_move_8_aw_pi(void);
		void m68k_op_move_8_aw_pi7(void);
		void m68k_op_move_8_aw_pd(void);
		void m68k_op_move_8_aw_pd7(void);
		void m68k_op_move_8_aw_di(void);
		void m68k_op_move_8_aw_ix(void);
		void m68k_op_move_8_aw_aw(void);
		void m68k_op_move_8_aw_al(void);
		void m68k_op_move_8_aw_pcdi(void);
		void m68k_op_move_8_aw_pcix(void);
		void m68k_op_move_8_aw_i(void);
		void m68k_op_move_8_al_d(void);
		void m68k_op_move_8_al_ai(void);
		void m68k_op_move_8_al_pi(void);
		void m68k_op_move_8_al_pi7(void);
		void m68k_op_move_8_al_pd(void);
		void m68k_op_move_8_al_pd7(void);
		void m68k_op_move_8_al_di(void);
		void m68k_op_move_8_al_ix(void);
		void m68k_op_move_8_al_aw(void);
		void m68k_op_move_8_al_al(void);
		void m68k_op_move_8_al_pcdi(void);
		void m68k_op_move_8_al_pcix(void);
		void m68k_op_move_8_al_i(void);
		void m68k_op_move_16_d_d(void);
		void m68k_op_move_16_d_a(void);
		void m68k_op_move_16_d_ai(void);
		void m68k_op_move_16_d_pi(void);
		void m68k_op_move_16_d_pd(void);
		void m68k_op_move_16_d_di(void);
		void m68k_op_move_16_d_ix(void);
		void m68k_op_move_16_d_aw(void);
		void m68k_op_move_16_d_al(void);
		void m68k_op_move_16_d_pcdi(void);
		void m68k_op_move_16_d_pcix(void);
		void m68k_op_move_16_d_i(void);
		void m68k_op_move_16_ai_d(void);
		void m68k_op_move_16_ai_a(void);
		void m68k_op_move_16_ai_ai(void);
		void m68k_op_move_16_ai_pi(void);
		void m68k_op_move_16_ai_pd(void);
		void m68k_op_move_16_ai_di(void);
		void m68k_op_move_16_ai_ix(void);
		void m68k_op_move_16_ai_aw(void);
		void m68k_op_move_16_ai_al(void);
		void m68k_op_move_16_ai_pcdi(void);
		void m68k_op_move_16_ai_pcix(void);
		void m68k_op_move_16_ai_i(void);
		void m68k_op_move_16_pi_d(void);
		void m68k_op_move_16_pi_a(void);
		void m68k_op_move_16_pi_ai(void);
		void m68k_op_move_16_pi_pi(void);
		void m68k_op_move_16_pi_pd(void);
		void m68k_op_move_16_pi_di(void);
		void m68k_op_move_16_pi_ix(void);
		void m68k_op_move_16_pi_aw(void);
		void m68k_op_move_16_pi_al(void);
		void m68k_op_move_16_pi_pcdi(void);
		void m68k_op_move_16_pi_pcix(void);
		void m68k_op_move_16_pi_i(void);
		void m68k_op_move_16_pd_d(void);
		void m68k_op_move_16_pd_a(void);
		void m68k_op_move_16_pd_ai(void);
		void m68k_op_move_16_pd_pi(void);
		void m68k_op_move_16_pd_pd(void);
		void m68k_op_move_16_pd_di(void);
		void m68k_op_move_16_pd_ix(void);
		void m68k_op_move_16_pd_aw(void);
		void m68k_op_move_16_pd_al(void);
		void m68k_op_move_16_pd_pcdi(void);
		void m68k_op_move_16_pd_pcix(void);
		void m68k_op_move_16_pd_i(void);
		void m68k_op_move_16_di_d(void);
		void m68k_op_move_16_di_a(void);
		void m68k_op_move_16_di_ai(void);
		void m68k_op_move_16_di_pi(void);
		void m68k_op_move_16_di_pd(void);
		void m68k_op_move_16_di_di(void);
		void m68k_op_move_16_di_ix(void);
		void m68k_op_move_16_di_aw(void);
		void m68k_op_move_16_di_al(void);
		void m68k_op_move_16_di_pcdi(void);
		void m68k_op_move_16_di_pcix(void);
		void m68k_op_move_16_di_i(void);
		void m68k_op_move_16_ix_d(void);
		void m68k_op_move_16_ix_a(void);
		void m68k_op_move_16_ix_ai(void);
		void m68k_op_move_16_ix_pi(void);
		void m68k_op_move_16_ix_pd(void);
		void m68k_op_move_16_ix_di(void);
		void m68k_op_move_16_ix_ix(void);
		void m68k_op_move_16_ix_aw(void);
		void m68k_op_move_16_ix_al(void);
		void m68k_op_move_16_ix_pcdi(void);
		void m68k_op_move_16_ix_pcix(void);
		void m68k_op_move_16_ix_i(void);
		void m68k_op_move_16_aw_d(void);
		void m68k_op_move_16_aw_a(void);
		void m68k_op_move_16_aw_ai(void);
		void m68k_op_move_16_aw_pi(void);
		void m68k_op_move_16_aw_pd(void);
		void m68k_op_move_16_aw_di(void);
		void m68k_op_move_16_aw_ix(void);
		void m68k_op_move_16_aw_aw(void);
		void m68k_op_move_16_aw_al(void);
		void m68k_op_move_16_aw_pcdi(void);
		void m68k_op_move_16_aw_pcix(void);
		void m68k_op_move_16_aw_i(void);
		void m68k_op_move_16_al_d(void);
		void m68k_op_move_16_al_a(void);
		void m68k_op_move_16_al_ai(void);
		void m68k_op_move_16_al_pi(void);
		void m68k_op_move_16_al_pd(void);
		void m68k_op_move_16_al_di(void);
		void m68k_op_move_16_al_ix(void);
		void m68k_op_move_16_al_aw(void);
		void m68k_op_move_16_al_al(void);
		void m68k_op_move_16_al_pcdi(void);
		void m68k_op_move_16_al_pcix(void);
		void m68k_op_move_16_al_i(void);
		void m68k_op_move_32_d_d(void);
		void m68k_op_move_32_d_a(void);
		void m68k_op_move_32_d_ai(void);
		void m68k_op_move_32_d_pi(void);
		void m68k_op_move_32_d_pd(void);
		void m68k_op_move_32_d_di(void);
		void m68k_op_move_32_d_ix(void);
		void m68k_op_move_32_d_aw(void);
		void m68k_op_move_32_d_al(void);
		void m68k_op_move_32_d_pcdi(void);
		void m68k_op_move_32_d_pcix(void);
		void m68k_op_move_32_d_i(void);
		void m68k_op_move_32_ai_d(void);
		void m68k_op_move_32_ai_a(void);
		void m68k_op_move_32_ai_ai(void);
		void m68k_op_move_32_ai_pi(void);
		void m68k_op_move_32_ai_pd(void);
		void m68k_op_move_32_ai_di(void);
		void m68k_op_move_32_ai_ix(void);
		void m68k_op_move_32_ai_aw(void);
		void m68k_op_move_32_ai_al(void);
		void m68k_op_move_32_ai_pcdi(void);
		void m68k_op_move_32_ai_pcix(void);
		void m68k_op_move_32_ai_i(void);
		void m68k_op_move_32_pi_d(void);
		void m68k_op_move_32_pi_a(void);
		void m68k_op_move_32_pi_ai(void);
		void m68k_op_move_32_pi_pi(void);
		void m68k_op_move_32_pi_pd(void);
		void m68k_op_move_32_pi_di(void);
		void m68k_op_move_32_pi_ix(void);
		void m68k_op_move_32_pi_aw(void);
		void m68k_op_move_32_pi_al(void);
		void m68k_op_move_32_pi_pcdi(void);
		void m68k_op_move_32_pi_pcix(void);
		void m68k_op_move_32_pi_i(void);
		void m68k_op_move_32_pd_d(void);
		void m68k_op_move_32_pd_a(void);
		void m68k_op_move_32_pd_ai(void);
		void m68k_op_move_32_pd_pi(void);
		void m68k_op_move_32_pd_pd(void);
		void m68k_op_move_32_pd_di(void);
		void m68k_op_move_32_pd_ix(void);
		void m68k_op_move_32_pd_aw(void);
		void m68k_op_move_32_pd_al(void);
		void m68k_op_move_32_pd_pcdi(void);
		void m68k_op_move_32_pd_pcix(void);
		void m68k_op_move_32_pd_i(void);
		void m68k_op_move_32_di_d(void);
		void m68k_op_move_32_di_a(void);
		void m68k_op_move_32_di_ai(void);
		void m68k_op_move_32_di_pi(void);
		void m68k_op_move_32_di_pd(void);
		void m68k_op_move_32_di_di(void);
		void m68k_op_move_32_di_ix(void);
		void m68k_op_move_32_di_aw(void);
		void m68k_op_move_32_di_al(void);
		void m68k_op_move_32_di_pcdi(void);
		void m68k_op_move_32_di_pcix(void);
		void m68k_op_move_32_di_i(void);
		void m68k_op_move_32_ix_d(void);
		void m68k_op_move_32_ix_a(void);
		void m68k_op_move_32_ix_ai(void);
		void m68k_op_move_32_ix_pi(void);
		void m68k_op_move_32_ix_pd(void);
		void m68k_op_move_32_ix_di(void);
		void m68k_op_move_32_ix_ix(void);
		void m68k_op_move_32_ix_aw(void);
		void m68k_op_move_32_ix_al(void);
		void m68k_op_move_32_ix_pcdi(void);
		void m68k_op_move_32_ix_pcix(void);
		void m68k_op_move_32_ix_i(void);
		void m68k_op_move_32_aw_d(void);
		void m68k_op_move_32_aw_a(void);
		void m68k_op_move_32_aw_ai(void);
		void m68k_op_move_32_aw_pi(void);
		void m68k_op_move_32_aw_pd(void);
		void m68k_op_move_32_aw_di(void);
		void m68k_op_move_32_aw_ix(void);
		void m68k_op_move_32_aw_aw(void);
		void m68k_op_move_32_aw_al(void);
		void m68k_op_move_32_aw_pcdi(void);
		void m68k_op_move_32_aw_pcix(void);
		void m68k_op_move_32_aw_i(void);
		void m68k_op_move_32_al_d(void);
		void m68k_op_move_32_al_a(void);
		void m68k_op_move_32_al_ai(void);
		void m68k_op_move_32_al_pi(void);
		void m68k_op_move_32_al_pd(void);
		void m68k_op_move_32_al_di(void);
		void m68k_op_move_32_al_ix(void);
		void m68k_op_move_32_al_aw(void);
		void m68k_op_move_32_al_al(void);
		void m68k_op_move_32_al_pcdi(void);
		void m68k_op_move_32_al_pcix(void);
		void m68k_op_move_32_al_i(void);
		void m68k_op_movea_16_d(void);
		void m68k_op_movea_16_a(void);
		void m68k_op_movea_16_ai(void);
		void m68k_op_movea_16_pi(void);
		void m68k_op_movea_16_pd(void);
		void m68k_op_movea_16_di(void);
		void m68k_op_movea_16_ix(void);
		void m68k_op_movea_16_aw(void);
		void m68k_op_movea_16_al(void);
		void m68k_op_movea_16_pcdi(void);
		void m68k_op_movea_16_pcix(void);
		void m68k_op_movea_16_i(void);
		void m68k_op_movea_32_d(void);
		void m68k_op_movea_32_a(void);
		void m68k_op_movea_32_ai(void);
		void m68k_op_movea_32_pi(void);
		void m68k_op_movea_32_pd(void);
		void m68k_op_movea_32_di(void);
		void m68k_op_movea_32_ix(void);
		void m68k_op_movea_32_aw(void);
		void m68k_op_movea_32_al(void);
		void m68k_op_movea_32_pcdi(void);
		void m68k_op_movea_32_pcix(void);
		void m68k_op_movea_32_i(void);
		void m68k_op_move_16_frc_d(void);
		void m68k_op_move_16_frc_ai(void);
		void m68k_op_move_16_frc_pi(void);
		void m68k_op_move_16_frc_pd(void);
		void m68k_op_move_16_frc_di(void);
		void m68k_op_move_16_frc_ix(void);
		void m68k_op_move_16_frc_aw(void);
		void m68k_op_move_16_frc_al(void);
		void m68k_op_move_16_toc_d(void);
		void m68k_op_move_16_toc_ai(void);
		void m68k_op_move_16_toc_pi(void);
		void m68k_op_move_16_toc_pd(void);
		void m68k_op_move_16_toc_di(void);
		void m68k_op_move_16_toc_ix(void);
		void m68k_op_move_16_toc_aw(void);
		void m68k_op_move_16_toc_al(void);
		void m68k_op_move_16_toc_pcdi(void);
		void m68k_op_move_16_toc_pcix(void);
		void m68k_op_move_16_toc_i(void);
		void m68k_op_move_16_frs_d(void);
		void m68k_op_move_16_frs_ai(void);
		void m68k_op_move_16_frs_pi(void);
		void m68k_op_move_16_frs_pd(void);
		void m68k_op_move_16_frs_di(void);
		void m68k_op_move_16_frs_ix(void);
		void m68k_op_move_16_frs_aw(void);
		void m68k_op_move_16_frs_al(void);
		void m68k_op_move_16_tos_d(void);
		void m68k_op_move_16_tos_ai(void);
		void m68k_op_move_16_tos_pi(void);
		void m68k_op_move_16_tos_pd(void);
		void m68k_op_move_16_tos_di(void);
		void m68k_op_move_16_tos_ix(void);
		void m68k_op_move_16_tos_aw(void);
		void m68k_op_move_16_tos_al(void);
		void m68k_op_move_16_tos_pcdi(void);
		void m68k_op_move_16_tos_pcix(void);
		void m68k_op_move_16_tos_i(void);
		void m68k_op_move_32_fru(void);
		void m68k_op_move_32_tou(void);
		void m68k_op_movec_32_cr(void);
		void m68k_op_movec_32_rc(void);
		void m68k_op_movem_16_re_pd(void);
		void m68k_op_movem_16_re_ai(void);
		void m68k_op_movem_16_re_di(void);
		void m68k_op_movem_16_re_ix(void);
		void m68k_op_movem_16_re_aw(void);
		void m68k_op_movem_16_re_al(void);
		void m68k_op_movem_32_re_pd(void);
		void m68k_op_movem_32_re_ai(void);
		void m68k_op_movem_32_re_di(void);
		void m68k_op_movem_32_re_ix(void);
		void m68k_op_movem_32_re_aw(void);
		void m68k_op_movem_32_re_al(void);
		void m68k_op_movem_16_er_pi(void);
		void m68k_op_movem_16_er_ai(void);
		void m68k_op_movem_16_er_di(void);
		void m68k_op_movem_16_er_ix(void);
		void m68k_op_movem_16_er_aw(void);
		void m68k_op_movem_16_er_al(void);
		void m68k_op_movem_16_er_pcdi(void);
		void m68k_op_movem_16_er_pcix(void);
		void m68k_op_movem_32_er_pi(void);
		void m68k_op_movem_32_er_ai(void);
		void m68k_op_movem_32_er_di(void);
		void m68k_op_movem_32_er_ix(void);
		void m68k_op_movem_32_er_aw(void);
		void m68k_op_movem_32_er_al(void);
		void m68k_op_movem_32_er_pcdi(void);
		void m68k_op_movem_32_er_pcix(void);
		void m68k_op_movep_16_re(void);
		void m68k_op_movep_32_re(void);
		void m68k_op_movep_16_er(void);
		void m68k_op_movep_32_er(void);
		void m68k_op_moves_8_ai(void);
		void m68k_op_moves_8_pi(void);
		void m68k_op_moves_8_pi7(void);
		void m68k_op_moves_8_pd(void);
		void m68k_op_moves_8_pd7(void);
		void m68k_op_moves_8_di(void);
		void m68k_op_moves_8_ix(void);
		void m68k_op_moves_8_aw(void);
		void m68k_op_moves_8_al(void);
		void m68k_op_moves_16_ai(void);
		void m68k_op_moves_16_pi(void);
		void m68k_op_moves_16_pd(void);
		void m68k_op_moves_16_di(void);
		void m68k_op_moves_16_ix(void);
		void m68k_op_moves_16_aw(void);
		void m68k_op_moves_16_al(void);
		void m68k_op_moves_32_ai(void);
		void m68k_op_moves_32_pi(void);
		void m68k_op_moves_32_pd(void);
		void m68k_op_moves_32_di(void);
		void m68k_op_moves_32_ix(void);
		void m68k_op_moves_32_aw(void);
		void m68k_op_moves_32_al(void);
		void m68k_op_moveq_32(void);
		void m68k_op_muls_16_d(void);
		void m68k_op_muls_16_ai(void);
		void m68k_op_muls_16_pi(void);
		void m68k_op_muls_16_pd(void);
		void m68k_op_muls_16_di(void);
		void m68k_op_muls_16_ix(void);
		void m68k_op_muls_16_aw(void);
		void m68k_op_muls_16_al(void);
		void m68k_op_muls_16_pcdi(void);
		void m68k_op_muls_16_pcix(void);
		void m68k_op_muls_16_i(void);
		void m68k_op_mulu_16_d(void);
		void m68k_op_mulu_16_ai(void);
		void m68k_op_mulu_16_pi(void);
		void m68k_op_mulu_16_pd(void);
		void m68k_op_mulu_16_di(void);
		void m68k_op_mulu_16_ix(void);
		void m68k_op_mulu_16_aw(void);
		void m68k_op_mulu_16_al(void);
		void m68k_op_mulu_16_pcdi(void);
		void m68k_op_mulu_16_pcix(void);
		void m68k_op_mulu_16_i(void);
		void m68k_op_mull_32_d(void);
		void m68k_op_mull_32_ai(void);
		void m68k_op_mull_32_pi(void);
		void m68k_op_mull_32_pd(void);
		void m68k_op_mull_32_di(void);
		void m68k_op_mull_32_ix(void);
		void m68k_op_mull_32_aw(void);
		void m68k_op_mull_32_al(void);
		void m68k_op_mull_32_pcdi(void);
		void m68k_op_mull_32_pcix(void);
		void m68k_op_mull_32_i(void);

		// Opcodes from file: m68kopnz
		void m68k_op_nbcd_8_d(void);
		void m68k_op_nbcd_8_ai(void);
		void m68k_op_nbcd_8_pi(void);
		void m68k_op_nbcd_8_pi7(void);
		void m68k_op_nbcd_8_pd(void);
		void m68k_op_nbcd_8_pd7(void);
		void m68k_op_nbcd_8_di(void);
		void m68k_op_nbcd_8_ix(void);
		void m68k_op_nbcd_8_aw(void);
		void m68k_op_nbcd_8_al(void);
		void m68k_op_neg_8_d(void);
		void m68k_op_neg_8_ai(void);
		void m68k_op_neg_8_pi(void);
		void m68k_op_neg_8_pi7(void);
		void m68k_op_neg_8_pd(void);
		void m68k_op_neg_8_pd7(void);
		void m68k_op_neg_8_di(void);
		void m68k_op_neg_8_ix(void);
		void m68k_op_neg_8_aw(void);
		void m68k_op_neg_8_al(void);
		void m68k_op_neg_16_d(void);
		void m68k_op_neg_16_ai(void);
		void m68k_op_neg_16_pi(void);
		void m68k_op_neg_16_pd(void);
		void m68k_op_neg_16_di(void);
		void m68k_op_neg_16_ix(void);
		void m68k_op_neg_16_aw(void);
		void m68k_op_neg_16_al(void);
		void m68k_op_neg_32_d(void);
		void m68k_op_neg_32_ai(void);
		void m68k_op_neg_32_pi(void);
		void m68k_op_neg_32_pd(void);
		void m68k_op_neg_32_di(void);
		void m68k_op_neg_32_ix(void);
		void m68k_op_neg_32_aw(void);
		void m68k_op_neg_32_al(void);
		void m68k_op_negx_8_d(void);
		void m68k_op_negx_8_ai(void);
		void m68k_op_negx_8_pi(void);
		void m68k_op_negx_8_pi7(void);
		void m68k_op_negx_8_pd(void);
		void m68k_op_negx_8_pd7(void);
		void m68k_op_negx_8_di(void);
		void m68k_op_negx_8_ix(void);
		void m68k_op_negx_8_aw(void);
		void m68k_op_negx_8_al(void);
		void m68k_op_negx_16_d(void);
		void m68k_op_negx_16_ai(void);
		void m68k_op_negx_16_pi(void);
		void m68k_op_negx_16_pd(void);
		void m68k_op_negx_16_di(void);
		void m68k_op_negx_16_ix(void);
		void m68k_op_negx_16_aw(void);
		void m68k_op_negx_16_al(void);
		void m68k_op_negx_32_d(void);
		void m68k_op_negx_32_ai(void);
		void m68k_op_negx_32_pi(void);
		void m68k_op_negx_32_pd(void);
		void m68k_op_negx_32_di(void);
		void m68k_op_negx_32_ix(void);
		void m68k_op_negx_32_aw(void);
		void m68k_op_negx_32_al(void);
		void m68k_op_nop(void);
		void m68k_op_not_8_d(void);
		void m68k_op_not_8_ai(void);
		void m68k_op_not_8_pi(void);
		void m68k_op_not_8_pi7(void);
		void m68k_op_not_8_pd(void);
		void m68k_op_not_8_pd7(void);
		void m68k_op_not_8_di(void);
		void m68k_op_not_8_ix(void);
		void m68k_op_not_8_aw(void);
		void m68k_op_not_8_al(void);
		void m68k_op_not_16_d(void);
		void m68k_op_not_16_ai(void);
		void m68k_op_not_16_pi(void);
		void m68k_op_not_16_pd(void);
		void m68k_op_not_16_di(void);
		void m68k_op_not_16_ix(void);
		void m68k_op_not_16_aw(void);
		void m68k_op_not_16_al(void);
		void m68k_op_not_32_d(void);
		void m68k_op_not_32_ai(void);
		void m68k_op_not_32_pi(void);
		void m68k_op_not_32_pd(void);
		void m68k_op_not_32_di(void);
		void m68k_op_not_32_ix(void);
		void m68k_op_not_32_aw(void);
		void m68k_op_not_32_al(void);
		void m68k_op_or_8_er_d(void);
		void m68k_op_or_8_er_ai(void);
		void m68k_op_or_8_er_pi(void);
		void m68k_op_or_8_er_pi7(void);
		void m68k_op_or_8_er_pd(void);
		void m68k_op_or_8_er_pd7(void);
		void m68k_op_or_8_er_di(void);
		void m68k_op_or_8_er_ix(void);
		void m68k_op_or_8_er_aw(void);
		void m68k_op_or_8_er_al(void);
		void m68k_op_or_8_er_pcdi(void);
		void m68k_op_or_8_er_pcix(void);
		void m68k_op_or_8_er_i(void);
		void m68k_op_or_16_er_d(void);
		void m68k_op_or_16_er_ai(void);
		void m68k_op_or_16_er_pi(void);
		void m68k_op_or_16_er_pd(void);
		void m68k_op_or_16_er_di(void);
		void m68k_op_or_16_er_ix(void);
		void m68k_op_or_16_er_aw(void);
		void m68k_op_or_16_er_al(void);
		void m68k_op_or_16_er_pcdi(void);
		void m68k_op_or_16_er_pcix(void);
		void m68k_op_or_16_er_i(void);
		void m68k_op_or_32_er_d(void);
		void m68k_op_or_32_er_ai(void);
		void m68k_op_or_32_er_pi(void);
		void m68k_op_or_32_er_pd(void);
		void m68k_op_or_32_er_di(void);
		void m68k_op_or_32_er_ix(void);
		void m68k_op_or_32_er_aw(void);
		void m68k_op_or_32_er_al(void);
		void m68k_op_or_32_er_pcdi(void);
		void m68k_op_or_32_er_pcix(void);
		void m68k_op_or_32_er_i(void);
		void m68k_op_or_8_re_ai(void);
		void m68k_op_or_8_re_pi(void);
		void m68k_op_or_8_re_pi7(void);
		void m68k_op_or_8_re_pd(void);
		void m68k_op_or_8_re_pd7(void);
		void m68k_op_or_8_re_di(void);
		void m68k_op_or_8_re_ix(void);
		void m68k_op_or_8_re_aw(void);
		void m68k_op_or_8_re_al(void);
		void m68k_op_or_16_re_ai(void);
		void m68k_op_or_16_re_pi(void);
		void m68k_op_or_16_re_pd(void);
		void m68k_op_or_16_re_di(void);
		void m68k_op_or_16_re_ix(void);
		void m68k_op_or_16_re_aw(void);
		void m68k_op_or_16_re_al(void);
		void m68k_op_or_32_re_ai(void);
		void m68k_op_or_32_re_pi(void);
		void m68k_op_or_32_re_pd(void);
		void m68k_op_or_32_re_di(void);
		void m68k_op_or_32_re_ix(void);
		void m68k_op_or_32_re_aw(void);
		void m68k_op_or_32_re_al(void);
		void m68k_op_ori_8_d(void);
		void m68k_op_ori_8_ai(void);
		void m68k_op_ori_8_pi(void);
		void m68k_op_ori_8_pi7(void);
		void m68k_op_ori_8_pd(void);
		void m68k_op_ori_8_pd7(void);
		void m68k_op_ori_8_di(void);
		void m68k_op_ori_8_ix(void);
		void m68k_op_ori_8_aw(void);
		void m68k_op_ori_8_al(void);
		void m68k_op_ori_16_d(void);
		void m68k_op_ori_16_ai(void);
		void m68k_op_ori_16_pi(void);
		void m68k_op_ori_16_pd(void);
		void m68k_op_ori_16_di(void);
		void m68k_op_ori_16_ix(void);
		void m68k_op_ori_16_aw(void);
		void m68k_op_ori_16_al(void);
		void m68k_op_ori_32_d(void);
		void m68k_op_ori_32_ai(void);
		void m68k_op_ori_32_pi(void);
		void m68k_op_ori_32_pd(void);
		void m68k_op_ori_32_di(void);
		void m68k_op_ori_32_ix(void);
		void m68k_op_ori_32_aw(void);
		void m68k_op_ori_32_al(void);
		void m68k_op_ori_16_toc(void);
		void m68k_op_ori_16_tos(void);
		void m68k_op_pack_16_rr(void);
		void m68k_op_pack_16_mm_ax7(void);
		void m68k_op_pack_16_mm_ay7(void);
		void m68k_op_pack_16_mm_axy7(void);
		void m68k_op_pack_16_mm(void);
		void m68k_op_pea_32_ai(void);
		void m68k_op_pea_32_di(void);
		void m68k_op_pea_32_ix(void);
		void m68k_op_pea_32_aw(void);
		void m68k_op_pea_32_al(void);
		void m68k_op_pea_32_pcdi(void);
		void m68k_op_pea_32_pcix(void);
		void m68k_op_reset(void);
		void m68k_op_ror_8_s(void);
		void m68k_op_ror_16_s(void);
		void m68k_op_ror_32_s(void);
		void m68k_op_ror_8_r(void);
		void m68k_op_ror_16_r(void);
		void m68k_op_ror_32_r(void);
		void m68k_op_ror_16_ai(void);
		void m68k_op_ror_16_pi(void);
		void m68k_op_ror_16_pd(void);
		void m68k_op_ror_16_di(void);
		void m68k_op_ror_16_ix(void);
		void m68k_op_ror_16_aw(void);
		void m68k_op_ror_16_al(void);
		void m68k_op_rol_8_s(void);
		void m68k_op_rol_16_s(void);
		void m68k_op_rol_32_s(void);
		void m68k_op_rol_8_r(void);
		void m68k_op_rol_16_r(void);
		void m68k_op_rol_32_r(void);
		void m68k_op_rol_16_ai(void);
		void m68k_op_rol_16_pi(void);
		void m68k_op_rol_16_pd(void);
		void m68k_op_rol_16_di(void);
		void m68k_op_rol_16_ix(void);
		void m68k_op_rol_16_aw(void);
		void m68k_op_rol_16_al(void);
		void m68k_op_roxr_8_s(void);
		void m68k_op_roxr_16_s(void);
		void m68k_op_roxr_32_s(void);
		void m68k_op_roxr_8_r(void);
		void m68k_op_roxr_16_r(void);
		void m68k_op_roxr_32_r(void);
		void m68k_op_roxr_16_ai(void);
		void m68k_op_roxr_16_pi(void);
		void m68k_op_roxr_16_pd(void);
		void m68k_op_roxr_16_di(void);
		void m68k_op_roxr_16_ix(void);
		void m68k_op_roxr_16_aw(void);
		void m68k_op_roxr_16_al(void);
		void m68k_op_roxl_8_s(void);
		void m68k_op_roxl_16_s(void);
		void m68k_op_roxl_32_s(void);
		void m68k_op_roxl_8_r(void);
		void m68k_op_roxl_16_r(void);
		void m68k_op_roxl_32_r(void);
		void m68k_op_roxl_16_ai(void);
		void m68k_op_roxl_16_pi(void);
		void m68k_op_roxl_16_pd(void);
		void m68k_op_roxl_16_di(void);
		void m68k_op_roxl_16_ix(void);
		void m68k_op_roxl_16_aw(void);
		void m68k_op_roxl_16_al(void);
		void m68k_op_rtd_32(void);
		void m68k_op_rte_32(void);
		void m68k_op_rtm_32(void);
		void m68k_op_rtr_32(void);
		void m68k_op_rts_32(void);
		void m68k_op_sbcd_8_rr(void);
		void m68k_op_sbcd_8_mm_ax7(void);
		void m68k_op_sbcd_8_mm_ay7(void);
		void m68k_op_sbcd_8_mm_axy7(void);
		void m68k_op_sbcd_8_mm(void);
		void m68k_op_st_8_d(void);
		void m68k_op_st_8_ai(void);
		void m68k_op_st_8_pi(void);
		void m68k_op_st_8_pi7(void);
		void m68k_op_st_8_pd(void);
		void m68k_op_st_8_pd7(void);
		void m68k_op_st_8_di(void);
		void m68k_op_st_8_ix(void);
		void m68k_op_st_8_aw(void);
		void m68k_op_st_8_al(void);
		void m68k_op_sf_8_d(void);
		void m68k_op_sf_8_ai(void);
		void m68k_op_sf_8_pi(void);
		void m68k_op_sf_8_pi7(void);
		void m68k_op_sf_8_pd(void);
		void m68k_op_sf_8_pd7(void);
		void m68k_op_sf_8_di(void);
		void m68k_op_sf_8_ix(void);
		void m68k_op_sf_8_aw(void);
		void m68k_op_sf_8_al(void);
		void m68k_op_shi_8_d(void);
		void m68k_op_sls_8_d(void);
		void m68k_op_scc_8_d(void);
		void m68k_op_scs_8_d(void);
		void m68k_op_sne_8_d(void);
		void m68k_op_seq_8_d(void);
		void m68k_op_svc_8_d(void);
		void m68k_op_svs_8_d(void);
		void m68k_op_spl_8_d(void);
		void m68k_op_smi_8_d(void);
		void m68k_op_sge_8_d(void);
		void m68k_op_slt_8_d(void);
		void m68k_op_sgt_8_d(void);
		void m68k_op_sle_8_d(void);
		void m68k_op_shi_8_ai(void);
		void m68k_op_shi_8_pi(void);
		void m68k_op_shi_8_pi7(void);
		void m68k_op_shi_8_pd(void);
		void m68k_op_shi_8_pd7(void);
		void m68k_op_shi_8_di(void);
		void m68k_op_shi_8_ix(void);
		void m68k_op_shi_8_aw(void);
		void m68k_op_shi_8_al(void);
		void m68k_op_sls_8_ai(void);
		void m68k_op_sls_8_pi(void);
		void m68k_op_sls_8_pi7(void);
		void m68k_op_sls_8_pd(void);
		void m68k_op_sls_8_pd7(void);
		void m68k_op_sls_8_di(void);
		void m68k_op_sls_8_ix(void);
		void m68k_op_sls_8_aw(void);
		void m68k_op_sls_8_al(void);
		void m68k_op_scc_8_ai(void);
		void m68k_op_scc_8_pi(void);
		void m68k_op_scc_8_pi7(void);
		void m68k_op_scc_8_pd(void);
		void m68k_op_scc_8_pd7(void);
		void m68k_op_scc_8_di(void);
		void m68k_op_scc_8_ix(void);
		void m68k_op_scc_8_aw(void);
		void m68k_op_scc_8_al(void);
		void m68k_op_scs_8_ai(void);
		void m68k_op_scs_8_pi(void);
		void m68k_op_scs_8_pi7(void);
		void m68k_op_scs_8_pd(void);
		void m68k_op_scs_8_pd7(void);
		void m68k_op_scs_8_di(void);
		void m68k_op_scs_8_ix(void);
		void m68k_op_scs_8_aw(void);
		void m68k_op_scs_8_al(void);
		void m68k_op_sne_8_ai(void);
		void m68k_op_sne_8_pi(void);
		void m68k_op_sne_8_pi7(void);
		void m68k_op_sne_8_pd(void);
		void m68k_op_sne_8_pd7(void);
		void m68k_op_sne_8_di(void);
		void m68k_op_sne_8_ix(void);
		void m68k_op_sne_8_aw(void);
		void m68k_op_sne_8_al(void);
		void m68k_op_seq_8_ai(void);
		void m68k_op_seq_8_pi(void);
		void m68k_op_seq_8_pi7(void);
		void m68k_op_seq_8_pd(void);
		void m68k_op_seq_8_pd7(void);
		void m68k_op_seq_8_di(void);
		void m68k_op_seq_8_ix(void);
		void m68k_op_seq_8_aw(void);
		void m68k_op_seq_8_al(void);
		void m68k_op_svc_8_ai(void);
		void m68k_op_svc_8_pi(void);
		void m68k_op_svc_8_pi7(void);
		void m68k_op_svc_8_pd(void);
		void m68k_op_svc_8_pd7(void);
		void m68k_op_svc_8_di(void);
		void m68k_op_svc_8_ix(void);
		void m68k_op_svc_8_aw(void);
		void m68k_op_svc_8_al(void);
		void m68k_op_svs_8_ai(void);
		void m68k_op_svs_8_pi(void);
		void m68k_op_svs_8_pi7(void);
		void m68k_op_svs_8_pd(void);
		void m68k_op_svs_8_pd7(void);
		void m68k_op_svs_8_di(void);
		void m68k_op_svs_8_ix(void);
		void m68k_op_svs_8_aw(void);
		void m68k_op_svs_8_al(void);
		void m68k_op_spl_8_ai(void);
		void m68k_op_spl_8_pi(void);
		void m68k_op_spl_8_pi7(void);
		void m68k_op_spl_8_pd(void);
		void m68k_op_spl_8_pd7(void);
		void m68k_op_spl_8_di(void);
		void m68k_op_spl_8_ix(void);
		void m68k_op_spl_8_aw(void);
		void m68k_op_spl_8_al(void);
		void m68k_op_smi_8_ai(void);
		void m68k_op_smi_8_pi(void);
		void m68k_op_smi_8_pi7(void);
		void m68k_op_smi_8_pd(void);
		void m68k_op_smi_8_pd7(void);
		void m68k_op_smi_8_di(void);
		void m68k_op_smi_8_ix(void);
		void m68k_op_smi_8_aw(void);
		void m68k_op_smi_8_al(void);
		void m68k_op_sge_8_ai(void);
		void m68k_op_sge_8_pi(void);
		void m68k_op_sge_8_pi7(void);
		void m68k_op_sge_8_pd(void);
		void m68k_op_sge_8_pd7(void);
		void m68k_op_sge_8_di(void);
		void m68k_op_sge_8_ix(void);
		void m68k_op_sge_8_aw(void);
		void m68k_op_sge_8_al(void);
		void m68k_op_slt_8_ai(void);
		void m68k_op_slt_8_pi(void);
		void m68k_op_slt_8_pi7(void);
		void m68k_op_slt_8_pd(void);
		void m68k_op_slt_8_pd7(void);
		void m68k_op_slt_8_di(void);
		void m68k_op_slt_8_ix(void);
		void m68k_op_slt_8_aw(void);
		void m68k_op_slt_8_al(void);
		void m68k_op_sgt_8_ai(void);
		void m68k_op_sgt_8_pi(void);
		void m68k_op_sgt_8_pi7(void);
		void m68k_op_sgt_8_pd(void);
		void m68k_op_sgt_8_pd7(void);
		void m68k_op_sgt_8_di(void);
		void m68k_op_sgt_8_ix(void);
		void m68k_op_sgt_8_aw(void);
		void m68k_op_sgt_8_al(void);
		void m68k_op_sle_8_ai(void);
		void m68k_op_sle_8_pi(void);
		void m68k_op_sle_8_pi7(void);
		void m68k_op_sle_8_pd(void);
		void m68k_op_sle_8_pd7(void);
		void m68k_op_sle_8_di(void);
		void m68k_op_sle_8_ix(void);
		void m68k_op_sle_8_aw(void);
		void m68k_op_sle_8_al(void);
		void m68k_op_stop(void);
		void m68k_op_sub_8_er_d(void);
		void m68k_op_sub_8_er_ai(void);
		void m68k_op_sub_8_er_pi(void);
		void m68k_op_sub_8_er_pi7(void);
		void m68k_op_sub_8_er_pd(void);
		void m68k_op_sub_8_er_pd7(void);
		void m68k_op_sub_8_er_di(void);
		void m68k_op_sub_8_er_ix(void);
		void m68k_op_sub_8_er_aw(void);
		void m68k_op_sub_8_er_al(void);
		void m68k_op_sub_8_er_pcdi(void);
		void m68k_op_sub_8_er_pcix(void);
		void m68k_op_sub_8_er_i(void);
		void m68k_op_sub_16_er_d(void);
		void m68k_op_sub_16_er_a(void);
		void m68k_op_sub_16_er_ai(void);
		void m68k_op_sub_16_er_pi(void);
		void m68k_op_sub_16_er_pd(void);
		void m68k_op_sub_16_er_di(void);
		void m68k_op_sub_16_er_ix(void);
		void m68k_op_sub_16_er_aw(void);
		void m68k_op_sub_16_er_al(void);
		void m68k_op_sub_16_er_pcdi(void);
		void m68k_op_sub_16_er_pcix(void);
		void m68k_op_sub_16_er_i(void);
		void m68k_op_sub_32_er_d(void);
		void m68k_op_sub_32_er_a(void);
		void m68k_op_sub_32_er_ai(void);
		void m68k_op_sub_32_er_pi(void);
		void m68k_op_sub_32_er_pd(void);
		void m68k_op_sub_32_er_di(void);
		void m68k_op_sub_32_er_ix(void);
		void m68k_op_sub_32_er_aw(void);
		void m68k_op_sub_32_er_al(void);
		void m68k_op_sub_32_er_pcdi(void);
		void m68k_op_sub_32_er_pcix(void);
		void m68k_op_sub_32_er_i(void);
		void m68k_op_sub_8_re_ai(void);
		void m68k_op_sub_8_re_pi(void);
		void m68k_op_sub_8_re_pi7(void);
		void m68k_op_sub_8_re_pd(void);
		void m68k_op_sub_8_re_pd7(void);
		void m68k_op_sub_8_re_di(void);
		void m68k_op_sub_8_re_ix(void);
		void m68k_op_sub_8_re_aw(void);
		void m68k_op_sub_8_re_al(void);
		void m68k_op_sub_16_re_ai(void);
		void m68k_op_sub_16_re_pi(void);
		void m68k_op_sub_16_re_pd(void);
		void m68k_op_sub_16_re_di(void);
		void m68k_op_sub_16_re_ix(void);
		void m68k_op_sub_16_re_aw(void);
		void m68k_op_sub_16_re_al(void);
		void m68k_op_sub_32_re_ai(void);
		void m68k_op_sub_32_re_pi(void);
		void m68k_op_sub_32_re_pd(void);
		void m68k_op_sub_32_re_di(void);
		void m68k_op_sub_32_re_ix(void);
		void m68k_op_sub_32_re_aw(void);
		void m68k_op_sub_32_re_al(void);
		void m68k_op_suba_16_d(void);
		void m68k_op_suba_16_a(void);
		void m68k_op_suba_16_ai(void);
		void m68k_op_suba_16_pi(void);
		void m68k_op_suba_16_pd(void);
		void m68k_op_suba_16_di(void);
		void m68k_op_suba_16_ix(void);
		void m68k_op_suba_16_aw(void);
		void m68k_op_suba_16_al(void);
		void m68k_op_suba_16_pcdi(void);
		void m68k_op_suba_16_pcix(void);
		void m68k_op_suba_16_i(void);
		void m68k_op_suba_32_d(void);
		void m68k_op_suba_32_a(void);
		void m68k_op_suba_32_ai(void);
		void m68k_op_suba_32_pi(void);
		void m68k_op_suba_32_pd(void);
		void m68k_op_suba_32_di(void);
		void m68k_op_suba_32_ix(void);
		void m68k_op_suba_32_aw(void);
		void m68k_op_suba_32_al(void);
		void m68k_op_suba_32_pcdi(void);
		void m68k_op_suba_32_pcix(void);
		void m68k_op_suba_32_i(void);
		void m68k_op_subi_8_d(void);
		void m68k_op_subi_8_ai(void);
		void m68k_op_subi_8_pi(void);
		void m68k_op_subi_8_pi7(void);
		void m68k_op_subi_8_pd(void);
		void m68k_op_subi_8_pd7(void);
		void m68k_op_subi_8_di(void);
		void m68k_op_subi_8_ix(void);
		void m68k_op_subi_8_aw(void);
		void m68k_op_subi_8_al(void);
		void m68k_op_subi_16_d(void);
		void m68k_op_subi_16_ai(void);
		void m68k_op_subi_16_pi(void);
		void m68k_op_subi_16_pd(void);
		void m68k_op_subi_16_di(void);
		void m68k_op_subi_16_ix(void);
		void m68k_op_subi_16_aw(void);
		void m68k_op_subi_16_al(void);
		void m68k_op_subi_32_d(void);
		void m68k_op_subi_32_ai(void);
		void m68k_op_subi_32_pi(void);
		void m68k_op_subi_32_pd(void);
		void m68k_op_subi_32_di(void);
		void m68k_op_subi_32_ix(void);
		void m68k_op_subi_32_aw(void);
		void m68k_op_subi_32_al(void);
		void m68k_op_subq_8_d(void);
		void m68k_op_subq_8_ai(void);
		void m68k_op_subq_8_pi(void);
		void m68k_op_subq_8_pi7(void);
		void m68k_op_subq_8_pd(void);
		void m68k_op_subq_8_pd7(void);
		void m68k_op_subq_8_di(void);
		void m68k_op_subq_8_ix(void);
		void m68k_op_subq_8_aw(void);
		void m68k_op_subq_8_al(void);
		void m68k_op_subq_16_d(void);
		void m68k_op_subq_16_a(void);
		void m68k_op_subq_16_ai(void);
		void m68k_op_subq_16_pi(void);
		void m68k_op_subq_16_pd(void);
		void m68k_op_subq_16_di(void);
		void m68k_op_subq_16_ix(void);
		void m68k_op_subq_16_aw(void);
		void m68k_op_subq_16_al(void);
		void m68k_op_subq_32_d(void);
		void m68k_op_subq_32_a(void);
		void m68k_op_subq_32_ai(void);
		void m68k_op_subq_32_pi(void);
		void m68k_op_subq_32_pd(void);
		void m68k_op_subq_32_di(void);
		void m68k_op_subq_32_ix(void);
		void m68k_op_subq_32_aw(void);
		void m68k_op_subq_32_al(void);
		void m68k_op_subx_8_rr(void);
		void m68k_op_subx_16_rr(void);
		void m68k_op_subx_32_rr(void);
		void m68k_op_subx_8_mm_ax7(void);
		void m68k_op_subx_8_mm_ay7(void);
		void m68k_op_subx_8_mm_axy7(void);
		void m68k_op_subx_8_mm(void);
		void m68k_op_subx_16_mm(void);
		void m68k_op_subx_32_mm(void);
		void m68k_op_swap_32(void);
		void m68k_op_tas_8_d(void);
		void m68k_op_tas_8_ai(void);
		void m68k_op_tas_8_pi(void);
		void m68k_op_tas_8_pi7(void);
		void m68k_op_tas_8_pd(void);
		void m68k_op_tas_8_pd7(void);
		void m68k_op_tas_8_di(void);
		void m68k_op_tas_8_ix(void);
		void m68k_op_tas_8_aw(void);
		void m68k_op_tas_8_al(void);
		void m68k_op_trap(void);
		void m68k_op_trapt(void);
		void m68k_op_trapt_16(void);
		void m68k_op_trapt_32(void);
		void m68k_op_trapf(void);
		void m68k_op_trapf_16(void);
		void m68k_op_trapf_32(void);
		void m68k_op_traphi(void);
		void m68k_op_trapls(void);
		void m68k_op_trapcc(void);
		void m68k_op_trapcs(void);
		void m68k_op_trapne(void);
		void m68k_op_trapeq(void);
		void m68k_op_trapvc(void);
		void m68k_op_trapvs(void);
		void m68k_op_trappl(void);
		void m68k_op_trapmi(void);
		void m68k_op_trapge(void);
		void m68k_op_traplt(void);
		void m68k_op_trapgt(void);
		void m68k_op_traple(void);
		void m68k_op_traphi_16(void);
		void m68k_op_trapls_16(void);
		void m68k_op_trapcc_16(void);
		void m68k_op_trapcs_16(void);
		void m68k_op_trapne_16(void);
		void m68k_op_trapeq_16(void);
		void m68k_op_trapvc_16(void);
		void m68k_op_trapvs_16(void);
		void m68k_op_trappl_16(void);
		void m68k_op_trapmi_16(void);
		void m68k_op_trapge_16(void);
		void m68k_op_traplt_16(void);
		void m68k_op_trapgt_16(void);
		void m68k_op_traple_16(void);
		void m68k_op_traphi_32(void);
		void m68k_op_trapls_32(void);
		void m68k_op_trapcc_32(void);
		void m68k_op_trapcs_32(void);
		void m68k_op_trapne_32(void);
		void m68k_op_trapeq_32(void);
		void m68k_op_trapvc_32(void);
		void m68k_op_trapvs_32(void);
		void m68k_op_trappl_32(void);
		void m68k_op_trapmi_32(void);
		void m68k_op_trapge_32(void);
		void m68k_op_traplt_32(void);
		void m68k_op_trapgt_32(void);
		void m68k_op_traple_32(void);
		void m68k_op_trapv(void);
		void m68k_op_tst_8_d(void);
		void m68k_op_tst_8_ai(void);
		void m68k_op_tst_8_pi(void);
		void m68k_op_tst_8_pi7(void);
		void m68k_op_tst_8_pd(void);
		void m68k_op_tst_8_pd7(void);
		void m68k_op_tst_8_di(void);
		void m68k_op_tst_8_ix(void);
		void m68k_op_tst_8_aw(void);
		void m68k_op_tst_8_al(void);
		void m68k_op_tst_8_pcdi(void);
		void m68k_op_tst_8_pcix(void);
		void m68k_op_tst_8_i(void);
		void m68k_op_tst_16_d(void);
		void m68k_op_tst_16_a(void);
		void m68k_op_tst_16_ai(void);
		void m68k_op_tst_16_pi(void);
		void m68k_op_tst_16_pd(void);
		void m68k_op_tst_16_di(void);
		void m68k_op_tst_16_ix(void);
		void m68k_op_tst_16_aw(void);
		void m68k_op_tst_16_al(void);
		void m68k_op_tst_16_pcdi(void);
		void m68k_op_tst_16_pcix(void);
		void m68k_op_tst_16_i(void);
		void m68k_op_tst_32_d(void);
		void m68k_op_tst_32_a(void);
		void m68k_op_tst_32_ai(void);
		void m68k_op_tst_32_pi(void);
		void m68k_op_tst_32_pd(void);
		void m68k_op_tst_32_di(void);
		void m68k_op_tst_32_ix(void);
		void m68k_op_tst_32_aw(void);
		void m68k_op_tst_32_al(void);
		void m68k_op_tst_32_pcdi(void);
		void m68k_op_tst_32_pcix(void);
		void m68k_op_tst_32_i(void);
		void m68k_op_unlk_32_a7(void);
		void m68k_op_unlk_32(void);
		void m68k_op_unpk_16_rr(void);
		void m68k_op_unpk_16_mm_ax7(void);
		void m68k_op_unpk_16_mm_ay7(void);
		void m68k_op_unpk_16_mm_axy7(void);
		void m68k_op_unpk_16_mm(void);

		void d68000_1010(void);
		void d68000_1111(void);
		void d68000_abcd_rr(void);
		void d68000_abcd_mm(void);
		void d68000_add_er_8(void);
		void d68000_add_er_16(void);
		void d68000_add_er_32(void);
		void d68000_add_re_8(void);
		void d68000_add_re_16(void);
		void d68000_add_re_32(void);
		void d68000_adda_16(void);
		void d68000_adda_32(void);
		void d68000_addi_8(void);
		void d68000_addi_16(void);
		void d68000_addi_32(void);
		void d68000_addq_8(void);
		void d68000_addq_16(void);
		void d68000_addq_32(void);
		void d68000_addx_rr_8(void);
		void d68000_addx_rr_16(void);
		void d68000_addx_rr_32(void);
		void d68000_addx_mm_8(void);
		void d68000_addx_mm_16(void);
		void d68000_addx_mm_32(void);
		void d68000_and_er_8(void);
		void d68000_and_er_16(void);
		void d68000_and_er_32(void);
		void d68000_and_re_8(void);
		void d68000_and_re_16(void);
		void d68000_and_re_32(void);
		void d68000_andi_8(void);
		void d68000_andi_16(void);
		void d68000_andi_32(void);
		void d68000_andi_to_ccr(void);
		void d68000_andi_to_sr(void);
		void d68000_asr_s_8(void);
		void d68000_asr_s_16(void);
		void d68000_asr_s_32(void);
		void d68000_asr_r_8(void);
		void d68000_asr_r_16(void);
		void d68000_asr_r_32(void);
		void d68000_asr_ea(void);
		void d68000_asl_s_8(void);
		void d68000_asl_s_16(void);
		void d68000_asl_s_32(void);
		void d68000_asl_r_8(void);
		void d68000_asl_r_16(void);
		void d68000_asl_r_32(void);
		void d68000_asl_ea(void);
		void d68000_bcc_8(void);
		void d68000_bcc_16(void);
		void d68020_bcc_32(void);
		void d68000_bchg_r(void);
		void d68000_bchg_s(void);
		void d68000_bclr_r(void);
		void d68000_bclr_s(void);
		void d68010_bkpt(void);
		void d68020_bfchg(void);
		void d68020_bfclr(void);
		void d68020_bfexts(void);
		void d68020_bfextu(void);
		void d68020_bfffo(void);
		void d68020_bfins(void);
		void d68020_bfset(void);
		void d68020_bftst(void);
		void d68000_bra_8(void);
		void d68000_bra_16(void);
		void d68020_bra_32(void);
		void d68000_bset_r(void);
		void d68000_bset_s(void);
		void d68000_bsr_8(void);
		void d68000_bsr_16(void);
		void d68020_bsr_32(void);
		void d68000_btst_r(void);
		void d68000_btst_s(void);
		void d68020_callm(void);
		void d68020_cas_8(void);
		void d68020_cas_16(void);
		void d68020_cas_32(void);
		void d68020_cas2_16(void);
		void d68020_cas2_32(void);
		void d68000_chk_16(void);
		void d68020_chk_32(void);
		void d68020_chk2_cmp2_8(void);
		void d68020_chk2_cmp2_16(void);
		void d68020_chk2_cmp2_32(void);
		void d68040_cinv(void);
		void d68000_clr_8(void);
		void d68000_clr_16(void);
		void d68000_clr_32(void);
		void d68000_cmp_8(void);
		void d68000_cmp_16(void);
		void d68000_cmp_32(void);
		void d68000_cmpa_16(void);
		void d68000_cmpa_32(void);
		void d68000_cmpi_8(void);
		void d68020_cmpi_pcdi_8(void);
		void d68020_cmpi_pcix_8(void);
		void d68000_cmpi_16(void);
		void d68020_cmpi_pcdi_16(void);
		void d68020_cmpi_pcix_16(void);
		void d68000_cmpi_32(void);
		void d68020_cmpi_pcdi_32(void);
		void d68020_cmpi_pcix_32(void);
		void d68000_cmpm_8(void);
		void d68000_cmpm_16(void);
		void d68000_cmpm_32(void);
		void d68020_cpbcc_16(void);
		void d68020_cpbcc_32(void);
		void d68020_cpdbcc(void);
		void d68020_cpgen(void);
		void d68020_cprestore(void);
		void d68020_cpsave(void);
		void d68020_cpscc(void);
		void d68020_cptrapcc_0(void);
		void d68020_cptrapcc_16(void);
		void d68020_cptrapcc_32(void);
		void d68040_cpush(void);
		void d68000_dbra(void);
		void d68000_dbcc(void);
		void d68000_divs(void);
		void d68000_divu(void);
		void d68020_divl(void);
		void d68000_eor_8(void);
		void d68000_eor_16(void);
		void d68000_eor_32(void);
		void d68000_eori_8(void);
		void d68000_eori_16(void);
		void d68000_eori_32(void);
		void d68000_eori_to_ccr(void);
		void d68000_eori_to_sr(void);
		void d68000_exg_dd(void);
		void d68000_exg_aa(void);
		void d68000_exg_da(void);
		void d68000_ext_16(void);
		void d68000_ext_32(void);
		void d68020_extb_32(void);
		void d68000_jmp(void);
		void d68000_jsr(void);
		void d68000_lea(void);
		void d68000_link_16(void);
		void d68020_link_32(void);
		void d68000_lsr_s_8(void);
		void d68000_lsr_s_16(void);
		void d68000_lsr_s_32(void);
		void d68000_lsr_r_8(void);
		void d68000_lsr_r_16(void);
		void d68000_lsr_r_32(void);
		void d68000_lsr_ea(void);
		void d68000_lsl_s_8(void);
		void d68000_lsl_s_16(void);
		void d68000_lsl_s_32(void);
		void d68000_lsl_r_8(void);
		void d68000_lsl_r_16(void);
		void d68000_lsl_r_32(void);
		void d68000_lsl_ea(void);
		void d68000_move_8(void);
		void d68000_move_16(void);
		void d68000_move_32(void);
		void d68000_movea_16(void);
		void d68000_movea_32(void);
		void d68000_move_to_ccr(void);
		void d68010_move_fr_ccr(void);
		void d68000_move_fr_sr(void);
		void d68000_move_to_sr(void);
		void d68000_move_fr_usp(void);
		void d68000_move_to_usp(void);
		void d68010_movec(void);
		void d68000_movem_pd_16(void);
		void d68000_movem_pd_32(void);
		void d68000_movem_er_16(void);
		void d68000_movem_er_32(void);
		void d68000_movem_re_16(void);
		void d68000_movem_re_32(void);
		void d68000_movep_re_16(void);
		void d68000_movep_re_32(void);
		void d68000_movep_er_16(void);
		void d68000_movep_er_32(void);
		void d68010_moves_8(void);
		void d68010_moves_16(void);
		void d68010_moves_32(void);
		void d68000_moveq(void);
		void d68040_move16_pi_pi(void);
		void d68040_move16_pi_al(void);
		void d68040_move16_al_pi(void);
		void d68040_move16_ai_al(void);
		void d68040_move16_al_ai(void);
		void d68000_muls(void);
		void d68000_mulu(void);
		void d68020_mull(void);
		void d68000_nbcd(void);
		void d68000_neg_8(void);
		void d68000_neg_16(void);
		void d68000_neg_32(void);
		void d68000_negx_8(void);
		void d68000_negx_16(void);
		void d68000_negx_32(void);
		void d68000_nop(void);
		void d68000_not_8(void);
		void d68000_not_16(void);
		void d68000_not_32(void);
		void d68000_or_er_8(void);
		void d68000_or_er_16(void);
		void d68000_or_er_32(void);
		void d68000_or_re_8(void);
		void d68000_or_re_16(void);
		void d68000_or_re_32(void);
		void d68000_ori_8(void);
		void d68000_ori_16(void);
		void d68000_ori_32(void);
		void d68000_ori_to_ccr(void);
		void d68000_ori_to_sr(void);
		void d68020_pack_rr(void);
		void d68020_pack_mm(void);
		void d68000_pea(void);
		void d68000_reset(void);
		void d68000_ror_s_8(void);
		void d68000_ror_s_16(void);
		void d68000_ror_s_32(void);
		void d68000_ror_r_8(void);
		void d68000_ror_r_16(void);
		void d68000_ror_r_32(void);
		void d68000_ror_ea(void);
		void d68000_rol_s_8(void);
		void d68000_rol_s_16(void);
		void d68000_rol_s_32(void);
		void d68000_rol_r_8(void);
		void d68000_rol_r_16(void);
		void d68000_rol_r_32(void);
		void d68000_rol_ea(void);
		void d68000_roxr_s_8(void);
		void d68000_roxr_s_16(void);
		void d68000_roxr_s_32(void);
		void d68000_roxr_r_8(void);
		void d68000_roxr_r_16(void);
		void d68000_roxr_r_32(void);
		void d68000_roxr_ea(void);
		void d68000_roxl_s_8(void);
		void d68000_roxl_s_16(void);
		void d68000_roxl_s_32(void);
		void d68000_roxl_r_8(void);
		void d68000_roxl_r_16(void);
		void d68000_roxl_r_32(void);
		void d68000_roxl_ea(void) ;
		void d68010_rtd(void);
		void d68000_rte(void);
		void d68020_rtm(void);
		void d68000_rtr(void);
		void d68000_rts(void);
		void d68000_sbcd_rr(void);
		void d68000_sbcd_mm(void);
		void d68000_scc(void);
		void d68000_stop(void);
		void d68000_sub_er_8(void);
		void d68000_sub_er_16(void);
		void d68000_sub_er_32(void);
		void d68000_sub_re_8(void);
		void d68000_sub_re_16(void);
		void d68000_sub_re_32(void);
		void d68000_suba_16(void);
		void d68000_suba_32(void);
		void d68000_subi_8(void);
		void d68000_subi_16(void);
		void d68000_subi_32(void);
		void d68000_subq_8(void);
		void d68000_subq_16(void);
		void d68000_subq_32(void);
		void d68000_subx_rr_8(void);
		void d68000_subx_rr_16(void);
		void d68000_subx_rr_32(void);
		void d68000_subx_mm_8(void);
		void d68000_subx_mm_16(void);
		void d68000_subx_mm_32(void);
		void d68000_swap(void);
		void d68000_tas(void);
		void d68000_trap(void);
		void d68020_trapcc_0(void);
		void d68020_trapcc_16(void);
		void d68020_trapcc_32(void);
		void d68000_trapv(void);
		void d68000_tst_8(void);
		void d68020_tst_pcdi_8(void);
		void d68020_tst_pcix_8(void);
		void d68020_tst_i_8(void);
		void d68000_tst_16(void);
		void d68020_tst_a_16(void);
		void d68020_tst_pcdi_16(void);
		void d68020_tst_pcix_16(void);
		void d68020_tst_i_16(void);
		void d68000_tst_32(void);
		void d68020_tst_a_32(void);
		void d68020_tst_pcdi_32(void);
		void d68020_tst_pcix_32(void);
		void d68020_tst_i_32(void);
		void d68000_unlk(void);
		void d68020_unpk_rr(void);
		void d68020_unpk_mm(void);
		void d68000_illegal(void);

		int make_int_8(int value);
		int make_int_16(int value);
		char* make_signed_hex_str_8(uint val);
		char* make_signed_hex_str_16(uint val);
		char* make_signed_hex_str_32(uint val);
		char* get_imm_str_s(uint size);
		char* get_imm_str_u(uint size);
		char* get_ea_mode_str(uint instruction, uint size);

	public:
		/* used to build opcode handler jump table */
		typedef struct
		{
			void (mc68000::*opcode_handler)(void); /* handler function */
			uint mask;                    /* mask on opcode */
			uint match;                   /* what to match after masking */
			uint ea_mask;                 /* what ea modes are allowed */
		} opcode_struct;
		typedef struct
		{
			void (mc68000::*opcode_handler)(void); /* handler function */
			uint mask;			/* mask on opcode */
			uint match;			/* what to match after masking */
			unsigned char cycles[NUM_CPU_TYPES]; /* cycles each cpu type takes */
		} opcode_handler_struct;

		static opcode_handler_struct m68k_opcode_handler_table[];
		static opcode_struct g_opcode_info[];

		typedef struct
		{
			uint cpu_type;     /* CPU Type: 68000, 68010, 68EC020, or 68020 */
			uint dar[16];      /* Data and Address Registers */
			uint ppc;		   /* Previous program counter */
			uint pc;           /* Program Counter */
			uint sp[7];        /* User, Interrupt, and Master Stack Pointers */
			uint vbr;          /* Vector Base Register (m68010+) */
			uint sfc;          /* Source Function Code Register (m68010+) */
			uint dfc;          /* Destination Function Code Register (m68010+) */
			uint cacr;         /* Cache Control Register (m68020, unemulated) */
			uint caar;         /* Cache Address Register (m68020, unemulated) */
			uint ir;           /* Instruction Register */
			uint t1_flag;      /* Trace 1 */
			uint t0_flag;      /* Trace 0 */
			uint s_flag;       /* Supervisor */
			uint m_flag;       /* Master/Interrupt state */
			uint x_flag;       /* Extend */
			uint n_flag;       /* Negative */
			uint not_z_flag;   /* Zero, inverted for speedups */
			uint v_flag;       /* Overflow */
			uint c_flag;       /* Carry */
			uint int_mask;     /* I0-I2 */
			uint int_level;    /* State of interrupt pins IPL0-IPL2 -- ASG: changed from ints_pending */
			uint int_cycles;   /* ASG: extra cycles from generated interrupts */
			uint stopped;      /* Stopped state */
			uint pref_addr;    /* Last prefetch address */
			uint pref_data;    /* Data in the prefetch queue */
			uint address_mask; /* Available address pins */
			uint sr_mask;      /* Implemented status register bits */
		
			/* Clocks required for instructions / exceptions */
			uint cyc_bcc_notake_b;
			uint cyc_bcc_notake_w;
			uint cyc_dbcc_f_noexp;
			uint cyc_dbcc_f_exp;
			uint cyc_scc_r_false;
			uint cyc_movem_w;
			uint cyc_movem_l;
			uint cyc_shift;
			uint cyc_reset;
			uint8* cyc_instruction;
			uint8* cyc_exception;
		
			/* Callbacks to host */
			int  (*int_ack_callback)(int int_line);           /* Interrupt Acknowledge */
			void (*bkpt_ack_callback)(unsigned int data);     /* Breakpoint Acknowledge */
			void (*reset_instr_callback)(void);               /* Called when a RESET instruction is encountered */
			void (*pc_changed_callback)(unsigned int new_pc); /* Called when the PC changes by a large amount */
			void (*set_fc_callback)(unsigned int new_fc);     /* Called when the CPU function code changes */
			void (*instr_hook_callback)(int cycles);                /* Called every instruction cycle prior to execution */
		
		} m68ki_cpu_core;

		m68ki_cpu_core 	m68ki_cpu;
		uint 			m68ki_address_space = 0;
		sint 			m68ki_remaining_cycles = 0;

		static uint8          	m68ki_shift_8_table[];
		static uint16         	m68ki_shift_16_table[];
		static uint           	m68ki_shift_32_table[];

/*
extern uint           m68ki_tracing;
extern uint8          m68ki_exception_cycle_table[][256];
extern uint8          m68ki_ea_idx_cycle_table[];
*/

		unsigned char m68ki_cycles[NUM_CPU_TYPES][0x10000]; /* Cycles used by CPU type */
		void (mc68000::*m68ki_instruction_jump_table[0x10000])(void); /* opcode handler jump table */
		void (mc68000::*g_instruction_table[0x10000])(void);

		int g_initialized = 0;

		uint *m68k_cpu_dar[2];
		uint* m68k_movem_pi_table[16];
		uint* m68k_movem_pd_table[16];

		unsigned int m68k_get_reg(void* context, m68k_register_t regnum);
		void m68k_set_reg(m68k_register_t regnum, unsigned int value);
		void m68k_set_int_ack_callback(int  (*callback)(int int_level));
		void m68k_set_bkpt_ack_callback(void  (*callback)(unsigned int data));
		void m68k_set_reset_instr_callback(void  (*callback)(void));
		void m68k_set_pc_changed_callback(void  (*callback)(unsigned int new_pc));
		void m68k_set_fc_callback(void  (*callback)(unsigned int new_fc));
		void m68k_set_instr_hook_callback(void  (*callback)(int cycles));
		void m68k_set_cpu_type(unsigned int cpu_type);
		int m68k_cycles_run(void);
		int m68k_cycles_remaining(void);
		void m68k_modify_timeslice(int cycles);
		void m68k_end_timeslice(void);
		void m68k_set_irq(unsigned int int_level);
		void m68k_pulse_reset(void);
		void m68k_pulse_halt(void);
		unsigned int m68k_context_size();
		unsigned int m68k_get_context(void* dst);
		void m68k_set_context(void* src);
		int m68k_execute(int num_cycles);

		unsigned int m68k_disassemble(char* str_buff, unsigned int pc, unsigned int cpu_type);

		virtual int __fastcall		cpu_irq_ack(int level) = 0;
		virtual void __fastcall		cpu_set_fc(int discard) = 0;
		virtual void __fastcall		cpu_inst_hook(int cycles) = 0;
		virtual void __fastcall		cpu_pulse_reset(void) = 0;
		virtual UINT8 __fastcall 	cpu_read_byte(int address) = 0;
		virtual UINT16 __fastcall 	cpu_read_word(int address) = 0;
		virtual UINT32 __fastcall 	cpu_read_long(int address) = 0;
		virtual void __fastcall 	cpu_write_byte(int address, UINT8 value) = 0;
		virtual void __fastcall 	cpu_write_word(int address, UINT16 value) = 0;
		virtual void __fastcall 	cpu_write_long(int address, UINT32 value) = 0;
};

#endif // m68kcpuH

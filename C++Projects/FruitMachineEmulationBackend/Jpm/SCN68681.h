//	Original (C) C.J.Wren 2000
//  Modified Nick Sanders June/July 2014
#pragma once

#include "LoadSave.h"
#include "EDC.h"

// IMR/ISR Register
#define TxRDYA 0x1
#define RxRDYA 0x2
#define DBA    0x4
#define CNTRDY 0x8
#define TxRDYB 0x10
#define RxRDYB 0x20
#define DBB    0x40
#define IPC    0x80

// SR Register
#define RxRDY  0x1
#define FFULL  0x2
#define TxRDY  0x4
#define TxEMT  0x8
#define OVR    0x10
#define PE     0x20
#define FE     0x40
#define RB     0x40

#define BUFFER_SIZE 256

class DuartScn68681 {

// Internal registers
private:
    UINT8 mr1a = 0, mr1b = 0;
    UINT8 sra = 0, srb = 0;
    UINT8 mr2a = 0, mr2b = 0;
    UINT8 ipcr = 0, opcr = 0;
    UINT8 csra = 0, csrb = 0;
    UINT8 cra = 0, crb = 0;
    
    INT32 prescaler = 0;
    bool toggle = 0;
    INT32 clk = 0;
    bool RxA = 0, RxB = 0, TxA = 0, TxB = 0;

    EDCUNIT EDC;

public:
    UINT8 acr = 0;
    UINT8 ctur = 0, ctlr = 0;
    UINT8 isr = 0, imr = 0, ivr = 0;
    UINT8 ip = 0, op = 0;
    UINT8 opr = 0;
	UINT8 tba = 0, tbb = 0;
    UINT8 rba = 0, rbb = 0;
    UINT8 op_changed = 0;
    INT16 counter = 0;
    UINT8 bufferA[BUFFER_SIZE], lenA = 0;
    UINT8 send_buffA[BUFFER_SIZE];
    UINT8 bufferB[BUFFER_SIZE], lenB = 0;
    UINT8 send_buffB[BUFFER_SIZE];
    bool recva = 0, recvb = 0;

// Initialisation functions
protected:
    UINT8 chana = 0, chanb = 0;
    bool counter_running = 0;
    bool transmitA = 0, delayA = 0;
    bool transmitB = 0, delayB = 0;
    UINT8 rtsa = 0, rtsb = 0;
    UINT8 txa = 0, txb = 0;
    UINT8 buffposA = 0, send_countA = 0, send_posA = 0, send_delayA = 0;
    UINT8 buffposB = 0, send_countB = 0, send_posB = 0, send_delayB = 0;


// Read and write functions
public:
    void __fastcall ReceiveCharA(UINT8 ch);
    void __fastcall ReceiveCharB(UINT8 ch);
	void __fastcall reset(LoadSaveClass * LSCIn);
    void __fastcall tick(unsigned int num_clks);
    void __fastcall write(UINT8 offset, unsigned int value);
    unsigned int __fastcall read(UINT8 addr);

	void __fastcall SaveState();
	void __fastcall LoadState();

    UINT8* __fastcall GetEDCString(void);
    UINT8 __fastcall GetEDCAvailable(void);

private:
	LoadSaveClass * LSC;
};
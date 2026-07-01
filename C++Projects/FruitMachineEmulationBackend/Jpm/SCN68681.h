//	Original (C) C.J.Wren 2000
//  Modified Nick Sanders June/July 2014

#ifndef DuartH
#define DuartH

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
    unsigned char mr1a, mr1b;
    unsigned char sra, srb;
    unsigned char mr2a, mr2b;
    unsigned char ipcr, opcr;
    unsigned char csra, csrb;
    unsigned char cra, crb;
    
    int prescaler;
    bool toggle;
    int clk;
    bool RxA, RxB, TxA, TxB;

    EDCUNIT EDC;

public:
    unsigned char acr;
    unsigned char ctur, ctlr;
    unsigned char isr, imr, ivr;
    unsigned char ip, op;
    unsigned char opr;
	unsigned char tba, tbb;
    unsigned char rba, rbb;
    unsigned char op_changed;
    signed short counter;
    unsigned char bufferA[BUFFER_SIZE], lenA;
    unsigned char send_buffA[BUFFER_SIZE];
    unsigned char bufferB[BUFFER_SIZE], lenB;
    unsigned char send_buffB[BUFFER_SIZE];
    bool recva, recvb;

// Initialisation functions
protected:
    unsigned char chana, chanb;
    bool counter_running;
    bool transmitA, delayA;
    bool transmitB, delayB;
    unsigned char rtsa, rtsb;
    unsigned char txa, txb;
    unsigned char buffposA, send_countA, send_posA, send_delayA;
    unsigned char buffposB, send_countB, send_posB, send_delayB;


// Read and write functions
public:
    void ReceiveCharA(unsigned char ch);
    void ReceiveCharB(unsigned char ch);
	void reset(LoadSaveClass * LSCIn);
    void tick(unsigned int num_clks);
    void write(unsigned char offset, unsigned int value);
    unsigned int read(unsigned char addr);

	void SaveState();
	void LoadState();

    char * GetEDCString(void);
    UINT8 GetEDCAvailable(void);

private:
	LoadSaveClass * LSC;
};

#endif DuartH
//	Original (C) C.J.Wren 2000
//  Modified June/July 2014

#include "StdAfx.h"
#include "SCN68681.h"




void DuartScn68681::reset(LoadSaveClass * LSCIn)
{
	
	LSC = LSCIn;
    EDC.Reset(LSCIn);

	ivr = 0xf;
	counter = 0;
	chana = 0;
	chanb = 0;
	counter_running = false;
	imr = 0;
	isr = 0;
	acr = 0;
	opr = 0;
	mr1a = mr1b = 0;
	mr2a = mr2b = 0;
	csra = csrb = 0;
	sra = srb = 0;
	ip = 0x20;
	clk = 0;
	prescaler = 0;
	op = 0xff;
	txa = txb = 0;
	TxA = TxB = RxA = RxB = false;
	recva = recvb = false;
	buffposA = buffposB = 0;
}

void DuartScn68681::SaveState(){
	
    LSC->SaveToBuffer(mr1a);
	LSC->SaveToBuffer(mr1b);
	LSC->SaveToBuffer(sra);
	LSC->SaveToBuffer(srb);
	LSC->SaveToBuffer(mr2a);
	LSC->SaveToBuffer(mr2b);
	LSC->SaveToBuffer(ipcr);
	LSC->SaveToBuffer(opcr);
	LSC->SaveToBuffer(csra);
	LSC->SaveToBuffer(csrb);
	LSC->SaveToBuffer(cra);
	LSC->SaveToBuffer(crb);    
	LSC->SaveToBuffer(prescaler);
	LSC->SaveToBuffer(toggle);
	LSC->SaveToBuffer(clk);
	LSC->SaveToBuffer(RxA);
	LSC->SaveToBuffer(RxB);
	LSC->SaveToBuffer(TxA); 
	LSC->SaveToBuffer(TxB);
	LSC->SaveToBuffer(acr);
	LSC->SaveToBuffer(ctur);
	LSC->SaveToBuffer(ctlr);
	LSC->SaveToBuffer(isr); 
	LSC->SaveToBuffer(imr); 
	LSC->SaveToBuffer(ivr);
	LSC->SaveToBuffer(ip);
	LSC->SaveToBuffer(op);
	LSC->SaveToBuffer(opr);
	LSC->SaveToBuffer(tba);
	LSC->SaveToBuffer(tbb);
	LSC->SaveToBuffer(rba);
	LSC->SaveToBuffer(rbb);
	LSC->SaveToBuffer(op_changed);
	LSC->SaveToBuffer(counter);
	LSC->SaveToBuffer(lenA);	
	LSC->SaveToBuffer(lenB);	
	LSC->SaveToBuffer(recva);
	LSC->SaveToBuffer(recvb);
	LSC->SaveToBuffer(chana);
	LSC->SaveToBuffer(chanb);
	LSC->SaveToBuffer(counter_running);
	LSC->SaveToBuffer(transmitA);
	LSC->SaveToBuffer(delayA);
	LSC->SaveToBuffer(transmitB);
	LSC->SaveToBuffer(delayB);
	LSC->SaveToBuffer(rtsa);
	LSC->SaveToBuffer(rtsb);
	LSC->SaveToBuffer(txa);
	LSC->SaveToBuffer(txb);
	LSC->SaveToBuffer(buffposA);
	LSC->SaveToBuffer(send_countA);
	LSC->SaveToBuffer(send_posA);
	LSC->SaveToBuffer(send_delayA);
	LSC->SaveToBuffer(buffposB);
	LSC->SaveToBuffer(send_countB);
	LSC->SaveToBuffer(send_posB);
	LSC->SaveToBuffer(send_delayB);	

	for (int i = 0; i < BUFFER_SIZE; i++){
		LSC->SaveToBuffer(bufferA[i]);
		LSC->SaveToBuffer(send_buffA[i]);
		LSC->SaveToBuffer(bufferB[i]);
        LSC->SaveToBuffer(send_buffB[i]);    
	}

}
void DuartScn68681::LoadState(){

	LSC->LoadFromBuffer(mr1a);
	LSC->LoadFromBuffer(mr1b);
	LSC->LoadFromBuffer(sra);
	LSC->LoadFromBuffer(srb);
	LSC->LoadFromBuffer(mr2a);
	LSC->LoadFromBuffer(mr2b);
	LSC->LoadFromBuffer(ipcr);
	LSC->LoadFromBuffer(opcr);
	LSC->LoadFromBuffer(csra);
	LSC->LoadFromBuffer(csrb);
	LSC->LoadFromBuffer(cra);
	LSC->LoadFromBuffer(crb);    
	LSC->LoadFromBuffer(prescaler);
	LSC->LoadFromBuffer(toggle);
	LSC->LoadFromBuffer(clk);
	LSC->LoadFromBuffer(RxA);
	LSC->LoadFromBuffer(RxB);
	LSC->LoadFromBuffer(TxA); 
	LSC->LoadFromBuffer(TxB);
	LSC->LoadFromBuffer(acr);
	LSC->LoadFromBuffer(ctur);
	LSC->LoadFromBuffer(ctlr);
	LSC->LoadFromBuffer(isr); 
	LSC->LoadFromBuffer(imr); 
	LSC->LoadFromBuffer(ivr);
	LSC->LoadFromBuffer(ip);
	LSC->LoadFromBuffer(op);
	LSC->LoadFromBuffer(opr);
	LSC->LoadFromBuffer(tba);
	LSC->LoadFromBuffer(tbb);
	LSC->LoadFromBuffer(rba);
	LSC->LoadFromBuffer(rbb);
	LSC->LoadFromBuffer(op_changed);
	LSC->LoadFromBuffer(counter);
	LSC->LoadFromBuffer(lenA);	
	LSC->LoadFromBuffer(lenB);	
	LSC->LoadFromBuffer(recva);
	LSC->LoadFromBuffer(recvb);
	LSC->LoadFromBuffer(chana);
	LSC->LoadFromBuffer(chanb);
	LSC->LoadFromBuffer(counter_running);
	LSC->LoadFromBuffer(transmitA);
	LSC->LoadFromBuffer(delayA);
	LSC->LoadFromBuffer(transmitB);
	LSC->LoadFromBuffer(delayB);
	LSC->LoadFromBuffer(rtsa);
	LSC->LoadFromBuffer(rtsb);
	LSC->LoadFromBuffer(txa);
	LSC->LoadFromBuffer(txb);
	LSC->LoadFromBuffer(buffposA);
	LSC->LoadFromBuffer(send_countA);
	LSC->LoadFromBuffer(send_posA);
	LSC->LoadFromBuffer(send_delayA);
	LSC->LoadFromBuffer(buffposB);
	LSC->LoadFromBuffer(send_countB);
	LSC->LoadFromBuffer(send_posB);
	LSC->LoadFromBuffer(send_delayB);	
    

	for (int i = 0; i < BUFFER_SIZE; i++){
		LSC->LoadFromBuffer(bufferA[i]);
		LSC->LoadFromBuffer(send_buffA[i]);
		LSC->LoadFromBuffer(bufferB[i]);
		LSC->LoadFromBuffer(send_buffB[i]);        
	}

}

UINT8 * DuartScn68681::GetEDCString(void){
       
    return 0;

}

UINT8 DuartScn68681::GetEDCAvailable(void)
{
    return 0;
}

void DuartScn68681::tick(unsigned int num_clks)
{

int diff = 0;
bool check = true;

  clk = num_clks;

  if ( ( acr & 0x30 ) == 0x30 || ( acr & 0x60 ) == 0x60 )
  {
    while ( num_clks ) {
      if ( acr & 0x10 ) { // Do Prescaler
        prescaler++;
        if ( prescaler > 15 ) {
          prescaler = 0;
          counter--;
        }
      } else
        counter--;
      if ( !counter )
      {
        isr |= 0x8;
        counter = (ctur << 8) + ctlr;
        counter -= diff;
      }
      num_clks--;
    }
  }
  if ( txa ) {
    txa--;
    if ( !txa && (sra & TxRDY) ) {
      sra |= TxEMT; // Temp
      recva = true;
      if ( mr2a & 0x20 ) {
        TxA = false;
        rtsa = 0;
        isr &= ~TxRDYA;
        sra &= ~TxRDY;
      }
    }
  } else {
    if ( (sra & TxRDY) == 0 && TxA ) {
      bufferA[buffposA++] = tba;
      if ( buffposA++ >= BUFFER_SIZE)
        buffposA = 0;
      txa = 100;  //100
      sra |= TxRDY;
      isr |= TxRDYA;
    }
  }
  if ( txb ) {
    txb--;
    if ( !txb && (srb & TxRDY) ) {
      srb |= TxEMT; // Temp
      recvb = true;
      lenB = buffposB;
      buffposB = 0;
      if ( mr2b & 0x20 ) {
        TxB = false;
        rtsb = 0;
        isr &= ~TxRDYB;
        srb &= ~TxRDY;
      }
    }
  } else {
    if ( (srb & TxRDY) == 0 && TxB ) {
      bufferB[buffposB++] = tbb;
      if ( buffposB++ >= BUFFER_SIZE)
        buffposB = 0;
      txb = 100;
      srb |= TxRDY;
      isr |= TxRDYB;
    }
  }
}

void DuartScn68681::write(unsigned char offset, unsigned int value)
{
	unsigned char old_op;

  op_changed = 0;
  
  switch ( offset ) {
    case 0:
      if ( !chana )
        mr1a = value;
      else
        mr2a = value;
      chana = 1;
      break;
    case 1:
      csra = value;
      break;
    case 2:
      cra = value;
      if ( value & 1 ) {
        RxA = true;
      }
      if ( value & 2 ) {
        RxA = false;
      }
      if ( value & 4 ) {
        TxA = true;
        isr |= TxRDYA;
        sra |= (TxRDY + TxEMT);
      }
      if ( value & 8 ) {
        TxA = false;
        isr &= ~TxRDYA;
        sra &= ~(TxRDY + TxEMT);
      }
      switch ( (value & 0x70) >> 4 ) {
        case 0:
          break;
        case 1:
          chana = 0;
          break;
        case 2:
          RxA = false;
          break;
        case 3:
          TxA = false;
          isr &= ~TxRDYA;
          sra &= ~(TxRDY + TxEMT);
          break;
        case 4:
          sra &= 0x0f; // Reset Error Status
          break;
        case 5:
          isr &= ~DBA;
          break;
        case 6:
          // Start Break;
          break;
        case 7:
          // Stop Break
          break;
      }
      break;
    case 3:
      tba = value;
      if ( (mr2a & 0x80) == 0x80 ) { // Local Loopback
        rba = tba;
        sra |= (RxRDY + FFULL);
        isr |= RxRDYA;
      } else {
        sra &= ~(TxRDY + TxEMT);
        isr &= ~TxRDYA;

		EDC.Write(value);
        
      }

      break;
    case 4:
      acr = value;
      break;
    case 5:
      imr = value;
      break;
    case 6:
      ctur = value;
      break;
    case 7:
      ctlr = value;
      break;
    case 8:
      if ( !chanb )
        mr1b = value;
      else
        mr2b = value;
      chanb = 1;
      break;
    case 9:
      csrb = value;
      break;
    case 0x0a:
      crb = value;
      if ( value & 1 ) {
        RxB = true;
      }
      if ( value & 2 ) {
        RxB = false;
      }
      if ( value & 4 ) {
        TxB = true;
        isr |= TxRDYB;
        srb |= (TxRDY + TxEMT);
      }
      if ( value & 8 ) {
        TxB = false;
        isr &= ~TxRDYB;
        srb &= ~(TxRDY + TxEMT);
      }
      switch ( (value & 0x70) >> 4 ) {
        case 0:
          break;
        case 1:
          chanb = 0;
          break;
        case 2:
          RxB = false;
          break;
        case 3:
          TxB = false;
          isr &= ~TxRDYB;
          srb &= ~(TxRDY + TxEMT);
          break;
        case 4:
          srb &= 0x0f; // Reset Error Status
          break;
        case 5:
          isr &= ~DBB;
          break;
        case 6:
          // Start Break;
          break;
        case 7:
          // Stop Break
          break;
      }
      break;
    case 0x0b:
      tbb = value;
      if ( (mr2b & 0x80) == 0x80 ) { // Local Loopback
        rbb = tbb;
        srb |= (RxRDY + FFULL); // RxRDY FFULL
        isr |= RxRDYB;
      } else {
        srb &= ~(TxRDY + TxEMT);
        isr &= ~TxRDYB;
      }
      break;
    case 0x0c:
      ivr = value;
      break;
    case 0x0d:
      opcr = value;
      break;
    case 0x0e:
      old_op = opr;
      opr = opr | value;
      op = ~opr;
      op_changed = old_op ^ opr;
      break;
    case 0x0f:
      old_op = opr;
      opr = opr & ~value;
      op = ~opr;
      op_changed = old_op ^ opr;
	  
      break;
  }
}

unsigned int DuartScn68681::read(unsigned char addr)
{
unsigned int value = 0;

  switch ( addr ) {
    case 0:
      if ( !chana )
        value = mr1a;
      else
        value = mr2a;
      chana = 1;
      break;
    case 1:
      value = sra;
      break;
    case 2:
      value = 0;
      break;
    case 3:
      value = rba;
      isr &= ~RxRDYA;
      sra &= ~(FFULL + RxRDY);
      break;
    case 4:
      value = ipcr;
      isr &= ~IPC;
      break;
    case 5:
      value = isr;
      break;
    case 6:
      value = counter >> 8;
      break;
    case 7:
      value = counter & 0xff;
      break;
    case 8:
      if ( !chanb )
        value = mr1b;
      else
        value = mr2b;
      chanb = 1;
      break;
    case 9:
      value = srb;
      break;
    case 0x0a:
      value = 0;
      break;
    case 0x0b:
      value = rbb;
      isr &= ~RxRDYB;
      sra &= ~(FFULL + RxRDY);
      break;
    case 0x0c:
      value = ivr;
      break;
    case 0x0d:
      value = (ip);//The state of IACK should be shown in bit 6
      break;
    case 0x0e:
      counter_running = true;
      counter = (ctur << 8) + ctlr;
      break;
    case 0x0f:
      isr &= 0xf7;
      break;
  }

  return value;
}

void DuartScn68681::ReceiveCharA(unsigned char ch)
{
  isr |= RxRDYA;
  rba = ch;
}

void DuartScn68681::ReceiveCharB(unsigned char ch)
{
  isr |= RxRDYB;
  rbb = ch;
}




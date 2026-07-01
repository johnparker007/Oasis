#include "stdafx.h"
#include "EDC.h"

EDCUNIT::EDCUNIT() {
	ZeroMemory(EDCBuf, EDCBUFFERSIZE * sizeof(char));
}

EDCUNIT::~EDCUNIT() {

}

void EDCUNIT::SaveState() {

}

void EDCUNIT::LoadState() {

}

char* __fastcall EDCUNIT::getEDCString() {
	return NULL;
}

void __fastcall EDCUNIT::Write(UINT8 ByteIn) {

	static char Mode = 0;
	static char Length = 0;

	FILE* EdcFile;
	fopen_s(&EdcFile, "EDC.txt", "a");

	if (Mode == 0)
	{
		Mode = ByteIn;
	}
	else
	{
		switch (Mode) {
		case 0x2B://Cash Door Open
			if (ByteIn == 0x2B) {
				//Cash Door Open Mesage
				fprintf(EdcFile, "Cash Door Open \n");
			}
			Mode = 0;
			break;
		case 0x2C://Cash Door Closed
			if (ByteIn == 0x2C) {
				//Cash Door Closed Mesage
				fprintf(EdcFile, "Cash Door Closed \n");
			}
			Mode = 0;
			break;
		case 0x2D://Service Door Open
			if (ByteIn == 0x2D) {
				//Service Door Open Mesage
				fprintf(EdcFile, "Service Door Open \n");
			}
			Mode = 0;
			break;
		case 0x2E://Service Door Closed
			if (ByteIn == 0x2E) {
				//Service Door Closed Mesage
				fprintf(EdcFile, "Service Door Closed \n");
			}
			Mode = 0;
			break;
		case 0x2F://VTP 10p Units
			if (ByteIn == 0x2F) {
				//VTP Increase
				fprintf(EdcFile, "VTP++ \n");
			}
			Mode = 0;
			break;
		case 0x60://Primary Machine Message
			if (EDCSaveLength == 0) {
				EDCLength = 0;
				EDCSaveLength = ByteIn;
			}
			else {
				UINT8 ManName[4];
				UINT8 MachName[5];
				UINT8 Protocol;

				EDCBuf[EDCLength] = ByteIn;
				EDCLength++;
				if (EDCLength == (EDCSaveLength + 1)) {
					EDCBuf[EDCLength] = 0;
					Mode = 0;
					EDCSaveLength = 0;
					ManName[0] = EDCBuf[0];
					ManName[1] = EDCBuf[1];
					ManName[2] = EDCBuf[2];
					ManName[3] = 0;
					Protocol = EDCBuf[3];
					MachName[0] = EDCBuf[4];
					MachName[1] = EDCBuf[5];
					MachName[2] = EDCBuf[6];
					MachName[3] = EDCBuf[7];
					MachName[4] = 0;

					fprintf(EdcFile, "Primary: \n");
					fprintf(EdcFile, "    Man ID: %s \n", ManName);
					if (Protocol == 'N') {
						fprintf(EdcFile, "    Protocol: No Data \n");
					}
					else if (Protocol == 'P') {
						fprintf(EdcFile, "    Protocol: Data Usable \n");
					}

					fprintf(EdcFile, "    Machine ID: %s \n", MachName);
				}
			}
			break;

		case 0x61://Float level Message
			if (EDCSaveLength == 0) {
				EDCLength = 0;
				EDCSaveLength = ByteIn;
			}
			else {
				EDCBuf[EDCLength] = ByteIn;
				EDCLength++;
				if (EDCLength == (EDCSaveLength + 1)) {
					EDCBuf[EDCLength] = 0;
					Mode = 0;
					EDCSaveLength = 0;
					fprintf(EdcFile, "Float Level: %s \n", EDCBuf);
				}
			}
			break;

		case 0x62://Secondary Machine Message

			if (EDCSaveLength == 0) {
				EDCLength = 0;
				EDCSaveLength = ByteIn;
			}
			else {
				UINT8 Version[4];
				UINT8 Percent[4];

				EDCBuf[EDCLength] = ByteIn;
				EDCLength++;
				if (EDCLength == (EDCSaveLength + 1)) {
					EDCBuf[EDCLength] = 0;
					Mode = 0;
					EDCSaveLength = 0;

					Version[0] = EDCBuf[0];
					Version[1] = EDCBuf[1];
					Version[2] = EDCBuf[2];
					Version[3] = 0;
					Percent[0] = EDCBuf[4];
					Percent[1] = EDCBuf[5];
					Percent[2] = EDCBuf[6];
					Percent[3] = 0;
					fprintf(EdcFile, "Secondary: \n");

					fprintf(EdcFile, "    Version: %s \n", Version);

					switch (EDCBuf[3]) {//Payout Type
					case 'T': fprintf(EdcFile, "    Payout Type: Cash & Token \n");	break;
					case 'C': fprintf(EdcFile, "    Payout Type: Cash Only \n");	break;
					case 'X': fprintf(EdcFile, "    Payout Type: Not Applicable \n"); break;
					default:  fprintf(EdcFile, "    Payout Type: Unknown or Invalid \n"); break;
					}

					fprintf(EdcFile, "    Percent: %s", Percent);
					fprintf(EdcFile, "%% \n");
					switch (EDCBuf[7]) {//Machine Type
					case 'A': fprintf(EdcFile, "    Machine Type: AWP \n");	break;
					case 'B': fprintf(EdcFile, "    Machine Type: All cash \n"); break;
					case 'C': fprintf(EdcFile, "    Machine Type: Club \n"); break;
					case 'D': fprintf(EdcFile, "    Machine Type: Casino \n"); break;
					case 'S': fprintf(EdcFile, "    Machine Type: SWP \n");	break;
					case 'V': fprintf(EdcFile, "    Machine Type: Video \n"); break;
					case 'J': fprintf(EdcFile, "    Machine Type: Jukebox \n");	break;
					case 'P': fprintf(EdcFile, "    Machine Type: Pool \n"); break;
					case 'X': fprintf(EdcFile, "    Machine Type: Other \n"); break;
					default:  fprintf(EdcFile, "    Machine Type: Unknown or Invalid \n"); break;
					}

					fprintf(EdcFile, "    Stake: %i", EDCBuf[8]);
					fprintf(EdcFile, "p \n");

					switch (EDCBuf[9]) {//Payout Type
					case 'P': fprintf(EdcFile, "    Diversion Type: Passive \n"); break;
					case 'A': fprintf(EdcFile, "    Diversion Type: Active \n"); break;
					case 'X': fprintf(EdcFile, "    Diversion Type: Not Applicable \n"); break;
					default:  fprintf(EdcFile, "    Diversion Type: Unknown or Invalid \n"); break;
					}
				}
			}


			break;
		case 0x63://Critical Fault Message
			if (EDCSaveLength == 0) {
				EDCLength = 0;
				EDCSaveLength = ByteIn;
			}
			else {
				if (ByteIn == 0) ByteIn = 0x20; //Convert 0 to [SPACE]
				EDCBuf[EDCLength] = ByteIn;
				EDCLength++;
				if (EDCLength == (EDCSaveLength + 1)) {
					EDCBuf[EDCLength] = 0;
					Mode = 0;
					EDCSaveLength = 0;
					fprintf(EdcFile, "Critical Fault: %s \n", EDCBuf);
				}
			}
			break;
		case 0x64://Non Critical Fault Message
			if (EDCSaveLength == 0) {
				EDCLength = 0;
				EDCSaveLength = ByteIn;
			}
			else {
				if (ByteIn == 0) ByteIn = 0x20; //Convert 0 to [SPACE]
				EDCBuf[EDCLength] = ByteIn;
				EDCLength++;
				if (EDCLength == (EDCSaveLength + 1)) {
					EDCBuf[EDCLength] = 0;
					Mode = 0;
					EDCSaveLength = 0;
					fprintf(EdcFile, "Non-Critical Fault: %s \n", EDCBuf);
				}
			}
			break;
		case 0x65://Compliance Message or Game Outcome
			if (EDCSaveLength == 0) {
				EDCLength = 0;
				EDCSaveLength = ByteIn;
			}
			else {
				if (ByteIn == 0) ByteIn = 0x20; //Convert 0 to [SPACE]
				EDCBuf[EDCLength] = ByteIn;
				EDCLength++;
				if (EDCLength == (EDCSaveLength + 1)) {
					EDCBuf[EDCLength] = 0;
					Mode = 0;
					EDCSaveLength = 0;
					fprintf(EdcFile, "Compliance: %s \n", EDCBuf);
				}
			}
			break;

		case 0x66://Variable Data
			if (EDCSaveLength == 0) {
				EDCLength = 0;
				EDCSaveLength = ByteIn;
			}
			else {
				if (ByteIn == 0) ByteIn = 0x20; //Convert 0 to [SPACE]
				EDCBuf[EDCLength] = ByteIn;
				EDCLength++;
				if (EDCLength == (EDCSaveLength + 1)) {
					EDCBuf[EDCLength] = 0;
					Mode = 0;
					EDCSaveLength = 0;
					fprintf(EdcFile, "Variable: %s \n", EDCBuf);
				}
			}
			break;
		case 0: //NULL Char
			Mode = 0;
			fprintf(EdcFile, "NULL Message \n");
			break;
		case 4: //Mode Change
			Mode = 0;
			fprintf(EdcFile, "Mode Change \n");
			break;
		case 7: //Idle Message
			Mode = 0;
			fprintf(EdcFile, "Idle \n");
			break;
		case 0x20:
			if (ByteIn == 0x20) {
				//Token Payout To Float
				Mode = 0;
				fprintf(EdcFile, "Token Payout To Float \n");
			}
			break;
		case 0x21:
			if (ByteIn == 0x21) {
				//10p Payout To Float
				Mode = 0;
				fprintf(EdcFile, "10p Payout To Float \n");
			}
			break;
		case 0x22:
			if (ByteIn == 0x22) {
				//20p Payout To Float
				Mode = 0;
				fprintf(EdcFile, "20p Payout To Float \n");
			}
			break;
		case 0x23:
			if (ByteIn == 0x23) {
				//50p Payout To Float
				Mode = 0;
				fprintf(EdcFile, "50p Payout To Float \n");
			}
			break;
		case 0x24:
			if (ByteIn == 0x24) {
				//£1 Payout To Float
				Mode = 0;
				fprintf(EdcFile, "£1 Payout To Float \n");
			}
			break;
		case 0x25:
			if (ByteIn == 0x25) {
				//£2 Payout To Float
				Mode = 0;
				fprintf(EdcFile, "£2 Payout To Float \n");
			}
			break;
		case 0x26:
			if (ByteIn == 0x26) {
				//£5 Payout To Float
				Mode = 0;
				fprintf(EdcFile, "£5 Payout To Float \n");
			}
			break;
		case 0x27:
			if (ByteIn == 0x27) {
				//£20 Cash In
				Mode = 0;
				fprintf(EdcFile, "£20 Cash In \n");
			}
			break;
		case 0x28:
			if (ByteIn == 0x28) {
				//£50 Cash In
				Mode = 0;
				fprintf(EdcFile, "£50 Cash In \n");
			}
			break;
		case 0x29:
			if (ByteIn == 0x29) {
				//2p Cash In
				Mode = 0;
				fprintf(EdcFile, "2p Cash In \n");
			}
			break;
		case 0x2a:
			if (ByteIn == 0x2a) {
				//2p Cash Out
				Mode = 0;
				fprintf(EdcFile, "2p Cash Out \n");
			}
			break;
		case 0x30:
			if (ByteIn == 0x30) {
				//5p Cash In
				Mode = 0;
				fprintf(EdcFile, "5p Cash In \n");
			}
			break;
		case 0x31:
			if (ByteIn == 0x31) {
				//10p Cash In
				Mode = 0;
				fprintf(EdcFile, "10p Cash In \n");
			}
			break;
		case 0x32:
			if (ByteIn == 0x32) {
				//20p Cash In
				Mode = 0;
				fprintf(EdcFile, "20p Cash In \n");
			}
			break;
		case 0x33:
			if (ByteIn == 0x33) {
				//50p Cash In
				Mode = 0;
				fprintf(EdcFile, "50p Cash In \n");
			}
			break;
		case 0x34:
			if (ByteIn == 0x34) {
				//£1 Cash In
				Mode = 0;
				fprintf(EdcFile, "£1 Cash In \n");
			}
			break;
		case 0x35:
			if (ByteIn == 0x35) {
				//£2 Cash In
				Mode = 0;
				fprintf(EdcFile, "£2 Cash In \n");
			}
			break;
		case 0x36:
			if (ByteIn == 0x36) {
				//£5 Cash In
				Mode = 0;
				fprintf(EdcFile, "£5 Cash In \n");
			}
			break;
		case 0x37:
			if (ByteIn == 0x37) {
				//£10 Cash In
				Mode = 0;
				fprintf(EdcFile, "£10 Cash In \n");
			}
			break;
		case 0x38:
			if (ByteIn == 0x38) {
				//5p Token In
				Mode = 0;
				fprintf(EdcFile, "5p Token In \n");
			}
			break;
		case 0x39:
			if (ByteIn == 0x39) {
				//10p Token In
				Mode = 0;
				fprintf(EdcFile, "10p Token In \n");
			}
			break;
		case 0x3a:
			if (ByteIn == 0x3a) {
				//20p Token In
				Mode = 0;
				fprintf(EdcFile, "20p Token In \n");
			}
			break;
		case 0x3b:
			if (ByteIn == 0x3b) {
				//50p Token In
				Mode = 0;
				fprintf(EdcFile, "50p Token In \n");
			}
			break;
		case 0x3c:
			if (ByteIn == 0x3c) {
				//£1 Token In
				Mode = 0;
				fprintf(EdcFile, "£1 Token In \n");
			}
			break;
		case 0x3d:
			if (ByteIn == 0x3d) {
				//£2 Token In
				Mode = 0;
				fprintf(EdcFile, "£2 Token In \n");
			}
			break;
		case 0x3e:
			if (ByteIn == 0x3e) {
				//£5 Token In
				Mode = 0;
				fprintf(EdcFile, "£5 Token In \n");
			}
			break;
		case 0x3f:
			if (ByteIn == 0x3f) {
				//£10 Token In
				Mode = 0;
				fprintf(EdcFile, "£10 Token In \n");
			}
			break;
		case 0x40:
			if (ByteIn == 0x40) {
				//5p Cash Out
				Mode = 0;
				fprintf(EdcFile, "5p Cash Out \n");
			}
			break;
		case 0x41:
			if (ByteIn == 0x41) {
				//10p Cash Out
				Mode = 0;
				fprintf(EdcFile, "10p Cash Out \n");
			}
			break;
		case 0x42:
			if (ByteIn == 0x42) {
				//20p Cash Out
				Mode = 0;
				fprintf(EdcFile, "20p Cash Out \n");
			}
			break;
		case 0x43:
			if (ByteIn == 0x43) {
				//50p Cash Out
				Mode = 0;
				fprintf(EdcFile, "50p Cash Out \n");
			}
			break;
		case 0x44:
			if (ByteIn == 0x44) {
				//£1 Cash Out
				Mode = 0;
				fprintf(EdcFile, "£1 Cash Out \n");
			}
			break;
		case 0x45:
			if (ByteIn == 0x45) {
				//£2 Cash Out
				Mode = 0;
				fprintf(EdcFile, "£2 Cash Out \n");
			}
			break;
		case 0x46:
			if (ByteIn == 0x46) {
				//£5 Cash Out
				Mode = 0;
				fprintf(EdcFile, "£5 Cash Out \n");
			}
			break;
		case 0x47:
			if (ByteIn == 0x47) {
				//£10 Cash Out
				Mode = 0;
				fprintf(EdcFile, "£10 Cash Out \n");
			}
			break;
		case 0x48:
			if (ByteIn == 0x48) {
				//5p Token Out
				Mode = 0;
				fprintf(EdcFile, "5p Token Out \n");
			}
			break;
		case 0x49:
			if (ByteIn == 0x49) {
				//10p Token Out
				Mode = 0;
				fprintf(EdcFile, "10p Token Out \n");
			}
			break;
		case 0x4a:
			if (ByteIn == 0x4a) {
				//20p Token Out
				Mode = 0;
				fprintf(EdcFile, "20p Token Out \n");
			}
			break;
		case 0x4b:
			if (ByteIn == 0x4b) {
				//50p Token Out
				Mode = 0;
				fprintf(EdcFile, "50p Token Out \n");
			}
			break;
		case 0x4c:
			if (ByteIn == 0x4c) {
				//£1 Token Out
				Mode = 0;
				fprintf(EdcFile, "£1 Token Out \n");
			}
			break;
		case 0x4d:
			if (ByteIn == 0x4d) {
				//£2 Token Out
				Mode = 0;
				fprintf(EdcFile, "£2 Token Out \n");
			}
			break;
		case 0x4e:
			if (ByteIn == 0x4e) {
				//£5 Token Out
				Mode = 0;
				fprintf(EdcFile, "£5 Token Out \n");
			}
			break;
		case 0x4f:
			if (ByteIn == 0x4f) {
				//£10 Token Out
				Mode = 0;
				fprintf(EdcFile, "£10 Token Out \n");
			}
			break;
		case 0x50:
			if (ByteIn == 0x50) {
				//5p Cash Refill
				Mode = 0;
				fprintf(EdcFile, "5p Cash Refill \n");
			}
			break;
		case 0x51:
			if (ByteIn == 0x51) {
				//10p Cash Refill
				Mode = 0;
				fprintf(EdcFile, "10p Cash Refill \n");
			}
			break;
		case 0x52:
			if (ByteIn == 0x52) {
				//20p Cash Refill
				Mode = 0;
				fprintf(EdcFile, "20p Cash Refill \n");
			}
			break;
		case 0x53:
			if (ByteIn == 0x53) {
				//50p Cash Refill
				Mode = 0;
				fprintf(EdcFile, "50p Cash Refill \n");
			}
			break;
		case 0x54:
			if (ByteIn == 0x54) {
				//£1 Cash Refill
				Mode = 0;
				fprintf(EdcFile, "£1 Cash Refill \n");
			}
			break;
		case 0x55:
			if (ByteIn == 0x55) {
				//£2 Cash Refill
				Mode = 0;
				fprintf(EdcFile, "£2 Cash Refill \n");
			}
			break;
		case 0x56:
			if (ByteIn == 0x56) {
				//£5 Cash Refill
				Mode = 0;
				fprintf(EdcFile, "£5 Cash Refill \n");
			}
			break;
		case 0x57:
			if (ByteIn == 0x57) {
				//£10 Cash Refill
				Mode = 0;
				fprintf(EdcFile, "£10 Cash Refill \n");
			}
			break;
		case 0x58:
			if (ByteIn == 0x58) {
				//5p Token Refill
				Mode = 0;
				fprintf(EdcFile, "5p Token Refill \n");
			}
			break;
		case 0x59:
			if (ByteIn == 0x59) {
				//10p Token Refill
				Mode = 0;
				fprintf(EdcFile, "10p Token Refill \n");
			}
			break;
		case 0x5a:
			if (ByteIn == 0x5a) {
				//20p Token Refill
				Mode = 0;
				fprintf(EdcFile, "20p Token Refill \n");
			}
			break;
		case 0x5b:
			if (ByteIn == 0x5b) {
				//50p Token Refill
				Mode = 0;
				fprintf(EdcFile, "50p Token Refill \n");
			}
			break;
		case 0x5c:
			if (ByteIn == 0x5c) {
				//£1 Token Refill
				Mode = 0;
				fprintf(EdcFile, "£1 Token Refill \n");
			}
			break;
		case 0x5d:
			if (ByteIn == 0x5d) {
				//£2 Token Refill
				Mode = 0;
				fprintf(EdcFile, "£2 Token Refill \n");
			}
			break;
		case 0x5e:
			if (ByteIn == 0x5e) {
				//£5 Token Refill
				Mode = 0;
				fprintf(EdcFile, "£5 Token Refill \n");
			}
			break;
		case 0x5f:
			if (ByteIn == 0x5f) {
				//£10 Token Refill
				Mode = 0;
				fprintf(EdcFile, "£10 Token Refill \n");
			}
			break;
		}
	}
	

	fclose(EdcFile);
}
void __fastcall		EDCUNIT::Reset(LoadSaveClass* LSCIn) {
	LSC = LSCIn;
}


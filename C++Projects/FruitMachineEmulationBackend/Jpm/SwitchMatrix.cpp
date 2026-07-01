#include "stdafx.h"
#include "SwitchMatrix.h"

SwitchMatrix::SwitchMatrix(){	

	ZeroMemory(Matrix, MATRIXSIZE * sizeof(UINT8));

}

SwitchMatrix::~SwitchMatrix(){
	
}

void SwitchMatrix::Init(){

	UINT32 Cnt;

	for (Cnt = 0; Cnt < MATRIXSIZE; Cnt++){
		Matrix[Cnt] = 0;	
	}

}

void SwitchMatrix::TurnSwitchOn(UINT8 num){
	Matrix[num] = 1;
}

void SwitchMatrix::TurnSwitchOff(UINT8 num){
	Matrix[num] = 0;
}

UINT8 SwitchMatrix::ReadSwitch(UINT8 num){

	UINT8 ret;

	ret = Matrix[num];
	return ret;
}

UINT8 SwitchMatrix::ReadMatrix(UINT8 num){

	UINT8 ret;
	int loop;

	ret = Matrix[(num * 8) + 7];
	for (loop = 6; loop > -1; loop--){
		ret = (ret << 1);
		ret += Matrix[(num * 8) + loop];		
	}

	return ret;
}
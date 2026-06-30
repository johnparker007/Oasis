#include "stdafx.h"
#include "SwitchMatrix.h"

SwitchMatrix::SwitchMatrix(){	

	ZeroMemory(Matrix, MATRIXSIZE * sizeof(unsigned char));

}

SwitchMatrix::~SwitchMatrix(){
	
}

void SwitchMatrix::Init(){

	unsigned int Cnt;

	for (Cnt = 0; Cnt < MATRIXSIZE; Cnt++){
		Matrix[Cnt] = 0;	
	}

}

void SwitchMatrix::TurnSwitchOn(unsigned char num){
	Matrix[num] = 1;
}

void SwitchMatrix::TurnSwitchOff(unsigned char num){
	Matrix[num] = 0;
}

unsigned char SwitchMatrix::ReadSwitch(unsigned char num){

	unsigned char ret;

	ret = Matrix[num];
	return ret;
}

unsigned char SwitchMatrix::ReadMatrix(unsigned char num){

	unsigned char ret;
	int loop;

	ret = Matrix[(num * 8) + 7];
	for (loop = 6; loop > -1; loop--){
		ret = (ret << 1);
		ret += Matrix[(num * 8) + loop];		
	}

	return ret;
}
#pragma once

#define MATRIXSIZE 256

class SwitchMatrix {
public:

	SwitchMatrix();
	~SwitchMatrix();
	void TurnSwitchOn(UINT8 num);
	void TurnSwitchOff(UINT8 num);
	UINT8 ReadSwitch(UINT8 num);
	UINT8 ReadMatrix(UINT8 num);
	void Init(void);

private:

	UINT8 Matrix[MATRIXSIZE];

};
#ifndef SwitchMatrixH
#define SwitchMatrixH

#define MATRIXSIZE 256

class SwitchMatrix {
public:

	SwitchMatrix();
	~SwitchMatrix();
	void TurnSwitchOn(unsigned char num);
	void TurnSwitchOff(unsigned char num);
	unsigned char ReadSwitch(unsigned char num);
	unsigned char ReadMatrix(unsigned char num);
	void Init(void);

private:

	unsigned char Matrix[MATRIXSIZE];

};

#endif SwitchMatrixH
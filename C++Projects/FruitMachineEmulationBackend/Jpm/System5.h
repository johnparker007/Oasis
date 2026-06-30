#ifndef SYS5H
#define SYS5H

class SYSTEM5 {
protected:

private:
	
public:

	unsigned int cpu_read_byte(unsigned int address);
	unsigned int cpu_read_word(unsigned int address);
	unsigned int cpu_read_long(unsigned int address);
	
	void cpu_write_byte(unsigned int address, unsigned int value);
	void cpu_write_word(unsigned int address, unsigned int value);
	void cpu_write_long(unsigned int address, unsigned int value);

	SYSTEM5();
	~SYSTEM5();

};

#endif // SYS5H
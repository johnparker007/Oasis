/////////////////////////////////////////////////////////////////////////////
// Filename: LoadSaveCompressDLLClass.h
////////////////////////////////////////////////////////////////////////////////
#ifndef _LSCCLASS_H_
#define _LSCCLASS_H_

#include <stddef.h>
#include <cstdio>

////////////////////////////////////////////////////////////////////////////////
// Class name: LoadSaveCompressDLLClass
////////////////////////////////////////////////////////////////////////////////
#define FORMATVERSION 0
#define NUMFILES 1

class LoadSaveClass
{
public:

    // De/Constructor
    LoadSaveClass();
    ~LoadSaveClass();

    // Save
    void SaveInit(unsigned int BufferSize);
    void SaveToFile(char* FileString);
    void SaveToBuffer(char Var);
    void SaveToBuffer(unsigned char Var);
    void SaveToBuffer(short Var);
    void SaveToBuffer(unsigned short Var);
    void SaveToBuffer(int Var);
    void SaveToBuffer(unsigned int Var);
    void SaveToBuffer(long Var);
    void SaveToBuffer(unsigned long Var);
    void SaveToBuffer(bool Var);
    void SaveToBuffer(char* Var);
    void SaveVersionToBuffer();

    // De/Compress
    void CompressFiles(char* FolderString, char* SaveFileString);
    void DeCompressFiles(char* FolderString, char* LoadFileString);

    // Load
    void LoadInit(char* FileString);
    void LoadEnd();
    void LoadFromBuffer(char& Var);
    void LoadFromBuffer(unsigned char& Var);
    void LoadFromBuffer(short& Var);
    void LoadFromBuffer(unsigned short& Var);
    void LoadFromBuffer(int& Var);
    void LoadFromBuffer(unsigned int& Var);
    void LoadFromBuffer(long& Var);
    void LoadFromBuffer(unsigned long& Var);
    void LoadFromBuffer(bool& Var);
    void LoadStringFromBuffer(char* Var);
    void LoadVersionFromBuffer();

private:
    // Kept as a member for source compatibility with older code, but the safe
    // implementation uses local FILE* handles wherever possible.
    FILE* IOFile = 0;

    size_t loadPointer = 0;
    size_t savePointer = 0;
    size_t loadSize = 0;
    size_t saveCapacity = 0;

    int LoadedFormatVersion = 0;

    unsigned char* loadBuffer = 0;
    unsigned char* saveBuffer = 0;

    bool EnsureSaveCapacity(size_t bytesToAdd);
    bool CanLoadBytes(size_t bytesToRead) const;
};

#endif

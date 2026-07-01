/////////////////////////////////////////////////////////////////////////////
// Filename: LoadSaveClass.h
////////////////////////////////////////////////////////////////////////////////
#pragma once

#include <cstdio>

////////////////////////////////////////////////////////////////////////////////
// Class name: LoadSaveClass
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
    void SaveInit(UINT32 BufferSize);
    void SaveToFile(UINT8* FileString);
    void SaveToBuffer(INT8 Var);
    void SaveToBuffer(UINT8 Var);
    void SaveToBuffer(INT16 Var);
    void SaveToBuffer(UINT16 Var);
    void SaveToBuffer(INT32 Var);
    void SaveToBuffer(UINT32 Var);
    void SaveToBuffer(INT64 Var);
    void SaveToBuffer(UINT64 Var);
    void SaveToBuffer(bool Var);
    void SaveToBuffer(float Var);
    void SaveToBuffer(UINT8* Var);
    void SaveVersionToBuffer();

    // Load
    void LoadInit(UINT8* FileString);
    void LoadEnd();
    void LoadFromBuffer(INT8& Var);
    void LoadFromBuffer(UINT8& Var);
    void LoadFromBuffer(INT16& Var);
    void LoadFromBuffer(UINT16& Var);
    void LoadFromBuffer(INT32& Var);
    void LoadFromBuffer(UINT32& Var);
    void LoadFromBuffer(INT64& Var);
    void LoadFromBuffer(UINT64& Var);
    void LoadFromBuffer(bool& Var);
    void LoadFromBuffer(float& Var);
    void LoadStringFromBuffer(UINT8* Var);
    void LoadVersionFromBuffer();

private:
    // Kept as a member for source compatibility with older code, but the safe
    // implementation uses local FILE* handles wherever possible.

    FILE* IOFile = 0;

    size_t loadPointer = 0;
    size_t savePointer = 0;
    size_t loadSize = 0;
    size_t saveCapacity = 0;

    INT32 LoadedFormatVersion = 0;

    UINT8* loadBuffer = 0;
    UINT8* saveBuffer = 0;

    bool EnsureSaveCapacity(size_t bytesToAdd);
    bool CanLoadBytes(size_t bytesToRead) const;
};
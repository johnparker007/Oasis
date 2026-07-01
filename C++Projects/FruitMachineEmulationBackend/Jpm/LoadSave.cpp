#include "stdafx.h"
#include "LoadSave.h"

#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include <limits.h>
#include <stdint.h>
#include <string>



#ifndef UINT8
typedef unsigned char UINT8;
#endif
#ifndef UINT16
typedef unsigned short UINT16;
#endif
#ifndef UINT32
typedef unsigned int UINT32;
#endif

namespace
{
    // Preserve the existing DLL save format: currently this class stores only STATE.
    const char* const FileNames[] = { "STATE", NULL };

    // On-disk per-file header, in existing order:
    // InUse, ULength, UCRC, CLength, CCRC, StartPos.
    const size_t FileHeaderBytes = 1u + (5u * 4u);

    static unsigned char* AllocBytes(size_t bytes)
    {
        if (bytes == 0) bytes = 1;
        return static_cast<unsigned char*>(malloc(bytes));
    }

    static std::string BuildPath(const char* folder, const char* file)
    {
        std::string result;
        if (folder) result += folder;
        if (file) result += file;
        return result;
    }

    static bool GetFileLength(FILE* file, UINT32& length)
    {
        length = 0;
        if (!file) return false;
        if (fseek(file, 0L, SEEK_END) != 0) return false;
        long pos = ftell(file);
        if (pos < 0) return false;
        if (fseek(file, 0L, SEEK_SET) != 0) return false;
        length = static_cast<UINT32>(pos);
        return true;
    }

    static bool ReadExact(FILE* file, void* dst, size_t bytes)
    {
        if (bytes == 0) return true;
        return fread(dst, 1, bytes, file) == bytes;
    }

}

LoadSaveClass::LoadSaveClass()
{
}

LoadSaveClass::~LoadSaveClass()
{
    if (IOFile) {
        fclose(IOFile);
        IOFile = NULL;
    }
    if (loadBuffer) {
        free(loadBuffer);
        loadBuffer = NULL;
    }
    if (saveBuffer) {
        free(saveBuffer);
        saveBuffer = NULL;
    }
    loadPointer = 0;
    savePointer = 0;
    loadSize = 0;
    saveCapacity = 0;
}

bool LoadSaveClass::EnsureSaveCapacity(size_t bytesToAdd)
{
    if (bytesToAdd == 0) return true;

    if (!saveBuffer) {
        size_t initial = bytesToAdd > 1024 ? bytesToAdd : 1024;
        saveBuffer = AllocBytes(initial);
        if (!saveBuffer) {
            saveCapacity = 0;
            savePointer = 0;
            return false;
        }
        saveCapacity = initial;
        savePointer = 0;
        return true;
    }

    if (savePointer > SIZE_MAX - bytesToAdd) return false;
    size_t required = savePointer + bytesToAdd;
    if (required <= saveCapacity) return true;

    size_t newCapacity = saveCapacity ? saveCapacity : 1024;
    while (newCapacity < required) {
        if (newCapacity > SIZE_MAX / 2) {
            newCapacity = required;
            break;
        }
        newCapacity *= 2;
    }

    unsigned char* newBuffer = static_cast<unsigned char*>(realloc(saveBuffer, newCapacity));
    if (!newBuffer) return false;

    saveBuffer = newBuffer;
    saveCapacity = newCapacity;
    return true;
}

bool LoadSaveClass::CanLoadBytes(size_t bytesToRead) const
{
    return loadBuffer && loadPointer <= loadSize && bytesToRead <= (loadSize - loadPointer);
}

void LoadSaveClass::SaveInit(unsigned int BufferSize)
{
    if (saveBuffer) {
        free(saveBuffer);
        saveBuffer = NULL;
    }

    savePointer = 0;
    saveCapacity = BufferSize ? BufferSize : 1;
    saveBuffer = AllocBytes(saveCapacity);
    if (!saveBuffer) {
        saveCapacity = 0;
    }
}

void LoadSaveClass::SaveToFile(char* FileString)
{
    if (!FileString || !saveBuffer) {
        if (saveBuffer) {
            free(saveBuffer);
            saveBuffer = NULL;
        }
        savePointer = 0;
        saveCapacity = 0;
        return;
    }

    FILE* file = NULL;
    fopen_s(&file, FileString, "wb");
    if (file) {
        if (savePointer > 0) {
            fwrite(saveBuffer, 1, savePointer, file);
        }
        fclose(file);
    }

    free(saveBuffer);
    saveBuffer = NULL;
    savePointer = 0;
    saveCapacity = 0;
}

void LoadSaveClass::SaveVersionToBuffer()
{
    SaveToBuffer(static_cast<UINT32>(FORMATVERSION));
}

void LoadSaveClass::LoadVersionFromBuffer()
{
    UINT32 val = 0;
    LoadFromBuffer(val);
    LoadedFormatVersion = static_cast<int>(val);
}

void LoadSaveClass::CompressFiles(char* FolderString, char* SaveFileString)
{
    UINT8 inUse[NUMFILES] = { 0 };
    UINT32 fileLens[NUMFILES] = { 0 };
    size_t estimatedLength = 0;

    for (UINT32 fileLoop = 0; fileLoop < NUMFILES; ++fileLoop) {
        const char* fileName = FileNames[fileLoop];
        if (!fileName) break;

        estimatedLength += FileHeaderBytes;

        std::string wholeName = BuildPath(FolderString, fileName);
        FILE* file = NULL;
        fopen_s(&file, wholeName.c_str(), "rb");
        if (!file) {
            inUse[fileLoop] = 0;
            fileLens[fileLoop] = 0;
            continue;
        }

        
        fclose(file);
    }

    if (estimatedLength > UINT_MAX) {
        return;
    }

    SaveInit(static_cast<unsigned int>(estimatedLength + 1));
    if (!saveBuffer) return;

    for (UINT32 fileLoop = 0; fileLoop < NUMFILES; ++fileLoop) {
        const char* fileName = FileNames[fileLoop];
        if (!fileName) break;

        UINT32 ULength = 0;
        UINT32 UCRC = 0;
        UINT32 CCRC = 0;
        UINT32 CLength = 0;
        UINT32 StartPos = 0; // Kept for binary compatibility with existing save files.

        std::string wholeName = BuildPath(FolderString, fileName);
        FILE* file = NULL;
        fopen_s(&file, wholeName.c_str(), "rb");

        if (!file) {
            SaveToBuffer(static_cast<UINT8>(0));
            SaveToBuffer(ULength);
            SaveToBuffer(UCRC);
            SaveToBuffer(CLength);
            SaveToBuffer(CCRC);
            SaveToBuffer(StartPos);
            continue;
        }

        UINT32 fileLen = 0;
        if (!GetFileLength(file, fileLen)) {
            fclose(file);
            SaveToBuffer(static_cast<UINT8>(0));
            SaveToBuffer(ULength);
            SaveToBuffer(UCRC);
            SaveToBuffer(CLength);
            SaveToBuffer(CCRC);
            SaveToBuffer(StartPos);
            continue;
        }

        unsigned char* openBuff = AllocBytes(fileLen);
        if (!openBuff) {
            fclose(file);
            return;
        }

        bool readOk = ReadExact(file, openBuff, fileLen);
        fclose(file);
        if (!readOk) {
            free(openBuff);
            return;
        }

        ULength = fileLen;
        

        SaveToBuffer(inUse[fileLoop]);
        SaveToBuffer(ULength);
        SaveToBuffer(UCRC);
        SaveToBuffer(CLength);
        SaveToBuffer(CCRC);
        SaveToBuffer(StartPos);

        free(openBuff);
    }

    SaveToFile(SaveFileString);
}

void LoadSaveClass::DeCompressFiles(char* FolderString, char* LoadFileString)
{
    LoadInit(LoadFileString);
    if (!loadBuffer) return;

    for (UINT32 fileLoop = 0; fileLoop < NUMFILES; ++fileLoop) {
        const char* fileName = FileNames[fileLoop];
        if (!fileName) break;

        UINT8 InUse = 0;
        UINT32 ULength = 0;
        UINT32 UCRC = 0;
        UINT32 CLength = 0;
        UINT32 CCRC = 0;
        UINT32 StartPos = 0;

        if (!CanLoadBytes(FileHeaderBytes)) {
            LoadEnd();
            return;
        }

        LoadFromBuffer(InUse);
        LoadFromBuffer(ULength);
        LoadFromBuffer(UCRC);
        LoadFromBuffer(CLength);
        LoadFromBuffer(CCRC);
        LoadFromBuffer(StartPos);

        if (!InUse) {
            continue;
        }

        if (CLength == 0 || !CanLoadBytes(CLength)) {
            LoadEnd();
            return;
        }

        unsigned char* cmpBuff = AllocBytes(CLength);
        if (!cmpBuff) {
            LoadEnd();
            return;
        }

        memcpy(cmpBuff, loadBuffer + loadPointer, CLength);
        loadPointer += CLength;        

        SaveInit(ULength + 1);
        if (!saveBuffer) {
            free(cmpBuff);
            LoadEnd();
            return;
        }                

        savePointer = ULength;
        std::string wholeName = BuildPath(FolderString, fileName);
        SaveToFile(const_cast<char*>(wholeName.c_str()));
    }

    LoadEnd();
}

void LoadSaveClass::SaveToBuffer(char* Var)
{
    if (!Var) {
        SaveToBuffer(static_cast<char>(0));
        return;
    }

    size_t length = strlen(Var);
    if (!EnsureSaveCapacity(length + 1)) return;

    memcpy(saveBuffer + savePointer, Var, length);
    savePointer += length;
    saveBuffer[savePointer++] = 0;
}

void LoadSaveClass::SaveToBuffer(bool Var)
{
    SaveToBuffer(static_cast<UINT8>(Var ? 1 : 0));
}

void LoadSaveClass::SaveToBuffer(char Var)
{
    if (!EnsureSaveCapacity(1)) return;
    saveBuffer[savePointer++] = static_cast<UINT8>(Var);
}

void LoadSaveClass::SaveToBuffer(unsigned char Var)
{
    if (!EnsureSaveCapacity(1)) return;
    saveBuffer[savePointer++] = Var;
}

void LoadSaveClass::SaveToBuffer(short Var)
{
    SaveToBuffer(static_cast<UINT16>(Var));
}

void LoadSaveClass::SaveToBuffer(unsigned short Var)
{
    if (!EnsureSaveCapacity(2)) return;
    saveBuffer[savePointer++] = static_cast<UINT8>(Var & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 8) & 0xff);
}

void LoadSaveClass::SaveToBuffer(int Var)
{
    SaveToBuffer(static_cast<UINT32>(Var));
}

void LoadSaveClass::SaveToBuffer(unsigned int Var)
{
    if (!EnsureSaveCapacity(4)) return;
    saveBuffer[savePointer++] = static_cast<UINT8>(Var & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 8) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 16) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 24) & 0xff);
}

void LoadSaveClass::SaveToBuffer(long Var)
{
    SaveToBuffer(static_cast<UINT32>(Var));
}

void LoadSaveClass::SaveToBuffer(unsigned long Var)
{
    SaveToBuffer(static_cast<UINT32>(Var));
}

// LOADS
void LoadSaveClass::LoadInit(char* FileString)
{
    loadPointer = 0;
    loadSize = 0;

    if (loadBuffer) {
        free(loadBuffer);
        loadBuffer = NULL;
    }

    if (!FileString) return;

    FILE* file = NULL;
    fopen_s(&file, FileString, "rb");
    if (!file) return;

    UINT32 fileLen = 0;
    if (!GetFileLength(file, fileLen)) {
        fclose(file);
        return;
    }

    loadBuffer = AllocBytes(fileLen);
    if (!loadBuffer) {
        fclose(file);
        return;
    }

    if (!ReadExact(file, loadBuffer, fileLen)) {
        fclose(file);
        free(loadBuffer);
        loadBuffer = NULL;
        loadPointer = 0;
        loadSize = 0;
        return;
    }

    fclose(file);
    loadSize = fileLen;
}

void LoadSaveClass::LoadEnd()
{
    if (loadBuffer) {
        free(loadBuffer);
        loadBuffer = NULL;
    }
    loadPointer = 0;
    loadSize = 0;
}

void LoadSaveClass::LoadFromBuffer(char& Var)
{
    UINT8 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<char>(tmp);
}

void LoadSaveClass::LoadFromBuffer(bool& Var)
{
    UINT8 tmp = 0;
    LoadFromBuffer(tmp);
    Var = tmp ? true : false;
}

void LoadSaveClass::LoadFromBuffer(unsigned char& Var)
{
    if (!CanLoadBytes(1)) {
        Var = 0;
        return;
    }
    Var = loadBuffer[loadPointer++];
}

void LoadSaveClass::LoadFromBuffer(short& Var)
{
    UINT16 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<short>(tmp);
}

void LoadSaveClass::LoadFromBuffer(unsigned short& Var)
{
    if (!CanLoadBytes(2)) {
        Var = 0;
        return;
    }
    UINT32 val = loadBuffer[loadPointer++];
    val |= static_cast<UINT32>(loadBuffer[loadPointer++]) << 8;
    Var = static_cast<UINT16>(val);
}

void LoadSaveClass::LoadFromBuffer(int& Var)
{
    UINT32 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<int>(tmp);
}

void LoadSaveClass::LoadFromBuffer(unsigned int& Var)
{
    if (!CanLoadBytes(4)) {
        Var = 0;
        return;
    }
    UINT32 val = loadBuffer[loadPointer++];
    val |= static_cast<UINT32>(loadBuffer[loadPointer++]) << 8;
    val |= static_cast<UINT32>(loadBuffer[loadPointer++]) << 16;
    val |= static_cast<UINT32>(loadBuffer[loadPointer++]) << 24;
    Var = val;
}

void LoadSaveClass::LoadFromBuffer(long& Var)
{
    UINT32 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<long>(tmp);
}

void LoadSaveClass::LoadFromBuffer(unsigned long& Var)
{
    UINT32 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<unsigned long>(tmp);
}

void LoadSaveClass::LoadStringFromBuffer(char* Var)
{
    if (!Var) return;

    int cnt = 0;
    while (CanLoadBytes(1)) {
        unsigned char ch = loadBuffer[loadPointer++];
        Var[cnt++] = static_cast<char>(ch);
        if (ch == 0) return;
    }

    // Truncated string in save file. Null terminate the caller's buffer.
    Var[cnt] = 0;
}

#include "stdafx.h"
#include "LoadSave.h"

#include <string.h>
#include <stdio.h>
#include <limits.h>

#include <string>

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
        return static_cast<UINT8*>(new UINT8[bytes]);
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
        delete(loadBuffer);
        loadBuffer = NULL;
    }
    if (saveBuffer) {
        delete(saveBuffer);
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
        delete(saveBuffer);
        saveBuffer = NULL;
    }

    savePointer = 0;
    saveCapacity = BufferSize ? BufferSize : 1;
    saveBuffer = AllocBytes(saveCapacity);
    if (!saveBuffer) {
        saveCapacity = 0;
    }
}

void LoadSaveClass::SaveToFile(UINT8* FileString)
{
    if (!FileString || !saveBuffer) {
        if (saveBuffer) {
            delete(saveBuffer);
            saveBuffer = NULL;
        }
        savePointer = 0;
        saveCapacity = 0;
        return;
    }

    FILE* file = NULL;
    fopen_s(&file, (char*)FileString, "wb");
    if (file) {
        if (savePointer > 0) {
            fwrite(saveBuffer, 1, savePointer, file);
        }
        fclose(file);
    }

    delete(saveBuffer);
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

void LoadSaveClass::SaveToBuffer(UINT8* Var)
{
    if (!Var) {
        SaveToBuffer(static_cast<char>(0));
        return;
    }

    size_t length = strlen((char *)Var);
    if (!EnsureSaveCapacity(length + 1)) return;

    memcpy(saveBuffer + savePointer, Var, length);
    savePointer += length;
    saveBuffer[savePointer++] = 0;
}

void LoadSaveClass::SaveToBuffer(bool Var)
{
    SaveToBuffer(static_cast<UINT8>(Var ? 1 : 0));
}

void LoadSaveClass::SaveToBuffer(float Var)
{
    if (!EnsureSaveCapacity(4)) return;
    unsigned char bytes[sizeof(float)];
    memcpy(bytes, &Var, sizeof(float));
    saveBuffer[savePointer++] = bytes[0];
    saveBuffer[savePointer++] = bytes[1];
    saveBuffer[savePointer++] = bytes[2];
    saveBuffer[savePointer++] = bytes[3];
}

void LoadSaveClass::SaveToBuffer(INT8 Var)
{
    if (!EnsureSaveCapacity(1)) return;
    saveBuffer[savePointer++] = Var;
}

void LoadSaveClass::SaveToBuffer(UINT8 Var)
{
    if (!EnsureSaveCapacity(1)) return;
    saveBuffer[savePointer++] = Var;
}

void LoadSaveClass::SaveToBuffer(INT16 Var)
{
    SaveToBuffer(static_cast<UINT16>(Var));
}

void LoadSaveClass::SaveToBuffer(UINT16 Var)
{
    if (!EnsureSaveCapacity(2)) return;
    saveBuffer[savePointer++] = static_cast<UINT8>(Var & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 8) & 0xff);
}

void LoadSaveClass::SaveToBuffer(INT32 Var)
{
    SaveToBuffer(static_cast<UINT32>(Var));
}

void LoadSaveClass::SaveToBuffer(UINT32 Var)
{
    if (!EnsureSaveCapacity(4)) return;
    saveBuffer[savePointer++] = static_cast<UINT8>(Var & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 8) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 16) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 24) & 0xff);
}

void LoadSaveClass::SaveToBuffer(INT64 Var)
{
    SaveToBuffer(static_cast<UINT64>(Var));
}

void LoadSaveClass::SaveToBuffer(UINT64 Var)
{
    if (!EnsureSaveCapacity(8)) return;
    saveBuffer[savePointer++] = static_cast<UINT8>(Var & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 8) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 16) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 24) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 32) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 40) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 48) & 0xff);
    saveBuffer[savePointer++] = static_cast<UINT8>((Var >> 56) & 0xff);
}

// LOADS
void LoadSaveClass::LoadInit(UINT8* FileString)
{
    loadPointer = 0;
    loadSize = 0;

    if (loadBuffer) {
        delete(loadBuffer);
        loadBuffer = NULL;
    }

    if (!FileString) return;

    FILE* file = NULL;
    fopen_s(&file, (char*)FileString, "rb");
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
        delete(loadBuffer);
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
        delete(loadBuffer);
        loadBuffer = NULL;
    }
    loadPointer = 0;
    loadSize = 0;
}

void LoadSaveClass::LoadFromBuffer(INT8& Var)
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

void LoadSaveClass::LoadFromBuffer(float& Var)
{
    if (!CanLoadBytes(4)) {
        Var = 0;
        return;
    }
    // Read 4 bytes from the buffer into a temporary byte array
    unsigned char bytes[4];
    bytes[0] = loadBuffer[loadPointer++];
    bytes[1] = loadBuffer[loadPointer++];
    bytes[2] = loadBuffer[loadPointer++];
    bytes[3] = loadBuffer[loadPointer++];

    // Safely copy the bytes back into the float variable
    std::memcpy(&Var, bytes, sizeof(float));
}

void LoadSaveClass::LoadFromBuffer(UINT8& Var)
{
    if (!CanLoadBytes(1)) {
        Var = 0;
        return;
    }
    Var = loadBuffer[loadPointer++];
}

void LoadSaveClass::LoadFromBuffer(INT16& Var)
{
    UINT16 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<INT16>(tmp);
}

void LoadSaveClass::LoadFromBuffer(UINT16& Var)
{
    if (!CanLoadBytes(2)) {
        Var = 0;
        return;
    }
    UINT16 val = loadBuffer[loadPointer++];
    val |= static_cast<UINT16>(loadBuffer[loadPointer++]) << 8;
    Var = val;
}

void LoadSaveClass::LoadFromBuffer(INT32& Var)
{
    UINT32 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<INT32>(tmp);
}

void LoadSaveClass::LoadFromBuffer(UINT32& Var)
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

void LoadSaveClass::LoadFromBuffer(INT64& Var)
{
    UINT64 tmp = 0;
    LoadFromBuffer(tmp);
    Var = static_cast<INT64>(tmp);
}

void LoadSaveClass::LoadFromBuffer(UINT64& Var)
{
    if (!CanLoadBytes(8)) {
        Var = 0;
        return;
    }
    UINT64 val = loadBuffer[loadPointer++];
    val |= static_cast<UINT64>(loadBuffer[loadPointer++]) << 8;
    val |= static_cast<UINT64>(loadBuffer[loadPointer++]) << 16;
    val |= static_cast<UINT64>(loadBuffer[loadPointer++]) << 24;
    val |= static_cast<UINT64>(loadBuffer[loadPointer++]) << 32;
    val |= static_cast<UINT64>(loadBuffer[loadPointer++]) << 40;
    val |= static_cast<UINT64>(loadBuffer[loadPointer++]) << 48;
    val |= static_cast<UINT64>(loadBuffer[loadPointer++]) << 56;
    
    Var = val;
}

void LoadSaveClass::LoadStringFromBuffer(UINT8* Var)
{
    if (!Var) return;

    UINT32 cnt = 0;
    while (CanLoadBytes(1)) {
        UINT8 ch = loadBuffer[loadPointer++];
        Var[cnt++] = ch;
        if (ch == 0) return;
    }
    // Truncated string in save file. Null terminate the caller's buffer.
    Var[cnt] = 0;
}

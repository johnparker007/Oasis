#include "pch.h"
#include "mfmedll.h"
#include "detours.h"

#include <fstream>
using std::ofstream;
#include <iostream>
using std::cout;
using std::endl;

#include <atlstr.h>

#include <cstring>
#include <string>

#include < windows.h >



namespace MfmeDll
{
	const LPCWSTR OverrideLPDataMFMEStringWithASIM = L"MFME (Oasis - MFME Tools)";

	bool attached = false;

	// Registry variables
	extern _tRegGetValueW _RegGetValueW = nullptr;
	//extern _tRegSetValueExA _RegSetValueExA = nullptr;
	extern _tRegSetValueExW _RegSetValueExW = nullptr;
	//extern _tRegFlushKey _RegFlushKey = nullptr;
	//extern _tRegCloseKey _RegCloseKey = nullptr;
	//extern _tRegCreateKeyExA _RegCreateKeyExA = nullptr;
	extern _tRegCreateKeyExW _RegCreateKeyExW = nullptr;
	//extern _tRegCreateKeyTransactedW _RegCreateKeyTransactedW = nullptr;
	//extern _tRegOpenKeyExA _RegOpenKeyExA = nullptr;
	extern _tRegOpenKeyExW _RegOpenKeyExW = nullptr;
	extern _tRegEnumKeyExW _RegEnumKeyExW = nullptr;
	extern _tRegQueryValueExW _RegQueryValueExW = nullptr;

	// *** REVISED:  This actually Outputs all values.  First ones (not the MFME Rgeistry config keys are output to the root, then the config ones 
	// are output INTO THE MACHINE'S SPECIFIC LAYOUT folder
	DWORD WINAPI _hRegSetValueExW(HKEY hKey, LPCSTR lpValueName, DWORD Reserved, DWORD dwType, const BYTE* lpData, DWORD cbData)
	{
		if (lpValueName != NULL)
		{
			//WriteStringToEmptyFile((LPWSTR)lpValueName);
		}

		//return _RegSetValueExW(hKey, lpValueName, Reserved, dwType, lpData, cbData);
		return ERROR_SUCCESS;
	}

	// Creates files in the MFME root, but not in the Layout dir (no actual Registry config files)
	DWORD WINAPI _hRegGetValueW(HKEY hkey, LPCWSTR lpSubKey, LPCWSTR lpValue, DWORD dwFlags, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData)
	{
		if (lpSubKey != NULL)
		{
			// Prints some numbers and some unrelated values (Not MFME, or MFME Key values)
			//WriteStringToEmptyFile((LPWSTR)lpSubKey);
		}

		if (lpValue != NULL)
		{
			// Prints lots of 'system' type sounding values (Not MFME, or MFME Key values)
			//WriteStringToEmptyFile((LPWSTR)lpValue);
		}

		return _RegGetValueW(hkey, lpSubKey, lpValue, dwFlags, pdwType, pvData, pcbData);
	}

	// * Printing lpSubkey for this, it only shows a single entry "MFME" (which might be the CJW/MFME ? ) - actually makes an MFME in MFME root, and an MFME in the specific Layout dir
	DWORD WINAPI _hRegCreateKeyExW(HKEY hKey, LPCSTR lpSubKey, DWORD Reserved, LPSTR lpClass, DWORD dwOptions, REGSAM samDesired,
		const LPSECURITY_ATTRIBUTES lpSecurityAttributes, PHKEY phkResult, LPDWORD lpdwDisposition)
	{
		if (lpSubKey != NULL)
		{
			std::string utf8Key = to_utf8((LPCWSTR)lpSubKey);
			std::string keyToMatch("MFME");
			
			if (utf8Key.compare(keyToMatch) == 0)
			{
				lpSubKey = (LPCSTR)OverrideLPDataMFMEStringWithASIM;
			}

			//WriteStringToEmptyFile((LPWSTR)lpSubKey);
		}

		return _RegCreateKeyExW(hKey, lpSubKey, Reserved, lpClass, dwOptions, samDesired, lpSecurityAttributes, phkResult, lpdwDisposition);
	}

	// * This is another that outputs MFME  *********** THIS SEEMS TO BE THE ONE THAT FIXES SO MFME READS FROM ASIM REGISTRY!!!!!!!!!!!!!
	DWORD WINAPI _hRegOpenKeyExW(HKEY hKey, LPCSTR lpSubKey, DWORD ulOptions, REGSAM samDesired, PHKEY phkResult)
	{
		if (lpSubKey != NULL)
		{
			std::string utf8Key = to_utf8((LPCWSTR)lpSubKey);
			std::string keyToMatch("MFME");

			if (utf8Key.compare(keyToMatch) == 0)
			{
				lpSubKey = (LPCSTR)OverrideLPDataMFMEStringWithASIM;
			}

			//WriteStringToEmptyFile((LPWSTR)lpSubKey);
		}

		return _RegOpenKeyExW(hKey, lpSubKey, ulOptions, samDesired, phkResult);
	}
	
	// By storing returncode, then returning, we can print the valid lpNames - this only does some numbers and some chinese font names, NOT the registry key names
	DWORD WINAPI _hRegEnumKeyExW(HKEY hKey, DWORD dwIndex, LPWSTR lpName, LPDWORD lpcchName, LPDWORD lpReserved, LPWSTR lpClass, LPDWORD lpcchClass, PFILETIME lpftLastWriteTime)
	{
// XXX Just to write test.txt and return real function result --------------------------------------------------
//CreateTestTextFile(); 
// XXX End test ------------------------------------------------------------------------------------------------

		// trying a call then a delayed return to allow printing/manipulation:
		LSTATUS returncode = _RegEnumKeyExW(hKey, dwIndex, lpName, lpcchName, lpReserved, lpClass, lpcchClass, lpftLastWriteTime);

//if (lpName != NULL)
//{
//	std::string utf8Key = to_utf8((LPWSTR)lpName);
//
//	HANDLE fHandle =
//		CreateFile((LPWSTR)lpName,
//			FILE_GENERIC_READ | FILE_GENERIC_WRITE,
//			FILE_SHARE_READ | FILE_SHARE_WRITE,
//			NULL, CREATE_ALWAYS,
//			FILE_ATTRIBUTE_NORMAL,
//			NULL);
//
//	//file handle code check ommited for brevity
//	DWORD bytesWritten;
//	WriteFile(fHandle, utf8Key.c_str(), strlen((const char*)utf8Key.c_str()), &bytesWritten, NULL);
//	CloseHandle(fHandle);
//}

return returncode;

		//return _RegEnumKeyExW(hKey, dwIndex, lpName, lpcchName, lpReserved, lpClass, lpcchClass, lpftLastWriteTime);

		// trying a call then a delayed return to allow printing/manipulation:
	}

	LPCWSTR OverrideLPDataString = L"0";

	// ****** THIS IS THE ONE!  It reads the values from the MFME registry named values
	DWORD WINAPI _hRegQueryValueExW(HKEY hKey, LPCWSTR lpValueName, LPDWORD lpReserved, LPDWORD lpType, LPBYTE lpData, LPDWORD lpcbData)
	{
		LSTATUS returncode = _RegQueryValueExW(hKey, lpValueName, lpReserved, lpType, lpData, lpcbData);

//		if (lpValueName != NULL)
//		{
//			std::string utf8Key = to_utf8((LPCWSTR)lpValueName);
//			std::string keyToMatch("MeterPanelOff");
//			
//			if (utf8Key.compare(keyToMatch) == 0)
//			{
//				// ***************** TRY TO CHANGE THE STRING VALUE **********************************************
//lpData = (LPBYTE)OverrideLPDataString;
//
//				HANDLE fHandle =
//					CreateFile((LPCWSTR)lpData,
//						FILE_GENERIC_READ | FILE_GENERIC_WRITE,
//						FILE_SHARE_READ | FILE_SHARE_WRITE,
//						NULL, CREATE_ALWAYS,
//						FILE_ATTRIBUTE_NORMAL,
//						NULL);
//
//				//file handle code check ommited for brevity
//				DWORD bytesWritten;
//				WriteFile(fHandle, utf8Key.c_str(), strlen((const char*)utf8Key.c_str()), &bytesWritten, NULL);
//				CloseHandle(fHandle);
//			}
//		}

		return returncode;
	}






	void Setup()
	{
		if (attached)
		{
			return;
		}

		HMODULE hMod = GetModuleHandle(L"KernelBase.dll");

		if (!hMod)
			return;


		// Registry setup
		_RegSetValueExW = (_tRegSetValueExW)GetProcAddress(hMod, "RegSetValueExW");
		_RegCreateKeyExW = (_tRegCreateKeyExW)GetProcAddress(hMod, "RegCreateKeyExW");
		_RegOpenKeyExW = (_tRegOpenKeyExW)GetProcAddress(hMod, "RegOpenKeyExW");

		DetourTransactionBegin();
		DetourUpdateThread(GetCurrentThread());

		//// Registry cd
		DetourAttach(&(PVOID&)_RegSetValueExW, _hRegSetValueExW); // <<<<<<<<<<< **** THIS ONE WORKS !!!!!!!!!!!!!!!!
		DetourAttach(&(PVOID&)_RegCreateKeyExW, _hRegCreateKeyExW);
		DetourAttach(&(PVOID&)_RegOpenKeyExW, _hRegOpenKeyExW);

		DetourTransactionCommit();

		attached = true;
	}

	void Detach()
	{
		if (!attached)
			return;

		DetourTransactionBegin();
		DetourUpdateThread(GetCurrentThread());

		// Registry
		//DetourDetach(&(PVOID&)_RegSetValueExA, _hRegSetValueExA);

// XXX will not bother detaching all the experiments with regsitry read/write yet

		DetourTransactionCommit();

		attached = false;
	}

	std::string to_utf8(const wchar_t* buffer, int len)
	{
		int nChars = ::WideCharToMultiByte(
			CP_UTF8,
			0,
			buffer,
			len,
			NULL,
			0,
			NULL,
			NULL);
		if (nChars == 0) return "";

		std::string newbuffer;
		newbuffer.resize(nChars);
		::WideCharToMultiByte(
			CP_UTF8,
			0,
			buffer,
			len,
			const_cast<char*>(newbuffer.c_str()),
			nChars,
			NULL,
			NULL);

		return newbuffer;
	}

	std::string to_utf8(const std::wstring& str)
	{
		return to_utf8(str.c_str(), (int)str.size());
	}

	void WriteStringToEmptyFile(LPWSTR str)
	{
		std::string utf8Key = to_utf8(str);
		
		HANDLE fHandle =
			CreateFile((LPWSTR)str,
				FILE_GENERIC_READ | FILE_GENERIC_WRITE,
				FILE_SHARE_READ | FILE_SHARE_WRITE,
				NULL, CREATE_ALWAYS,
				FILE_ATTRIBUTE_NORMAL,
				NULL);
		
		//file handle code check ommited for brevity
		DWORD bytesWritten;
		WriteFile(fHandle, utf8Key.c_str(), strlen((const char*)utf8Key.c_str()), &bytesWritten, NULL);
		CloseHandle(fHandle);
	}
}

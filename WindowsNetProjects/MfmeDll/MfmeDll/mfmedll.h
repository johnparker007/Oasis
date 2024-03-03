#pragma once

#include <Windows.h>
#include <string>

//#include <detours.h>
//#include "detours.h"

namespace MfmeDll
{
	extern double speed;
	extern bool attached;

	extern bool speedSetBackToNormal;
	extern long long offsetToStopTimeGoingBackwards;

	// *************** Speed ************************************************
	//typedef DWORD(WINAPI* _tGetTickCount)(void);
	//extern _tGetTickCount _GetTickCount;
	//extern DWORD _GetTickCount_BaseTime;

	//typedef ULONGLONG(WINAPI* _tGetTickCount64)(void);
	//extern _tGetTickCount64 _GetTickCount64;
	//extern DWORD _GetTickCount64_BaseTime;

	typedef BOOL(WINAPI* _tQueryPerformanceCounter)(LARGE_INTEGER*);
	extern _tQueryPerformanceCounter _QueryPerformanceCounter;
	extern LARGE_INTEGER _QueryPerformanceCounter_BaseTime;

	//DWORD WINAPI _hGetTickCount();
	//DWORD WINAPI _hGetTickCount64();
	DWORD WINAPI _hQueryPerformanceCounter(LARGE_INTEGER* lpPerformanceCount);

	// *************** Registry ************************************************
	
	// trying RegSetValueExA - got returncode/params from: https://docs.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regsetvalueexa

	//typedef LSTATUS(WINAPI* _tRegSetValueExA)(HKEY, LPCSTR, DWORD, DWORD, const BYTE*, DWORD); 
	//extern _tRegSetValueExA _RegSetValueExA;

	typedef LSTATUS(WINAPI* _tRegSetValueExW)(HKEY, LPCSTR, DWORD, DWORD, const BYTE*, DWORD);
	extern _tRegSetValueExW _RegSetValueExW;

	typedef LSTATUS(WINAPI* _tRegGetValueW)(HKEY, LPCWSTR, LPCWSTR, DWORD, LPDWORD, PVOID, LPDWORD);
	extern _tRegGetValueW _RegGetValueW;

	//typedef LSTATUS(WINAPI* _tRegFlushKey)(HKEY);
	//extern _tRegFlushKey _RegFlushKey;

	//typedef LSTATUS(WINAPI* _tRegCloseKey)(HKEY);
	//extern _tRegCloseKey _RegCloseKey;

	//typedef LSTATUS(WINAPI* _tRegCreateKeyExA)(HKEY, LPCSTR, DWORD, LPSTR, DWORD, REGSAM, const LPSECURITY_ATTRIBUTES, PHKEY, LPDWORD);
	//extern _tRegCreateKeyExA _RegCreateKeyExA;

	typedef LSTATUS(WINAPI* _tRegCreateKeyExW)(HKEY, LPCSTR, DWORD, LPSTR, DWORD, REGSAM, const LPSECURITY_ATTRIBUTES, PHKEY, LPDWORD);
	extern _tRegCreateKeyExW _RegCreateKeyExW;

	//typedef LSTATUS(WINAPI* _tRegCreateKeyTransactedW)(HKEY, LPCWSTR, DWORD, LPWSTR, DWORD, REGSAM, const LPSECURITY_ATTRIBUTES, PHKEY, LPDWORD, HANDLE, PVOID);
	//extern _tRegCreateKeyTransactedW _RegCreateKeyTransactedW;

	//typedef LSTATUS(WINAPI* _tRegOpenKeyExA)(HKEY, LPCSTR, DWORD, REGSAM, PHKEY);
	//extern _tRegOpenKeyExA _RegOpenKeyExA;

	typedef LSTATUS(WINAPI* _tRegOpenKeyExW)(HKEY, LPCSTR, DWORD, REGSAM, PHKEY);
	extern _tRegOpenKeyExW _RegOpenKeyExW;

	typedef LSTATUS(WINAPI* _tRegEnumKeyExW)(HKEY, DWORD, LPWSTR, LPDWORD, LPDWORD, LPWSTR, LPDWORD, PFILETIME);
	extern _tRegEnumKeyExW _RegEnumKeyExW;

	typedef LSTATUS(WINAPI* _tRegQueryValueExW)(HKEY, LPCWSTR, LPDWORD, LPDWORD, LPBYTE, LPDWORD);
	extern _tRegQueryValueExW _RegQueryValueExW;



	//DWORD WINAPI _hRegSetValueExA(HKEY hKey, LPCSTR lpValueName, DWORD Reserved, DWORD dwType, const BYTE* lpData, DWORD cbData);
	DWORD WINAPI _hRegSetValueExW(HKEY hKey, LPCSTR lpValueName, DWORD Reserved, DWORD dwType, const BYTE* lpData, DWORD cbData);
	DWORD WINAPI _hRegGetValueW(HKEY hkey, LPCWSTR lpSubKey, LPCWSTR lpValue, DWORD dwFlags, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData);

	//DWORD WINAPI _hRegFlushKey(HKEY hKey);
	//DWORD WINAPI _hRegCloseKey(HKEY hKey);
	////DWORD WINAPI _hRegCreateKeyExA(HKEY hKey, LPCSTR lpSubKey, DWORD Reserved, LPSTR lpClass, DWORD dwOptions, REGSAM samDesired, 
	////	const LPSECURITY_ATTRIBUTES lpSecurityAttributes, PHKEY phkResult, LPDWORD lpdwDisposition);
	DWORD WINAPI _hRegCreateKeyExW(HKEY hKey, LPCSTR lpSubKey, DWORD Reserved, LPSTR lpClass, DWORD dwOptions, REGSAM samDesired,
		const LPSECURITY_ATTRIBUTES lpSecurityAttributes, PHKEY phkResult, LPDWORD lpdwDisposition);

	//DWORD WINAPI _hRegCreateKeyTransactedW(HKEY hKey, LPCWSTR lpSubKey, DWORD Reserved, LPWSTR lpClass, DWORD dwOptions, REGSAM samDesired,
	//	const LPSECURITY_ATTRIBUTES lpSecurityAttributes, PHKEY phkResult, LPDWORD lpdwDisposition, HANDLE hTransaction, PVOID pExtendedParemeter);

	//////DWORD WINAPI _hRegOpenKeyExA(HKEY hKey, LPCSTR lpSubKey, DWORD ulOptions, REGSAM samDesired, PHKEY phkResult);
	DWORD WINAPI _hRegOpenKeyExW(HKEY hKey, LPCSTR lpSubKey, DWORD ulOptions, REGSAM samDesired, PHKEY phkResult);

	DWORD WINAPI _hRegEnumKeyExW(HKEY hKey, DWORD dwIndex, LPWSTR lpName, LPDWORD lpcchName, LPDWORD lpReserved, LPWSTR lpClass, LPDWORD lpcchClass, PFILETIME lpftLastWriteTime);

	DWORD WINAPI _hRegQueryValueExW(HKEY hKey, LPCWSTR lpValueName, LPDWORD lpReserved, LPDWORD lpType, LPBYTE lpData, LPDWORD lpcbData);

	void Setup();
	void Detach();

	std::string to_utf8(const wchar_t* buffer, int len);
	std::string to_utf8(const std::wstring& str);

	void WriteStringToEmptyFile(LPWSTR str);

}

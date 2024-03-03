// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "mfmedll.h"

#include <synchapi.h>

#include <iostream>
#include <fstream>
using namespace std;


DWORD WINAPI MainThread(LPVOID lpParam)
{
	MfmeDll::Setup();

	// NOT DETACHING SO DLL STUFF STAYS ATTACHED

	return 0;
}

BOOL WINAPI DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved)
{
	if (dwReason == DLL_PROCESS_ATTACH)
	{
		CreateThread(0, 0x1000, &MainThread, NULL, 0, NULL);
	}

	return TRUE;
}

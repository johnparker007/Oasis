#pragma once
#include <windows.h>
#include <string>

#define DLLExport __declspec(dllexport)

typedef void(__stdcall* MenuItemCallback) (const UINT id);

extern "C"
{
    DLLExport void StartPlugin();
    DLLExport UINT AddMenuRoot(const char* menu);
    DLLExport UINT AddMenuItem(const UINT menuParent, const char* menuItem, bool hasSubItem);
    DLLExport bool IsMenuItemSelected(UINT menuId);
    DLLExport void SetMenuItemSelected(UINT menuId, bool state);
    DLLExport bool IsMenuItemEnabled(UINT menuId);
    DLLExport void SetMenuItemEnabled(UINT menuId, bool state);
    DLLExport void BuildMenu();
    DLLExport void SetMenuItemCallback(MenuItemCallback menuItemCallback);

    std::wstring s2ws(const std::string& s);
}
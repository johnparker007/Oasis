#pragma once
#include <windows.h>
#include <winuser.h>
#include <vector>

#include "NativeMenuBar.Windows.h"
#include "MenuItem.h"
#include "MenuItemComparator.h"

HWND hWindow;
HMENU hMenu;
std::vector<MenuItem> menuItems;
MenuItemCallback callback;
WNDPROC wndProcUnity;

LRESULT CALLBACK WndProc(HWND hwnd, UINT Message, WPARAM wParam, LPARAM lParam)
{
    if(Message == WM_COMMAND)
    {
        // If a menuItem match the selected item
        auto result = std::find_if(menuItems.begin(), menuItems.end(), MenuItemIdComparator((UINT)wParam));
        if (result != std::end(menuItems))
        {
            callback(result->GetId());
        }
    }

    // Let Unity handle the event
    return CallWindowProc(wndProcUnity, hwnd, Message, wParam, lParam);
}

void StartPlugin()
{
    // Get handle to the Unity window
    hWindow = GetActiveWindow();
    // Create the menu bar
    hMenu = CreateMenu();
    // Register the event callback while preserving a reference to the unity event handler
    wndProcUnity = (WNDPROC)SetWindowLongPtr(GetActiveWindow(), GWLP_WNDPROC, (LONG_PTR)WndProc);
}

UINT AddMenuRoot(const char* menu)
{
    std::string menuStr(menu);

    // If menuRoot does not already exist
    auto result = std::find_if(menuItems.begin(), menuItems.end(), MenuItemNameAndParentIdComparator(menuStr, 0));
    if(result == std::end(menuItems))
    {
        HMENU hMenuRoot = CreatePopupMenu();
        InsertMenu(hMenu, -1, MF_STRING | MF_POPUP, (UINT)hMenuRoot, s2ws(menuStr).c_str());
        menuItems.emplace_back(MenuItem(hMenuRoot, menu));
        return (UINT)hMenuRoot;
    }
    return 0;
}

UINT AddMenuItem(const UINT menuParent, const char* menuItem, bool hasSubItem)
{
    std::string menuItemStr(menuItem);
    
    // If menuParent already exist among items
    auto parentResult = std::find_if(menuItems.begin(), menuItems.end(), MenuItemIdComparator(menuParent));
    if (parentResult != std::end(menuItems))
    {
        // If menuItem with the same name does not already exist
        auto itemResult = std::find_if(menuItems.begin(), menuItems.end(), MenuItemNameAndParentIdComparator(menuItemStr, menuParent));
        if (itemResult == std::end(menuItems))
        {
            HMENU hMenuItem = CreatePopupMenu();
            AppendMenu(parentResult->GetMenuHandle(), hasSubItem ? MF_POPUP : MF_STRING, (UINT)hMenuItem, s2ws(menuItemStr).c_str());
            menuItems.emplace_back(MenuItem(hMenuItem, menuItem, parentResult->GetId()));
            return (UINT)hMenuItem;
        }
    }
    return 0;
}

void SetMenuItemSelected(const UINT menuId, bool state)
{
    // If menuItem exist
    auto result = std::find_if(menuItems.begin(), menuItems.end(), MenuItemIdComparator(menuId));
    if (result != std::end(menuItems))
    {
        MENUITEMINFO menuInfo{};
        menuInfo.cbSize = sizeof(MENUITEMINFO);
        menuInfo.fMask = MIIM_STATE;
        GetMenuItemInfo(hMenu, result->GetId(), false, &menuInfo);
        menuInfo.fState = state ? MFS_CHECKED : MFS_UNCHECKED;
        SetMenuItemInfo(hMenu, result->GetId(), false, &menuInfo);
    }
}

void SetMenuItemEnabled(UINT menuId, bool state)
{
    // If menuItem exist
    auto result = std::find_if(menuItems.begin(), menuItems.end(), MenuItemIdComparator(menuId));
    if (result != std::end(menuItems))
    {
        MENUITEMINFO menuInfo{};
        menuInfo.cbSize = sizeof(MENUITEMINFO);
        menuInfo.fMask = MIIM_STATE;
        GetMenuItemInfo(hMenu, result->GetId(), false, &menuInfo);
        menuInfo.fState = state ? (menuInfo.fState & ~MFS_GRAYED) : MFS_GRAYED;
        SetMenuItemInfo(hMenu, result->GetId(), false, &menuInfo);
    }
}

bool IsMenuItemSelected(UINT menuId)
{
    // If menuItem exist
    auto result = std::find_if(menuItems.begin(), menuItems.end(), MenuItemIdComparator(menuId));
    if (result != std::end(menuItems))
    {
        MENUITEMINFO menuInfo{};
        menuInfo.cbSize = sizeof(MENUITEMINFO);
        menuInfo.fMask = MIIM_STATE;
        GetMenuItemInfo(hMenu, result->GetId(), false, &menuInfo);
        return (menuInfo.fState & MFS_CHECKED) == MFS_CHECKED;
    }
}

bool IsMenuItemEnabled(UINT menuId)
{
    // If menuItem exist
    auto result = std::find_if(menuItems.begin(), menuItems.end(), MenuItemIdComparator(menuId));
    if (result != std::end(menuItems))
    {
        MENUITEMINFO menuInfo{};
        menuInfo.cbSize = sizeof(MENUITEMINFO);
        menuInfo.fMask = MIIM_STATE;
        GetMenuItemInfo(hMenu, result->GetId(), false, &menuInfo);
        return (menuInfo.fState & MFS_GRAYED) != MFS_GRAYED;
    }
}

void SetMenuItemCallback(MenuItemCallback menuItemCallback)
{
    if (menuItemCallback)
    {
        callback = menuItemCallback;
    }
}

void BuildMenu()
{
    SetMenu(hWindow, hMenu);
}

std::wstring s2ws(const std::string& s)
{
    int len;
    int slength = (int)s.length() + 1;
    len = MultiByteToWideChar(CP_ACP, 0, s.c_str(), slength, 0, 0);
    wchar_t* buf = new wchar_t[len];
    MultiByteToWideChar(CP_ACP, 0, s.c_str(), slength, buf, len);
    std::wstring r(buf);
    delete[] buf;
    return r;
}
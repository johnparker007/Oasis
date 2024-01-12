#pragma once
#include <windows.h>
#include <string>

#include "MenuItem.h"

MenuItem::MenuItem(HMENU hMenu, const char* name, UINT parentId)
{
	menuHandle = hMenu;
	menuParentId = parentId;
	menuName = std::string(name);
}

UINT MenuItem::GetId() const
{
	return (UINT)menuHandle;
}

std::string MenuItem::GetName() const
{
	return menuName;
}

UINT MenuItem::GetParentId() const
{
	return menuParentId;
}

HMENU MenuItem::GetMenuHandle() const
{
	return menuHandle;
}

#pragma once
#include <windows.h>
#include <string>
#include "MenuRoot.h"

MenuRoot::MenuRoot(HMENU hMenu, std::string name)
{
	menuHandle = hMenu;
	menuName = name;
}

std::string MenuRoot::GetName()
{
	return menuName;
}

HMENU MenuRoot::GetHandle()
{
	return menuHandle;
}

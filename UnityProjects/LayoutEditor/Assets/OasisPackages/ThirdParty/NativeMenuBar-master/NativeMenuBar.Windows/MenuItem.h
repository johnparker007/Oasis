#pragma once
#include <windows.h>
#include <string>

class MenuItem
{
public:
	MenuItem(HMENU hMenu, const char* name, UINT parentId = 0);

	UINT GetId() const;
	UINT GetParentId() const;
	std::string GetName() const;

	HMENU GetMenuHandle() const;

private:
	HMENU menuHandle;
	UINT menuParentId;
	std::string menuName;
};
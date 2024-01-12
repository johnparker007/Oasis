#pragma once
#include <string>

#include "MenuItem.h"

struct MenuItemNameAndParentIdComparator
{
public:
	explicit MenuItemNameAndParentIdComparator(std::string name, UINT parentId) { menuItemName = name; parentMenuItemId = parentId; }
	inline bool operator()(const MenuItem& m) const { return m.GetName() == menuItemName && m.GetParentId() == parentMenuItemId; }
private:
	std::string menuItemName;
	UINT parentMenuItemId;
};

struct MenuItemIdComparator
{
public:
	explicit MenuItemIdComparator(UINT id) : menuItemId(id) { }
	inline bool operator()(const MenuItem& m) const { return m.GetId() == menuItemId; }
private:
	UINT menuItemId;
};
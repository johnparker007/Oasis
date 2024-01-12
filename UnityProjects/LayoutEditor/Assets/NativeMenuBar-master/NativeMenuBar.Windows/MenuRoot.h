#pragma once
#include <windows.h>
#include <string>

class MenuRoot 
{      
private:
    HMENU menuHandle;
    std::string menuName;

public:
    MenuRoot(HMENU hmenu, std::string name);
    std::string GetName();
    HMENU GetHandle();
};
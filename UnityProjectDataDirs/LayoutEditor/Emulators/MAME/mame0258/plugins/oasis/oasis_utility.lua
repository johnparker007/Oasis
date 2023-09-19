local lib = {}


-- wraps calls in pcall
function lib:protected_call(callback, callback_name)
	local status, err = pcall(callback)
	if (not status) then
		print("@ERROR ## Lua runtime error on " .. callback_name .. " " .. tostring(err))
	end
end


function lib:find_port_and_field(tag, mask)
	if not (tag.sub(1, 1) == ":") then
		tag = ":" .. tag
	end

	local port = manager.machine.ioport.ports[tag]
	if not port then
		return
	end

	for k,v in pairs(port.fields) do
		if v.mask == tonumber(mask) then
			return v
		end
	end
end


function lib:quoted_string_split(text)
	local result = {}
	local e = 0
	local i = 1
	while true do
		local b = e+1
		b = text:find("%S",b)
		if b==nil then break end
		if text:sub(b,b)=="'" then
			e = text:find("'",b+1)
			b = b+1
		elseif text:sub(b,b)=='"' then
			e = text:find('"',b+1)
			b = b+1
		else
			e = text:find("%s",b+1)
		end
		if e==nil then e=#text+1 end

		result[i] = text:sub(b,e-1)
		i = i + 1
	end
	return result
end

function lib:split(text, delim)
    -- returns an array of fields based on text and delimiter (one character only)
    local result = {}
    local magic = "().%+-*?[]^$"

    if delim == nil then
        delim = "%s"
    elseif string.find(delim, magic, 1, true) then
        -- escape magic
        delim = "%"..delim
    end

    local pattern = "[^"..delim.."]+"
    for w in string.gmatch(text, pattern) do
        table.insert(result, w)
    end
    return result
end


return lib

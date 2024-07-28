local lib = {}

local utility = require('oasis/system/utility')


function lib:execute(args)
	local field = utility:find_port_and_field(args[2], args[3])
	if not field then
		print("@ERROR ### Can't find field mask '" .. tostring(tonumber(args[3])) .. "' on port '" .. args[2] .. "'")
		return
	end
	if not field.enabled then
		print("@ERROR ### Field '" .. args[2] .. "':" .. tostring(tonumber(args[3])) .. " is disabled")
		return
	end

	field:set_value(args[4])

	--print("@OK STATUS ### Field '" .. args[2] .. "':" .. tostring(args[3]) .. " set to " .. tostring(field.user_value))
	print("@OK STATUS ### Field '" .. args[2] .. "':" .. tostring(args[3]) .. " set - (TODO how to get value?)")	
end


return lib

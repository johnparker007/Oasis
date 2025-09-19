local lib = {}

local utility = require('oasis/system/utility')


function lib:execute(args)
	manager.machine.video.throttled = utility:toboolean(args[2])
	print("@OK STATUS ### Throttled set to " .. tostring(manager.machine.video.throttled))
end


return lib

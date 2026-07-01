local lib = {}


function lib:execute(args)
	manager.machine:save(args[2])
	manager.machine:exit()
	print("@OK ### Scheduled state save of '" .. args[2] .. "' and an exit")
end


return lib

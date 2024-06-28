local lib = {}


function lib:execute(args)
	manager.machine:save(args[2])
	print("@OK ### Scheduled state save of '" .. args[2] .. "'")
end


return lib

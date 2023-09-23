local lib = {}


function lib:execute(args)
	manager.machine:load(args[2])
	print("@OK ### Scheduled state load of '" .. args[2] .. "'")
end


return lib

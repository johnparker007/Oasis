local lib = {}


function lib:execute(args)
	manager.machine:exit()
	print("@OK ### Exit scheduled")
end


return lib

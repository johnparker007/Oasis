local lib = {}


function lib:execute(args)
	emu.unpause()
	print("@OK STATUS ### Resumed")
end


return lib

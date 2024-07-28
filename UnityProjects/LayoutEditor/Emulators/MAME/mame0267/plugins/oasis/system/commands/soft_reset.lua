local lib = {}


function lib:execute(args)
	--pause_when_restarted = machine().paused
	manager.machine:soft_reset()
	print("@OK ### Soft Reset Scheduled")
end


return lib

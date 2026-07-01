local lib = {}


function lib:execute(args)
	-- pause_when_restarted = machine().paused
	manager.machine:hard_reset()
	print("@OK ### Hard Reset Scheduled")
end


return lib

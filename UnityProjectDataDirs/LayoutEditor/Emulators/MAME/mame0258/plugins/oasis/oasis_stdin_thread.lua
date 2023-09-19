local lib = {}

local script = "return io.read()"

local utility = require('oasis/oasis_utility')
local command_processor = require('oasis/oasis_command_processor')

function lib:start()
	local console_thread = emu.thread()
	console_thread:start(script);
	
	print("@Oasis plugin: ### Started thread");	
	
	local initial_prestart_received = false
	local session_active = false
	
	-- we want to hold off until the prestart event; register a handler for it
	emu.register_prestart(function() 
		utility:protected_call(callback_prestart, "callback_prestart")
	end)
	
	function callback_prestart()
		print("@Oasis plugin: ### callback_prestart received");
		
		if not initial_prestart_received then
			print("@Oasis plugin: ### initial_prestart_received")
			initial_prestart_received = true
			
			print("@Oasis plugin: ### session_active")
			session_active = true				
		end	
	end

	-- register another handler to handle commands after prestart
	emu.register_periodic(function()
		utility:protected_call(callback_periodic, "callback_periodic")
	end)
	
	function callback_periodic()
		-- it is essential that we only perform these activities when there
		-- is an active session!
		if session_active then
			-- do we have a command?
			if not (console_thread.yield or console_thread.busy) then
			
				print("@Oasis plugin: ### command line received: " .. console_thread.result)
				-- invoke the command line
				command_processor:invoke_command_line(console_thread.result)

				-- continue on reading
				console_thread:start(script)
			end
		end
	end
end

return lib

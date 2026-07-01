local lib = {}

local script = "return io.read()"

local utility = require('oasis/system/utility')
local command_processor = require('oasis/system/command_processor')

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
	
	-- -- register another handler to output done frames
	-- emu.register_frame_done(function()
		-- utility:protected_call(callback_frame_done, "callback_frame_done")
	-- end)
	
	-- function callback_frame_done()
		-- -- it is essential that we only perform these activities when there
		-- -- is an active session!
		-- if session_active then
		
			-- local screen = manager.machine.screens["screen"]
			
			-- print("@Oasis plugin: ### frame done callback received!")
			
			-- --local screen_size = manager.machine.video:snapshot_size()

			-- --local pixel_data = manager.machine.video:snapshot_pixels()
			
			-- -- TOIMPROVE don't need to output screensize every frame, Oasis MameController can get this 
			-- -- once on init
			-- print("screen_size x: " .. screen.width)
			-- print("screen_size y: " .. screen.height)	
			-- print("pixel_data_start")
			
			-- -- doesn't slow down, so it's not pixels() that's slow, it is printing it to stdout
			-- print(screen:pixels()[50000])
			
			-- --print(screen:pixels())
			-- print("pixel_data_end")	
		
			-- -- -- do we have a command?
			-- -- if not (console_thread.yield or console_thread.busy) then
			
				-- -- print("@Oasis plugin: ### command line received: " .. console_thread.result)
				-- -- -- invoke the command line
				-- -- command_processor:invoke_command_line(console_thread.result)

				-- -- -- continue on reading
				-- -- console_thread:start(script)
			-- -- end
		-- end
	-- end	
end

return lib

local lib = {}


local utility = require('oasis/system/utility')

local command_set_input_value = require('oasis/system/commands/set_input_value')
local command_pause = require('oasis/system/commands/pause')
local command_resume = require('oasis/system/commands/resume')
local command_exit = require('oasis/system/commands/exit')
local command_soft_reset = require('oasis/system/commands/soft_reset')
local command_hard_reset = require('oasis/system/commands/hard_reset')
local command_throttled = require('oasis/system/commands/throttled')
local command_state_load = require('oasis/system/commands/state_load')
local command_state_save = require('oasis/system/commands/state_save')
local command_state_save_and_exit = require('oasis/system/commands/state_save_and_exit')
-- local command_snapshot_pixels = require('oasis/system/commands/snapshot_pixels')


function lib:invoke_command_line(line)
	local commands =
	{
		["exit"]						= command_exit,
		["soft_reset"]					= command_soft_reset,
		["hard_reset"]					= command_hard_reset,
		["throttled"]					= command_throttled,
		["pause"]						= command_pause,
		["resume"]						= command_resume,
		["state_load"]					= command_state_load,
		["state_save"]					= command_state_save,
		["state_save_and_exit"]			= command_state_save_and_exit,
		["set_input_value"]				= command_set_input_value,
		 
		--["snapshot_pixels"]				= command_snapshot_pixels,
		
		-- potentially useful:
		-- ["frame_update"] - if manually controlling via thread in MameController in Oasis
		-- ["get_samples"] - if manually controlling via thread in MameController in Oasis	
		-- ["sleep"]			
		-- ["debugger"]			
		-- ["input"]			
		-- ["set_attenuation"] - if not manually controlling, to allow per game volume setting of mame.exe audio
	}

	-- invoke the appropriate command
	local invocation = (function()
		-- check for "?" syntax
		if (line:sub(1, 1) == "?") then
			command_lua(line:sub(2))
		else
			local args = utility:quoted_string_split(line)
			
			if (commands[args[1]:lower()]) then
				commands[args[1]:lower()]:execute(args)
			else
				command_unknown(args)
			end
		end
	end)

	utility:protected_call(invocation, "invocation")
end

-- arbitrary Lua
function command_lua(expr)
	local func, err = load(expr)
	if not func then
		print("@ERROR ### " .. tostring(err))
		return
	end
	local result = func()
	if (result == nil) then
		print("@OK ### Command evaluated")
	else
		print("@OK ### Command evaluated; result = " .. tostring(result))
	end
end

function command_unknown(args)
	print("@ERROR ### Unrecognized command '" .. args[1] .. "'")
end


return lib

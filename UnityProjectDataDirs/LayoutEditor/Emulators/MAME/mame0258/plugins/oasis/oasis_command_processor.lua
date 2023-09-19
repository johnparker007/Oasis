local lib = {}

local utility = require('oasis/oasis_utility')

-- invokes a command line
function lib:invoke_command_line(line)
	-- command list
	local commands =
	{
		["exit"]						= command_exit,
		["ping"]						= command_ping,
		["sleep"]						= command_sleep,
		["soft_reset"]					= command_soft_reset,
		["hard_reset"]					= command_hard_reset,
		["throttled"]					= command_throttled,
		["throttle_rate"]				= command_throttle_rate,
		["frameskip"]					= command_frameskip,
		["pause"]						= command_pause,
		["resume"]						= command_resume,
		["debugger"]					= command_debugger,
		["input"]						= command_input,
		["paste"]						= command_paste,
		["set_attenuation"]				= command_set_attenuation,
		["set_natural_keyboard_in_use"]	= command_set_natural_keyboard_in_use,
		["state_load"]					= command_state_load,
		["state_save"]					= command_state_save,
		["state_save_and_exit"]			= command_state_save_and_exit,
		["save_snapshot"]				= command_save_snapshot,
		["begin_recording"]				= command_begin_recording,
		["end_recording"]				= command_end_recording,
		["load"]						= command_load,
		["unload"]						= command_unload,
		["create"]						= command_create,
		["change_slots"]				= command_change_slots,
		["seq_set"]						= command_seq_set,
		["seq_poll_start"]				= command_seq_poll_start,
		["seq_poll_stop"]				= command_seq_poll_stop,
		["set_input_value"]				= command_set_input_value,
		["set_mouse_enabled"]			= command_set_mouse_enabled,
		["show_profiler"]				= command_show_profiler,
		["set_cheat_state"]				= command_set_cheat_state,
		["dump_status"]					= command_dump_status
	}

	-- invoke the appropriate command
	local invocation = (function()
		-- check for "?" syntax
		if (line:sub(1, 1) == "?") then
			command_lua(line:sub(2))
		else
			-- split the arguments and invoke
			local args = utility:quoted_string_split(line)
			
			-- print("@Oasis ### " .. args[1])
			-- print("@Oasis ### " .. args[2])
			-- print("@Oasis ### " .. args[3])
			-- print("@Oasis ### " .. args[4])
			
			if (commands[args[1]:lower()]) then
				commands[args[1]:lower()](args)
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

-- not implemented command
function command_nyi(args)
	print("@ERROR ### Command '" .. args[1] .. "' not yet implemeted")
end

-- unknown command
function command_unknown(args)
	print("@ERROR ### Unrecognized command '" .. args[1] .. "'")
end


-- SET_INPUT_VALUE command
function command_set_input_value(args)
	local field = utility:find_port_and_field(args[2], args[3])
	if not field then
		print("@ERROR ### Can't find field mask '" .. tostring(tonumber(args[3])) .. "' on port '" .. args[2] .. "'")
		return
	end
	if not field.enabled then
		print("@ERROR ### Field '" .. args[2] .. "':" .. tostring(tonumber(args[3])) .. " is disabled")
		return
	end

	field:set_value(args[4])

	--print("@OK STATUS ### Field '" .. args[2] .. "':" .. tostring(args[3]) .. " set to " .. tostring(field.user_value))
	print("@OK STATUS ### Field '" .. args[2] .. "':" .. tostring(args[3]) .. " set - TODO how to get value ")	
end


return lib

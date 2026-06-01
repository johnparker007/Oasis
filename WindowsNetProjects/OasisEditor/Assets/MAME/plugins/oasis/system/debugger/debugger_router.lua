local protocol = require('oasis/system/debugger/debugger_protocol')
local state = require('oasis/system/debugger/debugger_state')

local lib = {}

local function debugger()
	if manager and manager.machine then
		return manager.machine.debugger
	end
	return nil
end

local function current_cpu()
	return state:current_cpu()
end

local function require_debugger()
	local dbg = debugger()
	if not dbg then
		error("MAME debugger is not available. Launch MAME with -debug.")
	end
	return dbg
end

local function cpu_list()
	local result = {}
	local current_tag = state:current_cpu_tag()
	if manager and manager.machine and manager.machine.devices then
		for tag, device in pairs(manager.machine.devices) do
			if device.debug then
				result[#result + 1] = {
					tag = tag,
					name = device.name or tag,
					isCurrent = tag == current_tag
				}
			end
		end
	end
	return result
end

function lib:handle(request)
	local op = request.op
	if op == "ping" then
		return { pong = true, available = state:is_available() }
	elseif op == "status" then
		return state:status()
	elseif op == "cpus" then
		return cpu_list()
	elseif op == "run" then
		require_debugger().execution_state = "run"
		state:emit_transition_if_needed()
		return state:status()
	elseif op == "break" then
		require_debugger().execution_state = "stop"
		state:emit_transition_if_needed()
		return state:status()
	elseif op == "step" then
		require_debugger()
		local cpu = current_cpu()
		if cpu and cpu.debug and cpu.debug.step then
			cpu.debug:step(1)
		else
			debugger():command("step")
		end
		state:emit_transition_if_needed()
		return state:status()
	end

	error("Unsupported debugger operation '" .. tostring(op) .. "'.")
end

return lib

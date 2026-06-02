local state = require('oasis/system/debugger/debugger_state')

local lib = {}

local function debugger()
	if manager and manager.machine then
		return manager.machine.debugger
	end
	return nil
end

local function require_debugger()
	local dbg = debugger()
	if not dbg then
		error("MAME debugger is not available. Launch MAME with -debug.")
	end
	return dbg
end

local function payload(request)
	return request.payload or request
end

local function current_cpu()
	return state:current_cpu()
end

local function normalize_cpu_tag(cpu)
	if cpu and cpu ~= "" then
		return cpu
	end
	local current_tag = state:current_cpu_tag()
	if current_tag and current_tag ~= "" then
		return current_tag
	end
	return ":maincpu"
end

local function require_cpu(cpu_tag)
	local tag = normalize_cpu_tag(cpu_tag)
	if not (manager and manager.machine and manager.machine.devices) then
		error("MAME devices are not available.")
	end
	local cpu = manager.machine.devices[tag]
	if not cpu then
		error("CPU device '" .. tostring(tag) .. "' was not found.")
	end
	if not cpu.debug then
		error("Device '" .. tostring(tag) .. "' does not expose a MAME device_debug interface.")
	end
	return cpu, cpu.debug, tag
end

local function number_value(value, name, required)
	if value == nil then
		if required then
			error("Missing numeric field '" .. name .. "'.")
		end
		return nil
	end
	local value_type = type(value)
	if value_type == "number" then
		return value
	end
	if value_type == "string" then
		local parsed = tonumber(value)
		if parsed then
			return parsed
		end
		parsed = tonumber(value:match("^0[xX]([0-9a-fA-F]+)$"), 16)
		if parsed then
			return parsed
		end
	end
	error("Field '" .. name .. "' must be numeric.")
end

local function requested_id(data)
	return number_value(data.mameId or data.debuggerId or data.id, "mameId", true)
end

local function address_space_name(data)
	return data.addressSpace or data.space or "program"
end

local function require_address_space(cpu, data)
	local name = address_space_name(data)
	if not cpu.spaces then
		error("CPU device '" .. tostring(cpu.tag) .. "' does not expose address spaces.")
	end

	local space = cpu.spaces[name]
	if not space then
		error("CPU device '" .. tostring(cpu.tag) .. "' does not expose address space '" .. tostring(name) .. "'.")
	end

	return space, name
end

local function breakpoint_model(bp, cpu_tag)
	return {
		debuggerId = bp.index,
		mameId = bp.index,
		cpu = cpu_tag,
		address = bp.address,
		enabled = bp.enabled,
		condition = bp.condition,
		action = bp.action
	}
end

local function set_breakpoint(debug, address, condition, action)
	-- MAME's Lua binding advertises condition/action as optional, but MAME 0.288
	-- can pass omitted/nil strings through to native code as null const char* values.
	-- Supplying explicit empty strings preserves the debugger's default behavior and
	-- avoids the native strlen crash seen when setting a PC breakpoint from Oasis.
	return debug:bpset(address, condition or "", action or "")
end

local function breakpoint_list(cpu_tag)
	local _, debug, tag = require_cpu(cpu_tag)
	local result = {}
	for _, bp in pairs(debug:bplist()) do
		result[#result + 1] = breakpoint_model(bp, tag)
	end
	table.sort(result, function(a, b) return a.mameId < b.mameId end)
	return result
end

local function watchpoint_type_to_mame(value)
	if value == nil then
		return "rw"
	end
	local text = tostring(value):lower()
	if text == "read" or text == "r" then
		return "r"
	elseif text == "write" or text == "w" then
		return "w"
	elseif text == "readwrite" or text == "read_write" or text == "rw" then
		return "rw"
	end
	error("Unsupported watchpoint type '" .. tostring(value) .. "'.")
end

local function watchpoint_type_from_mame(value)
	if value == "r" then
		return "read"
	elseif value == "w" then
		return "write"
	end
	return "readWrite"
end

local function watchpoint_hit_action(cpu_tag)
	-- MAME exposes wpaddr/wpdata/wpsize to watchpoint conditions/actions.  If the
	-- user did not supply an action, emit a structured Oasis event and then allow
	-- the default debugger stop behavior by not appending a go/run command.
	return 'printf "@OASIS_DEBUG_EVENT {\\"event\\":\\"watchpointHit\\",\\"cpu\\":\\"' .. cpu_tag .. '\\",\\"address\\":%d,\\"data\\":%d,\\"size\\":%d}",wpaddr,wpdata,wpsize'
end

local function watchpoint_model(wp, cpu_tag, space_name)
	return {
		debuggerId = wp.index,
		mameId = wp.index,
		cpu = cpu_tag,
		address = wp.address,
		length = wp.length,
		type = watchpoint_type_from_mame(wp.type),
		enabled = wp.enabled,
		condition = wp.condition,
		action = wp.action,
		addressSpace = space_name
	}
end

local function set_watchpoint(debug, space, type, address, length, condition, action)
	-- Keep optional debugger expressions non-null for the native Lua binding, as
	-- with breakpoints above.
	return debug:wpset(space, type, address, length, condition or "", action or "")
end

local function watchpoint_list(cpu_tag, data)
	local cpu, debug, tag = require_cpu(cpu_tag)
	local space, space_name = require_address_space(cpu, data or {})
	local result = {}
	for _, wp in pairs(debug:wplist(space)) do
		result[#result + 1] = watchpoint_model(wp, tag, space_name)
	end
	table.sort(result, function(a, b) return a.mameId < b.mameId end)
	return result
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
	table.sort(result, function(a, b) return a.tag < b.tag end)
	return result
end

function lib:handle(request)
	local op = request.op
	local data = payload(request)
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
	elseif op == "bp.set" then
		local _, debug, tag = require_cpu(data.cpu or request.cpu)
		local id = set_breakpoint(debug, number_value(data.address, "address", true), data.condition, data.action)
		return breakpoint_model(debug:bplist()[id], tag)
	elseif op == "bp.list" then
		return breakpoint_list(data.cpu or request.cpu)
	elseif op == "bp.enable" then
		local _, debug, tag = require_cpu(data.cpu or request.cpu)
		local id = requested_id(data)
		if debug:bpenable(id) == false then
			error("Breakpoint '" .. tostring(id) .. "' was not found.")
		end
		return breakpoint_model(debug:bplist()[id], tag)
	elseif op == "bp.disable" then
		local _, debug, tag = require_cpu(data.cpu or request.cpu)
		local id = requested_id(data)
		if debug:bpdisable(id) == false then
			error("Breakpoint '" .. tostring(id) .. "' was not found.")
		end
		return breakpoint_model(debug:bplist()[id], tag)
	elseif op == "bp.clear" then
		local _, debug, tag = require_cpu(data.cpu or request.cpu)
		local id = requested_id(data)
		if debug:bpclear(id) == false then
			error("Breakpoint '" .. tostring(id) .. "' was not found.")
		end
		return breakpoint_list(tag)
	elseif op == "wp.set" then
		local cpu, debug, tag = require_cpu(data.cpu or request.cpu)
		local action = data.action
		if action == nil or action == "" then
			action = watchpoint_hit_action(tag)
		end
		local space, space_name = require_address_space(cpu, data)
		local id = set_watchpoint(debug, space, watchpoint_type_to_mame(data.type), number_value(data.address, "address", true), number_value(data.length, "length", true), data.condition, action)
		return watchpoint_model(debug:wplist(space)[id], tag, space_name)
	elseif op == "wp.list" then
		return watchpoint_list(data.cpu or request.cpu, data)
	elseif op == "wp.enable" then
		local cpu, debug, tag = require_cpu(data.cpu or request.cpu)
		local id = requested_id(data)
		if debug:wpenable(id) == false then
			error("Watchpoint '" .. tostring(id) .. "' was not found.")
		end
		local space, space_name = require_address_space(cpu, data)
		return watchpoint_model(debug:wplist(space)[id], tag, space_name)
	elseif op == "wp.disable" then
		local cpu, debug, tag = require_cpu(data.cpu or request.cpu)
		local id = requested_id(data)
		if debug:wpdisable(id) == false then
			error("Watchpoint '" .. tostring(id) .. "' was not found.")
		end
		local space, space_name = require_address_space(cpu, data)
		return watchpoint_model(debug:wplist(space)[id], tag, space_name)
	elseif op == "wp.clear" then
		local cpu, debug, tag = require_cpu(data.cpu or request.cpu)
		local id = requested_id(data)
		if debug:wpclear(id) == false then
			error("Watchpoint '" .. tostring(id) .. "' was not found.")
		end
		return watchpoint_list(tag, data)
	end

	error("Unsupported debugger operation '" .. tostring(op) .. "'.")
end

return lib

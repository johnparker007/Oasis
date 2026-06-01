local protocol = require('oasis/system/debugger/debugger_protocol')

local lib = {}

lib.last_state = "unknown"
lib.last_cpu = nil
lib.last_pc = nil

local function debugger()
	if manager and manager.machine then
		return manager.machine.debugger
	end
	return nil
end

function lib:is_available()
	return debugger() ~= nil
end

function lib:normalize_state(state)
	if state == "run" or state == "running" then
		return "running"
	elseif state == "stop" or state == "stopped" then
		return "stopped"
	end
	return "unknown"
end

function lib:current_cpu()
	local dbg = debugger()
	if dbg and dbg.visible_cpu then
		return dbg.visible_cpu
	end
	if manager and manager.machine and manager.machine.devices then
		return manager.machine.devices[":maincpu"]
	end
	return nil
end

function lib:current_cpu_tag()
	local cpu = self:current_cpu()
	if cpu and cpu.tag then
		return cpu.tag
	end
	return nil
end

function lib:current_pc()
	local cpu = self:current_cpu()
	if cpu and cpu.state and cpu.state["PC"] then
		local status, value = pcall(function() return cpu.state["PC"].value end)
		if status then
			return value
		end
	end
	return nil
end

function lib:status()
	local dbg = debugger()
	local state = "unknown"
	if dbg and dbg.execution_state then
		state = self:normalize_state(dbg.execution_state)
	end
	return {
		available = dbg ~= nil,
		state = state,
		cpu = self:current_cpu_tag(),
		pc = self:current_pc()
	}
end

function lib:emit_transition_if_needed()
	local status = self:status()
	local state_changed = status.state ~= self.last_state
	self.last_state = status.state
	self.last_cpu = status.cpu
	self.last_pc = status.pc
	if state_changed and (status.state == "running" or status.state == "stopped") then
		protocol:write_event({ event = status.state, state = status.state, cpu = status.cpu, pc = status.pc })
	end
end

return lib

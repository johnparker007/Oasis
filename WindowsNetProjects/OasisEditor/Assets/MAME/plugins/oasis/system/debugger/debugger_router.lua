local lib = {}

local protocol = require('oasis/system/debugger/debugger_protocol')
local state = require('oasis/system/debugger/debugger_state')

local function try_call(callback)
    local ok, result = pcall(callback)
    if ok then
        return result
    end
    return nil
end

local function get_debugger()
    return try_call(function() return manager.machine.debugger end)
end

local function debugger_available()
    return get_debugger() ~= nil
end

local function command_debugger(command)
    local debugger = get_debugger()
    if debugger and debugger.command then
        return try_call(function() debugger:command(command) end)
    end
    return nil
end

local function debugger_execution_state()
    local debugger = get_debugger()
    if debugger then
        return try_call(function() return debugger.execution_state end)
    end
    return nil
end

local function get_cpu_devices()
    local cpus = {}
    local devices = try_call(function() return manager.machine.devices end)
    if devices then
        for tag, device in pairs(devices) do
            local is_cpu = try_call(function() return device.debug ~= nil end)
            if is_cpu then
                table.insert(cpus, {
                    tag = tostring(tag),
                    name = tostring(try_call(function() return device.name end) or tag),
                    isCurrent = #cpus == 0
                })
            end
        end
    end
    return cpus
end

local function current_cpu_tag()
    if state.current_cpu then
        return state.current_cpu
    end

    local visible_cpu = try_call(function() return get_debugger().visible_cpu end)
    if visible_cpu then
        local tag = try_call(function() return visible_cpu.tag end)
        if tag then return tostring(tag) end
    end

    local cpus = get_cpu_devices()
    if #cpus > 0 then
        return cpus[1].tag
    end
    return nil
end

local function get_cpu(cpu_tag)
    if not cpu_tag then return nil end
    return try_call(function() return manager.machine.devices[cpu_tag] end)
end

local function get_pc(cpu_tag)
    local cpu = get_cpu(cpu_tag)
    if not cpu then return nil end

    local pc = try_call(function() return cpu.state["PC"].value end)
    if pc ~= nil then return pc end
    pc = try_call(function() return cpu.state["CURPC"].value end)
    if pc ~= nil then return pc end
    pc = try_call(function() return cpu:state_string(0) end)
    return tonumber(pc)
end

local function build_status(message)
    local raw_state = debugger_execution_state()
    local execution_state = "unknown"
    if raw_state == "stop" then
        execution_state = "stopped"
    elseif raw_state == "run" then
        execution_state = "running"
    end

    local cpu = current_cpu_tag()
    return {
        isAvailable = debugger_available(),
        executionState = execution_state,
        state = execution_state,
        currentCpu = cpu,
        cpu = cpu,
        programCounter = get_pc(cpu),
        pc = get_pc(cpu),
        message = message
    }
end

local function emit_transition(status)
    if state:update(status) then
        protocol:write_event({
            event = status.executionState,
            state = status.executionState,
            cpu = status.currentCpu,
            pc = status.programCounter
        })
    end
end

function lib:handle(request)
    local op = request.op

    if op == "ping" then
        return { pong = "pong" }
    end

    if op == "status" then
        local status = build_status(nil)
        state:update(status)
        return status
    end

    if op == "cpus" then
        return get_cpu_devices()
    end

    if op == "run" then
        local cpu = get_cpu(current_cpu_tag())
        local ran = try_call(function() cpu.debug:go() end)
        if ran == nil then
            local debugger = get_debugger()
            try_call(function() debugger.execution_state = "run" end)
            command_debugger("go")
        end
        local status = build_status(nil)
        if status.executionState == "unknown" then status.executionState = "running"; status.state = "running" end
        emit_transition(status)
        return status
    end

    if op == "break" then
        local debugger = get_debugger()
        try_call(function() debugger.execution_state = "stop" end)
        local status = build_status(nil)
        if status.executionState == "unknown" then status.executionState = "stopped"; status.state = "stopped" end
        emit_transition(status)
        return status
    end

    if op == "step" then
        local cpu_tag = request.cpu or (request.params and request.params.cpu) or current_cpu_tag()
        state.current_cpu = cpu_tag
        local cpu = get_cpu(cpu_tag)
        local stepped = try_call(function() cpu.debug:step() end)
        if stepped == nil then
            command_debugger("step")
        end
        local status = build_status(nil)
        status.executionState = "stopped"
        status.state = "stopped"
        emit_transition(status)
        return status
    end

    error("Unsupported debugger operation '" .. tostring(op) .. "'.")
end

return lib

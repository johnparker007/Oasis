local lib = {}

lib.last_state = "unknown"
lib.current_cpu = nil
lib.current_pc = nil

function lib:update(status)
    local previous_state = self.last_state
    self.last_state = status.state or "unknown"
    self.current_cpu = status.currentCpu or status.current_cpu or status.cpu
    self.current_pc = status.programCounter or status.pc
    return previous_state ~= self.last_state
end

return lib

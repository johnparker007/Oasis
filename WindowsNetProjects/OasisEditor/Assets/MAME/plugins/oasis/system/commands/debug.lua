local lib = {}

local protocol = require('oasis/system/debugger/debugger_protocol')
local router = require('oasis/system/debugger/debugger_router')

function lib:execute(args)
	local payload = args[2]
	if not payload then
		protocol:write_response(0, false, {}, { code = "missing_payload", message = "Debugger command requires a JSON payload." })
		return
	end

	local ok, request = pcall(function() return protocol:decode(payload) end)
	if not ok or not request then
		protocol:write_response(0, false, {}, { code = "invalid_json", message = tostring(request) })
		return
	end

	local request_id = request.id or 0
	local handled, result = pcall(function() return router:handle(request) end)
	if handled then
		protocol:write_response(request_id, true, result, nil)
	else
		protocol:write_response(request_id, false, {}, { code = "debugger_error", message = tostring(result) })
	end
end

return lib

local lib = {}

local protocol = require('oasis/system/debugger/debugger_protocol')
local router = require('oasis/system/debugger/debugger_router')

function lib:execute(args)
    local payload_parts = {}
    for index = 2, #args do
        table.insert(payload_parts, args[index])
    end
    local json = table.concat(payload_parts, " ")
    local request, decode_error = protocol:decode_request(json)
    if not request then
        protocol:write_response(0, false, nil, decode_error)
        return
    end

    local ok, result = pcall(function() return router:handle(request) end)
    if ok then
        protocol:write_response(request.id, true, result or {})
    else
        protocol:write_response(request.id, false, nil, tostring(result))
    end
end

return lib

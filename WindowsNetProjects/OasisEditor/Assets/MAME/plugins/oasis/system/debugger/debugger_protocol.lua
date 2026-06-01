local lib = {}

lib.response_prefix = "@OASIS_DEBUG"
lib.event_prefix = "@OASIS_DEBUG_EVENT"

local function escape_string(value)
    value = tostring(value)
    value = value:gsub('\\', '\\\\')
    value = value:gsub('"', '\\"')
    value = value:gsub('\n', '\\n')
    value = value:gsub('\r', '\\r')
    value = value:gsub('\t', '\\t')
    return '"' .. value .. '"'
end

function lib:encode_json(value)
    local value_type = type(value)
    if value_type == "nil" then
        return "null"
    end
    if value_type == "boolean" or value_type == "number" then
        return tostring(value)
    end
    if value_type == "string" then
        return escape_string(value)
    end
    if value_type ~= "table" then
        return escape_string(value)
    end

    local is_array = true
    local max_index = 0
    for key, _ in pairs(value) do
        if type(key) ~= "number" or key < 1 or key % 1 ~= 0 then
            is_array = false
            break
        end
        if key > max_index then
            max_index = key
        end
    end

    local parts = {}
    if is_array then
        for index = 1, max_index do
            table.insert(parts, self:encode_json(value[index]))
        end
        return "[" .. table.concat(parts, ",") .. "]"
    end

    for key, item in pairs(value) do
        table.insert(parts, escape_string(key) .. ":" .. self:encode_json(item))
    end
    return "{" .. table.concat(parts, ",") .. "}"
end

local function read_string(json, key)
    return json:match('"' .. key .. '"%s*:%s*"([^"\\]*)"')
end

local function read_number(json, key)
    local value = json:match('"' .. key .. '"%s*:%s*(-?%d+)')
    if value then
        return tonumber(value)
    end
    return nil
end

function lib:decode_request(json)
    if type(json) ~= "string" or json == "" then
        return nil, "Missing JSON request payload."
    end

    local request = {
        id = read_number(json, "id"),
        op = read_string(json, "op"),
        cpu = read_string(json, "cpu")
    }

    request.params = {
        cpu = read_string(json, "cpu")
    }

    if not request.id then
        return nil, "Debugger request is missing numeric id."
    end
    if not request.op or request.op == "" then
        return nil, "Debugger request is missing op."
    end

    return request, nil
end

function lib:write_response(id, ok, result, error_message)
    local payload = {
        id = id,
        ok = ok,
        result = result,
        error = error_message
    }
    print(lib.response_prefix .. " " .. self:encode_json(payload))
end

function lib:write_event(event)
    print(lib.event_prefix .. " " .. self:encode_json(event))
end

return lib

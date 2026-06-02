local lib = {}

lib.response_prefix = "@OASIS_DEBUG"
lib.event_prefix = "@OASIS_DEBUG_EVENT"

local json_module = nil
pcall(function()
	json_module = require("json")
end)

local function escape_string(value)
	value = tostring(value)
	value = value:gsub('\\', '\\\\')
	value = value:gsub('"', '\\"')
	value = value:gsub('\b', '\\b')
	value = value:gsub('\f', '\\f')
	value = value:gsub('\n', '\\n')
	value = value:gsub('\r', '\\r')
	value = value:gsub('\t', '\\t')
	return '"' .. value .. '"'
end

local function is_array(value)
	local count = 0
	for key, _ in pairs(value) do
		if type(key) ~= "number" then
			return false
		end
		if key > count then
			count = key
		end
	end
	return count > 0
end

local function encode_value(value)
	local value_type = type(value)
	if value_type == "nil" then
		return "null"
	elseif value_type == "boolean" then
		return value and "true" or "false"
	elseif value_type == "number" then
		return tostring(value)
	elseif value_type == "string" then
		return escape_string(value)
	elseif value_type == "table" then
		local parts = {}
		if is_array(value) then
			for i = 1, #value do
				parts[#parts + 1] = encode_value(value[i])
			end
			return "[" .. table.concat(parts, ",") .. "]"
		end

		for key, item in pairs(value) do
			parts[#parts + 1] = escape_string(key) .. ":" .. encode_value(item)
		end
		return "{" .. table.concat(parts, ",") .. "}"
	end

	return escape_string(tostring(value))
end

function lib:encode(value)
	if json_module then
		if json_module.stringify then
			return json_module.stringify(value)
		end
		if json_module.encode then
			return json_module.encode(value)
		end
	end
	return encode_value(value)
end

local function unescape_json_string(value)
	value = value:gsub('\\"', '"')
	value = value:gsub('\\\\', '\\')
	value = value:gsub('\\/', '/')
	value = value:gsub('\\b', '\b')
	value = value:gsub('\\f', '\f')
	value = value:gsub('\\n', '\n')
	value = value:gsub('\\r', '\r')
	value = value:gsub('\\t', '\t')
	return value
end

local function match_json_string_field(payload, field)
	local _, value_start = payload:find('"' .. field .. '"%s*:%s*"')
	if not value_start then
		return nil
	end

	local value = {}
	local escaped = false
	for i = value_start + 1, #payload do
		local char = payload:sub(i, i)
		if escaped then
			value[#value + 1] = '\\' .. char
			escaped = false
		elseif char == '\\' then
			escaped = true
		elseif char == '"' then
			return unescape_json_string(table.concat(value))
		else
			value[#value + 1] = char
		end
	end

	return nil
end

function lib:decode(payload)
	if json_module then
		if json_module.parse then
			return json_module.parse(payload)
		end
		if json_module.decode then
			return json_module.decode(payload)
		end
	end

	-- Fallback decoder for Oasis debugger requests.  It supports the request
	-- shapes emitted by MameDebuggerProtocol, including the flat Phase 1/2
	-- shape and the Phase 3 payload object shape.
	local result = {}
	local id = payload:match('"id"%s*:%s*(%-?%d+)')
	local op = match_json_string_field(payload, "op")
	local cpu = match_json_string_field(payload, "cpu")
	if id then
		result.id = tonumber(id)
	end
	if op then
		result.op = op
	end
	if cpu then
		result.cpu = cpu
	end

	local payload_object = payload:match('"payload"%s*:%s*(%b{})')
	if payload_object then
		local decoded_payload = {}
		local function read_number(field)
			local value = payload_object:match('"' .. field .. '"%s*:%s*(%-?%d+)')
			if value then
				decoded_payload[field] = tonumber(value)
			end
		end

		local function read_string(field)
			local value = match_json_string_field(payload_object, field)
			if value then
				decoded_payload[field] = value
			end
		end

		read_string("cpu")
		read_string("condition")
		read_string("action")
		read_string("type")
		read_string("addressSpace")
		read_number("address")
		read_number("length")
		read_number("mameId")
		read_number("debuggerId")
		result.payload = decoded_payload
	end

	return result
end

function lib:write_response(id, ok, result, error)
	print(lib.response_prefix .. " " .. self:encode({ id = id, ok = ok, result = result or {}, error = error }))
end

function lib:write_event(event)
	print(lib.event_prefix .. " " .. self:encode(event))
end

return lib

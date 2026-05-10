local exports = {}
exports.name = "oasis"
exports.version = "0.0.1"
exports.description = "Oasis plugin"
exports.license = "MIT"
exports.author = { name = "John Parker" }


local oasis = exports

local utility = require('oasis/system/utility')
local stdin_thread = require('oasis/system/stdin_thread')


function oasis.startplugin()
	utility:protected_call(startplugin, "startplugin")
end

function startplugin()
	stdin_thread:start()
end


return exports

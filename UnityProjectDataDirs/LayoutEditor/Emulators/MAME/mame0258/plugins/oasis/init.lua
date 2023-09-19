local exports = {}
exports.name = "oasis"
exports.version = "0.0.1"
exports.description = "Oasis plugin"
exports.license = "MIT"
exports.author = { name = "John Parker" }


local oasis = exports

local utility = require('oasis/oasis_utility')
local stdin_thread = require('oasis/oasis_stdin_thread')


function oasis.startplugin()
	print("@Oasis plugin: ### stdin thread start called 1")
	utility:protected_call(startplugin, "startplugin")
end

function startplugin()
	stdin_thread:start()
	
	print("@Oasis plugin: ### stdin thread start called 2")
end


return exports

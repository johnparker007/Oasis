local lib = {}


function lib:execute(args)
	-- From MAME docs:
	-- Returns the pixels of a snapshot created using the current snapshot target configuration
	-- as 32-bit integers packed into a binary string in host Endian order. Pixels are organised
	-- in row-major order, from left to right then top to bottom. Pixel values are colours in RGB
	-- format packed into 32-bit integers.
	
	-- TODO check if better/faster to use screen[0].pixels??  prob not...
	
	--local screen_size = manager.machine.video:snapshot_size()

	--local pixel_data = manager.machine.video:snapshot_pixels()
	
	-- TOIMPROVE don't need to output screensize every frame, Oasis MameController can get this 
	-- once on init
	
	-- THIS IS ALL TOO SLOW IN TESTING, WILL NEED TO READ INTO HOW OTHER STUFF LIKE RETROARCH
	-- CAN DO IT, THEN POTENTIALLY IMPLEMENT IN MAME.  -attach_window is not good enough I don't think
	
	--print("screen_size: " .. screen_size)
	--print("pixel_data_start")
	--print(pixel_data)
	--print("pixel_data_end")
		
end


return lib

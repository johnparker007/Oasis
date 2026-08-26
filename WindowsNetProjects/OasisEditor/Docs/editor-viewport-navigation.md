# Editor viewport navigation

Editor viewports use mouse-wheel, cursor-centred zoom and middle-mouse drag to pan. `Fit` calculates the current content-to-viewport scale, centres the content, and clears pan. Fit is an action, not the meaning of 100%.

For raster views, **100% means Actual Pixels**: one authored source-raster pixel maps to one physical output pixel where WPF and the display permit. WPF device-independent sizes are therefore divided by the visual's DPI scale. Zoom percentages always describe this actual content scale and are never relative to Fit. A bounded preview can become visibly pixelated at high zoom; it does not change the authored dimensions, coordinates, or percentage semantics. Full-resolution/adaptive raster rendering is a separate future refinement.

Viewport state is transient editor state. Zooming, panning, fitting, and pointer-status updates are not persisted or undoable, do not dirty authored content, and must not invoke image processing or builds.

The compact bottom status bar contains content dimensions, cursor coordinates or contextual status, Fit, and an editable zoom percentage/control. Raster coordinates are zero-based authored-source pixel indices; they show a neutral placeholder outside the image.

Raster and logical views share navigation and control mechanics, while each view defines its content units. PR 1 applies these conventions only to primary and Override Artwork Geometry. Panel2D, Face Layout/Edit, Components Edit, and Illumination Lamps retain their existing behaviour until their deliberate later migration.

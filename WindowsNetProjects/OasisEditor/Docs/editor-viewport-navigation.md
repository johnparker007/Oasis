# Editor viewport navigation

Editor viewports use mouse-wheel, cursor-centred zoom and middle-mouse drag to pan. `Fit` calculates the current content-to-viewport scale, centres the content, and clears pan. Fit is an action, not the meaning of 100%.

For raster views, **100% means Actual Pixels**: one authored source-raster pixel maps to one physical output pixel where WPF and the display permit. For logical views, **100% means one logical document unit per physical display pixel**. WPF device-independent sizes are therefore divided by the visual's DPI scale. Zoom percentages always describe the actual content scale and are never relative to Fit.

Viewport state is transient editor state. Zooming, panning, fitting, and pointer-status updates are not persisted or undoable, do not dirty authored content, and must not invoke image processing or builds.

The compact bottom status bar contains content dimensions, cursor coordinates or contextual status, Fit, and an editable zoom percentage/control. Coordinates use the host's content space and show a neutral placeholder outside its meaningful bounds. The zoom tooltip identifies raster Actual Pixels or logical-unit semantics without adding explanatory status text.

The adopted views are primary and Override Artwork Geometry, Panel2D edit, and the shared logical Face edit surface used by Layout, Components, Illumination, and calibration destinations. Panel2D bounds use the first valid Background, otherwise the union of elements and Face Source Shapes. Its coordinates are the existing Panel2D document units. Face bounds use the canonical logical `SourceRegion` extent, falling back to the native 1024 × 1024 extent; Override raster resolution never changes Face-space coordinates.

Fit uses those bounds, current viewport size, and current DPI, centres content, and clears pan. Non-zero Panel2D origins are retained. Explicit percentages and resize do not activate a persistent Fit mode. At magnified scales, editor artwork/background rasters use nearest-neighbour sampling so source pixels remain hard-edged; vector overlays, handles, and text retain their normal rendering policies.

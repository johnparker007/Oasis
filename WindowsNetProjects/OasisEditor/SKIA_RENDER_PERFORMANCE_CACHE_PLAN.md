# Skia Render Performance Cache Plan

This document defines the next urgent workstream on the `codex/rebuild-skia-edit-view-from-game-view` branch: profiling and optimizing the Skia renderer with caching/layering rather than continuing broad renderer architecture changes.

## Current Problem

The new Skia Play View and Skia Edit View render correctly, but CPU usage remains high during busy emulation phases.

Observed behavior:

- many text lamps flashing rapidly can drive CPU to around 25-30%;
- opening both Edit View and Play View side-by-side can push CPU above 40%;
- performance remains poor during genuine high-change phases, so simply avoiding redraws when nothing changed is not enough.

This means the bottleneck is now likely in the Skia renderer implementation, not the old WPF visual tree.

## Key Direction

Do not rethink the whole UI architecture again yet.

Do not revert to WPF rendering.

Do not rely on `only draw when changed` as the primary optimization.

Instead, convert expensive per-frame immediate-mode drawing into cached rendering:

```text
expensive render work once
    -> cached bitmap/picture/layer
    -> cheap draw/blit during runtime frames
```

## Main Suspected Bottlenecks

### Text Lamps

Current text lamp rendering appears to do expensive work every frame:

- parse colors;
- create `SKPaint`;
- resolve typeface;
- measure text;
- wrap text;
- compute line layout;
- draw text.

For text lamps with simple on/off runtime state, this should become:

```text
pre-render off visual once
pre-render on visual once
runtime frame: draw cached bitmap/picture for current state
```

### Alpha / Segment Displays

Alpha display rendering currently loops through cells and segment paths every frame.

For segment displays, use cache entries keyed by:

```text
component id / display style / mask / brightness bucket / size / colors
```

Initial cache can be simple and pragmatic:

- cache recently used mask renders;
- invalidate cache when element size/style/color changes;
- quantize brightness if needed.

### Multiple Views

Edit View and Play View currently each render their own Skia surface.

Side-by-side views can roughly double CPU during busy frames.

Initial goal is not necessarily shared surface compositing, but Codex should measure and document whether view duplication is the dominant cost.

## Required First Step: Instrumentation

Before optimizing heavily, add focused instrumentation that can be enabled/disabled.

Instrumentation should report:

- total frame render time;
- per-renderer time:
  - background;
  - lamps;
  - text lamps;
  - alpha displays;
  - seven-segment displays;
  - reels;
- element counts per frame;
- text layout computations per frame;
- text cache hits/misses;
- segment cache hits/misses;
- number of SKPaint/SKPath/SKBitmap/SKPicture-like allocations if practical;
- Edit View FPS/render count;
- Play View FPS/render count.

Keep logging throttled.

Suggested output cadence:

```text
once every 2-5 seconds while diagnostics enabled
```

Do not spam Output Log for every frame.

## Diagnostics UI / Switch

Add a simple internal toggle if practical:

```text
Enable Skia renderer diagnostics
```

This can initially be:

- a debug constant;
- a hidden/internal setting;
- a menu item under diagnostics;
- or a temporary code switch documented clearly.

The main requirement is that John can turn it on and get useful timing data.

## Cache Architecture

Add renderer cache services rather than scattering static dictionaries everywhere.

Suggested types:

```text
Panel2DRenderCache
TextLampRenderCache
SegmentDisplayRenderCache
SkiaRenderCacheKey
SkiaRenderCacheInvalidationService
```

Cache inputs should include all data that affects visual output.

For text lamps, include:

- element id;
- width/height;
- display text;
- font family;
- font style;
- font size;
- text color;
- on/off colors;
- relevant lamp style fields;
- on/off/intensity bucket.

For segment/alpha cells, include:

- definition id;
- cell dimensions;
- segment mask;
- brightness bucket;
- on/off colors;
- style fields.

## Text Lamp Cache Strategy

Initial implementation should prioritize text lamps because they are likely the largest cost.

Recommended first pass:

```text
TextLampRenderCache
    GetOrCreate(element, stateBucket)
        -> SKImage/SKBitmap/SKPicture cached visual
```

State buckets:

```text
Off
On
```

If lamp intensity can vary continuously, start with quantized buckets:

```text
0.0, 0.25, 0.5, 0.75, 1.0
```

or initially only support:

```text
Off = intensity <= 0
On = intensity > 0
```

and document limitations.

The cached visual should include:

- background/fill;
- text layout;
- text drawing;
- any borders if applicable.

Runtime frame should only draw the cached visual into the element bounds.

## Text Layout Cache

Even before full bitmap caching, text layout should be cached.

Cache:

- wrapped lines;
- line widths;
- line heights;
- baseline positions.

Keyed by:

- text;
- font;
- font size;
- bounds width/height;
- wrapping/alignment options.

This avoids repeated `MeasureText`/wrapping every frame.

## Color / Paint / Typeface Caching

Avoid per-frame parsing and paint creation where practical.

Cache:

- parsed SKColor values;
- SKTypeface values;
- immutable style descriptors;
- SKPaint templates if safe.

Be careful with disposable Skia objects. Document ownership rules.

## Segment / Alpha Cache Strategy

After text lamps, optimize alpha/segment rendering.

Recommended first pass:

```text
AlphaCellRenderCache
    key = definition + mask + brightnessBucket + colors + scale/size bucket
```

Runtime frame:

```text
for each cell:
    draw cached cell visual
```

If per-cell bitmap caching is awkward, use `SKPicture` or prebuilt paths/paints carefully.

Important: avoid reparsing SVG path data per frame. Existing code already lazy-loads definitions, but drawing every path every frame can still be expensive.

## Static Layer Cache

Add a static panel layer cache after the component caches if needed.

Static layer contains:

- background;
- non-runtime/non-changing visuals;
- possibly off-state visuals for components that are not currently changing.

Invalidate static layer when:

- document layout changes;
- pan/zoom rendering strategy requires it;
- element style changes;
- element order changes;
- assets change.

Do not start here unless instrumentation shows per-element caches are insufficient.

## Multi-View Strategy

If both Edit View and Play View are open, both currently render separately.

After instrumentation, decide whether to:

1. accept doubled render cost after caches;
2. cap inactive/background view to lower FPS;
3. add a shared render snapshot/cache;
4. disable one view's runtime redraw when not visible/focused, if acceptable.

Do not implement option 4 as the primary fix unless John explicitly accepts reduced liveness.

Both views should ideally remain live.

## Performance Targets

Initial practical targets:

- busy lamp phases should not saturate a CPU core;
- one Skia view should be significantly below current 25-30% CPU on John's test layout;
- two visible Skia views should be materially better than current 40%+;
- text lamps should show high cache hit rate during flashing;
- alpha/segment displays should not visibly stall the UI.

## Tests

Add non-visual unit tests for cache keys and invalidation.

Suggested tests:

- text cache key changes when text changes;
- text cache key changes when size changes;
- text cache key changes when font changes;
- text cache key changes when colors change;
- cache hit for repeated same lamp/state;
- cache miss after style change;
- cache hit for repeated alpha mask;
- cache miss for different alpha mask;
- brightness quantization behaves as expected;
- cache eviction does not throw.

Avoid heavy pixel-perfect rendering tests.

## Recommended Codex Steps

### Step 1 - Add Renderer Diagnostics

Add timing/counter instrumentation first.

Do not optimize blindly.

Produce Output Log summary every few seconds when enabled.

### Step 2 - Profile John's Scenario

After diagnostics are added, John should run the busy machine and report:

- total frame time;
- per-renderer time;
- text layout count;
- text cache status if implemented;
- alpha render time;
- Edit vs Play render counts.

### Step 3 - Cache Text Layout

Cache wrapped text layout and measured lines.

Verify diagnostics show layout computations drop sharply.

### Step 4 - Cache Text Lamp Rendered Visuals

Pre-render text lamp on/off visuals or quantized intensity visuals.

Runtime frame should draw cached image/picture.

### Step 5 - Cache Segment / Alpha Cell Visuals

Add alpha/segment cell cache keyed by mask/style/brightness bucket.

Runtime frame should draw cached cell visuals rather than redraw all segment paths repeatedly.

### Step 6 - Reduce Per-Frame Allocations

Remove obvious per-frame allocations:

- repeated `new SKPaint` where avoidable;
- repeated color parsing;
- repeated temporary lists;
- repeated typeface resolution;
- repeated text wrapping.

### Step 7 - Evaluate Static Layer Cache

Only after steps 1-6, decide whether static layer caching is needed.

### Step 8 - Evaluate Multi-View Cost

Measure Edit+Play side-by-side again.

If still high, consider:

- shared cache objects across views;
- lower FPS cap for unfocused view;
- shared rendered snapshot for same document/runtime frame.

## Manual Verification

John should verify:

- diagnostics appear and are readable;
- busy text-lamp machine reports clear timing data;
- text layout/render cache hits are high during flashing;
- CPU drops materially for one view;
- CPU drops materially for two views;
- visual output remains acceptable;
- Play View and Edit View still render correctly;
- pan/zoom/editing still works;
- no crashes from Skia object disposal/caching.

## Out Of Scope

Do not implement yet:

- HLSL/GPU structured-buffer renderer;
- DirectX renderer;
- global architecture rewrite;
- disabling live rendering in Edit View as the primary fix;
- removing Play View liveness as the primary fix.

This workstream is specifically about making the current Skia renderer efficient through profiling, caching, and compositing.

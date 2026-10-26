# Editor GPU rendering experiment

Measured locally on 2026-10-26, Windows 10, NVIDIA RTX 3060, driver 610.88.
Original editor revision: `bb2504d5` (SkiaSharp 2.88.8). Updated stack: SkiaSharp
3.119.4 and OpenTK.GLWpfControl 4.3.3. The modern WPF package requires the
Windows 10.0.19041 target; without it NuGet chooses the .NET Framework build,
which references OpenTK 3 and cannot run against OpenTK 4.

## What changed

- Connected Face, Panel2D and Play viewports use GPU-backed Skia. Disconnected
  offscreen captures retain software rendering. GPU initialization/render failure
  falls back to software; `OASIS_RENDER_BACKEND=cpu` explicitly selects software.
- Hidden/unloaded views release their GPU resources. Drawing is demand-driven.
  Both Skia and the underlying GL/WPF bridge are disposed on unload.
- Lamp text, seven-segment and alpha caches record `SKPicture` drawing commands
  instead of fixed-resolution bitmaps. Layout and geometry caching remain, while
  glyphs and paths rasterize at the current zoom.
- The override alignment view decodes full-resolution sources asynchronously and
  draws the registered quadrilateral through a projective transform. Position,
  scale, opacity, zoom and pan reuse those sources, avoiding the 1000-pixel
  rectified-preview/PNG path. Wheel zoom and middle-button panning are supported.
  The older bounded preview service remains available to non-live consumers.
- Production export remains on its existing quality-first CPU path. Identity
  override registrations now preserve source pixels exactly, and output without
  an enabled override copies the verified Base file exactly. These changes fix
  three regressions exposed by the newer Skia encoder/sampling implementation.

## Results

Median milliseconds per completed render at 1920 x 1080. These include GPU
completion (`GL.Finish`), but exclude WPF presentation, image decoding and asset
import. There are 15 warmup frames and 100 measured frames per case; pan changes
between frames. Lamp state is static and this is not an emulation benchmark.

| Layout | Zoom | Original 2.88 CPU | Updated 3.119 GPU, scalable caches |
| --- | ---: | ---: | ---: |
| Alphabet, 169 elements | 50% | 1.28 | 2.31 |
| Alphabet | 100% | 1.06 | 2.23 |
| Alphabet | 400% | 3.37 | 1.00 |
| Fortune 500 DX, 83 elements | 50% | 2.61 | 2.15 |
| Fortune 500 DX | 100% | 2.99 | 1.32 |
| Fortune 500 DX | 400% | 5.41 | 1.12 |

GPU overhead can make small text-heavy scenes slower. All measured medians remain
well below a 16.7 ms frame budget. Some GPU p95 results are worse than the original
CPU results; the JSON files retain those measurements rather than implying a
universal speedup. The first same-version CPU/GPU prototype showed a much larger
17.2-to-1.8 ms improvement for Fortune at 50%, but that was the **new** Skia CPU
renderer, not the original application baseline. It must not be presented as the
original application's before/after result.

Full-resolution override experiment, 4096 x 3072 source, skewed registration,
1920 x 1080 render target, changing alignment/opacity:

| Backend | Median | p95 |
| --- | ---: | ---: |
| CPU, full source | 30.98 ms | 31.91 ms |
| GPU, full source | 0.75 ms | 2.84 ms |

The previous bounded decode/rectify/PNG preparation took 205 ms and produced
1000 x 794 pixels on this synthetic fixture. That is a one-time preparation
measurement, **not** a previous per-frame cost. The live view avoids that work
for alignment edits, but initial full-resolution decode/upload still costs time.

Raw measurements are in `Results/`. Baseline measurements used the untouched
original editor/test output and the same render loop in an isolated harness.
Failed/empty exploratory imports were rejected and are excluded from results.
No FMLs, imported artwork or test projects are committed.

## Reproduce

Run from the repository root on Windows with the .NET 9 desktop SDK and a working
OpenGL driver. Use a fresh output directory for every run. Source FMLs must be
MFME v20.1. The harness copies them into its disposable workspace and runs the
real importer and save services.

```powershell
$project = 'WindowsNetProjects/OasisEditor/OasisEditor.RenderingBenchmarks'
dotnet run --project $project -c Release -- C:\tmp\oasis-layout-run C:\layouts\example.fml
dotnet run --project $project -c Release -- C:\tmp\oasis-alignment-run --alignment
dotnet run --project $project -c Release -- --wpf C:\tmp\oasis-layout-run\example\Benchmark\Assets\Panel2D\Imported\asset.panel2d C:\tmp\oasis-wpf-run
dotnet run --project $project -c Release -- --wpf-alignment C:\tmp\oasis-alignment-run\source-4096.png C:\tmp\oasis-alignment-wpf-run
```

`--wpf` tests the real Panel2D view in a non-activating window positioned offscreen:
resize, zoom, pan, unload/reload and System/Light/Dark themes. It captures actual
Skia output, not the user's desktop. Repeat with `OASIS_RENDER_BACKEND=cpu` to
verify software rendering. `--wpf-alignment` checks asynchronous full-resolution
loading and GPU rendering in the actual alignment view.

Verification completed: original 1445/1445 editor tests; updated 1448/1448;
165/165 scripting tests; full solution build; GPU and software WPF lifecycle smoke tests; full-resolution
alignment WPF smoke test; visual inspection of layout and alignment captures.
Builds retain existing nullable/analyzer warnings and deprecated Skia drawing API
warnings. The bridge logs an MSAA-framebuffer warning on this machine and proceeds
successfully without MSAA. Native emulation/gameplay and other GPU/driver hardware
were not tested by this rendering experiment.

## Remaining boundaries

This does not accelerate offline calibration, sharpening, export or the CPU Face
lighting compositor. Geometry registration's separate bounded WPF source preview
also remains unchanged. Large source textures consume CPU/GPU memory; this is not
a tiled out-of-core image editor. Driver failure has a software fallback, but
driver/device coverage beyond the machine above remains unverified.

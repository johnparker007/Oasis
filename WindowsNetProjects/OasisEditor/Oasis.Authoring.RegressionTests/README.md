# Oasis authoring regression tests

This portable `net9.0` suite preserves reusable regression coverage from the former WPF test project. Its test sources live here so the suite targets the extracted Core, RuntimeBuild, Rendering2D/3D, Emulation, Scripting, and current Editor contracts directly. The legacy WPF test project and obsolete UI fixtures are retired.

Coverage includes document and asset serialization, machine runtime/build packages, Face artwork generation and processing, Panel/Face rendering, 3D scene semantics, Fabric ABI and platform configuration, imported FML mapping, and scripting/runtime contracts.

The first consolidated run passed **640 of 640 tests** on 2026-10-27:

```powershell
dotnet test WindowsNetProjects/OasisEditor/Oasis.Authoring.RegressionTests/Oasis.Authoring.RegressionTests.csproj
```

WPF-specific XAML/view tests and tests coupled to the retired document tabs, WPF ViewModels, command handlers, and Inspector controls are not carried into this suite. Avalonia headless/session tests cover the current editor interaction and document-history paths. Native integration remains in `OasisEditor.NativeIntegrationTests`, targeting the portable Emulation/Core libraries. Tests require explicitly configured compatible native artifacts; the MSVC layout probe is Windows-specific. This managed regression suite does not establish native emulation, audible playback or desktop interaction.

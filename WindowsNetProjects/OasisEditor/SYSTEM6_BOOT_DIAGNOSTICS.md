# System 6 Boot Diagnostics

## Native alpha diagnostics

The native System 6 DLL export/API `GetAlphaChar` is deprecated for Oasis diagnostics because the current DLL implementation is known to return `0` from `SYSTEM6::GetAlphaChar` and does not reflect the live alpha display state.

Oasis now binds and polls the native segment API `SYSTEM6GetAlphaSegments` instead. Startup/runtime diagnostic polling reads segment positions `0..15` and logs each value in both hexadecimal and decimal form, for example:

```text
System6 alpha segs: [0]=0x0000/0 [1]=0x44CF/17615 ...
```

The compact 16-position line is emitted when the alpha segment values change. If the export is missing, the startup/debug log records that alpha segment polling is disabled.

## Percent switch configuration

The native DLL export `SetPercent(char Percent)` is bound as an optional Native System 6 bridge call. The native implementation maps the low four bits of the supplied value onto percentage switches 8-11.

Oasis stores the project-specific value in Project Settings under the Native DLL ROMs / System6 ROMs settings, alongside the existing System 6 reel opto configuration. The setting is persisted as `project_settings.System6NativeRoms.PercentSwitchValue` and is shown as **Percent switch value**. Valid values are `0..15`; the default is `0`.

During Native System 6 startup, after ROM load/reset and at the same configuration stage as reel optos, Oasis logs whether `SetPercent` was found, the configured project value, and the byte value passed to the DLL.

## If alpha segments remain all zero

If diagnostics continue to log all zero segment values after this change, the log trail should prove that `SYSTEM6GetAlphaSegments` was successfully bound and called for positions `0..15`. Next investigation steps:

1. Confirm the Native System 6 project is reaching the first run loop after ROM load and reset.
2. Compare `SetPercent` and reel opto values with a known-working TechsClass configuration.
3. Check whether the game requires additional door, stake/prize, meter, or DIP switch configuration before writing alpha output.
4. Verify whether the DLL's `SYSTEM6GetAlphaSegments` export expects signed `char` indexing or another display bank selector for this specific game.
5. If values change but render incorrectly, map the returned segment bit layout against Oasis alpha segment definitions before changing the native polling path.

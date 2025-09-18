# LayoutEditor external executables

This directory stores executables that are required by the LayoutEditor project at runtime.
Place Windows-specific tools in the `Windows` subfolder so they can be located by the
`ExternalExecutableUtility` helper and copied into Windows builds. The `test.exe` file is a
placeholder that can be replaced by the real executable in a future commit.

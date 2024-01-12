# Native Menu Bar for Unity

This plugin provides native menu bar functionalities for Windows builds using Unity.

![Screenshot1](Screen1.png)
![Screenshot1](Screen2.png)
![Screenshot1](Screen3.png)

# Feature

| Features/Plateforms    | Windows   | Mac           | Linux         | Editor              |
|------------------------|-----------|---------------|---------------|---------------------|
| Nested MenuItems       | Unlimited | Not Supported | Not Supported | Supported           |
| Enabled/Disabled state | Supported | Not Supported | Not Supported | Supported           |
| Toggle state           | Supported | Not Supported | Not Supported | Not Supported       |
| Keyboard shortcut      | Supported | Not Supported | Not Supported | Partially Supported |


Currently only Windows platform is supported at runtime

# Installation

## From package manager through git

- Add the following line in your manifest.json
```
"com.parazite.nativemenubar": "https://github.com/parazite13/NativeMenuBar.git?path=/NativeMenuBar.Unity/Packages/NativeMenuBar",
```

## As third party asset in your project

 - Download the latest package from the release
 - Add it into your Unity project either in the *Assets* folder or inside the *Packages* folder


# Usage

 - Add the *MenuBar* component to any gameObject
 - Fill the component settings using the inspector

You can follow the example in the sample Unity project under *NativeMenuBar.Unity*
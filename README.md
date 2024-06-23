# Architecture
<!-- Mermaid diagram tools/info:
    https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/creating-diagrams
    https://mermaid.js.org/intro/
    And a live editor for quick testing here: https://mermaid.live/
-->

```mermaid
block-beta
    columns 3
    doc>"Oasis Suite Architecture"]:3

    block:row1:3
        Hub["Launcher / Hub"]
        AssetLibrary[("Asset Libary")]
        MachineDatabase[("Machine Database")]
    end

    block:row2:3
        MfmeLayoutExtractor["MFME Layout Extractor"]
        LayoutEditor["Layout Editor"]
        MameLayoutBuilder["MAME Layout Builder"]
    end

    block:row3:3
        ArcadeSimulator["Arcade Simulator"]
        CabinetEditor["Cabinet Editor"]
        MachinePlayer["Machine Player"]
    end
    
```

# Roadmap

```mermaid
gantt
    title Oasis Components
    dateFormat X
    axisFormat %s
    section Project
    Launcher/Hub : 2, 3        
    section Data
    Machine Database : 4, 5    
    Asset Library : 4, 6    
    section Layout
    MFME Layout Extractor : 0, 2    
    Layout Editor : 1, 7    
    MAME Layout Builder : 2, 3     
    section 3d
    Cabinet Editor : 3, 5    
    Machine Player : 4, 6    
    Arcade Simulator : 5, 8    
```

All TODO items are now being tracked on a private Jira server.  A way may be found to share a public view of this in future.

# Unity technical details
Unity version is 2021 LTS (2021.3.15f1 LTS at the time of writing), as the Arcade Simulator project is built on 2021.2.7f1. The plan is to switch to 2023 LTS when that is released in Q4 2024, as well as general improvements, this will allow for removal of the Unity splashscreen delay when starting the various programs that make up the Oasis suite.

We are using Built-in Render Pipeline, this is due to lightmap baking being impractical for user-created environments (Arcades, Crazy Golf courses, race tracks etc)... all Unity runtime-lightmap baking solutions seemed lacking (on device, remote lightmap baking server etc), and by the time this is a mature product, RTX cards may be relatively standard and affordable, meaning dynamic light/shadows with no baked maps required.

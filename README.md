# Architecture
TODO - test diagram below, further docs on 'mermaid' syntax for expanding this here:

https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/creating-diagrams

https://mermaid.js.org/intro/

And a live editor for quick testing here: https://mermaid.live/

```mermaid
graph TD;
    A-->B;
    A-->C;
    B-->D;
    C-->D;
```

# Roadmap
TODO

# Features
TODO

# To do list

## MFME Tools
- [x] Create as Windows C# Net app

### Extractor
- [x] Implement window capture system
- [ ] Implement scraping (a lot of this can be ported from Arcade Sim source)
  - [ ] Properties window
    - [ ]  Components
      - [ ] Reels
        - [ ] Reel
        - [ ] Disc Reel
        - [ ] TODO other reel types
      - [ ] Lamps
        - [ ] Lamp
        - [ ] Prism Lamp
        - [ ] TODO other lamp types
      - [ ] TODO add remaining components/groups
    - [ ] Font and text (including clicking into Font window for font size/style)
  - [ ] Chr 8x lamp value scraping (only needed for MPU4)
  - [ ] Configuration (will be useful for setting up traits in MAME drivers)
    - [ ] MPU4 (maybe already in ArcadeSim source)
    - [ ] Scorpion 1 (maybe already in ArcadeSim source)
    - [ ] Scorpion 2 (maybe already in ArcadeSim source)
    - [ ] Scorpion 4
    - [ ] Impact (maybe already in ArcadeSim source)
    - [ ] TODO add remaining platforms
    
### Injector
  -[ ] TODO make this list (MFME layout injection is a very low priority feature at present)

## Layout Editor
- [ ] TODO create this list

## Machine Editor
- [ ] TODO create this list

## Machine Player
- [ ] TODO create this list

## Arcade Simulator
- [ ] TODO create this list

## Multiplayer
- [ ] TODO create this list

## Oasis Hub
- [ ] TODO create this list


# Unity technical details
Unity version is 2021 LTS (2021.3.15f1 LTS at the time of writing), as the Arcade Simulator project is built on 2021.2.7f1. The plan is to switch to 2023 LTS when that is released in Q4 2024, as well as general improvements, this will allow for removal of the Unity splashscreen delay when starting the various programs that make up the Oasis suite.

We are using Built-in Render Pipeline, this is due to lightmap baking being impractical for user-created environments (Arcades, Crazy Golf courses, race tracks etc)... all Unity runtime-lightmap baking solutions seemed lacking (on device, remote lightmap baking server etc), and by the time this is a mature product, RTX cards may be relatively standard and affordable, meaning dynamic light/shadows with no baked maps required.

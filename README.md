# ReferenceFinderWizard

![2024-05-18 09 38 53](https://github.com/Nytra/ResoniteReferenceFinderWizard/assets/14206961/67b351d5-2639-495b-b79d-923b45d2744e)

![2024-05-18 09 40 05](https://github.com/Nytra/ResoniteReferenceFinderWizard/assets/14206961/086d96e4-a927-44f5-89f0-f5e3ad38b5c8)

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that adds a new wizard that lets you find references to things.

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [ReferenceFinderWizard.dll](https://github.com/Nytra/ResoniteReferenceFinderWizard/releases/latest/download/ReferenceFinderWizard.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
3. Get [ContextMenuHookLib](https://git.unix.dog/yosh/ResoniteContextMenuHookLib/releases) and put it in 'rml_mods' also.
4. Start the game. If you want to verify that the mod is working you can check your Resonite logs.

## Usage

When you have a Dev Tool equipped and are holding a reference, pressing secondary will give you the option to find references to whatever is held

---

Clicking the burger menu next to any field in the inspector will have a new option to find references to the field

---

The wizard can be found in the DevTool's 'Create New' menu under Editors/Reference Finder Wizard (Mod). <br>

You can drop any world element into the labeled field to search for anything in the world that references it. <br>
  
There are options to search for references to children slots, contained components, or contained members of the search element (which includes fields and lists etc). <br>
  
There are also options to filter out certain references that you may not be interested in, for example if they are non-persistent, inside of inspector UI, or contained within the search element itself. <br>
  
You can check `Spawn detail text` which gives more detailed and structured information which includes the reference ID's of each reference, the hierarchy path to get to them, and optionally the Type of the reference. <br>

# Underground Resource List - RimWorld Mod

A RimWorld mod that adds a button to the bottom toolbar which opens a window displaying a list of all available deep resources in the game.

## Features

- **Bottom Toolbar Button**: Adds a new button to RimWorld's bottom toolbar
- **Deep Resource List Window**: Opens a scrollable window showing:
  - Icons for each deep resource type
  - Resource names
  - Deep commonality values
  - Total count of deep resource types

## Setup Instructions

### 1. Install RimWorld
Make sure RimWorld is installed. The game directory is typically located at:
- **Steam**: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`
- **GOG**: Check your GOG installation directory

### 2. Copy Mod to RimWorld
Copy this entire mod folder to RimWorld's Mods directory:
```
RimWorld/Mods/UndergroundResourceList/
```

### 3. Development Setup (For C# Code)

If you plan to write custom C# code:

1. **Install Visual Studio** (Community Edition is free)
   - Make sure to install the ".NET Desktop Development" workload

2. **Create a C# Class Library Project**
   - Create a new project in the `Source` folder (if you create one)
   - Target Framework: `.NET Framework 4.7.2` or `.NET Framework 4.8`
   - Project Type: Class Library

3. **Reference RimWorld Assemblies**
   Add references to these DLLs from your RimWorld installation:
   - `Assembly-CSharp.dll` (located in `RimWorld/RimWorldWin64_Data/Managed/`)
   - `UnityEngine.CoreModule.dll`
   - `UnityEngine.IMGUIModule.dll`
   - `UnityEngine.TextRenderingModule.dll`
   - Other Unity modules as needed

4. **Build Output**
   - Configure your project to output the compiled `.dll` to `Assemblies/YourModName.dll`
   - Or manually copy the compiled DLL to the `Assemblies` folder

### 4. Mod Libraries

**Important**: Do NOT include library DLLs directly in your mod. Instead, declare them as dependencies.

#### HugsLib (Recommended for many mods)
- **Package ID**: `UnlimitedHugs.HugsLib`
- **Steam Workshop**: https://steamcommunity.com/sharedfiles/filedetails/?id=818773962
- **GitHub**: https://github.com/UnlimitedHugs/RimworldHugsLib
- Add it to `modDependencies` in `About/About.xml` if you use it

#### Other Common Libraries
- **Harmony**: Usually included with RimWorld, but check if you need a specific version
- **JecsTools**: For advanced modding features
- **ResearchPal**: If you want to add research nodes

**Rule of Thumb**: If a library is available on Steam Workshop, make it a dependency rather than bundling it.

### 5. Folder Structure

```
UndergroundResourceList/
├── About/
│   └── About.xml                    # Mod metadata
├── Defs/                            # XML definition files
│   └── MainButtons/
│       └── UndergroundResourceListButton.xml  # Button definition
├── Assemblies/                      # Compiled C# DLLs
│   └── UndergroundResourceList.dll  # (after building)
├── Textures/                        # Image files (optional)
│   └── UI/                          # Custom button icon (optional)
├── Languages/                       # Translation files (optional)
│   └── (translation files)
└── Source/                          # C# source code
    ├── UndergroundResourceListMod.cs
    ├── DeepResourceListWindow.cs
    ├── MainButtonWorker_UndergroundResourceList.cs
    └── UndergroundResourceList.csproj
```

## Development Resources

- **Official Wiki**: https://rimworldwiki.com/wiki/Modding_Tutorials
- **Modding Wiki**: https://rimworldmodding.wiki.gg/
- **GitHub Tutorials**: https://github.com/Lobz/modding-Rimworld-tutorial
- **RimWorld Discord**: For community support

## How It Works

1. **MainButtonDef**: The XML file in `Defs/MainButtons/` defines the button that appears in the bottom toolbar
2. **MainButtonWorker**: The C# class handles what happens when the button is clicked (opens the window)
3. **DeepResourceListWindow**: The window class that displays all deep resources (ThingDefs with `deepCommonality > 0`)

## Building the Mod

1. Open `Source/UndergroundResourceList.csproj` in Visual Studio
2. Add references to RimWorld DLLs (see comments in the .csproj file)
3. Build the project (Debug or Release)
4. The compiled DLL will be output to `Assemblies/UndergroundResourceList.dll`
5. Copy the entire mod folder to RimWorld's Mods directory

## Customizing

- **Button Icon**: Add a custom icon to `Textures/UI/` and update the path in `UndergroundResourceListButton.xml`
- **Window Appearance**: Modify `DeepResourceListWindow.cs` to change colors, layout, or add more information
- **Button Position**: Adjust the `order` value in the MainButtonDef XML to change button position

## Notes

- RimWorld uses **XML** for most game content definitions
- **C#** is used for custom logic and behaviors
- Mods are loaded in dependency order
- Test your mod thoroughly before publishing!
- The button icon path may need to be adjusted if the default doesn't work - you can create a custom icon or use an existing RimWorld icon path


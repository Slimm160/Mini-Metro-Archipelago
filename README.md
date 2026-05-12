# Mini Metro for Archipelago
Mini Metro runs on **Unity 2022.3.62f2** | modded using **BepInEx 5** (Mono)

## Setup

```
London
Paris
New York City
Warsaw
Lisbon
Tokyo
Chicago
Budapest
Berlin
Melbourne
Hong Kong
Barcelona
Osaka
Stockholm
Saint Petersburg
Boston
Montreal
San Francisco
Sao Paulo
Seoul
Santiago
Washington, D.C.
Tashkent
Singapore
Cairo
Istanbul
Shanghai
Guangzhou
Nanjing
Chongqing
Mumbai
Addis Ababa
Lagos
Auckland
```

## BepInEx Setup

Mini Metro runs on **Unity 2022.3.62f2** | modded using **BepInEx 5** (Mono)

> **Note:** BepInEx 5.4.23.5 includes a specific fix for Unity 2022.3.62 builds (missing `get_graphicsDeviceID`). Always use this version or newer.

> **Note:** Untested for unix builds.

### 1. Download BepInEx

Download the latest stable release: **BepInEx 5.4.23.5**

Pick the correct build for your Mini Metro build:

| OS | Architecture | Download | SHA-256 | Size |
|---|---|---|---|---|
| Windows | 64-bit | [BepInEx_win_x64_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip)
| Windows | 32-bit *(most common)* | [BepInEx_win_x86_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip)
| Linux | 64-bit | [BepInEx_linux_x64_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_linux_x64_5.4.23.5.zip) | 
| Linux | 32-bit | [BepInEx_linux_x86_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_linux_x86_5.4.23.5.zip) |
| macOS | Universal | [BepInEx_macos_universal_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip) |
| Any | Patcher only | [BepInEx_Patcher_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_Patcher_5.4.23.5.zip) |

### 2. Locate Your Mini Metro Game Folder

1. Open **Steam** and go to your Library.
2. Right-click **Mini Metro → Manage → Browse local files**.
3. This opens the game's root folder — it should contain `Mini Metro.exe`.

### 3. Install BepInEx

1. Extract the downloaded `.zip`.
2. Copy **all** extracted files and folders directly into your Mini Metro root folder.

After extracting, your game folder should look like this:

```
Mini Metro/
├── BepInEx/
├── Mini Metro_Data/
├── Mini Metro.exe
├── doorstop_config.ini    ← added by BepInEx
├── winhttp.dll            ← added by BepInEx (Windows)
└── ...
```

---

## Unstripping Managed Code

> **This step is required.** Mini Metro ships with stripped managed libraries, which causes `MissingMethodException` and `TargetInvocationException` errors at runtime. BepInEx will not load and mods will not work correctly until the unstripped libraries are in place.

Mini Metro's `MiniMetro_Data/Managed/` folder contains stripped versions of core Unity DLLs — they are much smaller than normal because Unity removed unused code at build time. You need to generate full replacement libraries from a clean Unity project and load them via BepInEx's `dll_search_path_override` mechanism.

> **Note:** Pre-built libraries downloaded from the BepInEx library mirror for this version are broken. You must generate them yourself using Unity Hub.

### 4. Install Unity Hub and Unity 2022.3.62f2

1. Download and install [Unity Hub](https://unity.com/download).
2. Open Unity Hub and go to the **Installs** tab.
3. Click **Install Editor** and find **Unity 2022.3.62f2**.
   - If it doesn't appear in the list, find it in the [Unity Download Archive](https://unity.com/releases/editor/archive) and open its link with Unity Hub.
4. Install with default settings — no additional modules are needed.

### 5. Build a Blank Unity Project

1. In Unity Hub go to **Projects → New Project**.
2. Select editor version **2022.3.62f2**.
3. Choose **2D** (template doesn't matter much).
4. Name the project anything and pick a location, then click **Create Project**.
5. Once Unity opens, go to **File → Build and Run** (`Ctrl+B`).
6. Choose a build output folder and remember it (e.g. `C:\UnityBuild\`).
7. Wait for the build to finish. When the built game launches, close it with `Alt+F4`.

### 6. Collect the Unstripped Libraries

Your full unstripped `.dll` files are now at:

```
C:\UnityBuild\<ProjectName>_Data\Managed\
```

Copy all `.dll` files from that folder.

### 7. Set Up `unstripped_libs` in Mini Metro

1. Create a new folder called `unstripped_libs` inside your Mini Metro root:

```
Mini Metro/
├── BepInEx/
├── unstripped_libs/    ← create this
├── Mini Metro.exe
└── ...
```

2. Paste all the copied `.dll` files into `unstripped_libs/`.

3. Open `doorstop_config.ini` in the Mini Metro root folder and add or update this line:

```ini
dll_search_path_override = unstripped_libs
```

4. Save the file.

### 8. Run the Game Once

Launch Mini Metro **through Steam** (not by double-clicking the `.exe`). Let it reach the main menu, then close it.

This first run lets BepInEx initialize and generate its folder structure, including `BepInEx/plugins/` and `BepInEx/config/`.

> **Linux / macOS users:** BepInEx on non-Windows platforms requires running via the provided shell script. Rename `run_bepinex.sh` and follow the [BepInEx Unix setup guide](https://docs.bepinex.dev/articles/advanced/steam_interop.html) to configure Steam's launch options.

Your final folder structure should now look like:

```
Mini Metro/
├── BepInEx/
│   ├── config/
│   ├── core/
│   ├── patchers/
│   └── plugins/
├── unstripped_libs/
│   ├── mscorlib.dll
│   ├── UnityEngine.dll
│   └── ... (all other .dll files)
├── Mini Metro_Data/
├── Mini Metro.exe
├── doorstop_config.ini
└── winhttp.dll
```

---

### Troubleshooting

| Problem | Fix |
|---|---|
| `dll_search_path_override` has no effect | Confirm the line is in `doorstop_config.ini` in the game root, not inside the `BepInEx/` folder |
| Unity 2022.3.62f2 not in Hub install list | Find it in the [Unity Download Archive](https://unity.com/releases/editor/archive) and open with Unity Hub |

---

### References

- [BepInEx GitHub — v5.4.23.5 Release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5)
- [BepInEx Installation Docs](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
- [Unity Managed Code Unstripping — Oxidyze](https://oxidyze.com/post/unity-managed-code-unstripping/)
- [Unity Download Archive](https://unity.com/releases/editor/archive)
- [Archipelago Multiworld](https://archipelago.gg/)

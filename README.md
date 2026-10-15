# Unknowns Client

A casting and utility client for **Gorilla Tag** (Unity 6, URP). It adds a casting camera, HUD,
scoreboards and leaderboards, name tags, a wardrobe, recording, voice and audio tools, lighting,
weather and shader looks, old-update season packs, and VR tablets (camera, utility, ref and SandBox).

The desktop launcher **Unknown's Suite** injects the client with SharpMonoInjector, so it doesn't
need BepInEx. There is also a set of optional BepInEx add-ons.

## Layout

| Folder | What it is |
| --- | --- |
| `UnknownsCastingUI/` | The client (`Unknowns Client.dll`), plus its embedded images, sounds, fonts, models and the prebuilt shader bundle (`Rendering/Bundles/unknowns_shaders`) |
| `UnknownsCameraLauncher/` | The desktop launcher `Unknown's Suite.exe`, which carries the client, bootstrap, Harmony and injector inside it |
| `UnknownsCameraBootstrap/` | The small loader that is injected first and then loads the client |
| `UnknownsUtilitySuite/` | The utility add-on catalog the launcher uses |
| `UnknownsCameraLauncher/SharpMonoInjector/` | SharpMonoInjector (MIT licence), compiled into the launcher |
| `Addons/` | The BepInEx add-on projects (core, per-feature add-ons, Update Changer) |

## Requirements

- Windows with the .NET SDK, version 8 or newer. The projects target .NET Framework 4.8, and the
  reference assemblies come from NuGet.
- Gorilla Tag installed. The build uses the game's DLLs from `Gorilla Tag_Data\Managed`, and those
  are not in this repo.
- BepInEx 5 installed in Gorilla Tag, only if you want the add-ons. Without it, that step is skipped.

## Building

Double-click `BUILD.bat`. It asks for your Gorilla Tag folder, and if it found one already (the
folder from last time, or one through Steam), you can just press Enter. You can also pass the folder:

```bat
BUILD.bat "D:\SteamLibrary\steamapps\common\Gorilla Tag"
```

The release goes to `output\Customer\` (`Unknown's Suite.exe` and `BepInEx Add-ons\`), and the full
log goes to `build.log`.

To build only the client:

```bat
dotnet build UnknownsClient.csproj -c Release "-p:GorillaTagPath=D:\steam\steamapps\common\Gorilla Tag" "-p:CameraEdition=Plus" -o out
```

- `GorillaTagPath` is the Gorilla Tag folder. It defaults to
  `C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag`.
- `CameraEdition` is `Plus` (the default), `Basic` or `Utility`.

## Notes

- The csproj lists every source file by name. Add new `.cs` files to `UnknownsClient.csproj`.
- The shader bundle is built in a separate Unity 6000.2.9f1 project and committed here as a file.
- The Gotham fonts are commercial, so they are not in this repo. If they're in
  `UnknownsCastingUI/Fonts`, the build embeds them; if not, it builds without them.
- The old-update season packs live in their own repo, `Unknowncastingdev/Old-gtag-updates`. The
  Update Changer downloads them.

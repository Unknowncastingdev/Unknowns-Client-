# Unknowns Client

A casting and utility client for **Gorilla Tag** (Unity 6, URP). It adds a casting camera, HUD,
scoreboards and leaderboards, name tags, a wardrobe, recording, voice and audio tools, lighting,
weather and shader looks, old-update season packs, and VR tablets (camera, utility, ref and SandBox).

The client is injected with SharpMonoInjector by the desktop launcher **Unknown's Suite**, so it
needs no BepInEx. A separate set of optional BepInEx add-ons is built too.

## Layout

| Folder | What it is |
| --- | --- |
| `UnknownsCastingUI/` | The client (`Unknowns Client.dll`), plus its embedded images, sounds, fonts, models and the prebuilt shader bundle (`Rendering/Bundles/unknowns_shaders`) |
| `UnknownsCameraLauncher/` | The desktop launcher `Unknown's Suite.exe`, which carries the client, bootstrap, Harmony and injector inside it |
| `UnknownsCameraBootstrap/` | The small loader that is injected first and then loads the client |
| `UnknownsUtilitySuite/` | The utility add-on catalog that the launcher shares |
| `Addons/` | BepInEx add-on projects (core, per-feature add-ons, Update Changer) |
| `ThirdParty/SharpMonoInjector/` | SharpMonoInjector (MIT) |
| `SeasonPacks/` | Unity and Python tools that build the old-update season packs |
| `ExampleUpdatePacks/` | An example custom update pack |
| `Changelogs/` | Release notes for each version (`V12_72_...` to `V13_50_...`) and build notes |

## Requirements

- Windows with a .NET SDK (8 or newer; tested with 10.0.400). The projects target .NET Framework 4.8,
  and the reference assemblies come from NuGet.
- A Gorilla Tag install. The build references the game's DLLs in `Gorilla Tag_Data\Managed`, and
  they are not part of this repo.

## Building

Build only the client:

```bat
dotnet build UnknownsClient.csproj -c Release "-p:GorillaTagPath=D:\steam\steamapps\common\Gorilla Tag" "-p:CameraEdition=Plus" -o out
```

MSBuild properties:

- `GorillaTagPath`: the Gorilla Tag install folder. It defaults to
  `C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag`. `ManagedPath` comes from it
  (`<GorillaTagPath>\Gorilla Tag_Data\Managed`), and you can override `ManagedPath` on its own.
- `CameraEdition`: `Plus` (default), `Basic` or `Utility`. It sets the `UNKNOWNS_PLUS`,
  `UNKNOWNS_BASIC` or `UNKNOWNS_UTILITY` define.

The full release (utility suite, client, bootstrap, SharpMonoInjector, launcher with the payloads
embedded, then the BepInEx add-ons) is built with:

```bat
BUILD.bat Release "D:\steam\steamapps\common\Gorilla Tag"
```

Without a path, `BUILD.bat` looks for Gorilla Tag in the usual Steam folders (`FIND_GORILLA_TAG.ps1`).
It stages the builds in `.build\`, writes `build.log` and puts the release in `output\Customer\`.
The add-ons alone can be built with `BUILD_ADDONS.ps1`, which writes them to `AddonsOutput\`.
If a build fails, `BUILD_DIAGNOSTICS.bat` helps find the cause. See also `README_RELEASE.md`.

Notes:

- The csproj lists every source file explicitly (`EnableDefaultCompileItems=false`). Add new `.cs`
  files to it, and don't remove a file the csproj still references.
- `UnknownsCastingUI/Rendering/Bundles/unknowns_shaders` is built in a separate Unity 6000.2.9f1
  shader project (`BUILD_SHADERS.ps1`) and committed here as a binary.
- The Gotham fonts are commercial and not in this repo. `import_gotham_fonts.ps1` imports them from
  a local archive, and the build skips them when they are missing.

## Not in this repo

These are ignored by `.gitignore`: build output (`bin/`, `obj/`, `.build/`, `output/`,
`AddonsOutput/`, logs) and the old-update season pack pieces (`oldgtag/`). The season packs are
built with `BUILD_SEASON_PACKS.ps1`, split with `SPLIT_SEASON_PACKS.ps1` and published separately,
and the Update Changer downloads them.

## Third-party notices

See `THIRD_PARTY_NOTICES_*.md`, `ThirdParty/SharpMonoInjector.NOTICE.txt` and
`ThirdParty/SharpMonoInjector/LICENSE`.

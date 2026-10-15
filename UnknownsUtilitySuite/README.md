# Unknowns Utility Suite

Modular Utility add-on subsystem used by **Unknown's Suite**.

Initial add-ons:
- Auto Ref
- Self Rig Lerping
- Time Changer
- Player Manager

`UtilityAddonCatalog.cs` is dependency-free shared selection/entitlement code linked into the launcher, bootstrap, and injected client. `Runtime/UtilityAddonPanel.cs` owns the in-game ADD-ONS page integration and is linked into `UnknownsClient.csproj`, so SharpMono still injects a single runtime DLL instead of requiring a second DLL beside the game.

A `util-` key activates a Utility-only account through the same activation flow as BASIC/PLUS. A valid Utility login enables the gear in Unknown's Suite. The selected add-ons are staged into the one-time runtime authorization and intersected with the server-authorized add-on list before the desktop Utility UI builds. The desktop add-on window changes height based on the number of enabled add-ons.

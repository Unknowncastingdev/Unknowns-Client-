using System;
using System.Collections.Generic;
using System.Linq;

namespace UnknownsUtilitySuite
{
    internal static class UtilityAddonIds
    {
        internal const string CastingMod = "casting-mod";
        internal const string AutoRef = "auto-ref";
        internal const string SelfRigLerping = "self-rig-lerping";
        internal const string TimeChanger = "time-changer";
        // Kept as a legacy internal ID so older saved state can be ignored safely.
        internal const string PlayerManager = "player-manager";
        internal const string Wasd = "wasd";
        internal const string LeavesColor = "environment-colors";
        internal const string NameTags = "name-tags";
        internal const string Room = "room";
        internal const string Tts = "tts";
        internal const string UtilityTablet = "utility-tablet";
        internal const string RefTablet = "ref-tablet";
        // V13.15-16: was the UPDATE CHANGER add-on; old updates are part of Casting Mod now
        internal const string UpdateChanger = "update-changer";

        internal static readonly string[] UtilityPages =
        {
            AutoRef,
            TimeChanger,
            SelfRigLerping,
            LeavesColor,
            NameTags,
            Tts,
            Wasd,
            Room
        };

        internal static readonly string[] All =
        {
            CastingMod,
            UtilityTablet,
            RefTablet,
            AutoRef,
            TimeChanger,
            SelfRigLerping,
            LeavesColor,
            NameTags,
            Tts,
            Wasd,
            Room
        };
    }

    internal sealed class UtilityAddonDescriptor
    {
        internal string Id { get; }
        internal string Name { get; }
        internal string Description { get; }

        internal UtilityAddonDescriptor(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    internal static class UtilityAddonCatalog
    {
        internal const string DomainEntitledKey = "UnknownsUtility.Entitled";
        internal const string DomainEnabledAddonsKey = "UnknownsUtility.EnabledAddons";
        internal const string DomainAllowedAddonsKey = "UnknownsUtility.AllowedAddons";

        internal static readonly UtilityAddonDescriptor[] Addons =
        {
            new UtilityAddonDescriptor(UtilityAddonIds.CastingMod, "CASTING MOD", "Casting camera, desktop manager, HUD, recording, and casting controls."),
            new UtilityAddonDescriptor(UtilityAddonIds.UtilityTablet, "UTILITY TABLET", "The in-game Utility tablet. Needs Casting Mod."),
            new UtilityAddonDescriptor(UtilityAddonIds.RefTablet, "REF TABLET", "The in-game referee tablet. Needs Casting Mod."),
            new UtilityAddonDescriptor(UtilityAddonIds.AutoRef, "AUTO REF", "Match/ref controls and round tools."),
            new UtilityAddonDescriptor(UtilityAddonIds.TimeChanger, "TIME CHANGER", "Client-side time-of-day controls."),
            new UtilityAddonDescriptor(UtilityAddonIds.SelfRigLerping, "SELF RIG LERPING", "Smooth the local presentation rig."),
            new UtilityAddonDescriptor(UtilityAddonIds.LeavesColor, "ENVIRONMENT COLORS", "V57 Leaves, Forest, and Lava color controls with live color preview/picker."),
            new UtilityAddonDescriptor(UtilityAddonIds.NameTags, "NAME TAGS", "V57 NameTags controls with live draggable preview, stats, fonts, and platform badge layout."),
            new UtilityAddonDescriptor(UtilityAddonIds.Tts, "TTS", "Desktop text-to-speech and lobby voice routing controls."),
            new UtilityAddonDescriptor(UtilityAddonIds.Wasd, "WASD", "Desktop movement speed, jump, sprint, and sensitivity controls."),
            new UtilityAddonDescriptor(UtilityAddonIds.Room, "ROOM JOINER", "Current room, room-code join, and leave controls.")
        };

        // All UI pages ship in release builds. Individual feature toggles still default OFF.
        internal static string DefaultSelectionCsv => string.Join(",", UtilityAddonIds.All);

        // V12.75: what a fresh install (or the one-time V3 settings migration)
        // turns on in the launcher's Add-ons list: Casting Mod only. Every Utility
        // add-on starts unticked and can still be enabled from the gear menu.
        internal static string DefaultEnabledSelectionCsv => UtilityAddonIds.CastingMod + "," + UtilityAddonIds.UtilityTablet + "," + UtilityAddonIds.RefTablet;

        internal static string NormalizeSelectionCsv(string value)
        {
            var requested = new HashSet<string>(
                (value ?? string.Empty).Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => (v ?? string.Empty).Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
            var result = UtilityAddonIds.All.Where(requested.Contains).ToArray();
            return string.Join(",", result);
        }

        internal static HashSet<string> ParseSelection(string value)
        {
            return new HashSet<string>(
                NormalizeSelectionCsv(value).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        // Keyless release: Utility is a built-in capability, not an entitlement.
        internal static bool RuntimeEntitled => true;

        private static string runtimeCacheRaw;
        private static HashSet<string> runtimeCache;

        private static HashSet<string> RuntimeSelectionCached
        {
            get
            {
                try
                {
                    var raw = Convert.ToString(AppDomain.CurrentDomain.GetData(DomainEnabledAddonsKey)) ?? string.Empty;
                    if (runtimeCache == null || !string.Equals(runtimeCacheRaw, raw, StringComparison.Ordinal))
                    {
                        runtimeCacheRaw = raw;
                        runtimeCache = ParseSelection(raw);
                    }
                    return runtimeCache;
                }
                catch
                {
                    return runtimeCache ?? (runtimeCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }
            }
        }

        internal static HashSet<string> RuntimeEnabledAddons
            => new HashSet<string>(RuntimeSelectionCached, StringComparer.OrdinalIgnoreCase);

        internal static bool RuntimeCastingModEnabled
            => RuntimeSelectionCached.Contains(UtilityAddonIds.CastingMod);

        internal static bool RuntimeAddonEnabled(string id)
        {
            try
            {
                if (AppDomain.CurrentDomain.GetData(DomainEnabledAddonsKey) == null) return true;
            }
            catch
            {
                return true;
            }
            return RuntimeSelectionCached.Contains(id);
        }

        internal static int RuntimeEnabledUtilityPageCount
            => UtilityAddonIds.UtilityPages.Count(RuntimeSelectionCached.Contains);

        internal static float SuggestedPanelHeight(int enabledCount)
        {
            enabledCount = Math.Max(0, Math.Min(UtilityAddonIds.UtilityPages.Length, enabledCount));
            return 118f + Math.Max(0, enabledCount - 1) * 72f;
        }
    }
}

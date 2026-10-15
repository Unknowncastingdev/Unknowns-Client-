using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnknownsCastingUI.AutoRef;
using UnknownsCastingUI.Extensions;
using UnknownsCastingUI.Security;
using UnknownsCastingUI.UI.AssetBundles;
using UnknownsUtilitySuite;

namespace UnknownsCastingUI
{
  public partial class UIBehaviour
  {
    private GameObject utilityAddonHost;
    private TMP_Text utilityAddonHeaderStatus;
    private TMP_Text utilityAddonAutoRefStatus;
    private TMP_Text utilityAddonSelfRigStatus;
    private TMP_Text utilityAddonTimeStatus;
    private TMP_Text utilityAddonPlayerStatus;
    private TMP_Text utilityAddonPlayerMetaStatus;
    private int utilityAddonPlayerIndex;
    private HashSet<string> utilityRuntimeAddons;

    // Utility edition uses a desktop IMGUI window, not a world-space tablet.
    // The window height follows the number of selected add-ons so the UI stays
    // compact when only one or two modules are enabled.
    private bool utilityDesktopVisible;
    private bool utilityDesktopInitialized;
    private Rect utilityDesktopRect = new Rect(80f, 80f, 440f, 260f);
    private int utilityDesktopWindowId;
    private Vector2 utilityDesktopScroll;
    private string utilityDesktopActiveAddon;

    private bool utilityLeavesColorEnabled;
    private float utilityLeavesHue = 0.33f;
    private float utilityLeavesSaturation = 0.72f;
    // Brightness is an exposure multiplier, not clamped HSV value. Values above 1
    // let white/bright leaves actually render white instead of merely removing tint.
    private float utilityLeavesBrightness = 1.25f;
    private const float UtilityEnvironmentTransitionSpeed = 4.25f;
    private float utilityLeavesNextScanAt = -1f;
    private readonly List<UtilityLeafRendererState> utilityLeafRenderers = new List<UtilityLeafRendererState>();
    private readonly MaterialPropertyBlock utilityLeafPropertyBlock = new MaterialPropertyBlock();

    private int utilityEnvironmentColorMode; // 0 leaves, 1 forest, 3 lava (2 was removed)
    private bool utilityForestColorEnabled;
    private float utilityForestHue = 0.72f;
    private float utilityForestSaturation = 0.45f;
    private float utilityForestBrightness = 1f;
    private float utilityForestNextScanAt = -1f;
    private readonly List<UtilityForestRendererState> utilityForestRenderers = new List<UtilityForestRendererState>();
    private readonly MaterialPropertyBlock utilityForestPropertyBlock = new MaterialPropertyBlock();

    private bool utilityLightsColorEnabled;
    private float utilityLightsHue = 0.72f;
    private float utilityLightsSaturation = 0.55f;
    private float utilityLightsBrightness = 1f;
    private float utilityLightsNextScanAt = -1f;
    private readonly List<UtilityLightState> utilityLights = new List<UtilityLightState>();

    private bool utilityLavaColorEnabled;
    private float utilityLavaHue = 0.035f;
    private float utilityLavaSaturation = 0.92f;
    private float utilityLavaBrightness = 1.35f;
    private float utilityLavaNextScanAt = -1f;
    private readonly List<UtilityLavaRendererState> utilityLavaRenderers = new List<UtilityLavaRendererState>();
    // Gorilla can replace a tagged rig's renderer material from VRRig.materialsToChangeTo
    // after our renderer scan. Keep the source lava/infected materials tinted too so a
    // SetMaterial() refresh cannot immediately overwrite the selected color.
    private readonly List<UtilityLavaMaterialState> utilityLavaSourceMaterials = new List<UtilityLavaMaterialState>();
    private readonly MaterialPropertyBlock utilityLavaPropertyBlock = new MaterialPropertyBlock();
    private bool utilityEnvironmentPrefsLoaded;
    private int utilityLavaLastRenderReapplyFrame = -1;

    private bool utilityColorPickerOpen;
    private int utilityColorPickerMode = -1;
    private Texture2D utilityColorWheelTexture;

    private bool utilityLightingGlobalsCaptured;
    private Color utilityOriginalAmbientLight;
    private Color utilityOriginalAmbientSky;
    private Color utilityOriginalAmbientEquator;
    private Color utilityOriginalAmbientGround;
    private Color utilityOriginalFogColor;
    private Color utilityOriginalSubtractiveShadow;
    private Color utilityCurrentAmbientLight;
    private Color utilityCurrentAmbientSky;
    private Color utilityCurrentAmbientEquator;
    private Color utilityCurrentAmbientGround;
    private Color utilityCurrentFogColor;
    private Color utilityCurrentSubtractiveShadow;

    private sealed class UtilityLeafRendererState
    {
      internal Renderer Renderer;
      internal Color OriginalColor = Color.white;
      internal Color CurrentColor = Color.white;
      internal bool HasColor;
      internal bool HasBaseColor;
      internal MaterialPropertyBlock OriginalBlock = new MaterialPropertyBlock();
    }

    private sealed class UtilityForestRendererState
    {
      internal Renderer Renderer;
      internal Color OriginalColor = Color.white;
      internal Color CurrentColor = Color.white;
      internal bool HasColor;
      internal bool HasBaseColor;
      internal MaterialPropertyBlock OriginalBlock = new MaterialPropertyBlock();
    }

    private sealed class UtilityLightState
    {
      internal Light Light;
      internal Color OriginalColor = Color.white;
      internal Color CurrentColor = Color.white;
      internal float OriginalIntensity = 1f;
      internal float CurrentIntensity = 1f;
    }

    private sealed class UtilityLavaMaterialState
    {
      internal Material Material;
      internal readonly Dictionary<string, Color> OriginalColors = new Dictionary<string, Color>(StringComparer.Ordinal);
      internal readonly Dictionary<string, Color> CurrentColors = new Dictionary<string, Color>(StringComparer.Ordinal);
    }

    private sealed class UtilityLavaRendererState
    {
      internal Renderer Renderer;
      internal readonly List<UtilityLavaMaterialState> Materials = new List<UtilityLavaMaterialState>();
    }

    private bool HasUtilityAddon(string id)
    {
      if (!UtilityAddonCatalog.RuntimeEntitled) return false;
      if (this.utilityRuntimeAddons == null) this.utilityRuntimeAddons = UtilityAddonCatalog.RuntimeEnabledAddons;
      return this.utilityRuntimeAddons.Contains(id);
    }

    private void EnsureUtilityEnvironmentColorPrefsLoaded()
    {
      if (this.utilityEnvironmentPrefsLoaded) return;
      this.utilityEnvironmentPrefsLoaded = true;
      try
      {
        this.utilityLeavesColorEnabled = PlayerPrefs.GetInt("UnknownsUtility_LeavesColorEnabled", 0) != 0;
        this.utilityLeavesHue = Mathf.Repeat(PlayerPrefs.GetFloat("UnknownsUtility_LeavesHue", this.utilityLeavesHue), 1f);
        this.utilityLeavesSaturation = Mathf.Clamp01(PlayerPrefs.GetFloat("UnknownsUtility_LeavesSaturation", this.utilityLeavesSaturation));
        this.utilityLeavesBrightness = Mathf.Clamp(PlayerPrefs.GetFloat("UnknownsUtility_LeavesBrightness", this.utilityLeavesBrightness), 0.10f, 4f);
        this.utilityEnvironmentColorMode = Mathf.Clamp(PlayerPrefs.GetInt("UnknownsUtility_EnvironmentColorMode", 0), 0, 3);
        if (this.utilityEnvironmentColorMode == 2) this.utilityEnvironmentColorMode = 0;
        this.utilityForestColorEnabled = PlayerPrefs.GetInt("UnknownsUtility_ForestColorEnabled", 0) != 0;
        this.utilityForestHue = Mathf.Repeat(PlayerPrefs.GetFloat("UnknownsUtility_ForestHue", this.utilityForestHue), 1f);
        this.utilityForestSaturation = Mathf.Clamp01(PlayerPrefs.GetFloat("UnknownsUtility_ForestSaturation", this.utilityForestSaturation));
        this.utilityForestBrightness = Mathf.Clamp(PlayerPrefs.GetFloat("UnknownsUtility_ForestBrightness", this.utilityForestBrightness), 0.10f, 4f);
        this.utilityLavaColorEnabled = PlayerPrefs.GetInt("UnknownsUtility_LavaColorEnabled", 0) != 0;
        this.utilityLavaHue = Mathf.Repeat(PlayerPrefs.GetFloat("UnknownsUtility_LavaHue", this.utilityLavaHue), 1f);
        this.utilityLavaSaturation = Mathf.Clamp01(PlayerPrefs.GetFloat("UnknownsUtility_LavaSaturation", this.utilityLavaSaturation));
        this.utilityLavaBrightness = Mathf.Clamp(PlayerPrefs.GetFloat("UnknownsUtility_LavaBrightness", this.utilityLavaBrightness), 0.10f, 4f);
      }
      catch { }
    }

    private void InitializeUtilityDesktopMode()
    {
      this.utilityRuntimeAddons = UtilityAddonCatalog.RuntimeEnabledAddons;
      this.utilityDesktopWindowId = 700000 + (Guid.NewGuid().GetHashCode() & 0x0FFFFF);
      this.utilityDesktopVisible = false;
      this.utilityDesktopInitialized = true;
      this.utilityDesktopActiveAddon = this.GetFirstEnabledUtilityAddon();

      // Load only what the desktop Utility window needs. The Utility edition must
      // not create the old in-world tablet/camera UI.
      try { this.LoadDesktopBrandingIcon(); } catch { }
      try
      {
        // Only restore state for add-ons that were actually selected for this
        // injection. Old PlayerPrefs from a previous session must never make a
        // different add-on silently run Self Rig or Environment work in the background.
        if (this.HasUtilityAddon(UtilityAddonIds.SelfRigLerping))
          this.selfRigSmoothness = PlayerPrefs.GetFloat("UnknownsCameraTablet_SelfRigLerp", this.selfRigSmoothness);
        this.selfRigLerpingEnabled = false;

        if (this.HasUtilityAddon(UtilityAddonIds.Wasd))
        {
          this.wasdSpeed = PlayerPrefs.GetFloat("UnknownsUtility_WASDSpeed", this.wasdSpeed);
          this.wasdSensitivity = PlayerPrefs.GetFloat("UnknownsUtility_WASDSensitivity", this.wasdSensitivity);
        }
        this.wasdEnabled = this.HasUtilityAddon(UtilityAddonIds.Wasd) && PlayerPrefs.GetInt("UnknownsUtility_WASDEnabled2", 1) != 0;

        if (this.HasUtilityAddon(UtilityAddonIds.LeavesColor))
        {
          this.EnsureUtilityEnvironmentColorPrefsLoaded();
          // Release rule: selected Utility modules load with their feature switches OFF.
          // Preserve the user's color/slider values, but never resume expensive renderer
          // work merely because an older session saved the toggle as enabled.
          this.utilityLeavesColorEnabled = false;
          this.utilityForestColorEnabled = false;
          this.utilityLavaColorEnabled = false;
        }
        else
        {
          this.utilityLeavesColorEnabled = false;
          this.utilityForestColorEnabled = false;
          this.utilityLavaColorEnabled = false;
        }
        this.utilityLightsColorEnabled = false;
      }
      catch { }

      if ((UnityEngine.Object)this.utilityTabletRoot != (UnityEngine.Object)null)
      {
        try { UnityEngine.Object.Destroy(this.utilityTabletRoot); } catch { }
        this.utilityTabletRoot = null;
      }

      float width = this.GetUtilityDesktopWidth();
      float height = this.GetUtilityDesktopHeight();
      this.utilityDesktopRect = new Rect(
        Mathf.Max(12f, (Screen.width - width) * 0.5f),
        Mathf.Max(12f, (Screen.height - height) * 0.5f),
        width,
        height);
    }

    private string GetFirstEnabledUtilityAddon()
    {
      if (this.HasUtilityAddon(UtilityAddonIds.AutoRef)) return UtilityAddonIds.AutoRef;
      if (this.HasUtilityAddon(UtilityAddonIds.SelfRigLerping)) return UtilityAddonIds.SelfRigLerping;
      if (this.HasUtilityAddon(UtilityAddonIds.TimeChanger)) return UtilityAddonIds.TimeChanger;
      if (this.HasUtilityAddon(UtilityAddonIds.Wasd)) return UtilityAddonIds.Wasd;
      if (this.HasUtilityAddon(UtilityAddonIds.LeavesColor)) return UtilityAddonIds.LeavesColor;
      if (this.HasUtilityAddon(UtilityAddonIds.NameTags)) return UtilityAddonIds.NameTags;
      if (this.HasUtilityAddon(UtilityAddonIds.Room)) return UtilityAddonIds.Room;
      if (this.HasUtilityAddon(UtilityAddonIds.Tts)) return UtilityAddonIds.Tts;
      return string.Empty;
    }

    private void EnsureValidUtilityDesktopAddon()
    {
      if (string.IsNullOrEmpty(this.utilityDesktopActiveAddon) || !this.HasUtilityAddon(this.utilityDesktopActiveAddon))
        this.utilityDesktopActiveAddon = this.GetFirstEnabledUtilityAddon();
    }

    private float GetUtilityDesktopWidth()
    {
      this.EnsureValidUtilityDesktopAddon();
      return Mathf.Clamp(458f, 410f, Mathf.Max(410f, Screen.width - 28f));
    }

    private float GetUtilityDesktopHeight()
    {
      int count = this.utilityRuntimeAddons != null ? this.utilityRuntimeAddons.Count : UtilityAddonCatalog.RuntimeEnabledAddons.Count;
      if (!UtilityAddonCatalog.RuntimeEntitled || count <= 0) return 210f;

      this.EnsureValidUtilityDesktopAddon();
      float baseHeight = 200f + Mathf.Clamp(count - 1, 0, 5) * 3f;
      if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.AutoRef, StringComparison.Ordinal))
        baseHeight = 525f;
      else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.SelfRigLerping, StringComparison.Ordinal))
        baseHeight = 285f;
      else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.TimeChanger, StringComparison.Ordinal))
        baseHeight = 270f;
      else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.Wasd, StringComparison.Ordinal))
        baseHeight = 330f;
      else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.LeavesColor, StringComparison.Ordinal))
        baseHeight = 430f;
      else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.NameTags, StringComparison.Ordinal))
        baseHeight = 620f;
      else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.Room, StringComparison.Ordinal))
        baseHeight = 660f;
      else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.Tts, StringComparison.Ordinal))
        baseHeight = 560f;

      return Mathf.Clamp(baseHeight, 210f, Mathf.Max(260f, Screen.height - 28f));
    }

    private void ToggleUtilityDesktopWindow()
    {
      if (!this.utilityDesktopInitialized) this.InitializeUtilityDesktopMode();
      this.utilityDesktopVisible = !this.utilityDesktopVisible;
      if (this.utilityDesktopVisible)
      {
        this.EnsureValidUtilityDesktopAddon();
        float width = this.GetUtilityDesktopWidth();
        float height = this.GetUtilityDesktopHeight();
        this.utilityDesktopRect.width = width;
        this.utilityDesktopRect.height = height;
        this.utilityDesktopRect.x = Mathf.Clamp(this.utilityDesktopRect.x, 8f, Mathf.Max(8f, Screen.width - width - 8f));
        this.utilityDesktopRect.y = Mathf.Clamp(this.utilityDesktopRect.y, 8f, Mathf.Max(8f, Screen.height - height - 8f));
      }
    }

    private void DrawUtilityDesktopOnly()
    {
      if (!this.utilityDesktopInitialized) this.InitializeUtilityDesktopMode();
      if (!this.utilityDesktopVisible) return;

      if (!this.stylesCreated) this.CreateStyles();
      if (this.roundedWindowStyle == null) this.BuildRoundedStyles();

      float targetWidth = this.GetUtilityDesktopWidth();
      float targetHeight = this.GetUtilityDesktopHeight();
      this.utilityDesktopRect.width = targetWidth;
      this.utilityDesktopRect.height = targetHeight;
      this.utilityDesktopRect.x = Mathf.Clamp(this.utilityDesktopRect.x, 8f, Mathf.Max(8f, Screen.width - this.utilityDesktopRect.width - 8f));
      this.utilityDesktopRect.y = Mathf.Clamp(this.utilityDesktopRect.y, 8f, Mathf.Max(8f, Screen.height - this.utilityDesktopRect.height - 8f));

      int previousDepth = GUI.depth;
      GUI.depth = -500;
      this.utilityDesktopRect = GUI.Window(
        this.utilityDesktopWindowId,
        this.utilityDesktopRect,
        this.DrawUtilityDesktopWindow,
        string.Empty,
        this.roundedWindowStyle);
      GUI.depth = previousDepth;

      this.DrawToasts();
    }

    private bool DrawUtilityDesktopAddonTab(string id, string label)
    {
      if (!this.HasUtilityAddon(id)) return false;
      bool active = string.Equals(this.utilityDesktopActiveAddon, id, StringComparison.Ordinal);
      if (this.DesktopGUILayoutButton(label, active ? this.roundedButtonActiveStyle : this.roundedButtonStyle,
          GUILayout.Height(24f), GUILayout.ExpandWidth(true)))
      {
        this.utilityDesktopActiveAddon = id;
        this.utilityDesktopScroll = Vector2.zero;
        this.utilityColorPickerOpen = false;
        this.utilityColorPickerMode = -1;
        this.playerPropertiesPopupRig = null;
        this.reportMenuPlayer = null;
        return true;
      }
      return false;
    }

    private void DrawUtilityDesktopWindow(int id)
    {
      this.BeginTooltipWindow("UtilityDesktop");
      GUILayout.Space(3f);
      this.DrawDesktopBrandHeader("UNKNOWN'S SUITE", "UTILITY", false);
      GUILayout.Space(6f);

      int count = this.utilityRuntimeAddons != null ? this.utilityRuntimeAddons.Count : 0;
      GUIStyle statusStyle = new GUIStyle(this.roundedLabelStyle)
      {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 9,
        fontStyle = FontStyle.Bold
      };
      statusStyle.normal.textColor = new Color(0.72f, 0.64f, 0.84f, 1f);

      if (!UtilityAddonCatalog.RuntimeEntitled)
      {
        GUILayout.Label("UTILITY PAGES", statusStyle, GUILayout.Height(24f));
        GUILayout.Label("Utility pages are built into this release.", this.roundedLabelStyle);
      }
      else if (count == 0)
      {
        GUILayout.Label("0 ADD-ONS LOADED", statusStyle, GUILayout.Height(24f));
        GUILayout.Label("Open the gear in Unknown's Suite and choose which Utility add-ons to load.", this.roundedLabelStyle);
      }
      else
      {
        this.EnsureValidUtilityDesktopAddon();
        GUILayout.Label(count + " ADD-ON" + (count == 1 ? "" : "S") + " LOADED", statusStyle, GUILayout.Height(18f));
        GUILayout.BeginHorizontal();
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.AutoRef, "AUTO REF");
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.SelfRigLerping, "SELF RIG");
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.TimeChanger, "TIME");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.Wasd, "WASD");
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.LeavesColor, "ENVIRONMENT");
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.NameTags, "NAMETAGS");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.Room, "ROOM");
        this.DrawUtilityDesktopAddonTab(UtilityAddonIds.Tts, "TTS / VOICE");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(7f);

          if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.LeavesColor, StringComparison.Ordinal))
            this.HandleUtilityColorPickerInput(this.utilityDesktopRect.width, this.utilityDesktopRect.height);
          bool pickerOwnsInput = string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.LeavesColor, StringComparison.Ordinal) && this.utilityColorPickerOpen;
          bool previousGuiEnabled = GUI.enabled;
          if (pickerOwnsInput) GUI.enabled = false;
          this.utilityDesktopScroll = GUILayout.BeginScrollView(this.utilityDesktopScroll, false, true, GUIStyle.none, this.GetDesktopScrollbarStyle(), GUIStyle.none, GUILayout.ExpandHeight(true));
          this.desktopCompactUi = true;
          if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.AutoRef, StringComparison.Ordinal))
            this.DrawUtilityDesktopAutoRefPlusControls();
          else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.SelfRigLerping, StringComparison.Ordinal))
            this.DrawUtilityDesktopSelfRigPlusControls();
          else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.TimeChanger, StringComparison.Ordinal))
            this.DrawUtilityDesktopTimePlusControls();
          else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.Wasd, StringComparison.Ordinal))
            this.DrawUtilityDesktopWasdPlusControls();
          else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.LeavesColor, StringComparison.Ordinal))
            this.DrawUtilityDesktopLeavesColorControls();
          else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.NameTags, StringComparison.Ordinal))
            this.DrawNameTagsTab();
          else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.Room, StringComparison.Ordinal))
            this.DrawRoomTab();
          else if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.Tts, StringComparison.Ordinal))
            this.DrawDesktopTtsTab();
          this.desktopCompactUi = false;
          GUILayout.EndScrollView();
          GUI.enabled = previousGuiEnabled;
          // Draw the picker outside the scroll-view clipping group. This keeps the
          // wheel fully visible and makes pointer coordinates stable while dragging.
          if (string.Equals(this.utilityDesktopActiveAddon, UtilityAddonIds.LeavesColor, StringComparison.Ordinal))
            this.DrawUtilityColorPickerPopup();
      }

      this.DrawPendingTooltip(this.utilityDesktopRect.width, this.utilityDesktopRect.height);
      GUI.DragWindow(new Rect(0f, 0f, this.utilityDesktopRect.width, 66f));
    }

    private void DrawUtilityDesktopSelfRigPlusControls()
    {
      GUILayout.Label("Self Rig Lerping", this.sectionHeaderStyle);
      GUILayout.Space(7f);
      bool oldSelfRig = this.selfRigLerpingEnabled;
      this.selfRigLerpingEnabled = this.DrawToggleRow("Self Rig Lerping", this.selfRigLerpingEnabled);
      if (oldSelfRig != this.selfRigLerpingEnabled)
      {
        this.selfRigPoseInitialized = false;
        if (this.selfRigLerpingEnabled) this.EnsureSelfLerpedRig();
        else this.DestroySelfLerpedRig();
        PlayerPrefs.SetInt("UnknownsSelfRigLerping", this.selfRigLerpingEnabled ? 1 : 0);
        PlayerPrefs.Save();
        this.ShowToast("Self Rig Lerping", this.selfRigLerpingEnabled ? "Enabled (client-side clone)" : "Disabled");
      }
      if (this.selfRigLerpingEnabled)
      {
        this.DrawSliderWithInput("Self Rig Lerp:", this.selfRigSmoothness, 0f, 6f, "SelfRigSmoothness", "F2", v =>
        {
          this.selfRigSmoothness = Mathf.Clamp(v, 0f, 6f);
          PlayerPrefs.SetFloat("UnknownsCameraTablet_SelfRigLerp", this.selfRigSmoothness);
          PlayerPrefs.Save();
        });
        this.selfRigVrShowsNormalRig = true;
      }
    }

    private void DrawUtilityDesktopAutoRefPlusControls()
    {
      GUILayout.Label("Auto Ref", this.sectionHeaderStyle);
      GUILayout.Space(5f);
      AutoRefManager m = this.autoRefManager ?? AutoRefManager.Instance;
      if (m != null)
      {
        GUIStyle scoreStyle = new GUIStyle(this.roundedLabelStyle)
        {
          alignment = TextAnchor.MiddleCenter,
          fontSize = 17,
          fontStyle = FontStyle.Bold
        };
        scoreStyle.normal.textColor = new Color(0.92f, 0.86f, 1f, 1f);
        if (this.desktopCompactUi) scoreStyle.fontSize = 14;
        GUILayout.Label("SCORE   " + m.team1Score + "  :  " + m.team2Score, scoreStyle, GUILayout.Height(this.UiH(31f)), GUILayout.ExpandWidth(true));
        GUILayout.Space(5f);
      }
      // Reuse the PLUS controls directly: enable, team sizes, first-to,
      // warnings and the full per-player PLAYING/OUT selector.
      this.DrawAutoRefInlineControls();
    }

    private void DrawUtilityDesktopTimePlusControls()
    {
      GUILayout.Label("Time Changer", this.sectionHeaderStyle);
      GUILayout.Space(7f);
      bool previous = this.timeEnabled;
      this.timeEnabled = this.DrawToggleRow("Time Control", this.timeEnabled);
      if (previous != this.timeEnabled)
      {
        if (this.timeEnabled) this.ApplyTimeOfDay();
        this.ShowToast("Time", this.timeEnabled ? "Enabled" : "Disabled");
      }
      if (this.timeEnabled)
      {
        this.DrawSliderWithInput("Time of Day:", (float)this.timeOfDay, 0f, 9f, "TimeOfDay", "F0", v =>
        {
          this.timeOfDay = Mathf.RoundToInt(v);
          this.ApplyTimeOfDay();
        });
        GUILayout.Label("Current: " + this.GetUtilityTimeName(), this.roundedLabelStyle);
      }
    }

    private void DrawUtilityDesktopWasdPlusControls()
    {
      GUILayout.Label("WASD Movement", this.sectionHeaderStyle);
      GUILayout.Space(6f);
      bool old = this.wasdEnabled;
      this.wasdEnabled = this.DrawToggleRow("WASD", this.wasdEnabled);
      if (old != this.wasdEnabled)
      {
        if (!this.wasdEnabled)
        {
          this.wasdVelocity = Vector3.zero;
          this.wasdJumpVelocity = Vector3.zero;
        }
        PlayerPrefs.SetInt("UnknownsUtility_WASDEnabled2", this.wasdEnabled ? 1 : 0);
        PlayerPrefs.Save();
        this.ShowToast("WASD", this.wasdEnabled ? "Enabled" : "Disabled");
      }
      if (this.wasdEnabled)
      {
        this.DrawSliderWithInput("Movement Speed:", this.wasdSpeed, 1f, 50f, "UtilityWASDSpeed", "F1", v =>
        {
          this.wasdSpeed = v;
          PlayerPrefs.SetFloat("UnknownsUtility_WASDSpeed", this.wasdSpeed);
          PlayerPrefs.Save();
        });
        this.DrawSliderWithInput("Mouse Sensitivity:", this.wasdSensitivity, 0.1f, 50f, "UtilityWASDSens", "F2", v =>
        {
          this.wasdSensitivity = v;
          PlayerPrefs.SetFloat("UnknownsUtility_WASDSensitivity", this.wasdSensitivity);
          PlayerPrefs.Save();
        });
      }
    }

    private static Color GetUtilityHdrTint(float hue, float saturation, float brightness)
    {
      Color baseColor = Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(saturation), 1f);
      float exposure = Mathf.Clamp(brightness, 0.10f, 4f);
      return new Color(baseColor.r * exposure, baseColor.g * exposure, baseColor.b * exposure, 1f);
    }

    private Color GetUtilityLeavesTargetColor()
    {
      return GetUtilityHdrTint(this.utilityLeavesHue, this.utilityLeavesSaturation, this.utilityLeavesBrightness);
    }

    private Color GetUtilityForestTargetColor()
    {
      return GetUtilityHdrTint(this.utilityForestHue, this.utilityForestSaturation, this.utilityForestBrightness);
    }

    private Color GetUtilityLightsTargetColor()
    {
      // Light.color itself stays normalized; brightness is applied to intensity.
      Color c = Color.HSVToRGB(Mathf.Repeat(this.utilityLightsHue, 1f), Mathf.Clamp01(this.utilityLightsSaturation), 1f);
      c.a = 1f;
      return c;
    }

    private Color GetUtilityLavaTargetColor()
    {
      return GetUtilityHdrTint(this.utilityLavaHue, this.utilityLavaSaturation, this.utilityLavaBrightness);
    }

    private bool DrawUtilityEnvironmentModeButton(string text, int mode)
    {
      bool active = this.utilityEnvironmentColorMode == mode;
      if (this.DesktopGUILayoutButton(text, active ? this.roundedButtonActiveStyle : this.roundedButtonStyle,
          GUILayout.Height(this.UiH(26f)), GUILayout.ExpandWidth(true)))
      {
        this.utilityEnvironmentColorMode = mode;
        this.utilityColorPickerOpen = false;
        this.utilityColorPickerMode = -1;
        PlayerPrefs.SetInt("UnknownsUtility_EnvironmentColorMode", mode);
        PlayerPrefs.Save();
        return true;
      }
      return false;
    }

    private void DrawUtilityColorPreview(Color hdrColor, string label)
    {
      Color preview = new Color(Mathf.Clamp01(hdrColor.r), Mathf.Clamp01(hdrColor.g), Mathf.Clamp01(hdrColor.b), 1f);
      Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(this.UiH(28f)), GUILayout.ExpandWidth(true));
      this.DrawRoundedRect(previewRect, 9, preview, new Color(0.36f, 0.20f, 0.48f, 0.95f), 1);
      GUIStyle previewText = new GUIStyle(this.roundedLabelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 9, fontStyle = FontStyle.Bold };
      previewText.normal.textColor = preview.grayscale < 0.48f ? Color.white : new Color(0.08f, 0.06f, 0.10f, 1f);
      GUI.Label(previewRect, label, previewText);
      GUILayout.Space(3f);
    }

    private Texture2D GetUtilityColorWheelTexture()
    {
      if ((UnityEngine.Object)this.utilityColorWheelTexture != (UnityEngine.Object)null) return this.utilityColorWheelTexture;
      const int size = 180;
      Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
      tex.name = "UnknownsUtilityColorWheel";
      tex.wrapMode = TextureWrapMode.Clamp;
      tex.filterMode = FilterMode.Bilinear;
      Color[] pixels = new Color[size * size];
      float center = (size - 1) * 0.5f;
      float radius = center - 2f;
      for (int y = 0; y < size; ++y)
      {
        for (int x = 0; x < size; ++x)
        {
          float dx = x - center;
          float dy = y - center; // texture Y is bottom-up; top of GUI wheel therefore has positive dy
          float dist = Mathf.Sqrt(dx * dx + dy * dy);
          int index = y * size + x;
          if (dist > radius)
          {
            pixels[index] = new Color(0f, 0f, 0f, 0f);
            continue;
          }
          float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f), 1f);
          float sat = Mathf.Clamp01(dist / radius);
          Color c = Color.HSVToRGB(hue, sat, 1f);
          c.a = 1f;
          pixels[index] = c;
        }
      }
      tex.SetPixels(pixels);
      tex.Apply(false, true);
      this.utilityColorWheelTexture = tex;
      return tex;
    }

    private void GetUtilityColorValues(int mode, out float hue, out float saturation, out float brightness)
    {
      if (mode == 1)
      {
        hue = this.utilityForestHue; saturation = this.utilityForestSaturation; brightness = this.utilityForestBrightness; return;
      }
      if (mode == 2)
      {
        hue = this.utilityLightsHue; saturation = this.utilityLightsSaturation; brightness = this.utilityLightsBrightness; return;
      }
      if (mode == 3)
      {
        hue = this.utilityLavaHue; saturation = this.utilityLavaSaturation; brightness = this.utilityLavaBrightness; return;
      }
      hue = this.utilityLeavesHue; saturation = this.utilityLeavesSaturation; brightness = this.utilityLeavesBrightness;
    }

    private void SetUtilityPickerHueSaturation(int mode, float hue, float saturation)
    {
      hue = Mathf.Repeat(hue, 1f);
      saturation = Mathf.Clamp01(saturation);
      if (mode == 1)
      {
        this.utilityForestHue = hue; this.utilityForestSaturation = saturation;
        PlayerPrefs.SetFloat("UnknownsUtility_ForestHue", hue); PlayerPrefs.SetFloat("UnknownsUtility_ForestSaturation", saturation);
      }
      else if (mode == 2)
      {
        this.utilityLightsHue = hue; this.utilityLightsSaturation = saturation;
        PlayerPrefs.SetFloat("UnknownsUtility_LightsHue", hue); PlayerPrefs.SetFloat("UnknownsUtility_LightsSaturation", saturation);
      }
      else if (mode == 3)
      {
        this.utilityLavaHue = hue; this.utilityLavaSaturation = saturation;
        PlayerPrefs.SetFloat("UnknownsUtility_LavaHue", hue); PlayerPrefs.SetFloat("UnknownsUtility_LavaSaturation", saturation);
      }
      else
      {
        this.utilityLeavesHue = hue; this.utilityLeavesSaturation = saturation;
        PlayerPrefs.SetFloat("UnknownsUtility_LeavesHue", hue); PlayerPrefs.SetFloat("UnknownsUtility_LeavesSaturation", saturation);
      }
      // Saved once when the drag ends (PlayerPrefs.Save on every drag event hitched).
      if (!this.utilityColorWheelDragging) PlayerPrefs.Save();
    }

    private bool DrawUtilityColorPickerButton(int mode, Color color)
    {
      this.utilityColorPickerOpen = false;
      float hue, saturation, brightness;
      this.GetUtilityColorValues(mode, out hue, out saturation, out brightness);
      float value = 1f;
      bool released;
      Rect side;
      bool changed = this.DrawHsvWheelBlock("UtilityEnv" + mode, ref hue, ref saturation, ref value, false, out released, out side);
      if (changed || released)
      {
        this.utilityColorWheelDragging = !released;
        this.SetUtilityPickerHueSaturation(mode, hue, saturation);
      }
      return changed;
    }

    private void DrawUtilityColorPickerPopup()
    {
      this.DrawUtilityColorPickerPopup(this.utilityDesktopRect.width, this.utilityDesktopRect.height);
    }

    // V12.77 color wheel:
    //  * the popup is modal - every mouse/scroll event over the popup is handled
    //    (and consumed) BEFORE the page underneath is drawn, so sliders/buttons
    //    under the wheel can no longer grab the click;
    //  * once you press on the wheel, the pointer follows the mouse for the whole
    //    drag (clamped to the rim when you leave the circle) instead of stopping
    //    as soon as the mouse leaves the wheel;
    //  * standard HSV wheel (white centre only at zero saturation, V78).
    private bool utilityColorWheelDragging;

    private void GetUtilityColorPickerLayout(float hostWidth, float hostHeight, out Rect panel, out Rect wheelRect, out Rect closeRect)
    {
      float width = Mathf.Min(238f, hostWidth - 28f);
      float height = 238f;
      float px = Mathf.Clamp((hostWidth - width) * 0.5f, 10f, Mathf.Max(10f, hostWidth - width - 10f));
      float py = 76f;
      if (py + height > hostHeight - 10f)
        py = Mathf.Max(52f, hostHeight - height - 10f);
      panel = new Rect(px, py, width, height);
      const float wheelSize = 168f;
      wheelRect = new Rect(panel.center.x - wheelSize * 0.5f, panel.y + 38f, wheelSize, wheelSize);
      closeRect = new Rect(panel.xMax - 35f, panel.y + 7f, 24f, 22f);
    }

    private void ApplyUtilityWheelPoint(Rect wheelRect, Vector2 mouse)
    {
      float radius = wheelRect.width * 0.5f - 2f;
      Vector2 dScreen = mouse - wheelRect.center;
      Vector2 d = new Vector2(dScreen.x, -dScreen.y);
      float normalizedRadius = Mathf.Clamp01(d.magnitude / Mathf.Max(1f, radius));
      float selectedHue = normalizedRadius <= 0.0001f ? 0f : Mathf.Repeat(Mathf.Atan2(d.y, d.x) / (Mathf.PI * 2f), 1f);
      float hue, saturation, brightness;
      this.GetUtilityColorValues(this.utilityColorPickerMode, out hue, out saturation, out brightness);
      if (normalizedRadius <= 0.0001f) selectedHue = hue;
      this.SetUtilityPickerHueSaturation(this.utilityColorPickerMode, selectedHue, normalizedRadius);
    }

    // Call at the START of the host window, before its page content is drawn.
    private void HandleUtilityColorPickerInput(float hostWidth, float hostHeight)
    {
      Event e = Event.current;
      if (e == null) return;
      if (!this.utilityColorPickerOpen || this.utilityColorPickerMode < 0)
      {
        this.utilityColorWheelDragging = false;
        return;
      }

      Rect panel, wheelRect, closeRect;
      this.GetUtilityColorPickerLayout(hostWidth, hostHeight, out panel, out wheelRect, out closeRect);
      Vector2 mouse = e.mousePosition;

      if (this.utilityColorWheelDragging)
      {
        if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
        {
          this.ApplyUtilityWheelPoint(wheelRect, mouse);
          e.Use();
          return;
        }
        if (e.rawType == EventType.MouseUp)
        {
          this.utilityColorWheelDragging = false;
          PlayerPrefs.Save();
          e.Use();
          return;
        }
      }

      bool mouseEvent = e.type == EventType.MouseDown || e.type == EventType.MouseUp ||
                        e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel;
      if (!mouseEvent || !panel.Contains(mouse)) return;

      if (e.type == EventType.MouseDown && e.button == 0)
      {
        if (closeRect.Contains(mouse))
        {
          this.utilityColorPickerOpen = false;
          this.utilityColorWheelDragging = false;
        }
        else if ((mouse - wheelRect.center).magnitude <= wheelRect.width * 0.5f + 4f)
        {
          this.utilityColorWheelDragging = true;
          this.ApplyUtilityWheelPoint(wheelRect, mouse);
        }
      }
      // Nothing underneath the popup may react to this event.
      e.Use();
    }

    private void DrawUtilityColorPickerPopup(float hostWidth, float hostHeight)
    {
      if (!this.utilityColorPickerOpen || this.utilityColorPickerMode < 0) return;

      Rect panel, wheelRect, closeRect;
      this.GetUtilityColorPickerLayout(hostWidth, hostHeight, out panel, out wheelRect, out closeRect);
      this.DrawRoundedRect(panel, 14, new Color(0.060f, 0.043f, 0.078f, 0.995f), new Color(0.34f, 0.18f, 0.47f, 1f), 2);

      GUIStyle title = new GUIStyle(this.roundedLabelStyle)
      {
        fontSize = 10,
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.MiddleLeft
      };
      GUI.Label(new Rect(panel.x + 13f, panel.y + 8f, 150f, 22f), "COLOR", title);
      bool closeHover = Event.current != null && closeRect.Contains(Event.current.mousePosition);
      this.DrawRoundedRect(closeRect, 6, closeHover ? new Color(0.25f, 0.15f, 0.30f, 1f) : new Color(0.13f, 0.10f, 0.16f, 1f), Color.clear, 0);
      GUIStyle closeStyle = new GUIStyle(this.roundedLabelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
      GUI.Label(closeRect, "×", closeStyle);

      Rect wheelBack = new Rect(wheelRect.x - 3f, wheelRect.y - 3f, wheelRect.width + 6f, wheelRect.height + 6f);
      this.DrawRoundedRect(wheelBack, (int)(wheelBack.width * 0.5f), new Color(0.13f, 0.09f, 0.17f, 1f), new Color(0.30f, 0.16f, 0.42f, 1f), 1);
      Texture2D wheel = this.GetUtilityColorWheelTexture();
      if ((UnityEngine.Object)wheel != (UnityEngine.Object)null) GUI.DrawTexture(wheelRect, wheel, ScaleMode.StretchToFill, true);

      // The marker sits exactly on the selected (target) hue/saturation, so it is
      // always right under the mouse while dragging.
      float hue, saturation, brightness;
      this.GetUtilityColorValues(this.utilityColorPickerMode, out hue, out saturation, out brightness);
      float radius = wheelRect.width * 0.5f - 2f;
      float angle = hue * Mathf.PI * 2f;
      Vector2 marker = new Vector2(
        wheelRect.center.x + Mathf.Cos(angle) * radius * saturation,
        wheelRect.center.y - Mathf.Sin(angle) * radius * saturation);
      this.DrawRoundedRect(new Rect(marker.x - 7f, marker.y - 7f, 14f, 14f), 7, new Color(0.05f, 0.04f, 0.06f, 1f), Color.white, 2);
      Color markerFill = Color.HSVToRGB(hue, saturation, 1f);
      this.DrawRoundedRect(new Rect(marker.x - 4f, marker.y - 4f, 8f, 8f), 4, markerFill, Color.clear, 0);

      Color selected = this.utilityColorPickerMode == 1 ? this.GetUtilityForestTargetColor() :
                       (this.utilityColorPickerMode == 3 ? this.GetUtilityLavaTargetColor() : this.GetUtilityLeavesTargetColor());
      Rect selectedRect = new Rect(panel.x + 18f, panel.yMax - 23f, panel.width - 36f, 10f);
      Color display = new Color(Mathf.Clamp01(selected.r), Mathf.Clamp01(selected.g), Mathf.Clamp01(selected.b), 1f);
      this.DrawRoundedRect(selectedRect, 5, display, new Color(0.32f, 0.18f, 0.44f, 1f), 1);
    }

    private void DrawUtilityDesktopLeavesColorControls()
    {
      this.EnsureUtilityEnvironmentColorPrefsLoaded();
      GUILayout.Label("Environment Colors", this.sectionHeaderStyle);
      GUILayout.Space(4f);
      GUILayout.BeginHorizontal();
      this.DrawUtilityEnvironmentModeButton("LEAVES", 0);
      this.DrawUtilityEnvironmentModeButton("FOREST", 1);
      this.DrawUtilityEnvironmentModeButton("LAVA", 3);
      GUILayout.EndHorizontal();
      GUILayout.Space(5f);

      if (this.utilityEnvironmentColorMode == 1)
      {
        bool old = this.utilityForestColorEnabled;
        this.utilityForestColorEnabled = this.DrawToggleRow("Forest Color", this.utilityForestColorEnabled);
        if (old != this.utilityForestColorEnabled)
        {
          this.utilityForestNextScanAt = -1f;
          PlayerPrefs.SetInt("UnknownsUtility_ForestColorEnabled", this.utilityForestColorEnabled ? 1 : 0);
          PlayerPrefs.Save();
          this.ShowToast("Forest Color", this.utilityForestColorEnabled ? "Enabled" : "Restoring original forest");
        }
        this.DrawUtilityColorPreview(this.GetUtilityForestTargetColor(), "FOREST COLOR");
        this.DrawUtilityColorPickerButton(1, this.GetUtilityForestTargetColor());
        GUILayout.Space(10f); // V12.79: sliders sit a bit lower under the colour button
        this.DrawSliderWithInput("Saturation:", this.utilityForestSaturation * 100f, 0f, 100f, "UtilityForestSat", "F0", v =>
        {
          this.utilityForestSaturation = Mathf.Clamp01(v / 100f);
          PlayerPrefs.SetFloat("UnknownsUtility_ForestSaturation", this.utilityForestSaturation); PlayerPrefs.Save();
        });
        this.DrawSliderWithInput("Brightness:", this.utilityForestBrightness * 100f, 10f, 400f, "UtilityForestBrightness", "F0", v =>
        {
          this.utilityForestBrightness = Mathf.Clamp(v / 100f, 0.10f, 4f);
          PlayerPrefs.SetFloat("UnknownsUtility_ForestBrightness", this.utilityForestBrightness); PlayerPrefs.Save();
        });
        return;
      }

      if (this.utilityEnvironmentColorMode == 3)
      {
        bool old = this.utilityLavaColorEnabled;
        this.utilityLavaColorEnabled = this.DrawToggleRow("Lava Color", this.utilityLavaColorEnabled);
        if (old != this.utilityLavaColorEnabled)
        {
          this.utilityLavaNextScanAt = -1f;
          PlayerPrefs.SetInt("UnknownsUtility_LavaColorEnabled", this.utilityLavaColorEnabled ? 1 : 0);
          PlayerPrefs.Save();
          this.ShowToast("Lava Color", this.utilityLavaColorEnabled ? "Enabled" : "Restoring original lava");
        }
        this.DrawUtilityColorPreview(this.GetUtilityLavaTargetColor(), "LAVA COLOR");
        this.DrawUtilityColorPickerButton(3, this.GetUtilityLavaTargetColor());
        GUILayout.Space(10f); // V12.79: sliders sit a bit lower under the colour button
        this.DrawSliderWithInput("Saturation:", this.utilityLavaSaturation * 100f, 0f, 100f, "UtilityLavaSat", "F0", v =>
        {
          this.utilityLavaSaturation = Mathf.Clamp01(v / 100f);
          PlayerPrefs.SetFloat("UnknownsUtility_LavaSaturation", this.utilityLavaSaturation); PlayerPrefs.Save();
        });
        this.DrawSliderWithInput("Brightness:", this.utilityLavaBrightness * 100f, 10f, 400f, "UtilityLavaBrightness", "F0", v =>
        {
          this.utilityLavaBrightness = Mathf.Clamp(v / 100f, 0.10f, 4f);
          PlayerPrefs.SetFloat("UnknownsUtility_LavaBrightness", this.utilityLavaBrightness); PlayerPrefs.Save();
        });
        return;
      }

      bool oldLeaves = this.utilityLeavesColorEnabled;
      this.utilityLeavesColorEnabled = this.DrawToggleRow("Leaves Color", this.utilityLeavesColorEnabled);
      if (oldLeaves != this.utilityLeavesColorEnabled)
      {
        this.utilityLeavesNextScanAt = -1f;
        PlayerPrefs.SetInt("UnknownsUtility_LeavesColorEnabled", this.utilityLeavesColorEnabled ? 1 : 0);
        PlayerPrefs.Save();
        this.ShowToast("Leaves Color", this.utilityLeavesColorEnabled ? "Enabled" : "Restoring original colors");
      }

      bool hidden = this.UtilityLeavesHidden;
      if (this.DesktopGUILayoutButton(hidden ? "SHOW LEAVES" : "REMOVE LEAVES", hidden ? this.roundedButtonActiveStyle : this.roundedButtonStyle, GUILayout.Height(this.UiH(26f))))
      {
        this.UtilityLeavesHidden = !hidden;
        this.ShowToast("Leaves", !hidden ? "Leaves removed" : "Leaves restored");
      }
      GUILayout.Space(4f);
      bool spring = this.UtilitySpringLeaves;
      bool nextSpring = this.DrawToggleRow("Blossom Leaves", spring);
      if (nextSpring != spring)
      {
        this.UtilitySpringLeaves = nextSpring;
        this.ShowToast("Leaves", nextSpring ? "Blossom leaves on" : "Normal leaves restored");
      }
      GUILayout.Space(4f);
      this.DrawUtilityColorPreview(this.GetUtilityLeavesTargetColor(), "LEAF COLOR");
      this.DrawUtilityColorPickerButton(0, this.GetUtilityLeavesTargetColor());
      GUILayout.Space(10f); // V12.79: sliders sit a bit lower under the colour button
      this.DrawSliderWithInput("Saturation:", this.utilityLeavesSaturation * 100f, 0f, 100f, "UtilityLeafSat", "F0", v =>
      {
        this.utilityLeavesSaturation = Mathf.Clamp01(v / 100f);
        PlayerPrefs.SetFloat("UnknownsUtility_LeavesSaturation", this.utilityLeavesSaturation); PlayerPrefs.Save();
      });
      this.DrawSliderWithInput("Brightness:", this.utilityLeavesBrightness * 100f, 10f, 400f, "UtilityLeafValue", "F0", v =>
      {
        this.utilityLeavesBrightness = Mathf.Clamp(v / 100f, 0.10f, 4f);
        PlayerPrefs.SetFloat("UnknownsUtility_LeavesBrightness", this.utilityLeavesBrightness); PlayerPrefs.Save();
      });
    }

    internal void UpdateUtilityRuntimeAddons(float deltaTime)
    {
      // The assembly is compiled as PLUS, but Casting Mod can be disabled at
      // runtime. Use the *runtime* edition here; otherwise every Utility-only
      // injection accidentally enters the PLUS environment-color path.
      if (ProductEdition.IsPlusRuntime)
      {
        this.EnsureUtilityEnvironmentColorPrefsLoaded();
        this.UpdateUtilityForestColor(deltaTime);
        this.UpdateUtilityLeavesColor(deltaTime);
        this.UpdateUtilityLavaColor(deltaTime);
        return;
      }

      if (!ProductEdition.IsUtilityRuntime || !UtilityAddonCatalog.RuntimeEntitled) return;
      if (this.HasUtilityAddon(UtilityAddonIds.Wasd) && this.wasdEnabled && !this.freecam && Keyboard.current != null)
        this.HandleWASD(deltaTime);
      if (this.HasUtilityAddon(UtilityAddonIds.LeavesColor))
      {
        this.EnsureUtilityEnvironmentColorPrefsLoaded();
        this.UpdateUtilityForestColor(deltaTime);
        this.UpdateUtilityLeavesColor(deltaTime);
        this.UpdateUtilityLavaColor(deltaTime);
      }
    }

    // V12.79: "Remove Leaves" - hides every leaf renderer (restored exactly when off).
    private bool utilityLeavesHidden;
    private bool utilityLeavesHiddenLoaded;
    private float utilityLeavesHideScanAt = -1f;
    private readonly Dictionary<Renderer, bool> utilityHiddenLeafRenderers = new Dictionary<Renderer, bool>();

    private bool UtilityLeavesHidden
    {
      get
      {
        if (!this.utilityLeavesHiddenLoaded)
        {
          this.utilityLeavesHiddenLoaded = true;
          this.utilityLeavesHidden = PlayerPrefs.GetInt("UnknownsUtility_LeavesHidden", 0) != 0;
        }
        return this.utilityLeavesHidden;
      }
      set
      {
        this.utilityLeavesHiddenLoaded = true;
        this.utilityLeavesHidden = value;
        PlayerPrefs.SetInt("UnknownsUtility_LeavesHidden", value ? 1 : 0);
        PlayerPrefs.Save();
        this.utilityLeavesHideScanAt = -1f;
        if (!value) this.RestoreHiddenLeaves();
      }
    }

    private void UpdateHiddenLeaves()
    {
      if (!this.UtilityLeavesHidden) return;
      if (this.utilityLeavesHideScanAt < 0f || Time.unscaledTime >= this.utilityLeavesHideScanAt)
      {
        this.utilityLeavesHideScanAt = Time.unscaledTime + 3f;
        this.ScanUtilityLeafRenderers();
      }
      for (int i = 0; i < this.utilityLeafRenderers.Count; ++i)
      {
        UtilityLeafRendererState st = this.utilityLeafRenderers[i];
        if (st == null || (UnityEngine.Object)st.Renderer == (UnityEngine.Object)null) continue;
        if (!this.utilityHiddenLeafRenderers.ContainsKey(st.Renderer)) this.utilityHiddenLeafRenderers[st.Renderer] = st.Renderer.enabled;
        if (st.Renderer.enabled) st.Renderer.enabled = false;
      }
    }

    private void RestoreHiddenLeaves()
    {
      foreach (KeyValuePair<Renderer, bool> kv in this.utilityHiddenLeafRenderers)
      {
        try { if ((UnityEngine.Object)kv.Key != (UnityEngine.Object)null) kv.Key.enabled = kv.Value; } catch { }
      }
      this.utilityHiddenLeafRenderers.Clear();
    }

    private void UpdateUtilityLeavesColor(float deltaTime)
    {
      this.UpdateSpringLeaves();
      this.UpdateHiddenLeaves();
      if (this.utilityLeavesColorEnabled && (this.utilityLeavesNextScanAt < 0f || Time.unscaledTime >= this.utilityLeavesNextScanAt))
      {
        this.ScanUtilityLeafRenderers();
        this.utilityLeavesNextScanAt = Time.unscaledTime + 6f;
      }
      if (this.utilityLeafRenderers.Count == 0) return;

      Color selected = this.GetUtilityLeavesTargetColor();
      float t = 1f - Mathf.Exp(-UtilityEnvironmentTransitionSpeed * Mathf.Max(0.001f, deltaTime));
      for (int i = this.utilityLeafRenderers.Count - 1; i >= 0; --i)
      {
        UtilityLeafRendererState state = this.utilityLeafRenderers[i];
        if (state == null || (UnityEngine.Object)state.Renderer == (UnityEngine.Object)null)
        {
          this.utilityLeafRenderers.RemoveAt(i);
          continue;
        }
        Color target = this.utilityLeavesColorEnabled ? new Color(selected.r, selected.g, selected.b, state.OriginalColor.a) : state.OriginalColor;
        state.CurrentColor = Color.Lerp(state.CurrentColor, target, t);
        try
        {
          this.utilityLeafPropertyBlock.Clear();
          state.Renderer.GetPropertyBlock(this.utilityLeafPropertyBlock);
          if (state.HasColor) this.utilityLeafPropertyBlock.SetColor("_Color", state.CurrentColor);
          if (state.HasBaseColor) this.utilityLeafPropertyBlock.SetColor("_BaseColor", state.CurrentColor);
          state.Renderer.SetPropertyBlock(this.utilityLeafPropertyBlock);

          if (!this.utilityLeavesColorEnabled && ColorDistance(state.CurrentColor, state.OriginalColor) < 0.006f)
          {
            state.Renderer.SetPropertyBlock(state.OriginalBlock);
            this.utilityLeafRenderers.RemoveAt(i);
          }
        }
        catch { }
      }
    }

    private void UpdateUtilityForestColor(float deltaTime)
    {
      if (this.utilityForestColorEnabled && (this.utilityForestNextScanAt < 0f || Time.unscaledTime >= this.utilityForestNextScanAt))
      {
        this.ScanUtilityForestRenderers();
        this.utilityForestNextScanAt = Time.unscaledTime + 6f;
      }
      if (this.utilityForestRenderers.Count == 0) return;

      Color selected = this.GetUtilityForestTargetColor();
      float t = 1f - Mathf.Exp(-UtilityEnvironmentTransitionSpeed * Mathf.Max(0.001f, deltaTime));
      for (int i = this.utilityForestRenderers.Count - 1; i >= 0; --i)
      {
        UtilityForestRendererState state = this.utilityForestRenderers[i];
        if (state == null || (UnityEngine.Object)state.Renderer == (UnityEngine.Object)null)
        {
          this.utilityForestRenderers.RemoveAt(i);
          continue;
        }

        // Leaves have their own higher-priority color channel. Forest Color still
        // colors them when Leaves Color is off, so FOREST really can affect the whole map.
        bool leafOverride = this.utilityLeavesColorEnabled && this.IsUtilityLeafRenderer(state.Renderer);
        bool lavaOverride = this.utilityLavaColorEnabled && this.IsUtilityLavaRenderer(state.Renderer);
        if (leafOverride || lavaOverride) continue;

        Color target = this.utilityForestColorEnabled ? new Color(selected.r, selected.g, selected.b, state.OriginalColor.a) : state.OriginalColor;
        state.CurrentColor = Color.Lerp(state.CurrentColor, target, t);
        try
        {
          this.utilityForestPropertyBlock.Clear();
          state.Renderer.GetPropertyBlock(this.utilityForestPropertyBlock);
          if (state.HasColor) this.utilityForestPropertyBlock.SetColor("_Color", state.CurrentColor);
          if (state.HasBaseColor) this.utilityForestPropertyBlock.SetColor("_BaseColor", state.CurrentColor);
          state.Renderer.SetPropertyBlock(this.utilityForestPropertyBlock);
          if (!this.utilityForestColorEnabled && ColorDistance(state.CurrentColor, state.OriginalColor) < 0.006f)
          {
            state.Renderer.SetPropertyBlock(state.OriginalBlock);
            this.utilityForestRenderers.RemoveAt(i);
          }
        }
        catch { }
      }
    }

    private void ScanUtilityForestRenderers()
    {
      try
      {
        GameObject forestRoot = GameObject.Find("Environment Objects/LocalObjects_Prefab/Forest");
        // V12.79: the stump (tree room) lives outside the Forest root - colour it too.
        List<Renderer> all = new List<Renderer>();
        if ((UnityEngine.Object)forestRoot != (UnityEngine.Object)null) all.AddRange(forestRoot.GetComponentsInChildren<Renderer>(true));
        foreach (Transform stumpRoot in this.FindUtilityStumpRoots())
          if ((UnityEngine.Object)stumpRoot != (UnityEngine.Object)null) all.AddRange(stumpRoot.GetComponentsInChildren<Renderer>(true));
        if (all.Count == 0) return;
        Renderer[] renderers = all.ToArray();
        for (int i = 0; i < renderers.Length; ++i)
        {
          Renderer renderer = renderers[i];
          if ((UnityEngine.Object)renderer == (UnityEngine.Object)null || !renderer.gameObject.activeInHierarchy) continue;
          // Avoid a hard compile-time dependency on UnityEngine.ParticleSystemModule.
          // ParticleSystemRenderer lives in that optional Unity module on newer Gorilla Tag builds.
          string rendererTypeName = renderer.GetType().Name;
          if (string.Equals(rendererTypeName, "ParticleSystemRenderer", StringComparison.Ordinal) || renderer is TrailRenderer || renderer is LineRenderer) continue;
          // V12.97: Forest Color is for the map only - never a player, a gorilla
          // display or a mirror/screen that shows players
          if (IsUtilityPlayerLikeRenderer(renderer)) continue;

          bool exists = false;
          for (int j = 0; j < this.utilityForestRenderers.Count; ++j)
          {
            if ((UnityEngine.Object)this.utilityForestRenderers[j].Renderer == (UnityEngine.Object)renderer) { exists = true; break; }
          }
          if (exists) continue;

          Material[] mats = renderer.sharedMaterials;
          Material source = (mats != null && mats.Length > 0) ? mats[0] : null;
          if ((UnityEngine.Object)source == (UnityEngine.Object)null) continue;
          bool hasColor = source.HasProperty("_Color");
          bool hasBaseColor = source.HasProperty("_BaseColor");
          if (!hasColor && !hasBaseColor) continue;
          Color original = Color.white;
          try { original = hasBaseColor ? source.GetColor("_BaseColor") : source.GetColor("_Color"); } catch { }
          UtilityForestRendererState state = new UtilityForestRendererState
          {
            Renderer = renderer,
            OriginalColor = original,
            CurrentColor = original,
            HasColor = hasColor,
            HasBaseColor = hasBaseColor
          };
          try { renderer.GetPropertyBlock(state.OriginalBlock); } catch { }
          this.utilityForestRenderers.Add(state);
        }
      }
      catch { }
    }

    private static readonly string[] UtilityPlayerLikeNames = { "gorilla", "monke", "display_body", "mirror", "player", "mannequin", "plush", "wardrobe", "cosmetic", "camera", "screen", "tablet" };

    // Renderers that are, or show, players: anything on a rig, skinned gorilla
    // bodies and displays, and mirrors/screens drawing a camera picture.
    private static bool IsUtilityPlayerLikeRenderer(Renderer renderer)
    {
      try
      {
        if ((UnityEngine.Object)renderer.GetComponentInParent<VRRig>() != (UnityEngine.Object)null) return true;
        if (renderer is SkinnedMeshRenderer) return true;
        Material m = renderer.sharedMaterial;
        if ((UnityEngine.Object)m != (UnityEngine.Object)null && m.HasProperty("_MainTex") && m.GetTexture("_MainTex") is RenderTexture) return true;
        Transform t = renderer.transform;
        for (int depth = 0; t != null && depth < 4; depth++, t = t.parent)
        {
          string n = (t.name ?? string.Empty).ToLowerInvariant();
          for (int i = 0; i < UtilityPlayerLikeNames.Length; i++)
            if (n.Contains(UtilityPlayerLikeNames[i])) return true;
        }
        string matName = (UnityEngine.Object)m != (UnityEngine.Object)null ? (m.name ?? string.Empty).ToLowerInvariant() : string.Empty;
        return matName.Contains("mirror") || matName.Contains("gorilla") || matName.Contains("fur");
      }
      catch { return false; }
    }

    private readonly List<Transform> utilityStumpRoots = new List<Transform>();
    private float utilityStumpRootsScanAt = -1f;

    // Top-most objects named like the stump / tree room, found once and cached
    // (re-scanned every 30 s or when they are unloaded).
    private List<Transform> FindUtilityStumpRoots()
    {
      bool valid = this.utilityStumpRoots.Count > 0;
      for (int i = 0; i < this.utilityStumpRoots.Count && valid; ++i)
        if ((UnityEngine.Object)this.utilityStumpRoots[i] == (UnityEngine.Object)null) valid = false;
      if (valid && Time.unscaledTime < this.utilityStumpRootsScanAt) return this.utilityStumpRoots;
      this.utilityStumpRoots.Clear();
      this.utilityStumpRootsScanAt = Time.unscaledTime + 30f;
      try
      {
        GameObject forestRoot = GameObject.Find("Environment Objects/LocalObjects_Prefab/Forest");
        Transform forest = (UnityEngine.Object)forestRoot != (UnityEngine.Object)null ? forestRoot.transform : null;
        GameObject envRoot = GameObject.Find("Environment Objects/LocalObjects_Prefab");
        Stack<Transform> stack = new Stack<Transform>();
        if ((UnityEngine.Object)envRoot != (UnityEngine.Object)null) stack.Push(envRoot.transform);
        int visited = 0;
        while (stack.Count > 0 && visited < 60000)
        {
          Transform t = stack.Pop();
          visited++;
          if ((UnityEngine.Object)t == (UnityEngine.Object)null) continue;
          if (forest != null && t == forest) continue; // already covered
          string n = (t.name ?? string.Empty).ToLowerInvariant();
          if (n.Contains("stump") || n.Contains("treeroom") || n.Contains("tree room"))
          {
            this.utilityStumpRoots.Add(t);
            continue; // children come with it
          }
          for (int c = 0; c < t.childCount; ++c) stack.Push(t.GetChild(c));
        }
      }
      catch { }
      return this.utilityStumpRoots;
    }

    private void CaptureUtilityLightingGlobals()
    {
      if (this.utilityLightingGlobalsCaptured) return;
      try
      {
        this.utilityOriginalAmbientLight = RenderSettings.ambientLight;
        this.utilityOriginalAmbientSky = RenderSettings.ambientSkyColor;
        this.utilityOriginalAmbientEquator = RenderSettings.ambientEquatorColor;
        this.utilityOriginalAmbientGround = RenderSettings.ambientGroundColor;
        this.utilityOriginalFogColor = RenderSettings.fogColor;
        this.utilityOriginalSubtractiveShadow = RenderSettings.subtractiveShadowColor;
        this.utilityCurrentAmbientLight = this.utilityOriginalAmbientLight;
        this.utilityCurrentAmbientSky = this.utilityOriginalAmbientSky;
        this.utilityCurrentAmbientEquator = this.utilityOriginalAmbientEquator;
        this.utilityCurrentAmbientGround = this.utilityOriginalAmbientGround;
        this.utilityCurrentFogColor = this.utilityOriginalFogColor;
        this.utilityCurrentSubtractiveShadow = this.utilityOriginalSubtractiveShadow;
        this.utilityLightingGlobalsCaptured = true;
      }
      catch { }
    }

    private static Color MultiplyUtilityLightingColor(Color source, Color tint, float brightness)
    {
      float mult = Mathf.Clamp(brightness, 0.10f, 4f);
      // Preserve the original lighting strength, but use the selected picker hue
      // instead of multiplying channel-by-channel (which could make chosen colors
      // appear to do nothing when the original light had a weak RGB channel).
      float sourceStrength = Mathf.Max(0.08f, source.grayscale);
      return new Color(tint.r * sourceStrength * mult, tint.g * sourceStrength * mult, tint.b * sourceStrength * mult, source.a);
    }

    private void UpdateUtilityLightingGlobals(Color selected, float brightness, float t)
    {
      if (!this.utilityLightingGlobalsCaptured) this.CaptureUtilityLightingGlobals();
      if (!this.utilityLightingGlobalsCaptured) return;
      try
      {
        Color targetAmbientLight = this.utilityLightsColorEnabled ? MultiplyUtilityLightingColor(this.utilityOriginalAmbientLight, selected, brightness) : this.utilityOriginalAmbientLight;
        Color targetSky = this.utilityLightsColorEnabled ? MultiplyUtilityLightingColor(this.utilityOriginalAmbientSky, selected, brightness) : this.utilityOriginalAmbientSky;
        Color targetEquator = this.utilityLightsColorEnabled ? MultiplyUtilityLightingColor(this.utilityOriginalAmbientEquator, selected, brightness) : this.utilityOriginalAmbientEquator;
        Color targetGround = this.utilityLightsColorEnabled ? MultiplyUtilityLightingColor(this.utilityOriginalAmbientGround, selected, brightness) : this.utilityOriginalAmbientGround;
        Color targetFog = this.utilityLightsColorEnabled ? MultiplyUtilityLightingColor(this.utilityOriginalFogColor, selected, Mathf.Lerp(1f, brightness, 0.45f)) : this.utilityOriginalFogColor;
        Color targetShadow = this.utilityLightsColorEnabled ? MultiplyUtilityLightingColor(this.utilityOriginalSubtractiveShadow, selected, Mathf.Lerp(1f, brightness, 0.35f)) : this.utilityOriginalSubtractiveShadow;
        this.utilityCurrentAmbientLight = Color.Lerp(this.utilityCurrentAmbientLight, targetAmbientLight, t);
        this.utilityCurrentAmbientSky = Color.Lerp(this.utilityCurrentAmbientSky, targetSky, t);
        this.utilityCurrentAmbientEquator = Color.Lerp(this.utilityCurrentAmbientEquator, targetEquator, t);
        this.utilityCurrentAmbientGround = Color.Lerp(this.utilityCurrentAmbientGround, targetGround, t);
        this.utilityCurrentFogColor = Color.Lerp(this.utilityCurrentFogColor, targetFog, t);
        this.utilityCurrentSubtractiveShadow = Color.Lerp(this.utilityCurrentSubtractiveShadow, targetShadow, t);
        RenderSettings.ambientLight = this.utilityCurrentAmbientLight;
        RenderSettings.ambientSkyColor = this.utilityCurrentAmbientSky;
        RenderSettings.ambientEquatorColor = this.utilityCurrentAmbientEquator;
        RenderSettings.ambientGroundColor = this.utilityCurrentAmbientGround;
        RenderSettings.fogColor = this.utilityCurrentFogColor;
        RenderSettings.subtractiveShadowColor = this.utilityCurrentSubtractiveShadow;
      }
      catch { }
    }

    private void UpdateUtilityLightsColor(float deltaTime)
    {
      if (this.utilityLightsColorEnabled && (this.utilityLightsNextScanAt < 0f || Time.unscaledTime >= this.utilityLightsNextScanAt))
      {
        this.ScanUtilityLights();
        this.utilityLightsNextScanAt = Time.unscaledTime + 6f;
      }
      Color selected = this.GetUtilityLightsTargetColor();
      float intensityMultiplier = Mathf.Clamp(this.utilityLightsBrightness, 0.10f, 4f);
      float t = 1f - Mathf.Exp(-UtilityEnvironmentTransitionSpeed * Mathf.Max(0.001f, deltaTime));
      this.UpdateUtilityLightingGlobals(selected, intensityMultiplier, t);
      if (this.utilityLights.Count == 0) return;
      for (int i = this.utilityLights.Count - 1; i >= 0; --i)
      {
        UtilityLightState state = this.utilityLights[i];
        if (state == null || (UnityEngine.Object)state.Light == (UnityEngine.Object)null)
        {
          this.utilityLights.RemoveAt(i);
          continue;
        }
        Color targetColor = this.utilityLightsColorEnabled ? selected : state.OriginalColor;
        float targetIntensity = this.utilityLightsColorEnabled ? state.OriginalIntensity * intensityMultiplier : state.OriginalIntensity;
        state.CurrentColor = Color.Lerp(state.CurrentColor, targetColor, t);
        state.CurrentIntensity = Mathf.Lerp(state.CurrentIntensity, targetIntensity, t);
        try
        {
          state.Light.color = state.CurrentColor;
          state.Light.intensity = state.CurrentIntensity;
          if (!this.utilityLightsColorEnabled && ColorDistance(state.CurrentColor, state.OriginalColor) < 0.006f && Mathf.Abs(state.CurrentIntensity - state.OriginalIntensity) < 0.01f)
            this.utilityLights.RemoveAt(i);
        }
        catch { }
      }
    }

    private void ScanUtilityLights()
    {
      try
      {
        Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
        if (lights == null) return;
        for (int i = 0; i < lights.Length; ++i)
        {
          Light light = lights[i];
          if ((UnityEngine.Object)light == (UnityEngine.Object)null || !light.gameObject.scene.IsValid()) continue;
          bool exists = false;
          for (int j = 0; j < this.utilityLights.Count; ++j)
          {
            if ((UnityEngine.Object)this.utilityLights[j].Light == (UnityEngine.Object)light) { exists = true; break; }
          }
          if (exists) continue;
          this.utilityLights.Add(new UtilityLightState
          {
            Light = light,
            OriginalColor = light.color,
            CurrentColor = light.color,
            OriginalIntensity = light.intensity,
            CurrentIntensity = light.intensity
          });
        }
      }
      catch { }
    }

    private static float ColorDistance(Color a, Color b)
    {
      return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);
    }

    private static readonly string[] UtilityLavaColorProperties = new[]
    {
      "_Color", "_BaseColor", "_EmissionColor", "_TintColor", "_Tint",
      "_LavaColor", "_LiquidColor", "_UnderwaterColor", "_FogColor", "_BaseTint",
      "_MainColor", "_AlbedoColor", "_DiffuseColor", "_GlowColor", "_EmissionTint",
      "_PrimaryColor", "_SecondaryColor", "_EdgeColor", "_PlayerColor",
      "_SkinColor", "_BodyColor", "_GorillaColor", "_Color1", "_Color2"
    };

    private bool ApplyUtilityLavaMaterialState(UtilityLavaMaterialState materialState, Color selected, float t)
    {
      if (materialState == null || (UnityEngine.Object)materialState.Material == (UnityEngine.Object)null) return true;
      bool finishedRestore = true;
      List<string> props = new List<string>(materialState.OriginalColors.Keys);
      for (int p = 0; p < props.Count; ++p)
      {
        string prop = props[p];
        Color original = materialState.OriginalColors[prop];
        Color current;
        if (!materialState.CurrentColors.TryGetValue(prop, out current)) current = original;
        bool emissionLike = prop.IndexOf("Emission", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            prop.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0;
        float alpha = original.a;
        Color target = this.utilityLavaColorEnabled
          ? new Color(selected.r, selected.g, selected.b, alpha)
          : original;
        if (emissionLike && this.utilityLavaColorEnabled)
        {
          target.r *= 1.50f; target.g *= 1.50f; target.b *= 1.50f;
        }
        current = Color.Lerp(current, target, t);
        materialState.CurrentColors[prop] = current;
        try { materialState.Material.SetColor(prop, current); } catch { }
        if (ColorDistance(current, original) >= 0.006f) finishedRestore = false;
      }
      // Some Gorilla materials expose their tint only through Material.color even
      // when the shader property list is unusual. Keep that alias in sync too.
      if (this.utilityLavaColorEnabled)
      {
        try
        {
          if (materialState.Material.HasProperty("_Color"))
            materialState.Material.color = materialState.CurrentColors.ContainsKey("_Color")
              ? materialState.CurrentColors["_Color"] : new Color(selected.r, selected.g, selected.b, 1f);
        }
        catch { }
      }
      return finishedRestore;
    }

    internal void ReapplyUtilityLavaColorForRender()
    {
      if (!this.utilityLavaColorEnabled) return;
      // Materials are global state, so doing the same work once for every camera in
      // a frame is redundant. One reapply per frame is enough for all camera passes.
      if (this.utilityLavaLastRenderReapplyFrame == Time.frameCount) return;
      this.utilityLavaLastRenderReapplyFrame = Time.frameCount;
      this.EnsureUtilityEnvironmentColorPrefsLoaded();
      Color selected = this.GetUtilityLavaTargetColor();

      // Re-assert cached material state immediately. Gorilla Tag can call SetMaterial()
      // after our normal Update tick when a player becomes tagged, which otherwise
      // restores the stock green/infected appearance right before rendering.
      for (int i = 0; i < this.utilityLavaRenderers.Count; ++i)
      {
        UtilityLavaRendererState state = this.utilityLavaRenderers[i];
        if (state == null || (UnityEngine.Object)state.Renderer == (UnityEngine.Object)null) continue;
        for (int m = 0; m < state.Materials.Count; ++m)
          this.ApplyUtilityLavaMaterialState(state.Materials[m], selected, 1f);
      }
      for (int i = 0; i < this.utilityLavaSourceMaterials.Count; ++i)
        this.ApplyUtilityLavaMaterialState(this.utilityLavaSourceMaterials[i], selected, 1f);

      // Also hit the CURRENT tagged rig material directly. This covers the exact
      // frame where Gorilla swaps a new infected material instance onto mainSkin
      // before the periodic scanner has had time to capture that new instance.
      try
      {
        // Do not call Resources.FindObjectsOfTypeAll from a render callback. The
        // rig cache is maintained by room callbacks/name-tag reconciliation.
        IList<VRRig> rigs = UnknownsCastingUI.Cache.GorillaRigCache.ActiveRigs;
        if (rigs != null)
        {
          for (int r = 0; r < rigs.Count; ++r)
          {
            VRRig rig = rigs[r];
            if ((UnityEngine.Object)rig == (UnityEngine.Object)null || !rig.gameObject.scene.IsValid()) continue;
            int matIndex = 0;
            try { matIndex = rig.setMatIndex; } catch { }
            if (matIndex == 0) continue;
            Renderer skin = null;
            try { skin = rig.mainSkin; } catch { }
            if ((UnityEngine.Object)skin == (UnityEngine.Object)null) continue;
            Material[] mats = null;
            try { mats = skin.materials; } catch { }
            if (mats == null) continue;
            for (int m = 0; m < mats.Length; ++m)
            {
              Material material = mats[m];
              if ((UnityEngine.Object)material == (UnityEngine.Object)null) continue;
              this.ApplyUtilityLavaMaterialImmediate(material, selected);
            }
          }
        }
      }
      catch { }
    }

    private void ApplyUtilityLavaMaterialImmediate(Material material, Color selected)
    {
      if ((UnityEngine.Object)material == (UnityEngine.Object)null) return;
      for (int p = 0; p < UtilityLavaColorProperties.Length; ++p)
      {
        string prop = UtilityLavaColorProperties[p];
        try
        {
          if (!material.HasProperty(prop)) continue;
          Color old = material.GetColor(prop);
          bool emissionLike = prop.IndexOf("Emission", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              prop.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0;
          Color target = new Color(selected.r, selected.g, selected.b, old.a);
          if (emissionLike)
          {
            target.r *= 1.50f; target.g *= 1.50f; target.b *= 1.50f;
            try { material.EnableKeyword("_EMISSION"); } catch { }
          }
          material.SetColor(prop, target);
        }
        catch { }
      }
      try
      {
        if (material.HasProperty("_Color"))
          material.color = new Color(selected.r, selected.g, selected.b, material.color.a);
      }
      catch { }
    }

    private void UpdateUtilityLavaColor(float deltaTime)
    {
      if (this.utilityLavaColorEnabled && (this.utilityLavaNextScanAt < 0f || Time.unscaledTime >= this.utilityLavaNextScanAt))
      {
        this.ScanUtilityLavaRenderers();
        this.utilityLavaNextScanAt = Time.unscaledTime + 5f;
      }
      if (this.utilityLavaRenderers.Count == 0 && this.utilityLavaSourceMaterials.Count == 0) return;

      Color selected = this.GetUtilityLavaTargetColor();
      float t = 1f - Mathf.Exp(-UtilityEnvironmentTransitionSpeed * Mathf.Max(0.001f, deltaTime));
      for (int i = this.utilityLavaRenderers.Count - 1; i >= 0; --i)
      {
        UtilityLavaRendererState state = this.utilityLavaRenderers[i];
        if (state == null || (UnityEngine.Object)state.Renderer == (UnityEngine.Object)null)
        {
          this.utilityLavaRenderers.RemoveAt(i);
          continue;
        }

        bool finishedRestore = true;
        for (int m = state.Materials.Count - 1; m >= 0; --m)
        {
          UtilityLavaMaterialState materialState = state.Materials[m];
          if (materialState == null || (UnityEngine.Object)materialState.Material == (UnityEngine.Object)null)
          {
            state.Materials.RemoveAt(m);
            continue;
          }
          if (!this.ApplyUtilityLavaMaterialState(materialState, selected, t)) finishedRestore = false;
        }

        if (!this.utilityLavaColorEnabled && finishedRestore)
          this.utilityLavaRenderers.RemoveAt(i);
      }

      for (int i = this.utilityLavaSourceMaterials.Count - 1; i >= 0; --i)
      {
        UtilityLavaMaterialState sourceState = this.utilityLavaSourceMaterials[i];
        if (sourceState == null || (UnityEngine.Object)sourceState.Material == (UnityEngine.Object)null)
        {
          this.utilityLavaSourceMaterials.RemoveAt(i);
          continue;
        }
        bool restored = this.ApplyUtilityLavaMaterialState(sourceState, selected, t);
        if (!this.utilityLavaColorEnabled && restored) this.utilityLavaSourceMaterials.RemoveAt(i);
      }
    }

    private UtilityLavaMaterialState CaptureUtilityLavaMaterial(Material material)
    {
      if ((UnityEngine.Object)material == (UnityEngine.Object)null) return null;
      UtilityLavaMaterialState ms = new UtilityLavaMaterialState { Material = material };
      for (int c = 0; c < UtilityLavaColorProperties.Length; ++c)
      {
        string prop = UtilityLavaColorProperties[c];
        try
        {
          if (!material.HasProperty(prop)) continue;
          Color original = material.GetColor(prop);
          ms.OriginalColors[prop] = original;
          ms.CurrentColors[prop] = original;
        }
        catch { }
      }
      return ms.OriginalColors.Count > 0 ? ms : null;
    }

    private void ScanUtilityLavaSourceMaterials()
    {
      try
      {
        VRRig[] rigs = Resources.FindObjectsOfTypeAll<VRRig>();
        if (rigs == null) return;
        for (int r = 0; r < rigs.Length; ++r)
        {
          VRRig rig = rigs[r];
          if ((UnityEngine.Object)rig == (UnityEngine.Object)null || !rig.gameObject.scene.IsValid() || rig.materialsToChangeTo == null) continue;
          int currentIndex = 0;
          try { currentIndex = rig.setMatIndex; } catch { }
          for (int i = 0; i < rig.materialsToChangeTo.Length; ++i)
          {
            Material material = rig.materialsToChangeTo[i];
            if ((UnityEngine.Object)material == (UnityEngine.Object)null) continue;
            // Index 0 is normally the untagged/player-color material. Tagged source
            // slots are accepted even when recent Gorilla builds give them generic names.
            bool candidate = this.MaterialLooksLikeLava(material) || (i > 0 && currentIndex == i);
            if (!candidate) continue;
            bool exists = false;
            for (int j = 0; j < this.utilityLavaSourceMaterials.Count; ++j)
              if ((UnityEngine.Object)this.utilityLavaSourceMaterials[j].Material == (UnityEngine.Object)material) { exists = true; break; }
            if (exists) continue;
            UtilityLavaMaterialState captured = this.CaptureUtilityLavaMaterial(material);
            if (captured != null) this.utilityLavaSourceMaterials.Add(captured);
          }
        }
      }
      catch { }
    }

    private void ScanUtilityLavaRenderers()
    {
      this.ScanUtilityLavaSourceMaterials();
      try
      {
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; ++i)
        {
          Renderer renderer = renderers[i];
          if ((UnityEngine.Object)renderer == (UnityEngine.Object)null || !renderer.gameObject.scene.IsValid()) continue;
          if (!this.IsUtilityLavaRenderer(renderer)) continue;

          bool exists = false;
          for (int j = 0; j < this.utilityLavaRenderers.Count; ++j)
          {
            if ((UnityEngine.Object)this.utilityLavaRenderers[j].Renderer == (UnityEngine.Object)renderer) { exists = true; break; }
          }
          if (exists) continue;

          UtilityLavaRendererState state = new UtilityLavaRendererState { Renderer = renderer };
          Material[] materials = null;
          try { materials = renderer.materials; } catch { } // instance materials so the live shader definitely receives the tint
          if (materials == null || materials.Length == 0) continue;

          bool rendererNamedLava = LooksLikeLavaName(this.GetUtilityRendererSearchName(renderer));
          for (int m = 0; m < materials.Length; ++m)
          {
            Material material = materials[m];
            if ((UnityEngine.Object)material == (UnityEngine.Object)null) continue;
            if (!rendererNamedLava && !this.MaterialLooksLikeLava(material)) continue;

            UtilityLavaMaterialState ms = this.CaptureUtilityLavaMaterial(material);
            if (ms != null) state.Materials.Add(ms);
          }

          if (state.Materials.Count > 0) this.utilityLavaRenderers.Add(state);
        }
      }
      catch { }
    }

    private string GetUtilityRendererSearchName(Renderer renderer)
    {
      if ((UnityEngine.Object)renderer == (UnityEngine.Object)null) return string.Empty;
      string value = string.Empty;
      try
      {
        Transform t = renderer.transform;
        int depth = 0;
        while ((UnityEngine.Object)t != (UnityEngine.Object)null && depth++ < 8)
        {
          value += "/" + t.name;
          t = t.parent;
        }
      }
      catch { }
      return value;
    }

    private bool IsUtilityLavaRenderer(Renderer renderer)
    {
      if ((UnityEngine.Object)renderer == (UnityEngine.Object)null) return false;
      try
      {
        // Do not exclude VRRig renderers: Gorilla Tag's infected/lava gorilla texture
        // lives on player-rig materials, and is one of the primary lava targets.
        if ((UnityEngine.Object)renderer.GetComponentInParent<Canvas>() != (UnityEngine.Object)null) return false;
        VRRig rig = renderer.GetComponentInParent<VRRig>();
        if ((UnityEngine.Object)rig != (UnityEngine.Object)null && rig.setMatIndex != 0) return true;
      }
      catch { }

      string objectName = renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
      if (LooksLikeLavaName(objectName)) return true;
      try
      {
        Transform t = renderer.transform != null ? renderer.transform.parent : null;
        for (int depth = 0; depth < 4 && t != null; ++depth, t = t.parent)
          if (LooksLikeLavaName(t.name)) return true;
      }
      catch { }
      try
      {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if ((UnityEngine.Object)filter != (UnityEngine.Object)null && (UnityEngine.Object)filter.sharedMesh != (UnityEngine.Object)null && LooksLikeLavaName(filter.sharedMesh.name)) return true;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if ((UnityEngine.Object)skinned != (UnityEngine.Object)null && (UnityEngine.Object)skinned.sharedMesh != (UnityEngine.Object)null && LooksLikeLavaName(skinned.sharedMesh.name)) return true;
      }
      catch { }
      Material[] mats = renderer.sharedMaterials;
      if (mats != null)
        for (int i = 0; i < mats.Length; ++i)
          if ((UnityEngine.Object)mats[i] != (UnityEngine.Object)null && this.MaterialLooksLikeLava(mats[i])) return true;
      return false;
    }

    // V12.79: cached per material - the rescans no longer re-inspect every texture.
    private readonly Dictionary<Material, bool> utilityMaterialLooksLikeLavaCache = new Dictionary<Material, bool>();

    private bool MaterialLooksLikeLava(Material material)
    {
      if ((UnityEngine.Object)material == (UnityEngine.Object)null) return false;
      bool cached;
      if (this.utilityMaterialLooksLikeLavaCache.TryGetValue(material, out cached)) return cached;
      if (this.utilityMaterialLooksLikeLavaCache.Count > 20000) this.utilityMaterialLooksLikeLavaCache.Clear();
      bool result = this.MaterialLooksLikeLavaUncached(material);
      this.utilityMaterialLooksLikeLavaCache[material] = result;
      return result;
    }

    private bool MaterialLooksLikeLavaUncached(Material material)
    {
      if ((UnityEngine.Object)material == (UnityEngine.Object)null) return false;
      if (LooksLikeLavaName(material.name)) return true;
      try
      {
        if ((UnityEngine.Object)material.shader != (UnityEngine.Object)null && LooksLikeLavaName(material.shader.name)) return true;
      }
      catch { }
      try
      {
        // V12.79: mainTexture logs an error (every call!) on shaders without _MainTex.
        Texture main = material.HasProperty("_MainTex") ? material.mainTexture : null;
        if ((UnityEngine.Object)main != (UnityEngine.Object)null && LooksLikeLavaName(main.name)) return true;
        string[] textureProps = material.GetTexturePropertyNames();
        if (textureProps != null)
        {
          for (int i = 0; i < textureProps.Length; ++i)
          {
            Texture tex = null;
            try { tex = material.GetTexture(textureProps[i]); } catch { }
            if ((UnityEngine.Object)tex != (UnityEngine.Object)null && LooksLikeLavaName(tex.name)) return true;
          }
        }
      }
      catch { }
      try
      {
        if (material.HasProperty("_BaseMap"))
        {
          Texture tex = material.GetTexture("_BaseMap");
          if ((UnityEngine.Object)tex != (UnityEngine.Object)null && LooksLikeLavaName(tex.name)) return true;
        }
      }
      catch { }
      try
      {
        if (material.HasProperty("_EmissionMap"))
        {
          Texture tex = material.GetTexture("_EmissionMap");
          if ((UnityEngine.Object)tex != (UnityEngine.Object)null && LooksLikeLavaName(tex.name)) return true;
        }
      }
      catch { }
      return false;
    }

    private static bool LooksLikeLavaName(string value)
    {
      if (string.IsNullOrEmpty(value)) return false;
      string n = value.ToLowerInvariant();
      return n.Contains("lava") || n.Contains("magma") || n.Contains("molten") ||
             n.Contains("infected") || n.Contains("infection") || n.Contains("tagged") ||
             n.Contains("lavasmall") || n.Contains("liquid_lava") || n.Contains("lavafloor") || n.Contains("lavaplane");
    }

    private void ScanUtilityLeafRenderers()
    {
      try
      {
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; ++i)
        {
          Renderer renderer = renderers[i];
          if ((UnityEngine.Object)renderer == (UnityEngine.Object)null || !renderer.gameObject.scene.IsValid() || !renderer.gameObject.activeInHierarchy) continue;
          if (!this.IsUtilityLeafRenderer(renderer)) continue;

          bool exists = false;
          for (int j = 0; j < this.utilityLeafRenderers.Count; ++j)
          {
            if ((UnityEngine.Object)this.utilityLeafRenderers[j].Renderer == (UnityEngine.Object)renderer) { exists = true; break; }
          }
          if (exists) continue;

          Material[] mats = renderer.sharedMaterials;
          Material source = null;
          if (mats != null)
          {
            for (int m = 0; m < mats.Length; ++m)
            {
              if ((UnityEngine.Object)mats[m] != (UnityEngine.Object)null && this.MaterialLooksLikeLeaves(mats[m]))
              {
                source = mats[m];
                break;
              }
            }
          }
          if ((UnityEngine.Object)source == (UnityEngine.Object)null && mats != null && mats.Length > 0) source = mats[0];
          if ((UnityEngine.Object)source == (UnityEngine.Object)null) continue;

          bool hasColor = source.HasProperty("_Color");
          bool hasBaseColor = source.HasProperty("_BaseColor");
          if (!hasColor && !hasBaseColor) continue;
          Color original = Color.white;
          try { original = hasBaseColor ? source.GetColor("_BaseColor") : source.GetColor("_Color"); } catch { }
          UtilityLeafRendererState state = new UtilityLeafRendererState
          {
            Renderer = renderer,
            OriginalColor = original,
            CurrentColor = original,
            HasColor = hasColor,
            HasBaseColor = hasBaseColor
          };
          try { renderer.GetPropertyBlock(state.OriginalBlock); } catch { }
          this.utilityLeafRenderers.Add(state);
        }
      }
      catch { }
    }

    private bool IsUtilityLeafRenderer(Renderer renderer)
    {
      string objectName = renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
      if (LooksLikeLeafName(objectName)) return true;
      try
      {
        Transform t = renderer.transform != null ? renderer.transform.parent : null;
        for (int depth = 0; depth < 3 && t != null; ++depth, t = t.parent)
          if (LooksLikeLeafName(t.name)) return true;
      }
      catch { }
      try
      {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if ((UnityEngine.Object)filter != (UnityEngine.Object)null && (UnityEngine.Object)filter.sharedMesh != (UnityEngine.Object)null && LooksLikeLeafName(filter.sharedMesh.name)) return true;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if ((UnityEngine.Object)skinned != (UnityEngine.Object)null && (UnityEngine.Object)skinned.sharedMesh != (UnityEngine.Object)null && LooksLikeLeafName(skinned.sharedMesh.name)) return true;
      }
      catch { }
      Material[] mats = renderer.sharedMaterials;
      if (mats != null)
        for (int i = 0; i < mats.Length; ++i)
          if ((UnityEngine.Object)mats[i] != (UnityEngine.Object)null && this.MaterialLooksLikeLeaves(mats[i])) return true;
      // V13.23: the Oct 2026 autumn canopy has no leaf names (texture-array slices)
      try { if (IsLeafSliceCanopy(renderer, true)) return true; } catch { }
      return false;
    }

    // V12.79: cached per material - the rescans no longer re-inspect every texture.
    private readonly Dictionary<Material, bool> utilityMaterialLooksLikeLeavesCache = new Dictionary<Material, bool>();

    private bool MaterialLooksLikeLeaves(Material material)
    {
      if ((UnityEngine.Object)material == (UnityEngine.Object)null) return false;
      bool cached;
      if (this.utilityMaterialLooksLikeLeavesCache.TryGetValue(material, out cached)) return cached;
      if (this.utilityMaterialLooksLikeLeavesCache.Count > 20000) this.utilityMaterialLooksLikeLeavesCache.Clear();
      bool result = this.MaterialLooksLikeLeavesUncached(material);
      this.utilityMaterialLooksLikeLeavesCache[material] = result;
      return result;
    }

    private bool MaterialLooksLikeLeavesUncached(Material material)
    {
      if ((UnityEngine.Object)material == (UnityEngine.Object)null) return false;
      if (LooksLikeLeafName(material.name)) return true;
      try
      {
        // V12.79: mainTexture logs an error (every call!) on shaders without _MainTex.
        Texture main = material.HasProperty("_MainTex") ? material.mainTexture : null;
        if ((UnityEngine.Object)main != (UnityEngine.Object)null && LooksLikeLeafName(main.name)) return true;
      }
      catch { }
      try
      {
        if (material.HasProperty("_BaseMap"))
        {
          Texture tex = material.GetTexture("_BaseMap");
          if ((UnityEngine.Object)tex != (UnityEngine.Object)null && LooksLikeLeafName(tex.name)) return true;
        }
      }
      catch { }
      return false;
    }

    private static bool LooksLikeLeafName(string value)
    {
      if (string.IsNullOrEmpty(value)) return false;
      string n = value.ToLowerInvariant();
      return n.Contains("leaf") || n.Contains("leaves");
    }

    private Rect BeginUtilityDesktopCard(string title, string status)
    {
      Rect row = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(68f), GUILayout.ExpandWidth(true));
      row.x += 2f;
      row.width -= 4f;
      this.DrawRoundedRect(row, 12, PanelInner, PurpleSoft, 1);

      GUIStyle titleStyle = new GUIStyle(this.roundedLabelStyle)
      {
        fontSize = 11,
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.UpperLeft
      };
      titleStyle.normal.textColor = Color.white;
      GUI.Label(new Rect(row.x + 12f, row.y + 9f, 210f, 20f), title, titleStyle);

      GUIStyle metaStyle = new GUIStyle(this.roundedLabelStyle)
      {
        fontSize = 8,
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.UpperLeft
      };
      metaStyle.normal.textColor = new Color(0.64f, 0.58f, 0.72f, 1f);
      GUI.Label(new Rect(row.x + 12f, row.y + 34f, 235f, 18f), status, metaStyle);
      GUILayout.Space(5f);
      return row;
    }

    private bool UtilityDesktopButton(Rect rect, string text, bool active = false)
    {
      GUIStyle style = active ? this.roundedButtonActiveStyle : this.roundedButtonStyle;
      return this.DesktopGUIButton(rect, text, style);
    }

    private void DrawUtilityDesktopAutoRefCard()
    {
      AutoRefManager manager = this.autoRefManager ?? AutoRefManager.Instance;
      string status = manager == null ? "ENGINE UNAVAILABLE" : ((manager.IsModEnabled ? "ON" : "OFF") + "  •  FIRST TO " + manager.CgtScoreToWin);
      Rect row = this.BeginUtilityDesktopCard("AUTO REF", status);
      bool enabled = manager != null && manager.IsModEnabled;
      if (this.UtilityDesktopButton(new Rect(row.xMax - 176f, row.y + 18f, 78f, 30f), enabled ? "ON" : "OFF", enabled) && manager != null)
        manager.ToggleEnabled();
      if (this.UtilityDesktopButton(new Rect(row.xMax - 90f, row.y + 18f, 78f, 30f), "FIRST +") && manager != null)
        manager.CycleScoreToWin();
    }

    private void DrawUtilityDesktopSelfRigCard()
    {
      string status = (this.selfRigLerpingEnabled ? "ON" : "OFF") + "  •  LERP " + this.selfRigSmoothness.ToString("0.00");
      Rect row = this.BeginUtilityDesktopCard("SELF RIG LERPING", status);
      if (this.UtilityDesktopButton(new Rect(row.xMax - 190f, row.y + 18f, 70f, 30f), this.selfRigLerpingEnabled ? "ON" : "OFF", this.selfRigLerpingEnabled))
      {
        this.selfRigLerpingEnabled = !this.selfRigLerpingEnabled;
        if (this.selfRigLerpingEnabled) this.EnsureSelfLerpedRig();
        PlayerPrefs.SetInt("UnknownsSelfRigLerping", this.selfRigLerpingEnabled ? 1 : 0);
        PlayerPrefs.Save();
      }
      if (this.UtilityDesktopButton(new Rect(row.xMax - 112f, row.y + 18f, 46f, 30f), "−"))
      {
        this.selfRigSmoothness = Mathf.Clamp(this.selfRigSmoothness - 0.25f, 0f, 6f);
        PlayerPrefs.SetFloat("UnknownsCameraTablet_SelfRigLerp", this.selfRigSmoothness);
        PlayerPrefs.Save();
      }
      if (this.UtilityDesktopButton(new Rect(row.xMax - 58f, row.y + 18f, 46f, 30f), "+"))
      {
        this.selfRigSmoothness = Mathf.Clamp(this.selfRigSmoothness + 0.25f, 0f, 6f);
        PlayerPrefs.SetFloat("UnknownsCameraTablet_SelfRigLerp", this.selfRigSmoothness);
        PlayerPrefs.Save();
      }
    }

    private void DrawUtilityDesktopTimeCard()
    {
      string status = (this.timeEnabled ? "ON" : "OFF") + "  •  " + this.GetUtilityTimeName();
      Rect row = this.BeginUtilityDesktopCard("TIME CHANGER", status);
      if (this.UtilityDesktopButton(new Rect(row.xMax - 190f, row.y + 18f, 70f, 30f), this.timeEnabled ? "ON" : "OFF", this.timeEnabled))
      {
        this.timeEnabled = !this.timeEnabled;
        if (this.timeEnabled) this.ApplyTimeOfDay();
      }
      if (this.UtilityDesktopButton(new Rect(row.xMax - 112f, row.y + 18f, 46f, 30f), "<"))
      {
        this.timeOfDay = (this.timeOfDay + 9) % 10;
        if (this.timeEnabled) this.ApplyTimeOfDay();
      }
      if (this.UtilityDesktopButton(new Rect(row.xMax - 58f, row.y + 18f, 46f, 30f), ">"))
      {
        this.timeOfDay = (this.timeOfDay + 1) % 10;
        if (this.timeEnabled) this.ApplyTimeOfDay();
      }
    }

    private void DrawUtilityDesktopPlayerManagerCard()
    {
      this.RefreshPlayerList();
      string playerName = "NO PLAYERS";
      string meta = "JOIN A ROOM TO MANAGE PLAYERS";
      if (this.playerRigs != null && this.playerRigs.Count > 0)
      {
        this.utilityAddonPlayerIndex = Mathf.Clamp(this.utilityAddonPlayerIndex, 0, this.playerRigs.Count - 1);
        VRRig rig = this.playerRigs[this.utilityAddonPlayerIndex];
        playerName = rig != null && !string.IsNullOrWhiteSpace(rig.playerNameVisible) ? rig.playerNameVisible : "PLAYER";
        string platform = "UNKNOWN";
        int ping = -1;
        try { platform = this.GetPlayerPlatform(rig); } catch { }
        try { ping = rig.GetPing(); } catch { }
        meta = (this.utilityAddonPlayerIndex + 1) + " / " + this.playerRigs.Count + "  •  " + platform + "  •  " + (ping >= 0 ? ping + "ms" : "--ms");
      }

      Rect row = this.BeginUtilityDesktopCard("PLAYER MANAGER  •  " + playerName, meta);
      bool havePlayers = this.playerRigs != null && this.playerRigs.Count > 0;
      if (this.UtilityDesktopButton(new Rect(row.xMax - 184f, row.y + 18f, 40f, 30f), "<") && havePlayers)
        this.CycleUtilityAddonPlayer(-1);
      if (this.UtilityDesktopButton(new Rect(row.xMax - 136f, row.y + 18f, 40f, 30f), ">") && havePlayers)
        this.CycleUtilityAddonPlayer(1);
      if (this.UtilityDesktopButton(new Rect(row.xMax - 88f, row.y + 18f, 76f, 30f), "SELECT") && havePlayers)
        this.SelectUtilityAddonPlayer();
    }

    private void BuildUtilityAddonPanel(Transform panel)
    {
      this.utilityRuntimeAddons = UtilityAddonCatalog.RuntimeEnabledAddons;
      int count = UtilityAddonCatalog.RuntimeEnabledUtilityPageCount;

      this.CreateTabletText(panel, "UTILITY ADD-ONS", 0f, 184f, 14f, FontStyles.Bold, TextAlignmentOptions.Center, 330f, 24f);
      this.utilityAddonHeaderStatus = this.CreateTabletText(panel,
        count + " UTILITY PAGES AVAILABLE",
        0f, 158f, 9.1f, FontStyles.Bold, TextAlignmentOptions.Center, 470f, 19f);
      this.utilityAddonHeaderStatus.color = new Color(0.78f, 0.68f, 0.93f, 0.96f);

      float hostHeight = Mathf.Clamp(UtilityAddonCatalog.SuggestedPanelHeight(count), 118f, 334f);
      // Keep the top edge fixed below the title/status while the bottom edge grows
      // downward as more add-ons are selected in Unknown's Suite.
      float hostCenterY = 125f - hostHeight * 0.5f;
      this.utilityAddonHost = this.CreateUtilitySectionCard(panel, "UtilityAddonHost", new Vector2(0f, hostCenterY), new Vector2(760f, hostHeight));

      if (!UtilityAddonCatalog.RuntimeEntitled)
      {
        TMP_Text locked = this.CreateTabletText(this.utilityAddonHost.transform,
          "Utility pages are included for every user in this release.",
          0f, 0f, 12f, FontStyles.Bold, TextAlignmentOptions.Center, 650f, 70f);
        locked.color = new Color(0.76f, 0.72f, 0.82f, 0.96f);
        return;
      }

      if (count == 0)
      {
        TMP_Text empty = this.CreateTabletText(this.utilityAddonHost.transform,
          "No add-ons selected. Open the gear in Unknown's Suite and choose what to load.",
          0f, 0f, 11f, FontStyles.Bold, TextAlignmentOptions.Center, 650f, 70f);
        empty.color = new Color(0.76f, 0.72f, 0.82f, 0.96f);
        return;
      }

      float y = hostHeight * 0.5f - 42f;
      if (this.HasUtilityAddon(UtilityAddonIds.AutoRef))
      {
        this.BuildAutoRefAddonCard(this.utilityAddonHost.transform, y);
        y -= 72f;
      }
      if (this.HasUtilityAddon(UtilityAddonIds.TimeChanger))
      {
        this.BuildTimeChangerAddonCard(this.utilityAddonHost.transform, y);
        y -= 72f;
      }
      if (this.HasUtilityAddon(UtilityAddonIds.SelfRigLerping))
      {
        this.BuildSelfRigAddonCard(this.utilityAddonHost.transform, y);
        y -= 72f;
      }
      if (this.HasUtilityAddon(UtilityAddonIds.LeavesColor))
      {
        this.BuildSimpleUtilityAddonCard(this.utilityAddonHost.transform, "EnvironmentColors", "ENVIRONMENT COLORS", "LEAVES • FOREST • LAVA", y, null, null);
        y -= 72f;
      }
      if (this.HasUtilityAddon(UtilityAddonIds.NameTags))
      {
        this.BuildSimpleUtilityAddonCard(this.utilityAddonHost.transform, "NameTags", "NAMETAGS", "LIVE PREVIEW + LAYOUT", y, () => NameTags.Enabled, () =>
        {
          NameTags.Enabled = !NameTags.Enabled;
          NameTags.UnRenderTags();
          if (NameTags.Enabled) NameTags.DisplayNameTags();
        });
        y -= 72f;
      }
      if (this.HasUtilityAddon(UtilityAddonIds.Tts))
      {
        this.BuildSimpleUtilityAddonCard(this.utilityAddonHost.transform, "TTS", "TTS", "LOCAL + LOBBY VOICE", y, null, null);
        y -= 72f;
      }
      if (this.HasUtilityAddon(UtilityAddonIds.Wasd))
      {
        this.BuildSimpleUtilityAddonCard(this.utilityAddonHost.transform, "WASD", "WASD", "DESKTOP MOVEMENT", y, () => this.wasdEnabled, () =>
        {
          this.wasdEnabled = !this.wasdEnabled;
          if (!this.wasdEnabled) { this.wasdVelocity = Vector3.zero; this.wasdJumpVelocity = Vector3.zero; }
          PlayerPrefs.SetInt("UnknownsUtility_WASDEnabled2", this.wasdEnabled ? 1 : 0);
          PlayerPrefs.Save();
        });
        y -= 72f;
      }
      if (this.HasUtilityAddon(UtilityAddonIds.Room))
      {
        this.BuildSimpleUtilityAddonCard(this.utilityAddonHost.transform, "RoomJoiner", "ROOM JOINER", "ROOM CODE • JOIN • LEAVE", y, null, null);
      }
    }

    private void BuildSimpleUtilityAddonCard(Transform parent, string key, string title, string subtitle, float y, Func<bool> active, Action toggle)
    {
      GameObject row = this.CreateAddonRow(parent, key, y);
      this.CreateTabletText(row.transform, title, -258f, 11f, 10.5f, FontStyles.Bold, TextAlignmentOptions.Left, 260f, 20f);
      TMP_Text status = this.CreateTabletText(row.transform, subtitle, -258f, -12f, 8.4f, FontStyles.Bold, TextAlignmentOptions.Left, 300f, 18f);
      status.color = new Color(0.70f, 0.63f, 0.82f, 1f);
      if (active != null && toggle != null)
        this.CreateUtilityPill(row.transform, "TOGGLE", new Vector2(220f, 0f), new Vector2(132f, 32f), active, toggle, 9f);
      else
      {
        TMP_Text ready = this.CreateTabletText(row.transform, "READY", 238f, 0f, 9f, FontStyles.Bold, TextAlignmentOptions.Center, 110f, 24f);
        ready.color = new Color(0.72f, 0.90f, 0.76f, 1f);
      }
    }

    private GameObject CreateAddonRow(Transform parent, string name, float y)
    {
      GameObject row = this.CreateUtilitySectionCard(parent, "UtilityAddon_" + name, new Vector2(0f, y), new Vector2(714f, 68f));
      Image image = row.GetComponent<Image>();
      if (image != null) image.color = new Color(0.067f, 0.062f, 0.078f, 0.995f);
      return row;
    }

    private void BuildAutoRefAddonCard(Transform parent, float y)
    {
      GameObject row = this.CreateAddonRow(parent, "AutoRef", y);
      this.CreateTabletText(row.transform, "AUTO REF", -258f, 11f, 10.8f, FontStyles.Bold, TextAlignmentOptions.Left, 150f, 20f);
      this.utilityAddonAutoRefStatus = this.CreateTabletText(row.transform, "OFF", -258f, -12f, 8.8f, FontStyles.Bold, TextAlignmentOptions.Left, 180f, 18f);
      this.utilityAddonAutoRefStatus.color = new Color(0.70f, 0.63f, 0.82f, 1f);
      this.CreateUtilityPill(row.transform, "TOGGLE", new Vector2(38f, 0f), new Vector2(126f, 32f),
        () => (this.autoRefManager ?? AutoRefManager.Instance) != null && (this.autoRefManager ?? AutoRefManager.Instance).IsModEnabled,
        () =>
        {
          AutoRefManager m = this.autoRefManager ?? AutoRefManager.Instance;
          if (m != null) m.ToggleEnabled();
        }, 9f);
      this.CreateUtilityPill(row.transform, "FIRST TO +", new Vector2(198f, 0f), new Vector2(148f, 32f), () => false,
        () => { AutoRefManager m = this.autoRefManager ?? AutoRefManager.Instance; if (m != null) m.CycleScoreToWin(); }, 8.8f);
    }

    private void BuildSelfRigAddonCard(Transform parent, float y)
    {
      GameObject row = this.CreateAddonRow(parent, "SelfRig", y);
      this.CreateTabletText(row.transform, "SELF RIG LERPING", -258f, 11f, 10.3f, FontStyles.Bold, TextAlignmentOptions.Left, 205f, 20f);
      this.utilityAddonSelfRigStatus = this.CreateTabletText(row.transform, "OFF", -258f, -12f, 8.8f, FontStyles.Bold, TextAlignmentOptions.Left, 190f, 18f);
      this.utilityAddonSelfRigStatus.color = new Color(0.70f, 0.63f, 0.82f, 1f);
      this.CreateUtilityPill(row.transform, "TOGGLE", new Vector2(4f, 0f), new Vector2(112f, 32f), () => this.selfRigLerpingEnabled,
        () =>
        {
          this.selfRigLerpingEnabled = !this.selfRigLerpingEnabled;
          if (this.selfRigLerpingEnabled) this.EnsureSelfLerpedRig();
          PlayerPrefs.SetInt("UnknownsSelfRigLerping", this.selfRigLerpingEnabled ? 1 : 0);
        }, 9f);
      this.CreateUtilityPill(row.transform, "LERP -", new Vector2(132f, 0f), new Vector2(104f, 32f), () => false,
        () =>
        {
          this.selfRigSmoothness = Mathf.Clamp(this.selfRigSmoothness - 0.25f, 0f, 6f);
          PlayerPrefs.SetFloat("UnknownsCameraTablet_SelfRigLerp", this.selfRigSmoothness);
        }, 8.8f);
      this.CreateUtilityPill(row.transform, "LERP +", new Vector2(250f, 0f), new Vector2(104f, 32f), () => false,
        () =>
        {
          this.selfRigSmoothness = Mathf.Clamp(this.selfRigSmoothness + 0.25f, 0f, 6f);
          PlayerPrefs.SetFloat("UnknownsCameraTablet_SelfRigLerp", this.selfRigSmoothness);
        }, 8.8f);
    }

    private void BuildTimeChangerAddonCard(Transform parent, float y)
    {
      GameObject row = this.CreateAddonRow(parent, "TimeChanger", y);
      this.CreateTabletText(row.transform, "TIME CHANGER", -258f, 11f, 10.5f, FontStyles.Bold, TextAlignmentOptions.Left, 180f, 20f);
      this.utilityAddonTimeStatus = this.CreateTabletText(row.transform, "TIME", -258f, -12f, 8.8f, FontStyles.Bold, TextAlignmentOptions.Left, 190f, 18f);
      this.utilityAddonTimeStatus.color = new Color(0.70f, 0.63f, 0.82f, 1f);
      this.CreateUtilityPill(row.transform, "TOGGLE", new Vector2(4f, 0f), new Vector2(112f, 32f), () => this.timeEnabled,
        () => { this.timeEnabled = !this.timeEnabled; if (this.timeEnabled) this.ApplyTimeOfDay(); }, 9f);
      this.CreateUtilityPill(row.transform, "TIME <", new Vector2(132f, 0f), new Vector2(104f, 32f), () => false,
        () => { this.timeOfDay = (this.timeOfDay + 9) % 10; if (this.timeEnabled) this.ApplyTimeOfDay(); }, 8.8f);
      this.CreateUtilityPill(row.transform, "TIME >", new Vector2(250f, 0f), new Vector2(104f, 32f), () => false,
        () => { this.timeOfDay = (this.timeOfDay + 1) % 10; if (this.timeEnabled) this.ApplyTimeOfDay(); }, 8.8f);
    }

    private void BuildPlayerManagerAddonCard(Transform parent, float y)
    {
      GameObject row = this.CreateAddonRow(parent, "PlayerManager", y);
      Image rowImage = row.GetComponent<Image>();
      if (rowImage != null) rowImage.color = new Color(0.052f, 0.049f, 0.061f, 0.995f);

      TMP_Text eyebrow = this.CreateTabletText(row.transform, "PLAYER MANAGER", -258f, 19f, 7.8f, FontStyles.Bold, TextAlignmentOptions.Left, 225f, 15f);
      eyebrow.color = new Color(0.58f, 0.50f, 0.68f, 0.95f);
      this.utilityAddonPlayerStatus = this.CreateTabletText(row.transform, "NO PLAYER", -258f, -2f, 12.2f, FontStyles.Bold, TextAlignmentOptions.Left, 255f, 22f);
      this.utilityAddonPlayerStatus.color = new Color(0.96f, 0.94f, 0.99f, 1f);
      this.utilityAddonPlayerMetaStatus = this.CreateTabletText(row.transform, "NO PLAYERS IN ROOM", -258f, -23f, 7.8f, FontStyles.Normal, TextAlignmentOptions.Left, 285f, 16f);
      this.utilityAddonPlayerMetaStatus.color = new Color(0.63f, 0.59f, 0.70f, 0.98f);

      UtilityPill previous = this.CreateUtilityPill(row.transform, "<", new Vector2(57f, 0f), new Vector2(42f, 32f), () => false, () => this.CycleUtilityAddonPlayer(-1), 12f);
      UtilityPill next = this.CreateUtilityPill(row.transform, ">", new Vector2(110f, 0f), new Vector2(42f, 32f), () => false, () => this.CycleUtilityAddonPlayer(1), 12f);
      UtilityPill watch = this.CreateUtilityPill(row.transform, ProductEdition.IsUtility ? "SELECT" : "WATCH", new Vector2(245f, 0f), new Vector2(150f, 32f), () => false,
        ProductEdition.IsUtility ? (Action)this.SelectUtilityAddonPlayer : (Action)this.SpectateUtilityAddonPlayer, 9f);
      if (previous.image != null) previous.image.color = new Color(0.072f, 0.068f, 0.084f, 1f);
      if (next.image != null) next.image.color = new Color(0.072f, 0.068f, 0.084f, 1f);
      if (watch.image != null) watch.image.color = new Color(0.24f, 0.105f, 0.39f, 1f);
      if (watch.outline != null) watch.outline.effectColor = new Color(0.55f, 0.30f, 0.76f, 0.62f);
    }

    private void CycleUtilityAddonPlayer(int delta)
    {
      this.RefreshPlayerList();
      if (this.playerRigs == null || this.playerRigs.Count == 0)
      {
        this.utilityAddonPlayerIndex = 0;
        return;
      }
      this.utilityAddonPlayerIndex = (this.utilityAddonPlayerIndex + delta) % this.playerRigs.Count;
      if (this.utilityAddonPlayerIndex < 0) this.utilityAddonPlayerIndex += this.playerRigs.Count;
      this.UpdateUtilityAddonPanelLabels();
    }


    private void SelectUtilityAddonPlayer()
    {
      this.RefreshPlayerList();
      if (this.playerRigs == null || this.playerRigs.Count == 0) return;
      this.utilityAddonPlayerIndex = Mathf.Clamp(this.utilityAddonPlayerIndex, 0, this.playerRigs.Count - 1);
      VRRig rig = this.playerRigs[this.utilityAddonPlayerIndex];
      string name = rig != null && !string.IsNullOrWhiteSpace(rig.playerNameVisible) ? rig.playerNameVisible : "PLAYER";
      string platform = "UNKNOWN";
      try { platform = this.GetPlayerPlatform(rig); } catch { }
      this.ShowToast("Player Manager", name + " • " + platform);
    }

    private void SpectateUtilityAddonPlayer()
    {
      this.RefreshPlayerList();
      if (this.playerRigs == null || this.playerRigs.Count == 0) return;
      this.utilityAddonPlayerIndex = Mathf.Clamp(this.utilityAddonPlayerIndex, 0, this.playerRigs.Count - 1);
      if ((Object)this.spectatorCam == (Object)null) this.SpawnSpectatorCamera();
      this.SetSpectateTarget(this.utilityAddonPlayerIndex);
    }

    private void UpdateUtilityAddonPanelLabels()
    {
      if (!UtilityAddonCatalog.RuntimeEntitled) return;
      AutoRefManager m = this.autoRefManager ?? AutoRefManager.Instance;
      if (this.utilityAddonAutoRefStatus != null)
        this.utilityAddonAutoRefStatus.text = m == null ? "ENGINE UNAVAILABLE" : ((m.IsModEnabled ? "ON" : "OFF") + "  •  FIRST TO " + m.CgtScoreToWin);
      if (this.utilityAddonSelfRigStatus != null)
        this.utilityAddonSelfRigStatus.text = (this.selfRigLerpingEnabled ? "ON" : "OFF") + "  •  LERP " + this.selfRigSmoothness.ToString("0.00");
      if (this.utilityAddonTimeStatus != null)
        this.utilityAddonTimeStatus.text = (this.timeEnabled ? "ON" : "OFF") + "  •  SLOT " + Mathf.Clamp(this.timeOfDay, 0, 9);
      if (this.utilityAddonPlayerStatus != null)
      {
        this.RefreshPlayerList();
        if (this.playerRigs != null && this.playerRigs.Count > 0)
        {
          this.utilityAddonPlayerIndex = Mathf.Clamp(this.utilityAddonPlayerIndex, 0, this.playerRigs.Count - 1);
          VRRig rig = this.playerRigs[this.utilityAddonPlayerIndex];
          string name = rig != null && !string.IsNullOrWhiteSpace(rig.playerNameVisible) ? rig.playerNameVisible : "PLAYER";
          this.utilityAddonPlayerStatus.text = name;
          if (this.utilityAddonPlayerMetaStatus != null)
          {
            string platform = "UNKNOWN";
            string fps = "--";
            int ping = -1;
            try { platform = this.GetPlayerPlatform(rig); } catch { }
            try { fps = this.GetPlayerFPS(rig); } catch { }
            try { ping = rig.GetPing(); } catch { }
            string fpsLabel = fps.EndsWith("Hz", StringComparison.OrdinalIgnoreCase) ? fps.Substring(0, fps.Length - 2) + " FPS" : fps;
            this.utilityAddonPlayerMetaStatus.text =
              (this.utilityAddonPlayerIndex + 1) + " / " + this.playerRigs.Count +
              "   •   " + platform +
              "   •   " + fpsLabel +
              "   •   " + (ping >= 0 ? ping.ToString() + "ms" : "--ms");
          }
        }
        else
        {
          this.utilityAddonPlayerStatus.text = "NO PLAYERS";
          if (this.utilityAddonPlayerMetaStatus != null) this.utilityAddonPlayerMetaStatus.text = "JOIN A ROOM TO MANAGE PLAYERS";
        }
      }
    }
  }
}

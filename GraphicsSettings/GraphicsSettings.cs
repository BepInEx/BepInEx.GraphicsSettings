using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

[assembly: System.Reflection.AssemblyFileVersion(GraphicsSettings.GraphicsSettings.Version)]

namespace GraphicsSettings
{
    [BepInPlugin(GUID, "Graphics Settings", Version)]
    public class GraphicsSettings : BaseUnityPlugin
    {
        public const string GUID = "keelhauled.graphicssettings";
        public const string Version = "1.3.1";

        private const string CATEGORY_GENERAL = "General";
        private const string CATEGORY_RENDER = "Rendering";

        private const string DESCRIPTION_RESOLUTION = "Desired resolution. After clicking apply, resolution changes are usually automatically remembered by the game. If not, use this value to save the desired resolution. e.g. 1920x1080";
        private const string DESCRIPTION_ANISOFILTER = "Improves distant textures when they are being viewer from indirect angles.";
        private const string DESCRIPTION_VSYNC = "VSync synchronizes the output video of the graphics card to the refresh rate of the monitor. " +
                                                 "This prevents tearing and produces a smoother video output.\n" +
                                                 "Half vsync synchronizes the output to half the refresh rate of your monitor.";
        private const string DESCRIPTION_FRAMERATELIMIT = "Limits your framerate to whatever value is set. -1 equals unlocked framerate.\n" +
                                                          "VSync has to be disabled for this setting to take effect.";
        private const string DESCRIPTION_ANTIALIASING = "Smooths out jagged edges on objects.";
        private const string DESCRIPTION_RUNINBACKGROUND = "Should the game be running when it is in the background (when the window is not focused)?\n";
        private const string DESCRIPTION_OPTIMIZEINBACKGROUND = "Optimize the game when it is the background and unfocused. " +
                                                                "Settings such as anti-aliasing will be turned off or reduced in this state.";

        private ConfigEntry<string> Resolution { get; set; }
        private ConfigEntry<SettingEnum.DisplayMode> DisplayMode { get; set; }
        private ConfigEntry<int> SelectedMonitor { get; set; }
        private ConfigEntry<SettingEnum.VSyncType> VSync { get; set; }
        private ConfigEntry<int> FramerateLimit { get; set; }
        private ConfigEntry<SettingEnum.AntiAliasingMode> AntiAliasing { get; set; }
        private ConfigEntry<SettingEnum.AnisotropicFilteringMode> AnisotropicFiltering { get; set; }
        private ConfigEntry<SettingEnum.RunInBackgroundMode> RunInBackground { get; set; }
        private ConfigEntry<bool> OptimizeInBackground { get; set; }

        private string resolutionX = Screen.width.ToString();
        private string resolutionY = Screen.height.ToString();
        private bool framerateToggle = false;
        private WinAPI.WindowStyleFlags backupStandard;
        private WinAPI.WindowStyleFlags backupExtended;
        private bool backupDone = false;
        private UnityEngine.Object configManager;
        private bool configManagerSearch = false;

        private void Start()
        {
            if(DisplayMode.Value == SettingEnum.DisplayMode.BorderlessFullscreen)
                StartCoroutine(RemoveBorder());

            // If the resolution was actually set in the config, go ahead and apply it
            string[] dimensions = Resolution.Value.ToLower().Split('x', ',', ' ', '\t');
            int x, y;
            if (dimensions.Length == 2 &&
                int.TryParse(dimensions[0], out x) &&
                int.TryParse(dimensions[1], out y))
            {

                StartCoroutine(SetResolution(x, y));
            }
            else
            {
                // ignore invalid value; reset to default
                Resolution.BoxedValue = "";
            }
        }

        private void Awake()
        {
            Resolution = Config.Bind(CATEGORY_RENDER, "Resolution", "", new ConfigDescription(DESCRIPTION_RESOLUTION, null, new ConfigurationManagerAttributes { Order = 9, HideDefaultButton = true, CustomDrawer = new Action<ConfigEntryBase>(ResolutionDrawer) }));
            DisplayMode = Config.Bind(CATEGORY_RENDER, "Display mode", SettingEnum.DisplayMode.Default, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 10 }));
            SelectedMonitor = Config.Bind(CATEGORY_RENDER, "Selected monitor", 0, new ConfigDescription("", new AcceptableValueList<int>(Enumerable.Range(0, Display.displays.Length).ToArray()), new ConfigurationManagerAttributes { Order = 8 }));
            VSync = Config.Bind(CATEGORY_RENDER, "VSync", SettingEnum.VSyncType.Default, new ConfigDescription(DESCRIPTION_VSYNC, null, new ConfigurationManagerAttributes { Order = 7 }));
            FramerateLimit = Config.Bind(CATEGORY_RENDER, "Framerate limit", Application.targetFrameRate, new ConfigDescription(DESCRIPTION_FRAMERATELIMIT, null, new ConfigurationManagerAttributes { Order = 6, HideDefaultButton = true, CustomDrawer = new Action<ConfigEntryBase>(FramerateLimitDrawer) }));
            AntiAliasing = Config.Bind(CATEGORY_RENDER, "Anti-aliasing multiplier", SettingEnum.AntiAliasingMode.Default, new ConfigDescription(DESCRIPTION_ANTIALIASING));
            AnisotropicFiltering = Config.Bind(CATEGORY_RENDER, "Anisotropic filtering", SettingEnum.AnisotropicFilteringMode.Default, new ConfigDescription(DESCRIPTION_ANISOFILTER));
            RunInBackground = Config.Bind(CATEGORY_GENERAL, "Run in background", SettingEnum.RunInBackgroundMode.Default, new ConfigDescription(DESCRIPTION_RUNINBACKGROUND));
            OptimizeInBackground = Config.Bind(CATEGORY_GENERAL, "Optimize in background", true, new ConfigDescription(DESCRIPTION_OPTIMIZEINBACKGROUND));

            DisplayMode.SettingChanged += (sender, args) => SetDisplayMode();
            SelectedMonitor.SettingChanged += (sender, args) => StartCoroutine(SelectMonitor());

            InitSetting(FramerateLimit, SetFramerateLimit);
            InitSetting(VSync, () =>
            {
                if(VSync.Value != SettingEnum.VSyncType.Default)
                    QualitySettings.vSyncCount = (int)VSync.Value;
            });
            InitSetting(AntiAliasing, () =>
            {
                if(AntiAliasing.Value != SettingEnum.AntiAliasingMode.Default)
                    QualitySettings.antiAliasing = (int)AntiAliasing.Value;
            });
            InitSetting(AnisotropicFiltering, () =>
            {
                if(AnisotropicFiltering.Value != SettingEnum.AnisotropicFilteringMode.Default)
                    QualitySettings.anisotropicFiltering = (AnisotropicFiltering)AnisotropicFiltering.Value;
            });
            InitSetting(RunInBackground, () =>
            {
                if(RunInBackground.Value != SettingEnum.RunInBackgroundMode.Default)
                    Application.runInBackground = RunInBackground.Value == SettingEnum.RunInBackgroundMode.Yes;
            });
        }

        private int lastAntiAliasingValue = -1;
        private void OnApplicationFocus(bool hasFocus)
        {
            if(OptimizeInBackground.Value)
            {
                if (lastAntiAliasingValue < 0 || !hasFocus)
                    lastAntiAliasingValue = QualitySettings.antiAliasing;

                if (!hasFocus)
                    QualitySettings.antiAliasing = 0;
                else if (AntiAliasing.Value != SettingEnum.AntiAliasingMode.Default)
                    QualitySettings.antiAliasing = (int)AntiAliasing.Value;
                else
                    QualitySettings.antiAliasing = lastAntiAliasingValue;
            }
        }

        private void ResolutionDrawer(ConfigEntryBase configEntry)
        {
            string resX = GUILayout.TextField(resolutionX, GUILayout.Width(80));
            string resY = GUILayout.TextField(resolutionY, GUILayout.Width(80));

            if(resX != resolutionX && int.TryParse(resX, out _)) resolutionX = resX;
            if(resY != resolutionY && int.TryParse(resY, out _)) resolutionY = resY;

            if(GUILayout.Button("Apply", GUILayout.ExpandWidth(true)))
            {
                int x = int.Parse(resolutionX);
                int y = int.Parse(resolutionY);

                if(Screen.width != x || Screen.height != y)
                    StartCoroutine(SetResolution(x, y));
            }

            if (GUILayout.Toggle(Resolution.Value.Length > 0, new GUIContent("Save", "Ensure resolution is saved for future launches")))
            {
                Resolution.BoxedValue = $"{resolutionX}x{resolutionY}";
            }
            else
            {
                Resolution.BoxedValue = "";
            }

            GUILayout.Space(5);
            if(GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
            {
                var display = Display.displays[SelectedMonitor.Value];
                if (Screen.width != display.systemWidth || Screen.height != display.systemHeight)
                    StartCoroutine(SetResolution(display.systemWidth, display.systemHeight));
            }
        }

        IEnumerator SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, Screen.fullScreen);
            yield return null;

            UpdateConfigManagerSize();
            resolutionX = Screen.width.ToString();
            resolutionY = Screen.height.ToString();

            if(DisplayMode.Value == SettingEnum.DisplayMode.BorderlessFullscreen)
                StartCoroutine(RemoveBorder());
        }

        private void FramerateLimitDrawer(ConfigEntryBase configEntry)
        {
            var toggle = GUILayout.Toggle(framerateToggle, "Enabled", GUILayout.Width(70));
            if(toggle != framerateToggle)
            {
                framerateToggle = toggle;
                if(toggle)
                {
                    var refreshRate = Screen.currentResolution.refreshRate;
                    FramerateLimit.Value = refreshRate;
                    Application.targetFrameRate = refreshRate;
                }
                else
                {
                    FramerateLimit.Value = -1;
                    Application.targetFrameRate = -1;
                }
            }

            var slider = (int)GUILayout.HorizontalSlider(FramerateLimit.Value, 30, 200, GUILayout.ExpandWidth(true));
            if(slider != FramerateLimit.Value && framerateToggle)
            {
                FramerateLimit.Value = Application.targetFrameRate = slider;
                if(!framerateToggle)
                    framerateToggle = true;
            }

            GUILayout.Space(5);
            GUILayout.TextField(FramerateLimit.Value.ToString(), GUILayout.Width(40));
        }

        private void SetDisplayMode()
        {
            switch(DisplayMode.Value)
            {
                case SettingEnum.DisplayMode.Windowed:
                    MakeWindowed();
                    break;
                case SettingEnum.DisplayMode.Fullscreen:
                    MakeFullscreen();
                    break;
                case SettingEnum.DisplayMode.BorderlessFullscreen:
                    StartCoroutine(RemoveBorder());
                    break;
                case SettingEnum.DisplayMode.Default:
                    break;
            }
        }

        private void SetFramerateLimit()
        {
            Application.targetFrameRate = FramerateLimit.Value;
            framerateToggle = FramerateLimit.Value > 0;
        }

        private IEnumerator SelectMonitor()
        {
            // Set the target display and a low resolution.
            PlayerPrefs.SetInt("UnitySelectMonitor", SelectedMonitor.Value);
            Screen.SetResolution(800, 600, Screen.fullScreen);
            yield return null;

            // Restore resolution
            var targetDisplay = Display.displays[SelectedMonitor.Value];
            Screen.SetResolution(targetDisplay.renderingWidth, targetDisplay.renderingHeight, Screen.fullScreen);
            yield return null;

            UpdateConfigManagerSize();
            resolutionX = Screen.width.ToString();
            resolutionY = Screen.height.ToString();

            if(DisplayMode.Value == SettingEnum.DisplayMode.BorderlessFullscreen)
                StartCoroutine(RemoveBorder());
        }

        private IEnumerator RemoveBorder()
        {
            if(Screen.fullScreen)
            {
                Screen.SetResolution(Screen.width, Screen.height, false);
                yield return null;
            }

            var hwnd = WinAPI.GetActiveWindow();

            if(!backupDone)
            {
                backupStandard = WinAPI.GetWindowLongPtr(hwnd, WinAPI.WindowLongIndex.Style);
                backupExtended = WinAPI.GetWindowLongPtr(hwnd, WinAPI.WindowLongIndex.ExtendedStyle);
                backupDone = true;
            }

            var newStandard = backupStandard
                              & ~(WinAPI.WindowStyleFlags.Caption
                                 | WinAPI.WindowStyleFlags.ThickFrame
                                 | WinAPI.WindowStyleFlags.SystemMenu
                                 | WinAPI.WindowStyleFlags.MaximizeBox // same as TabStop
                                 | WinAPI.WindowStyleFlags.MinimizeBox // same as Group
                              );

            var newExtended = backupExtended
                              & ~(WinAPI.WindowStyleFlags.ExtendedDlgModalFrame
                                 | WinAPI.WindowStyleFlags.ExtendedComposited
                                 | WinAPI.WindowStyleFlags.ExtendedWindowEdge
                                 | WinAPI.WindowStyleFlags.ExtendedClientEdge
                                 | WinAPI.WindowStyleFlags.ExtendedLayered
                                 | WinAPI.WindowStyleFlags.ExtendedStaticEdge
                                 | WinAPI.WindowStyleFlags.ExtendedToolWindow
                                 | WinAPI.WindowStyleFlags.ExtendedAppWindow
                              );

            int width = Screen.width, height = Screen.height;
            WinAPI.SetWindowLongPtr(hwnd, WinAPI.WindowLongIndex.Style, newStandard);
            WinAPI.SetWindowLongPtr(hwnd, WinAPI.WindowLongIndex.ExtendedStyle, newExtended);
            WinAPI.SetWindowPos(hwnd, 0, 0, 0, width, height, WinAPI.SetWindowPosFlags.NoMove);
        }

        private void MakeWindowed()
        {
            RestoreBorder();
            Screen.SetResolution(Screen.width, Screen.height, false);
        }

        private void MakeFullscreen()
        {
            RestoreBorder();
            Screen.SetResolution(Screen.width, Screen.height, true);
        }

        private void InitSetting<T>(ConfigEntry<T> configEntry, Action setter)
        {
            setter();
            configEntry.SettingChanged += (sender, args) => setter();
        }

        private void RestoreBorder()
        {
            if(backupDone && DisplayMode.Value != SettingEnum.DisplayMode.BorderlessFullscreen)
            {
                var hwnd = WinAPI.GetActiveWindow();
                WinAPI.SetWindowLongPtr(hwnd, WinAPI.WindowLongIndex.Style, backupStandard);
                WinAPI.SetWindowLongPtr(hwnd, WinAPI.WindowLongIndex.ExtendedStyle, backupExtended);
            }
        }

        private void UpdateConfigManagerSize()
        {
            if(!configManagerSearch && !configManager)
            {
                configManagerSearch = true;
                var type = Type.GetType("ConfigurationManager.ConfigurationManager, ConfigurationManager", false);
                if(type != null)
                    configManager = FindObjectOfType(type);
            }

            if(configManager)
                Traverse.Create(configManager).Method("CalculateWindowRect").GetValue();
        }
    }
}

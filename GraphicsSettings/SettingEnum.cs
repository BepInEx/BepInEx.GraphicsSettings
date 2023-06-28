using System.ComponentModel;
using UnityEngine;

namespace GraphicsSettings
{
    internal class SettingEnum
    {
        public enum VSyncType
        {
            Default = -1,
            Disabled = 0,
            Enabled = 1,
            Half = 2
        }

        public enum DisplayMode
        {
            Default,
            Fullscreen,
            [Description("Borderless fullscreen")]
            BorderlessFullscreen,
            Windowed
        }

        public enum AntiAliasingMode
        {
            Default = -1,
            Disabled = 0,
            [Description("2x MSAA")]
            X2 = 2,
            [Description("4x MSAA")]
            X4 = 4,
            [Description("8x MSAA")]
            X8 = 8
        }
        
        public enum AnisotropicFilteringMode
        {
            Default = -1,
            Disable = AnisotropicFiltering.Disable,
            Enable = AnisotropicFiltering.Enable,
            ForceEnable = AnisotropicFiltering.ForceEnable,
        }
        
        public enum RunInBackgroundMode
        {
            Default = -1,
            No = 0,
            Yes = 1,
        }
    }
}

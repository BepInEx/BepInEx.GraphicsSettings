using System.ComponentModel;

namespace GraphicsSettings
{
    internal class SettingEnum
    {
        public enum VSyncType
        {
            Disabled,
            Enabled,
            Half
        }

        public enum DisplayMode
        {
            Fullscreen,
            [Description("Borderless fullscreen")]
            BorderlessFullscreen,
            Windowed
        }
    }
}

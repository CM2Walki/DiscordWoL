using System;

namespace DiscordWoL
{
    [Serializable]
    public class TargetDevice
    {
        public string DeviceName { get; set; } // PC name to show in the device's discord message
        public string MacAddress { get; set; } // MAC/physical address of the device
        public string IpAddress { get; set; } // IP address of this device, consider using a static one (must be in the same broadcast domain as the machine running this bot!)
        public string Emoji { get; set; } // Emoji to show in the device's message
        public int StartupTimeoutMs { get; set; } // You can provide a custom startup timeout if a device takes a lot longer than the others
        public TargetDeviceState DeviceState { get; set; } // Machine state to prevent spamming Discord

        public TargetDevice()
        {
            DeviceName = string.Empty;
            MacAddress = string.Empty;
            IpAddress = string.Empty;
            Emoji = string.Empty;
            DeviceState = TargetDeviceState.Unknown;
            StartupTimeoutMs = BreeBot.DefaultStartupTimeoutMs;
        }

        public TargetDevice(string deviceName, string macAddress, string ipAddress, string emoji, int startupTimeoutMs)
        {
            DeviceName = deviceName;
            MacAddress = macAddress;
            IpAddress = ipAddress;
            Emoji = emoji;
            DeviceState = TargetDeviceState.Unknown;
            StartupTimeoutMs = startupTimeoutMs;
        }
    }
}

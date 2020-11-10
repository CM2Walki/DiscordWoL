namespace DiscordWoL
{
    /// <summary>
    /// Unknown - Before initial setup
    /// Offline - Machine is not responding to pings ("Ready to receive WoL")
    /// Starting - WoL packet was sent to the machine
    /// Pingable - Machine is responding to pings ("Is running")
    /// </summary>
    public enum TargetDeviceState
    {
        Unknown,
        Offline,
        Starting,
        Pingable
    }
}

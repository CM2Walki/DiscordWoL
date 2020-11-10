using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DiscordWoL
{
    public class ConfigFile
    {
        public string DiscordToken;
        public ulong DiscordServerId;
        public ulong DiscordChannelId;
        public int StatusCheckIntervalMs;
        public List<TargetDevice> TargetDevices;
    }

    public static class ConfigHelper
    {
        private const string ConfigFilePath = "cfg/";
        private const string ConfigFileName = "config.json";

        public static ConfigFile LoadConfigFile()
        {
            ConfigFile configFile = null;
            var time = DateTime.Now;

            try
            {
                var cfgDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFilePath);
                Directory.CreateDirectory(cfgDirectory);

                var json = File.ReadAllText(Path.Combine(ConfigFilePath, ConfigFileName));
                configFile = JsonConvert.DeserializeObject<ConfigFile>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] Error reading config file | {0}", ex.Message);
                Environment.Exit(1);
            }

            if (configFile == null)
            {
                Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] Unable to parse JSON file. Check JSON syntax...");
                Environment.Exit(1);
            }

            if (configFile.TargetDevices.Count == 0)
            {
                Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] No Target Devices provided. Nothing to do here. Exiting...");
                Environment.Exit(1);
            }

            // Sanitize user input, prevents deadlocks
            configFile.StatusCheckIntervalMs = Math.Max(0, configFile.StatusCheckIntervalMs);

            return configFile;
        }
    }
}

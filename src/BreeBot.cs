using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

// dotnet publish -c Release -r win-x64 --self-contained=false
// dotnet publish -c Release -r linux-x64 --self-contained=false
// dotnet publish -c Release -r linux-arm --self-contained=false 
// dotnet publish -c Release -r osx-x64 --self-contained=false

namespace DiscordWoL
{
    // Keepin' it together, Bree?
    internal class BreeBot
    {
        // Default of how long to wait for a device to start up, before checking it's state again
        public const int DefaultStartupTimeoutMs = 60000;

        // If you change these, you might need to adjust their positioning using spaces in the code where they are used!
        private const string SendWakeOnLan = "🔌";
        private readonly IEmote _emoteSendWakeOnLan = new Emoji(SendWakeOnLan);

        private const string WaitingForTarget = "⏳";
        private readonly IEmote _emoteWaitingForTarget = new Emoji(WaitingForTarget);

        private const string TargetIsRunning = "🏃";
        private readonly IEmote _emoteTargetIsRunning = new Emoji(TargetIsRunning);

        // Stores the message associated with a target device
        private readonly ConcurrentDictionary<RestUserMessage, TargetDevice> _targetDeviceLinkedCache = new ConcurrentDictionary<RestUserMessage, TargetDevice>();

        private DiscordSocketClient _client; // The client connected to Discord
        private SocketGuild _guild; // "Discord Server"
        private SocketTextChannel _channel; // "Channel in the Discord Server"
        private ConfigFile _configFile; // Discord Token, Channel/Server IDs & Target devices are in here

        public static void Main() => new BreeBot().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var botConfig = new DiscordSocketConfig
            {
                ExclusiveBulkDelete = true
            };

            _client = new DiscordSocketClient(botConfig);
            _configFile = ConfigHelper.LoadConfigFile();

            _client.Log += Log;
            _client.ReactionAdded += OnReactionAddedAsync;
            
            await Login();

            await Task.Delay(15 * 1000); //Wait 15 seconds

            // Clear channel
            await FindAndPurgeChannel();
            // Spawn & Cache new messages
            await GenerateTargetDeviceMessages();
            // Begin Main Loop
            await MainLoop();

            await Task.Delay(-1);
        }

        private async Task Login()
        {
            await _client.LoginAsync(TokenType.Bot, _configFile.DiscordToken);
            await _client.StartAsync();
        }

        /// <summary>
        /// Main loop that checks the online status of target devices
        /// </summary>
        /// <returns></returns>
        private async Task MainLoop()
        {
            Task[] refreshTargetsTasks = new Task[_configFile.TargetDevices.Count];

            // Check target devices online status
            while (true)
            {
                using var enumerator = _targetDeviceLinkedCache.GetEnumerator();
                
                var i = 0;
                
                while (enumerator.MoveNext())
                {
                    if (refreshTargetsTasks[i] == null ||
                        refreshTargetsTasks[i].IsCompleted)
                    {
                        // Only generate the task if there is non yet or the previous task has completed
                        var message = enumerator.Current.Key;
                        refreshTargetsTasks[i] = RefreshTarget(message);
                    }

                    i++;
                }

                // Wait for at least one refresh to complete
                await Task.WhenAny(refreshTargetsTasks);

                // Walki: There is a weird issue where apparently,
                // Discord.NET doesn't recover correctly and will permanently disconnect the client,
                // this "should" solve this issue
                if (_client.ConnectionState == ConnectionState.Disconnected)
                {
                    await Login();
                }
            }
        }

        /// <summary>
        /// Generate Tasks that set up the per target messages
        /// </summary>
        /// <returns></returns>
        private async Task GenerateTargetDeviceMessages()
        {
            await SetupTutorialMessage();

            Task[] setupTargetsTasks = new Task[_configFile.TargetDevices.Count];

            for (int i = 0; i < _configFile.TargetDevices.Count; i++)
            {
                var embed = new EmbedBuilder
                {
                    Description = $"{_configFile.TargetDevices[i].Emoji} **- {_configFile.TargetDevices[i].DeviceName}**"
                };

                var message = await _channel.SendMessageAsync(null, false, embed.Build());

                setupTargetsTasks[i] = SetupTarget(message, _configFile.TargetDevices[i]);
            }

            await Task.WhenAll(setupTargetsTasks);
        }

        /// <summary>
        /// Setup the first message of the channel
        /// </summary>
        /// <returns></returns>
        private async Task SetupTutorialMessage()
        {
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Wake On Lan - Instructions"
                },

                // This has weird spacing because of different Emoji offsets
                Description = $"\\{SendWakeOnLan} - Request to switch on the device\n" +
                              $" \\{WaitingForTarget}   - Waiting for a response from the device\n" +
                              $" \\{TargetIsRunning}  - The target device is running"
            };

            await _channel.SendMessageAsync(null, false, embed.Build());
        }

        /// <summary>
        /// Refresh a cached target device
        /// </summary>
        /// <param name="message"></param>
        /// <param name="targetDevice"></param>
        /// <returns></returns>
        private async Task RefreshTarget(RestUserMessage message)
        {
            // This might seem weird, but we need the actual reference not a copy of it
            if (_targetDeviceLinkedCache.TryGetValue(message, out var targetDevice))
            {
                if (targetDevice.DeviceState == TargetDeviceState.Starting)
                {
                    // Wait for the targetDevice's startup timeout
                    await Task.Delay(targetDevice.StartupTimeoutMs);
                }
                else
                {
                    await Task.Delay(_configFile.StatusCheckIntervalMs);

                    // Double-check if the target device might have entered starting state during our delay
                    if (targetDevice.DeviceState == TargetDeviceState.Starting)
                    {
                        await Task.Delay(Math.Max(0, targetDevice.StartupTimeoutMs - _configFile.StatusCheckIntervalMs));
                    }
                }

                var pingable = await PingTarget(targetDevice.IpAddress);
                var time = DateTime.Now;

                if (pingable)
                {
                    if (targetDevice.DeviceState != TargetDeviceState.Pingable)
                    {
                        targetDevice.DeviceState = TargetDeviceState.Pingable;
                        await message.RemoveAllReactionsAsync();
                        await message.AddReactionAsync(_emoteTargetIsRunning);

                        Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] Device {targetDevice.DeviceName} is now running.");
                    }
                }
                else
                {
                    if (targetDevice.DeviceState != TargetDeviceState.Offline &&
                        targetDevice.DeviceState != TargetDeviceState.Starting)
                    {
                        targetDevice.DeviceState = TargetDeviceState.Offline;
                        await message.RemoveAllReactionsAsync();
                        await message.AddReactionAsync(_emoteSendWakeOnLan);

                        Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] Device {targetDevice.DeviceName} is now offline.");
                    }
                }

                _targetDeviceLinkedCache[message] = targetDevice;
            }
        }

        /// <summary>
        /// Setup and a cache a target device message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="targetDevice"></param>
        /// <returns></returns>
        private async Task SetupTarget(RestUserMessage message, TargetDevice targetDevice)
        {
            var pingable = await PingTarget(targetDevice.IpAddress);

            if (pingable)
            {
                targetDevice.DeviceState = TargetDeviceState.Pingable;
                await message.AddReactionAsync(_emoteTargetIsRunning);
            }
            else
            {
                targetDevice.DeviceState = TargetDeviceState.Offline;
                await message.AddReactionAsync(_emoteSendWakeOnLan);
            }

            _targetDeviceLinkedCache.TryAdd(message, targetDevice);
        }

        /// <summary>
        /// Simple ping as a task implementation
        /// </summary>
        /// <param name="targetDevice"></param>
        /// <returns></returns>
        private async Task<bool> PingTarget(string IPAddress)
        {
            return await Task.Run(() =>
                {
                    using var pingSender = new Ping();
                    // Since we are inside of task we can use the synced version of ping 
                    var reply = pingSender.Send(IPAddress, _configFile.StatusCheckIntervalMs - 1000);

                    if (reply.Status != IPStatus.Success)
                    {
                        return false;
                    }

                    return true;
                }
            );
        }

        /// <summary>
        /// Initial channel cleanup
        /// </summary>
        /// <returns></returns>
        private async Task FindAndPurgeChannel()
        {
            _guild = _client.GetGuild(_configFile.DiscordServerId);
            var time = DateTime.Now;

            if (_guild != null && _guild.IsConnected)
            {
                var channel = _guild.GetChannel(_configFile.DiscordChannelId);
                if (channel is SocketTextChannel botChannel)
                {
                    _channel = botChannel;
                    Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] Target server & channel found. Purging channel messages!");
                    await PurgeChat(_configFile.TargetDevices.Count + 10, botChannel);
                }
                else
                {
                    Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] Target ID not a text channel or Discord unavailable. Exiting...");
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] Target server not found or Discord unavailable. Exiting...");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Someone added or incremented a reaction
        /// </summary>
        /// <param name="rawMessage"></param>
        /// <param name="channel"></param>
        /// <param name="reaction"></param>
        /// <returns></returns>
        private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> rawMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            // Ignore reactions by ourselves
            if (reaction.UserId == _client.CurrentUser.Id)
            {
                return;
            }

            // Fetch message & targetDevice from cache
            if (GetRestUserMessage(rawMessage.Id, out var targetDevicePair))
            {
                var targetDevice = targetDevicePair.Value;
                var message = targetDevicePair.Key;

                if (reaction.Emote.Name == _emoteSendWakeOnLan.Name)
                {
                    targetDevice.DeviceState = TargetDeviceState.Starting;
                    await message.RemoveAllReactionsAsync();
                    await message.AddReactionAsync(_emoteWaitingForTarget);
                    Wol.Send(targetDevice.MacAddress);

                    var time = DateTime.Now;
                    Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] User ID: {reaction.UserId} sent WoL to device {targetDevice.DeviceName} ...");
                }
                else
                {
                    // Prevents people from spamming reactions
                    await message.RemoveReactionAsync(reaction.Emote, reaction.UserId);
                }
            }
        }

        /// <summary>
        /// Retrieve message & targetDevice pair from cache
        /// </summary>
        /// <param name="MessageID"></param>
        /// <param name="targetDevicePair"></param>
        /// <returns></returns>
        private bool GetRestUserMessage(ulong MessageID, out KeyValuePair<RestUserMessage, TargetDevice> targetDevicePair)
        {
            using (var enumerator = _targetDeviceLinkedCache.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Key.Id == MessageID)
                    {
                        targetDevicePair = enumerator.Current;
                        return true;
                    }
                }
            }

            targetDevicePair = new KeyValuePair<RestUserMessage, TargetDevice>();
            return false;
        }

        /// <summary>
        /// Tidy up channel
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        private async Task PurgeChat(int amount, SocketTextChannel channel)
        {
            try
            {
                var messages = await channel.GetMessagesAsync(amount + 1).FlattenAsync();
                await channel.DeleteMessagesAsync(messages);
            }
            catch (Exception)
            {
                // ignored
            }
        }


        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}

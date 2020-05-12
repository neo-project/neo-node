using Microsoft.Extensions.Configuration;
using Neo.Network.P2P;
using System.Threading;

namespace Neo
{
    public class Settings
    {
        public LoggerSettings Logger { get; }
        public StorageSettings Storage { get; }
        public P2PSettings P2P { get; }
        public UnlockWalletSettings UnlockWallet { get; }
        public string PluginURL { get; }

        static Settings _default;

        static bool UpdateDefault(IConfiguration configuration)
        {
            var settings = new Settings(configuration.GetSection("ApplicationConfiguration"));
            return null == Interlocked.CompareExchange(ref _default, settings, null);
        }

        public static bool Initialize(IConfiguration configuration)
        {
            return UpdateDefault(configuration);
        }

        public static Settings Default
        {
            get
            {
                if (_default == null)
                {
                    UpdateDefault(Utility.LoadConfig("config"));
                }

                return _default;
            }
        }

        public Settings(IConfigurationSection section)
        {
            this.Logger = new LoggerSettings(section.GetSection("Logger"));
            this.Storage = new StorageSettings(section.GetSection("Storage"));
            this.P2P = new P2PSettings(section.GetSection("P2P"));
            this.UnlockWallet = new UnlockWalletSettings(section.GetSection("UnlockWallet"));
            this.PluginURL = section.GetSection("PluginURL").Value;
        }
    }

    public class LoggerSettings
    {
        public string Path { get; }
        public bool ConsoleOutput { get; }
        public bool Started { get; }

        public LoggerSettings(IConfigurationSection section)
        {
            this.Path = string.Format(section.GetSection("Path").Value, ProtocolSettings.Default.Magic.ToString("X8"));
            this.ConsoleOutput = section.GetSection("ConsoleOutput").Get<bool>();
            this.Started = section.GetSection("Started").Get<bool>();
        }
    }

    public class StorageSettings
    {
        public string Engine { get; }

        public StorageSettings(IConfigurationSection section)
        {
            this.Engine = section.GetSection("Engine").Value;
        }
    }

    public class P2PSettings
    {
        public ushort Port { get; }
        public ushort WsPort { get; }
        public int MinDesiredConnections { get; }
        public int MaxConnections { get; }
        public int MaxConnectionsPerAddress { get; }

        public P2PSettings(IConfigurationSection section)
        {
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.WsPort = ushort.Parse(section.GetSection("WsPort").Value);
            this.MinDesiredConnections = section.GetValue("MinDesiredConnections", Peer.DefaultMinDesiredConnections);
            this.MaxConnections = section.GetValue("MaxConnections", Peer.DefaultMaxConnections);
            this.MaxConnectionsPerAddress = section.GetValue("MaxConnectionsPerAddress", 3);
        }
    }

    public class UnlockWalletSettings
    {
        public string Path { get; }
        public string Password { get; }
        public bool StartConsensus { get; }
        public bool IsActive { get; }

        public UnlockWalletSettings(IConfigurationSection section)
        {
            if (section.Exists())
            {
                this.Path = section.GetSection("Path").Value;
                this.Password = section.GetSection("Password").Value;
                this.StartConsensus = bool.Parse(section.GetSection("StartConsensus").Value);
                this.IsActive = bool.Parse(section.GetSection("IsActive").Value);
            }
        }
    }
}

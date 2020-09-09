using Microsoft.Extensions.Configuration;
using Neo.Network.P2P;
using System.Net;

namespace Neo
{
    internal class Settings
    {
        public PathsSettings Paths { get; }
        public P2PSettings P2P { get; }
        public RPCSettings RPC { get; }
        public UnlockWalletSettings UnlockWallet { get; }
        public string PluginURL { get; }

        public static Settings Default { get; }

        static Settings()
        {
            IConfigurationSection section = new ConfigurationBuilder().AddJsonFile("config.json").Build().GetSection("ApplicationConfiguration");
            Default = new Settings(section);
        }

        public Settings(IConfigurationSection section)
        {
            this.Paths = new PathsSettings(section.GetSection("Paths"));
            this.P2P = new P2PSettings(section.GetSection("P2P"));
            this.RPC = new RPCSettings(section.GetSection("RPC"));
            this.UnlockWallet = new UnlockWalletSettings(section.GetSection("UnlockWallet"));
            this.PluginURL = section.GetSection("PluginURL").Value;
        }
    }

    internal class PathsSettings
    {
        public string Chain { get; }
        public string Index { get; }

        public PathsSettings(IConfigurationSection section)
        {
            this.Chain = string.Format(section.GetSection("Chain").Value, Message.Magic.ToString("X8"));
            this.Index = string.Format(section.GetSection("Index").Value, Message.Magic.ToString("X8"));
        }
    }

    internal class P2PSettings
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

    internal class RPCSettings
    {
        public IPAddress BindAddress { get; }
        public ushort Port { get; }
        public string SslCert { get; }
        public string SslCertPassword { get; }
        public Fixed8 ExtraGasInvoke { get; }
        public int MaxConcurrentConnections { get; }

        public RPCSettings(IConfigurationSection section)
        {
            this.BindAddress = IPAddress.Parse(section.GetSection("BindAddress").Value);
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.SslCert = section.GetSection("SslCert").Value;
            this.SslCertPassword = section.GetSection("SslCertPassword").Value;
            this.ExtraGasInvoke = Fixed8.Parse(section.GetValue("ExtraGasInvoke", "0"));
            this.MaxConcurrentConnections = int.Parse(section.GetValue("MaxConcurrentConnections", "10"));
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

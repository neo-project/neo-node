// Copyright (C) 2015-2025 The Neo Project.
//
// MainService.CommandLine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using System.CommandLine;
using System.Reflection;

namespace Neo.CLI;

public partial class MainService
{
    public int OnStartWithCommandLine(string[] args)
    {
        var optionsMap = typeof(CommandLineOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => new
            {
                Property = p,
                Attribute = p.GetCustomAttribute<OptionAttribute>()
            })
            .Where(p => p.Attribute != null)
            .Select(p =>
            {
                var type = typeof(Option<>).MakeGenericType(p.Property.PropertyType);
                var option = (Option)Activator.CreateInstance(type, [p.Attribute!.Name, p.Attribute.Aliases])!;
                option.Description = p.Attribute.Description;
                return (p.Property, Option: option);
            })
            .ToList();
        var rootCommand = new RootCommand(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title);
        foreach (var (_, option) in optionsMap)
            rootCommand.Add(option);
        var result = rootCommand.Parse(args);
        var options = new CommandLineOptions();
        foreach (var (property, option) in optionsMap)
        {
            var getValueMethod = typeof(ParseResult).GetMethod(nameof(ParseResult.GetValue), 1, [typeof(Option<>).MakeGenericType(Type.MakeGenericMethodParameter(0))])!;
            object? value = getValueMethod.Invoke(result, [option]);
            property.SetValue(options, value);
        }
        Handle(options);
        return 0;
    }

    private void Handle(CommandLineOptions options)
    {
        IsBackground = options.Background;
        Start(options);
    }

    private static void CustomProtocolSettings(CommandLineOptions options, ProtocolSettings settings)
    {
        // if specified config, then load the config and check the network
        if (!string.IsNullOrEmpty(options.Config))
        {
            ProtocolSettings.Custom = ProtocolSettings.Load(options.Config);
        }
    }

    private static void CustomApplicationSettings(CommandLineOptions options, Settings settings)
    {
        var tempSetting = string.IsNullOrEmpty(options.Config)
            ? settings
            : new Settings(new ConfigurationBuilder().AddJsonFile(options.Config, optional: true).Build().GetSection("ApplicationConfiguration"));
        var customSetting = new Settings
        {
            Logger = tempSetting.Logger,
            Storage = new StorageSettings
            {
                Engine = options.DBEngine ?? tempSetting.Storage.Engine,
                Path = options.DBPath ?? tempSetting.Storage.Path
            },
            P2P = tempSetting.P2P,
            UnlockWallet = new UnlockWalletSettings
            {
                Path = options.Wallet ?? tempSetting.UnlockWallet.Path,
                Password = options.Password ?? tempSetting.UnlockWallet.Password
            },
            Contracts = tempSetting.Contracts
        };
        if (options.IsValid) Settings.Custom = customSetting;
    }
}

// Copyright (C) 2015-2026 The Neo Project.
//
// CommandCatalog.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.CLI;
using Neo.ConsoleService;
using Neo.Plugins;
using System.Reflection;

namespace Neo.ConsoleUI;

internal sealed record CommandInfo(
    string Key,
    string Category,
    string Description,
    MethodInfo Method,
    object Instance);

internal static class CommandCatalog
{
    public static IReadOnlyList<CommandInfo> Load(MainService service)
    {
        var list = new List<CommandInfo>();
        AddCommands(list, service, service);
        AddCommands(list, service, service, typeof(ConsoleServiceBase));
        foreach (var plugin in Plugin.Plugins)
            AddCommands(list, plugin, plugin);

        return list
            .OrderBy(c => c.Category, StringComparer.Ordinal)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddCommands(List<CommandInfo> list, object instance, object owner, Type? type = null)
    {
        type ??= instance.GetType();
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var attribute in method.GetCustomAttributes<ConsoleCommandAttribute>(inherit: true))
            {
                var key = string.Join(' ', attribute.Verbs);
                if (list.Any(c => c.Key == key && c.Method == method))
                    continue;
                list.Add(new CommandInfo(
                    key,
                    string.IsNullOrWhiteSpace(attribute.Category) ? "Commands" : attribute.Category,
                    attribute.Description,
                    method,
                    owner));
            }
        }
    }

    public static IEnumerable<string> Categories(IReadOnlyList<CommandInfo> commands)
        => commands.Select(c => c.Category).Distinct(StringComparer.Ordinal);

    public static IReadOnlyList<string> Completions(IReadOnlyList<CommandInfo> commands)
        => commands.Select(c => c.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

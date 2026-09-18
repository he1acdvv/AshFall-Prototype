using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Ashfall.Memory.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Ashfall.Memory;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class WeaveMemoriesCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public string Command => "weavememories";
    public string Description => "Forces or weaves an inter-character memory link between two entities.";
    public string Help => "Usage: weavememories <entityA or playerA> <entityB or playerB> [templatePrototypeId]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryResolveEntity(args[0], out var entityA))
        {
            shell.WriteError($"Could not resolve entity '{args[0]}'. Must be an entity ID or player name.");
            return;
        }

        if (!TryResolveEntity(args[1], out var entityB))
        {
            shell.WriteError($"Could not resolve entity '{args[1]}'. Must be an entity ID or player name.");
            return;
        }

        ProtoId<AshfallMemoryTemplatePrototype>? template = null;
        if (args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2]))
        {
            template = new ProtoId<AshfallMemoryTemplatePrototype>(args[2]);
        }

        var memorySys = _entManager.System<AshfallMemorySystem>();
        if (memorySys.TryForceWeaveMemory(entityA.Value, entityB.Value, template, out var msg))
        {
            shell.WriteLine(msg);
        }
        else
        {
            shell.WriteError(msg);
        }
    }

    private bool TryResolveEntity(string arg, [NotNullWhen(true)] out EntityUid? entity)
    {
        entity = null;

        if (int.TryParse(arg, out var id))
        {
            var netEnt = new NetEntity(id);
            if (_entManager.TryGetEntity(netEnt, out var resolved))
            {
                entity = resolved;
                return true;
            }
        }

        if (_playerManager.TryGetSessionByUsername(arg, out var session) && session.AttachedEntity is { } attached)
        {
            entity = attached;
            return true;
        }

        return false;
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class ListMemoriesCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public string Command => "listmemories";
    public string Description => "Lists all inter-character memory links for a given entity.";
    public string Help => "Usage: listmemories <entity or player>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryResolveEntity(args[0], out var entity))
        {
            shell.WriteError($"Could not resolve entity '{args[0]}'. Must be an entity ID or player name.");
            return;
        }

        var memorySys = _entManager.System<AshfallMemorySystem>();
        var memories = memorySys.GetEntityMemories(entity.Value);

        if (memories.Count == 0)
        {
            shell.WriteLine($"No memories found for {_entManager.ToPrettyString(entity.Value)}.");
            return;
        }

        shell.WriteLine($"Memories for {_entManager.ToPrettyString(entity.Value)} ({memories.Count}):");
        for (var i = 0; i < memories.Count; i++)
        {
            var m = memories[i];
            var disc = m.Discovered ? "DISCOVERED" : "UNDISCOVERED";
            shell.WriteLine($"  [{i + 1}] Partner: {_entManager.ToPrettyString(m.Partner)} | Template: {m.TemplateId} | Status: {disc}");
            shell.WriteLine($"      Summary: {m.Summary}");
        }
    }

    private bool TryResolveEntity(string arg, [NotNullWhen(true)] out EntityUid? entity)
    {
        entity = null;

        if (int.TryParse(arg, out var id))
        {
            var netEnt = new NetEntity(id);
            if (_entManager.TryGetEntity(netEnt, out var resolved))
            {
                entity = resolved;
                return true;
            }
        }

        if (_playerManager.TryGetSessionByUsername(arg, out var session) && session.AttachedEntity is { } attached)
        {
            entity = attached;
            return true;
        }

        return false;
    }
}

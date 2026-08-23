using Xunit;

namespace DndCampaign.Modules.Combat.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CombatDatabaseCollection
{
    public const string Name = "Combat database";
}

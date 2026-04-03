using FluentMigrator;

namespace Mod.DynamicEncounters.Database.Migrations;

[Migration(53)]
public class AddLockdownReinforcementsSpawnedToAlienWarEvent : Migration
{
    private const string TableName = "mod_alien_war_event";

    public override void Up()
    {
        Alter.Table(TableName).InSchema("public")
            .AddColumn("lockdown_reinforcements_spawned").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("lockdown_reinforcements_spawned").FromTable(TableName).InSchema("public");
    }
}

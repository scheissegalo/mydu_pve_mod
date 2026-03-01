using FluentMigrator;

namespace Mod.DynamicEncounters.Database.Migrations;

[Migration(52)]
public class AddAlienWarWreckTableAndEventStats : Migration
{
    private const string EventTable = "mod_alien_war_event";
    private const string WreckTable = "mod_alien_war_wreck";

    public override void Up()
    {
        Alter.Table(EventTable).InSchema("public")
            .AddColumn("total_spawned").AsInt32().Nullable()
            .AddColumn("shield_percent_at_end").AsDouble().Nullable()
            .AddColumn("outcome").AsString(50).Nullable();

        Create.Table(WreckTable)
            .InSchema("public")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("core_construct_id").AsInt64().NotNullable()
            .WithColumn("wreck_construct_id").AsInt64().NotNullable()
            .WithColumn("ship_name").AsString(200).NotNullable()
            .WithColumn("position_x").AsDouble().NotNullable()
            .WithColumn("position_y").AsDouble().NotNullable()
            .WithColumn("position_z").AsDouble().NotNullable()
            .WithColumn("destroyed_at").AsDateTime().NotNullable();

        Create.Index($"IX_{WreckTable}_core").OnTable(WreckTable)
            .InSchema("public")
            .OnColumn("core_construct_id").Ascending();
    }

    public override void Down()
    {
        Delete.Index($"IX_{WreckTable}_core").OnTable(WreckTable).InSchema("public");
        Delete.Table(WreckTable).InSchema("public");
        Delete.Column("total_spawned").FromTable(EventTable).InSchema("public");
        Delete.Column("shield_percent_at_end").FromTable(EventTable).InSchema("public");
        Delete.Column("outcome").FromTable(EventTable).InSchema("public");
    }
}

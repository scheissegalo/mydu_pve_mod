using FluentMigrator;

namespace Mod.DynamicEncounters.Database.Migrations;

[Migration(51)]
public class AddAlienWarEventTable : Migration
{
    private const string TableName = "mod_alien_war_event";
    private const string FieldSectorX = "sector_x";
    private const string FieldSectorY = "sector_y";
    private const string FieldSectorZ = "sector_z";

    public override void Up()
    {
        Create.Table(TableName)
            .InSchema("public")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("core_construct_id").AsInt64().NotNullable()
            .WithColumn(FieldSectorX).AsDouble().NotNullable()
            .WithColumn(FieldSectorY).AsDouble().NotNullable()
            .WithColumn(FieldSectorZ).AsDouble().NotNullable()
            .WithColumn("script_name").AsString(200).NotNullable()
            .WithColumn("cooldown_seconds_override").AsInt32().Nullable()
            .WithColumn("created_at").AsDateTime().WithDefault(SystemMethods.CurrentDateTime);

        Create.Index($"IX_{TableName}_core").OnTable(TableName)
            .InSchema("public")
            .OnColumn("core_construct_id").Ascending();
    }

    public override void Down()
    {
        Delete.Index($"IX_{TableName}_core").OnTable(TableName);
        Delete.Table(TableName);
    }
}

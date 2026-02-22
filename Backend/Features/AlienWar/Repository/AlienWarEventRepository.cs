using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Database.Interfaces;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;
using NQ;

namespace Mod.DynamicEncounters.Features.AlienWar.Repository;

public class AlienWarEventRepository(IServiceProvider provider) : IAlienWarEventRepository
{
    private const string TableName = "mod_alien_war_event";
    private readonly IPostgresConnectionFactory _factory = provider.GetRequiredService<IPostgresConnectionFactory>();

    public async Task AddAsync(AlienWarEventRecord record)
    {
        using var db = _factory.Create();
        db.Open();
        await db.ExecuteAsync(
            $"""
            INSERT INTO public.{TableName} (id, core_construct_id, sector_x, sector_y, sector_z, script_name, cooldown_seconds_override, created_at)
            VALUES (@id, @core_construct_id, @sector_x, @sector_y, @sector_z, @script_name, @cooldown_seconds_override, @created_at)
            """,
            new
            {
                id = record.Id,
                core_construct_id = (long)record.CoreConstructId,
                sector_x = record.Sector.x,
                sector_y = record.Sector.y,
                sector_z = record.Sector.z,
                script_name = record.ScriptName,
                cooldown_seconds_override = record.CooldownSecondsOverride,
                created_at = record.CreatedAt
            });
    }

    public async Task RemoveByCoreAsync(ulong coreConstructId)
    {
        using var db = _factory.Create();
        db.Open();
        await db.ExecuteAsync(
            $"DELETE FROM public.{TableName} WHERE core_construct_id = @core_construct_id",
            new { core_construct_id = (long)coreConstructId });
    }

    public async Task<IReadOnlyList<AlienWarEventRecord>> GetActiveAsync()
    {
        using var db = _factory.Create();
        db.Open();
        var rows = (await db.QueryAsync<AlienWarEventRow>(
            $"SELECT id, core_construct_id, sector_x, sector_y, sector_z, script_name, cooldown_seconds_override, created_at FROM public.{TableName}"
        )).ToList();
        return rows.Select(r => new AlienWarEventRecord
        {
            Id = r.id,
            CoreConstructId = (ulong)r.core_construct_id,
            Sector = new Vec3 { x = r.sector_x, y = r.sector_y, z = r.sector_z },
            ScriptName = r.script_name ?? string.Empty,
            CooldownSecondsOverride = r.cooldown_seconds_override,
            CreatedAt = r.created_at
        }).ToList();
    }

    private struct AlienWarEventRow
    {
        public Guid id { get; set; }
        public long core_construct_id { get; set; }
        public double sector_x { get; set; }
        public double sector_y { get; set; }
        public double sector_z { get; set; }
        public string script_name { get; set; }
        public int? cooldown_seconds_override { get; set; }
        public DateTime created_at { get; set; }
    }
}

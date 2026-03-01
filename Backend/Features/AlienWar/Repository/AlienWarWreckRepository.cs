using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Database.Interfaces;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;

namespace Mod.DynamicEncounters.Features.AlienWar.Repository;

public class AlienWarWreckRepository(IServiceProvider provider) : IAlienWarWreckRepository
{
    private readonly IPostgresConnectionFactory _factory = provider.GetRequiredService<IPostgresConnectionFactory>();

    public async Task AddAsync(AlienWarWreckRecord record)
    {
        using var db = _factory.Create();
        db.Open();
        await db.ExecuteAsync(
            """
            INSERT INTO public.mod_alien_war_wreck (id, core_construct_id, wreck_construct_id, ship_name, position_x, position_y, position_z, destroyed_at)
            VALUES (@Id, @CoreConstructId, @WreckConstructId, @ShipName, @PositionX, @PositionY, @PositionZ, @DestroyedAt)
            """,
            new
            {
                record.Id,
                CoreConstructId = (long)record.CoreConstructId,
                WreckConstructId = (long)record.WreckConstructId,
                record.ShipName,
                record.PositionX,
                record.PositionY,
                record.PositionZ,
                record.DestroyedAt
            });
    }

    public async Task<IReadOnlyList<AlienWarWreckRecord>> FindByCoreAsync(ulong coreConstructId)
    {
        using var db = _factory.Create();
        db.Open();
        var rows = await db.QueryAsync<WreckRow>(
            """
            SELECT id, core_construct_id, wreck_construct_id, ship_name, position_x, position_y, position_z, destroyed_at
            FROM public.mod_alien_war_wreck WHERE core_construct_id = @CoreConstructId ORDER BY destroyed_at
            """,
            new { CoreConstructId = (long)coreConstructId });
        return rows.Select(r => new AlienWarWreckRecord
        {
            Id = r.id,
            CoreConstructId = (ulong)r.core_construct_id,
            WreckConstructId = (ulong)r.wreck_construct_id,
            ShipName = r.ship_name,
            PositionX = r.position_x,
            PositionY = r.position_y,
            PositionZ = r.position_z,
            DestroyedAt = r.destroyed_at
        }).ToList();
    }

    private struct WreckRow
    {
        public Guid id { get; set; }
        public long core_construct_id { get; set; }
        public long wreck_construct_id { get; set; }
        public string ship_name { get; set; }
        public double position_x { get; set; }
        public double position_y { get; set; }
        public double position_z { get; set; }
        public DateTime destroyed_at { get; set; }
    }
}

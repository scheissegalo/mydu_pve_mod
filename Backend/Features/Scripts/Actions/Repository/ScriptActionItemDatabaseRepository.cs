using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Database.Interfaces;
using Mod.DynamicEncounters.Features.Scripts.Actions.Data;
using Mod.DynamicEncounters.Features.Scripts.Actions.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mod.DynamicEncounters.Features.Scripts.Actions.Repository;

public class ScriptActionItemDatabaseRepository(IServiceProvider provider) : IScriptActionItemRepository
{
    private readonly IPostgresConnectionFactory _factory = provider.GetRequiredService<IPostgresConnectionFactory>();

    public async Task AddAsync(ScriptActionItem item)
    {
        using var db = _factory.Create();
        db.Open();

        if (item.Id == Guid.Empty)
        {
            item.Id = Guid.NewGuid();
        }

        await db.ExecuteAsync(
            """
            INSERT INTO public.mod_script (id, name, content) 
            VALUES(@id, @name, @content::jsonb)
            """,
            new
            {
                id = item.Id,
                name = item.Name,
                content = JsonConvert.SerializeObject(item)
            }
        );
    }

    public Task SetAsync(IEnumerable<ScriptActionItem> items)
    {
        throw new NotSupportedException();
    }

    public async Task UpdateAsync(ScriptActionItem item)
    {
        using var db = _factory.Create();
        db.Open();

        await db.ExecuteAsync(
            """
            UPDATE public.mod_script SET
                content = @content::jsonb
            WHERE name = @name
            """,
            new
            {
                item.Id,
                content = JsonConvert.SerializeObject(item)
            }
        );
    }

    public Task AddRangeAsync(IEnumerable<ScriptActionItem> items)
    {
        throw new NotImplementedException("TODO LATER");
    }

    public async Task<ScriptActionItem?> FindAsync(object key)
    {
        using var db = _factory.Create();
        db.Open();

        var rows = (await db.QueryAsync<DbRow>(
                """SELECT * FROM public.mod_script WHERE name = @key""",
                new { key })
            ).ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        return MapToModel(rows[0]);
    }

    public async Task<IEnumerable<ScriptActionItem>> GetAllAsync()
    {
        using var db = _factory.Create();
        db.Open();

        var rows = (await db.QueryAsync<DbRow>("""
                                               SELECT * FROM public.mod_script
                                               """)).ToList();

        return rows.Select(MapToModel);
    }

    public async Task<long> GetCountAsync()
    {
        using var db = _factory.Create();
        db.Open();

        return await db.ExecuteScalarAsync<int>("""
                                                SELECT COUNT(0) FROM public.mod_script
                                                """);
    }

    public async Task DeleteAsync(Guid key)
    {
        using var db = _factory.Create();
        db.Open();

        await db.ExecuteAsync("DELETE FROM public.mod_script WHERE id = @key", new { key });
    }

    public Task Clear()
    {
        throw new NotImplementedException();
    }

    public async Task<bool> ActionExistAsync(string actionName)
    {
        using var db = _factory.Create();
        db.Open();

        return await db.ExecuteScalarAsync<long>(
            "SELECT COUNT(0) FROM public.mod_script WHERE name = @actionName",
            new { actionName }
        ) > 0;
    }

    public Task<ScriptActionItem?> FindAsync(string name)
    {
        return FindAsync((object)name);
    }

    private ScriptActionItem MapToModel(DbRow row)
    {
        try
        {
            // Parse JSON and fix Properties arrays before deserialization
            var json = JObject.Parse(row.content);
            FixPropertiesInJsonRecursively(json);
            
            var jsonString = json.ToString();
            var model = JsonConvert.DeserializeObject<ScriptActionItem>(jsonString);
            if (model == null)
            {
                throw new InvalidOperationException($"Failed to deserialize script {row.name}");
            }
            
            model.Id = row.id;
            
            // Ensure Properties is initialized (handle null case)
            if (model.Properties == null)
            {
                model.Properties = new Dictionary<string, object>();
            }
            
            // Recursively ensure nested Actions have Properties initialized
            FixPropertiesRecursively(model);
            
            return model;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize script {row.name}: {ex.Message}", ex);
        }
    }

    private void FixPropertiesInJsonRecursively(JToken token)
    {
        if (token == null) return;
        
        if (token is JObject obj)
        {
            // Fix Properties if it's an array
            if (obj["Properties"] != null && obj["Properties"].Type == JTokenType.Array)
            {
                obj["Properties"] = new JObject();
            }
            
            // Recursively fix nested Actions
            if (obj["Actions"] != null && obj["Actions"].Type == JTokenType.Array)
            {
                foreach (var action in obj["Actions"])
                {
                    FixPropertiesInJsonRecursively(action);
                }
            }
            
            // Recursively fix all other object/array properties
            foreach (var property in obj.Properties())
            {
                if (property.Value != null && (property.Value.Type == JTokenType.Object || property.Value.Type == JTokenType.Array))
                {
                    FixPropertiesInJsonRecursively(property.Value);
                }
            }
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
            {
                FixPropertiesInJsonRecursively(item);
            }
        }
    }

    private void FixPropertiesRecursively(ScriptActionItem item)
    {
        if (item.Properties == null)
        {
            item.Properties = new Dictionary<string, object>();
        }
        
        if (item.Actions != null)
        {
            foreach (var action in item.Actions)
            {
                FixPropertiesRecursively(action);
            }
        }
    }


    private struct DbRow
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public string content { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }
}
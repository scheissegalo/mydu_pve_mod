using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Database.Interfaces;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;
using Newtonsoft.Json.Linq;

namespace Mod.DynamicEncounters.Features.AlienWar.Services;

public class AlienCoreShieldService(IServiceProvider provider) : IAlienCoreShieldService
{
    private const long BaseShieldElementTypeId = 1430252067;
    /// <summary>When this property exists with a value, the shield is in lockdown until the given UTC time (value = Unix time in milliseconds).</summary>
    private const string LockdownEndPropertyName = "lockdownEnd";
    private const string HitHistoryPropertyName = "hitHistory";
    private const string ShieldMaxHpPropertyName = "shieldMaxHp";

    private readonly IPostgresConnectionFactory _factory = provider.GetRequiredService<IPostgresConnectionFactory>();
    private readonly ILogger<AlienCoreShieldService> _logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<AlienCoreShieldService>();

    public async Task<AlienCoreShieldStatus?> GetShieldStatusAsync(ulong constructId, int? cooldownSecondsOverride = null)
    {
        using var db = _factory.Create();
        db.Open();

        bool? shieldEnabled = null;
        try
        {
            var rows = (await db.QueryAsync<ShieldEnabledRow>(
                "SELECT shield_enabled FROM public.construct WHERE id = @constructId AND deleted_at IS NULL",
                new { constructId = (long)constructId }
            )).ToList();

            if (rows.Count == 0)
                return null;

            shieldEnabled = rows[0].shield_enabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AlienCoreShieldService: Failed to read shield_enabled for construct {ConstructId}", constructId);
            return null;
        }

        DateTime? lockdownEndAtUtc = null;
        long? lockdownEndUnixMs = null;
        double? shieldHealthPercent = null;

        if (cooldownSecondsOverride.HasValue)
        {
            lockdownEndAtUtc = DateTime.UtcNow.AddSeconds(cooldownSecondsOverride.Value);
        }
        else if (shieldEnabled == true)
        {
            long? elementId = null;
            try
            {
                var elementRows = (await db.QueryAsync<ElementIdRow>(
                    "SELECT id FROM public.element WHERE construct_id = @constructId AND element_type_id = @elementTypeId",
                    new { constructId = (long)constructId, elementTypeId = BaseShieldElementTypeId }
                )).ToList();

                if (elementRows.Count > 0)
                    elementId = elementRows[0].id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AlienCoreShieldService: Failed to read shield element for construct {ConstructId}", constructId);
            }

            if (elementId.HasValue)
            {
                try
                {
                    var propRows = (await db.QueryAsync<ElementPropertyRow>(
                        "SELECT name, property_type, value FROM public.element_property WHERE element_id = @elementId",
                        new { elementId = elementId.Value }
                    )).ToList();

                    var lockdownProp = propRows.FirstOrDefault(p =>
                        string.Equals(p.name, LockdownEndPropertyName, StringComparison.OrdinalIgnoreCase));

                    if (lockdownProp.name != null && lockdownProp.value != null && lockdownProp.value.Length > 0)
                    {
                        var (decodedUtc, rawMs) = DecodeLockdownEndWithRaw(lockdownProp.value);
                        lockdownEndAtUtc = decodedUtc;
                        lockdownEndUnixMs = rawMs;
                        if (lockdownEndAtUtc == null)
                            lockdownEndAtUtc = DateTime.UtcNow.AddDays(1); // property set but unreadable => assume lockdown
                    }

                    shieldHealthPercent = TryComputeShieldHealthPercent(propRows);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AlienCoreShieldService: Failed to read element_property for element {ElementId}", elementId);
                }
            }
        }

        // Ensure we always have raw Unix ms when we have a lockdown end time (for API debugging; serializer omits null)
        if (lockdownEndAtUtc.HasValue && !lockdownEndUnixMs.HasValue)
            lockdownEndUnixMs = new DateTimeOffset(lockdownEndAtUtc.Value, TimeSpan.Zero).ToUnixTimeMilliseconds();

        return new AlienCoreShieldStatus
        {
            ShieldEnabled = shieldEnabled ?? false,
            LockdownExitAtUtc = lockdownEndAtUtc,
            LockdownEndUnixMs = lockdownEndUnixMs,
            ShieldHealthPercent = shieldHealthPercent
        };
    }

    /// <summary>Decode lockdown end time; returns both UTC DateTime and raw Unix ms. Value can be ASCII numeric string or 8-byte Int64 (big/little-endian).</summary>
    private static (DateTime? utc, long? unixMs) DecodeLockdownEndWithRaw(byte[] value)
    {
        if (value == null || value.Length == 0)
            return (null, null);
        const long minReasonableMs = 1_000_000_000_000L;  // ~2001
        const long maxReasonableMs = 2_500_000_000_000L;  // ~2049

        // 1) Try ASCII numeric string (same as Laravel decodeProperty: game often stores integer as string in bytea)
        var str = Encoding.UTF8.GetString(value).TrimEnd('\0').Trim();
        if (str.Length > 0 && long.TryParse(str, out var parsedMs) && parsedMs >= minReasonableMs && parsedMs <= maxReasonableMs)
            return (DateTimeOffset.FromUnixTimeMilliseconds(parsedMs).UtcDateTime, parsedMs);

        // 2) Try 8-byte binary (big-endian then little-endian)
        if (value.Length >= 8)
        {
            try
            {
                long unixMs = ReadInt64BigEndian(value, 0);
                if (unixMs < minReasonableMs || unixMs > maxReasonableMs)
                    unixMs = BitConverter.ToInt64(value, 0);
                if (unixMs >= minReasonableMs && unixMs <= maxReasonableMs)
                    return (DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime, unixMs);
            }
            catch { }
        }

        return (null, null);
    }

    private static long ReadInt64BigEndian(byte[] bytes, int startIndex)
    {
        return ((long)bytes[startIndex] << 56)
               | ((long)bytes[startIndex + 1] << 48)
               | ((long)bytes[startIndex + 2] << 40)
               | ((long)bytes[startIndex + 3] << 32)
               | ((long)bytes[startIndex + 4] << 24)
               | ((long)bytes[startIndex + 5] << 16)
               | ((long)bytes[startIndex + 6] << 8)
               | bytes[startIndex + 7];
    }

    /// <summary>Compute shield health % from hitHistory.totalDamage and shieldMaxHp. Returns 0–100 or null.</summary>
    private static double? TryComputeShieldHealthPercent(System.Collections.Generic.List<ElementPropertyRow> propRows)
    {
        var hitHistoryProp = propRows.FirstOrDefault(p =>
            string.Equals(p.name, HitHistoryPropertyName, StringComparison.OrdinalIgnoreCase));
        var shieldMaxHpProp = propRows.FirstOrDefault(p =>
            string.Equals(p.name, ShieldMaxHpPropertyName, StringComparison.OrdinalIgnoreCase));
        if (hitHistoryProp.value == null || hitHistoryProp.value.Length == 0 ||
            shieldMaxHpProp.value == null || shieldMaxHpProp.value.Length == 0)
            return null;

        double? totalDamage = null;
        var hitHistoryStr = Encoding.UTF8.GetString(hitHistoryProp.value).TrimEnd('\0').Trim();
        if (hitHistoryStr.Length > 0)
        {
            try
            {
                var jo = JObject.Parse(hitHistoryStr);
                var totalDamageToken = jo["totalDamage"];
                if (totalDamageToken != null && (totalDamageToken.Type == JTokenType.Float || totalDamageToken.Type == JTokenType.Integer))
                    totalDamage = totalDamageToken.Value<double>();
            }
            catch { }
        }

        // Try ASCII string first (game often stores floating as string in bytea, e.g. "500000000")
        double? shieldMaxHp = null;
        var shieldMaxHpStr = Encoding.UTF8.GetString(shieldMaxHpProp.value).TrimEnd('\0').Trim();
        if (shieldMaxHpStr.Length > 0 && double.TryParse(shieldMaxHpStr, out var parsedHp) && parsedHp > 0)
            shieldMaxHp = parsedHp;
        if (!shieldMaxHp.HasValue)
            shieldMaxHp = DecodeDoubleOrFloat(shieldMaxHpProp.value);
        if (totalDamage.HasValue && shieldMaxHp.HasValue && shieldMaxHp.Value > 0)
        {
            var percent = (1.0 - totalDamage.Value / shieldMaxHp.Value) * 100.0;
            return Math.Clamp(percent, 0, 100);
        }
        return null;
    }

    private static double? DecodeDoubleOrFloat(byte[] value)
    {
        if (value == null)
            return null;
        try
        {
            if (value.Length >= 8)
            {
                var d = BitConverter.ToDouble(value, 0);
                if (!double.IsNaN(d) && !double.IsInfinity(d))
                    return d;
            }
            if (value.Length >= 4)
            {
                var f = BitConverter.ToSingle(value, 0);
                if (!float.IsNaN(f) && !float.IsInfinity(f))
                    return (double)f;
            }
        }
        catch { }
        return null;
    }

    private struct ShieldEnabledRow
    {
        public bool shield_enabled { get; set; }
    }

    private struct ElementIdRow
    {
        public long id { get; set; }
    }

    private struct ElementPropertyRow
    {
        public string name { get; set; }
        public int? property_type { get; set; }
        public byte[] value { get; set; }
    }
}

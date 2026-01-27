using System;

namespace Mod.DynamicEncounters.Overrides.ApiClient.Services;

public static class PveModBaseUrl
{
    private static string? _cachedUrl;
    
    public static string GetBaseUrl()
    {
        if (_cachedUrl == null)
        {
            var envUrl = Environment.GetEnvironmentVariable("DYNAMIC_ENCOUNTERS_URL");
            _cachedUrl = envUrl ?? "http://mod_dynamic_encounters:8080";
            
            Console.WriteLine($"[PveModBaseUrl] Using base URL: {_cachedUrl} (from env: {envUrl != null})");
        }
        
        return _cachedUrl;
    }
}
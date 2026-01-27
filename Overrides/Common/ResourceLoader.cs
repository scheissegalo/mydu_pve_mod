using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Mod.DynamicEncounters.Overrides.Common;

public static class ResourceLoader
{
    public static string GetStringContents(string resourceName)
    {
        var assembly = Assembly.GetAssembly(typeof(MyDuMod))!;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // List all available resources for debugging
            var availableResources = assembly.GetManifestResourceNames();
            var availableList = string.Join(", ", availableResources);
            throw new NullReferenceException(
                $"{resourceName} not found or is not an Embedded Resource. " +
                $"Available resources: {availableList}");
        }
        
        var sr = new StreamReader(stream);
        return sr.ReadToEnd();
    }
}
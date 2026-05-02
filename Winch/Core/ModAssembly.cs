using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Winch.Config;
using Winch.Util;

namespace Winch.Core;

public class ModAssembly
{
    public readonly string BasePath;
    public Assembly? LoadedAssembly { get; private set; }

    private readonly Dictionary<string, object> _metadata;

    // Cached metadata fields to avoid repeated dictionary lookups
    private readonly string _guid;
    private readonly string _assemblyRelativePath;
    private readonly string _name;
    private readonly string _author;
    private readonly string _version;
    private readonly string _minWinchVersion;
    private readonly string[] _dependencies;
    private readonly string[] _conflicts;
    private readonly string _preload;
    private readonly string _entrypoint;
    private readonly bool _applyPatches;

    public string AssetsPath => Path.Combine(BasePath, "Assets");
    public string AssemblyName => LoadedAssembly != null ? LoadedAssembly.GetName().Name : string.Empty;
    public string BasePathFolderName => Path.GetFileName(BasePath);
    public string GUID => _guid;
    public string AssemblyRelativePath => _assemblyRelativePath;
    public string Name => _name;
    public string CleanedUpName => _name.Replace("Dredge ", "").Replace("DREDGE ", "").Replace("D R E D G E ", "").Trim();
    public string Author => _author;
    public string Version => _version;
    public string MinWinchVersion => _minWinchVersion;
    public string[] Dependencies => _dependencies;
    public string[] Conflicts => _conflicts;
    public string Preload => _preload;
    public string Entrypoint => _entrypoint;
    public bool ApplyPatches => _applyPatches;
    public ModConfig? Config => ModConfig.TryGetConfig(GUID, out var config) ? config : null;
    public bool DefaultConfig => ModConfig.HasDefaultConfig(GUID);
    public ModConfig GetConfig() => ModConfig.GetConfig(GUID);

    private ModAssembly(string basePath) {
        BasePath = basePath;

        string metaPath = Path.Combine(basePath, "mod_meta.json");
        if (!File.Exists(metaPath))
            throw new FileNotFoundException($"Missing mod_meta.json file at '{basePath}'.");

        string metaText = File.ReadAllText(metaPath);
        _metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(metaText) ?? throw new InvalidOperationException($"Unable to parse mod_meta.json file at '{basePath}'.");

        // Normalize JToken/JValue entries so downstream lookups are simpler
        var keys = _metadata.Keys.ToArray();
        foreach (var k in keys)
        {
            if (_metadata[k] is JToken token)
            {
                try
                {
                    _metadata[k] = token.Type == JTokenType.Array ? token : token.ToObject<object>() ?? token;
                }
                catch
                {
                    _metadata[k] = token;
                }
            }
        }

        // Cache commonly used metadata values to avoid repeated dictionary access
        _guid = GetRequiredMetadataString("ModGUID");
        _assemblyRelativePath = GetRequiredMetadataString("ModAssembly");
        _name = GetRequiredMetadataString("Name").Spaced();
        _author = GetRequiredMetadataString("Author");
        _version = GetRequiredMetadataString("Version");
        _minWinchVersion = GetMetadataString("MinWinchVersion");
        _dependencies = GetMetadataStringArray("Dependencies");
        _conflicts = GetMetadataStringArray("Conflicts");
        _preload = GetMetadataString("Preload");
        _entrypoint = GetMetadataString("Entrypoint");
        _applyPatches = GetMetadataBool("ApplyPatches");

        ModConfig.RegisterBasePath(_guid, BasePath);
    }

    private string GetRequiredMetadataString(string key)
    {
        return GetMetadataString(key) ?? throw new MissingFieldException($"Required metadata field '{key}' is missing or empty in mod_meta.json for '{BasePathFolderName}'.");
    }

    private string GetMetadataString(string key)
    {
        var value = GetMetadata<string>(key);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value;
    }

    private string[] GetMetadataStringArray(string key)
    {
        return GetMetadataWithDefault(key, Array.Empty<string>());
    }

    private bool GetMetadataBool(string key)
    {
        return GetMetadataWithDefault(key, false);
    }

    private int GetMetadataInt(string key)
    {
        return GetMetadataWithDefault(key, 0);
    }

    private double GetMetadataDouble(string key)
    {
        return GetMetadataWithDefault(key, 0);
    }

    private T GetMetadataWithDefault<T>(string key, T defaultValue)
    {
        return GetMetadata<T>(key) ?? defaultValue;
    }

    private T? GetMetadata<T>(string key)
    {
        if (!_metadata.TryGetValue(key, out var raw) || raw == null)
            return default;

        // If the raw value is already the target type, return it
        if (raw is T t)
            return t;

        // Handle JToken conversions
        if (raw is JToken jt)
        {
            try { return jt.ToObject<T>(); } catch { return default; }
        }

        try
        {
            // Attempt JSON round-trip for complex conversions
            var json = JsonConvert.SerializeObject(raw);
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch
        {
            try
            {
                // Final fallback: attempt direct conversion
                return (T)Convert.ChangeType(raw, typeof(T));
            }
            catch
            {
                // If all conversions fail, return defaults
                return default;
            }
        }
    }

    internal static ModAssembly FromPath(string path)
    {
        return new ModAssembly(path);
    }


    internal void LoadAssembly()
    {
        string assemblyRelativePath = AssemblyRelativePath;
        string assemblyPath = Path.Combine(BasePath, assemblyRelativePath);
        if(!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Could not find mod assembly '{assemblyPath}'");

        CheckCompatibility();

        try
        {
            LoadedAssembly = Assembly.LoadFrom(assemblyPath);
            WinchCore.Log.Debug($"Loaded Assembly '{LoadedAssembly?.GetName().Name}' for mod '{GUID}'.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load assembly for mod '{GUID}' from path '{assemblyPath}'", ex);
        }

        if (!string.IsNullOrEmpty(Preload))
        {
            try
            {
                ProcessPreload();
            }
            catch (Exception ex)
            {
                throw new Exception($"Preload failed for mod '{GUID}'", ex);
            }
        }
    }

    internal void ExecuteAssembly()
    {
        if (LoadedAssembly == null)
            throw new NullReferenceException("Cannot execute assembly as LoadedAssembly is null");

        WinchCore.Log.Debug($"Initializing ModAssembly {LoadedAssembly.GetName().Name}...");

        if (Dependencies.Length > 0)
            ProcessDependencies();

        if (Conflicts.Length > 0)
            ProcessConflicts();

        if (!string.IsNullOrEmpty(Entrypoint))
            ProcessEntrypoint();
    }

    internal void CheckCompatibility()
    {
        if (!VersionUtil.ValidateVersion(Version))
            throw new FormatException("Mod Version has invalid format.");

        if (GUID.IsNullOrWhitespace())
            throw new MissingFieldException("No 'ModGUID' field found in Mod Metadata.");

        string minVer = MinWinchVersion;
        string winchVer = VersionUtil.GetVersion();

        if (minVer.IsNullOrWhitespace())
            WinchCore.Log.Warn($"No MinWinchVersion defined for mod '{GUID}'. Current Winch version is ({winchVer}). Mod will load anyway, but version conflicts may occur!");
        else
        {
            if (!VersionUtil.ValidateVersion(minVer))
                throw new FormatException($"MinWinchVersion ({minVer}) not in correct format for mod '{GUID}'.");

            WinchCore.Log.Debug($"Mod '{GUID}' requires Winch ({minVer})+; current ({winchVer}).");

            if (!VersionUtil.IsSameOrNewer(winchVer, minVer))
                throw new Exception($"Mod '{GUID}' requires Winch version ({minVer}) or newer, but the current Winch version is ({winchVer}). Please update Winch or the mod.");
        }
    }



    private void ProcessDependencies()
    {
        string[] deps = Dependencies;
        foreach (string dep in deps)
        {
            WinchCore.Log.Debug($"Processing dependency {dep}");

            if (string.IsNullOrWhiteSpace(dep))
            {
                WinchCore.Log.Error($"Empty dependency entry in mod {GUID}");
                ModAssemblyLoader.ErrorMods.Add(GUID);
                continue;
            }

            var parts = dep.Split(new[] { '@' }, 2);
            string depName = parts.Length > 0 ? parts[0] : string.Empty;
            string? depVersion = parts.Length > 1 ? parts[1] : null;

            WinchCore.Log.Debug($"Dependency name: '{depName}', min version: ({depVersion ?? "any"})");

            if (string.IsNullOrWhiteSpace(depName))
            {
                WinchCore.Log.Error($"Malformed dependency '{dep}' in mod {GUID}");
                ModAssemblyLoader.ErrorMods.Add(GUID);
                continue;
            }

            if (string.IsNullOrWhiteSpace(depVersion))
            {
                WinchCore.Log.Warn($"No minimum version specified for dependency '{depName}' in mod {GUID}. Will attempt to load any version of the dependency, but this may cause version conflicts. Specify by appending '@<version>' to the dependency name in mod_meta.json.");
            }

            bool executed = false;
            try
            {
                executed = ModAssemblyLoader.ExecuteModAssembly(depName, depVersion);
            }
            catch (Exception ex)
            {
                WinchCore.Log.Error($"Failed to execute dependency '{dep}' for mod {GUID}: {ex}");
            }

            if (!executed)
                ModAssemblyLoader.ErrorMods.Add(GUID);
        }
    }

    private void ProcessConflicts()
    {
        string[] conflicts = Conflicts;
        foreach (string conflict in conflicts)
        {
            WinchCore.Log.Debug($"Processing conflict {conflict}");
            if (ModAssemblyLoader.EnabledMods.TryGetValue(conflict, out bool enabled) && enabled)
            {
                WinchCore.Log.Error($"Mod {GUID} conflicts with {conflict}");
                ModAssemblyLoader.ErrorMods.Add(GUID);
            }
        }
    }

    private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private void ProcessEntrypoint()
    {
        string entrypointSetting = Entrypoint;
        if (!entrypointSetting.Contains("/"))
            throw new ArgumentException("Malformed Entrypoint in mod_meta.json");

        var parts = entrypointSetting.Split(new[] { '/' }, 2);
        string entrypointTypeName = parts[0];
        string entrypointMethodName = parts.Length > 1 ? parts[1] : string.Empty;

        Type entrypointType = LoadedAssembly?.GetType(entrypointTypeName) ??
                              throw new EntryPointNotFoundException($"Could not find type {entrypointTypeName} in Mod Assembly");

        var method = entrypointType.GetMethod(entrypointMethodName, AnyStatic);
        if (method == null)
            throw new EntryPointNotFoundException($"Could not find method {entrypointMethodName} in type {entrypointTypeName} in Mod Assembly");

        if (method.GetParameters().Length > 0)
            throw new EntryPointNotFoundException($"Entrypoint method {entrypointMethodName} in {entrypointTypeName} must take no parameters");

        WinchCore.Log.Debug($"Invoking entrypoint {entrypointType}.{entrypointMethodName}...");
        try { method.Invoke(null, Array.Empty<object>()); }
        catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
    }

    private void ProcessPreload()
    {
        string preloadSetting = Preload;
        if (!preloadSetting.Contains("/"))
            throw new ArgumentException("Malformed Preload in mod_meta.json");

        var parts = preloadSetting.Split(new[] { '/' }, 2);
        string preloadTypeName = parts[0];
        string preloadMethodName = parts.Length > 1 ? parts[1] : string.Empty;

        Type preloaderType = LoadedAssembly?.GetType(preloadTypeName) ??
                             throw new EntryPointNotFoundException($"Could not find type {preloadTypeName} in Mod Assembly");

        var method = preloaderType.GetMethod(preloadMethodName, AnyStatic);
        if (method == null)
            throw new EntryPointNotFoundException($"Could not find method {preloadMethodName} in type {preloadTypeName} in Mod Assembly");

        if (method.GetParameters().Length > 0)
            throw new EntryPointNotFoundException($"Preload method {preloadMethodName} in {preloadTypeName} must take no parameters");

        WinchCore.Log.Debug($"Invoking preloader {preloaderType}.{preloadMethodName}...");
        try { method.Invoke(null, Array.Empty<object>()); }
        catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
    }

    public override string ToString() => GUID;
}

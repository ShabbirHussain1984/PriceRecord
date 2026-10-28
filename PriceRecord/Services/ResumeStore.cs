using System.Text.Json;
using Microsoft.Extensions.Logging;
using PriceRecord.Config;
using PriceRecord.Models;

namespace PriceRecord.Services;

public interface IResumeStore
{
    ResumeState? Load();
    void Save(ResumeState state);
    void Clear();
}

/// <summary>Persists run progress to a small JSON file next to the executable.</summary>
public sealed class JsonResumeStore : IResumeStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    private readonly ILogger<JsonResumeStore> _log;
    private readonly object _sync = new();

    public JsonResumeStore(AppSettings settings, ILogger<JsonResumeStore> log)
    {
        _path = Path.IsPathRooted(settings.ResumeFile)
            ? settings.ResumeFile
            : Path.Combine(AppContext.BaseDirectory, settings.ResumeFile);
        _log = log;
    }

    public ResumeState? Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_path)) return null;
            try
            {
                return JsonSerializer.Deserialize<ResumeState>(File.ReadAllText(_path));
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _log.LogWarning(ex, "Ignoring unreadable resume file {Path}", _path);
                return null;
            }
        }
    }

    public void Save(ResumeState state)
    {
        lock (_sync)
        {
            state.UpdatedAt = DateTime.Now;
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));
            File.Move(temp, _path, overwrite: true);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}

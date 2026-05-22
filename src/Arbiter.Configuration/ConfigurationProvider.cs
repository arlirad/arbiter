using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Arbiter.Configuration;

public sealed class ConfigurationProvider : IDisposable
{
    private readonly IDisposable _changeTokenRegistration;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationProvider>? _logger;
    private readonly ConcurrentDictionary<string, Entry> _sections = new();

    public ConfigurationProvider(IConfiguration configuration, ILogger<ConfigurationProvider>? logger = null)
    {
        _configuration = configuration;
        _logger = logger;
        _changeTokenRegistration = ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            OnConfigurationChanged);
    }

    public void Dispose()
    {
        _changeTokenRegistration.Dispose();

        foreach (var entry in _sections.Values)
        {
            entry.Subject.OnCompleted();
            entry.Subject.Dispose();
        }

        _sections.Clear();
    }

    private void OnConfigurationChanged()
    {
        foreach (var (key, entry) in _sections.ToArray())
        {
            var section = _configuration.GetSection(key);
            var currentSnapshot = FlattenSection(section);

            lock (entry.Lock)
            {
                if (currentSnapshot == entry.Snapshot)
                    continue;

                try
                {
                    var value = section.Get(entry.Type);
                    if (value is not null)
                        entry.Subject.OnNext(value);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to bind configuration section '{SectionKey}'", key);
                }

                entry.Snapshot = currentSnapshot;
            }
        }
    }

    public IObservable<T> Observe<T>(string sectionKey)
    {
        var type = typeof(T);

        if (_sections.TryGetValue(sectionKey, out var existing))
            return existing.Type == type
                ? existing.Subject.Where(x => x is not null).Select(x => (T)x!)
                : throw new InvalidOperationException(
                    $"Section '{sectionKey}' is already observed as {existing.Type.Name}, cannot observe as {type.Name}.");

        var subject = new BehaviorSubject<object?>(null);
        var section = _configuration.GetSection(sectionKey);
        var snapshot = FlattenSection(section);

        try
        {
            var initialValue = section.Get(type);
            if (initialValue is not null)
                subject.OnNext(initialValue);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to bind configuration section '{SectionKey}'", sectionKey);
        }

        var entry = new Entry(subject, snapshot, type);

        var actual = _sections.GetOrAdd(sectionKey, entry);

        if (!ReferenceEquals(actual, entry))
        {
            if (actual.Type == type)
            {
                subject.Dispose();

                return actual.Subject.Where(x => x is not null).Select(x => (T)x!);
            }

            throw new InvalidOperationException(
                $"Section '{sectionKey}' is already observed as {actual.Type.Name}, cannot observe as {type.Name}.");
        }

        return subject.Where(x => x is not null).Select(x => (T)x!);
    }

    private static string FlattenSection(IConfigurationSection section)
    {
        var lines = new List<string>();
        FlattenSectionRecursive(section, lines);

        return string.Join("\n", lines);
    }

    private static void FlattenSectionRecursive(IConfigurationSection section, List<string> lines)
    {
        if (section.Value is not null)
            lines.Add($"{section.Path}={section.Value}");

        foreach (var child in section.GetChildren().OrderBy(c => c.Path))
            FlattenSectionRecursive(child, lines);
    }

    private sealed class Entry(BehaviorSubject<object?> subject, string snapshot, Type type)
    {
        public BehaviorSubject<object?> Subject
        {
            get;
        } = subject;
        public string Snapshot
        {
            get;
            set;
        } = snapshot;
        public Type Type
        {
            get;
        } = type;
        public object Lock
        {
            get;
        } = new();
    }
}

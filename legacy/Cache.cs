using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Arbiter.Infrastructure;

public class Cache : ICache
{
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(10);
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, FileEntry> _files = new();
    private readonly ConcurrentDictionary<string, ObjectEntry> _ties = new();
    private readonly ConcurrentDictionary<string, ObjectEntry> _objects = new();
    private readonly ConcurrentDictionary<string, SegmentHandle> _segments = new();

    private readonly Thread _striker;
    private readonly object _lock = new();

    private bool _abort = false;

    public Cache()
    {
        _watcher = new FileSystemWatcher
        {
            Path = Directory.GetCurrentDirectory(),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _watcher.Created += new FileSystemEventHandler(OnChanged);
        _watcher.Changed += new FileSystemEventHandler(OnChanged);
        _watcher.Deleted += new FileSystemEventHandler(OnChanged);
        _watcher.Renamed += new RenamedEventHandler(OnRenamed);

        _striker = new Thread(StrikerLoop);
        _striker.Start();
    }

    ~Cache()
    {
        _abort = true;
    }

    public SegmentHandle WatchSegment(string path)
    {
        path = Path.GetFullPath(path);

        var segment = new SegmentHandle(this, path);
        _segments[path] = segment;

        return segment;
    }

    public Stream? GetFile(string path)
    {
        path = Path.GetFullPath(path);

        if (_files.TryGetValue(path, out FileEntry? entry))
        {
            entry.Accessed(_timeout);
            return new MemoryStream(entry.Bytes);
        }
        else
        {
            lock (_lock)
            {
                try
                {
                    Stream file = File.OpenRead(path);
                    if (file.Length >= 33554432)
                        return file;

                    byte[] bytes = new byte[file.Length];

                    file.Read(bytes, 0, (int)file.Length);
                    file.Close();

                    _files[path] = new FileEntry(_timeout, bytes);

                    return new MemoryStream(bytes);
                }
                catch { return null; }
            }
        }
    }

    public bool GetFile(string path, out Stream? stream)
    {
        stream = GetFile(path);
        return stream != null;
    }

    public void SetTie<T>(string path, T obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        path = Path.GetFullPath(path);

        _ties[path] = new ObjectEntry(_timeout, obj);
    }

    public T? GetTie<T>(string path)
    {
        path = Path.GetFullPath(path);

        if (_ties.TryGetValue(path, out ObjectEntry? entry))
        {
            if (entry.Object.GetType() != typeof(T))
                return default;

            entry.Accessed(_timeout);
            return (T?)entry.Object;
        }
        else
        {
            return default;
        }
    }

    public void SetObject<T>(string identifier, T obj, TimeSpan timeout)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        _objects[identifier] = new ObjectEntry(timeout, obj);
    }

    public T? GetObject<T>(string identifier)
    {
        if (_objects.TryGetValue(identifier, out ObjectEntry? entry))
            return (T?)entry.Object;
        else
            return default;
    }

    public bool GetObject<T>(string identifier, out T? value)
    {
        if (_objects.TryGetValue(identifier, out ObjectEntry? entry))
        {
            value = (T?)entry.Object;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public bool DropObject(string identifier)
    {
        return _objects.TryRemove(identifier, out var _);
    }

    public void WriteDebugInfo(Stream stream)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);

        writer.WriteLine("<style>");
        writer.WriteLine("table td, table td * { vertical-align: top; }");
        // writer.WriteLine("* { font-family: \"Verdana\", \"sans-serif\" }");
        writer.WriteLine("</style>");
        writer.WriteLine("<table>");

        writer.WriteLine("<td colspan=\"3\"><h1>cached files</h1></td>");
        writer.WriteLine($"<tr><th>Path</th><th>Type</th><th>Timeout</th></tr>");

        foreach (var entry in _files)
            writer.WriteLine($"<tr><td>{entry.Key}</td><td>N/A</td><td>{entry.Value.TimeoutAt}</td></tr>");

        writer.WriteLine("<td colspan=\"3\"><hr><h1>cached ties</h1></td>");
        writer.WriteLine($"<tr><th>Path</th><th>Type</th><th>Timeout</th></tr>");

        foreach (var entry in _ties)
            writer.WriteLine($"<tr><td>{entry.Key}</td><td>{entry.Value.Object.GetType()}</td><td>{entry.Value.TimeoutAt}</td></tr>");

        writer.WriteLine("<td colspan=\"3\"><hr><h1>cached objects</h1></td>");
        writer.WriteLine($"<tr><th>Path</th><th>Type</th><th>Timeout</th></tr>");

        foreach (var entry in _objects)
            writer.WriteLine($"<tr><td>{entry.Key}</td><td>{entry.Value.Object.GetType()}</td><td>{entry.Value.TimeoutAt}</td></tr>");

        writer.WriteLine("<td colspan=\"3\"><hr></td>");
        writer.WriteLine("</table>");
    }

    private void OnChanged(object source, FileSystemEventArgs e)
    {
        StrikeFile(e.FullPath);

        foreach (var pair in _segments)
        {
            if (!e.FullPath.StartsWith(pair.Key))
                continue;

            pair.Value.Changed(_watcher, e.FullPath);
        }
    }

    private void OnRenamed(object source, RenamedEventArgs e)
    {
        StrikeFile(e.OldFullPath);

        foreach (var pair in _segments)
        {
            if (!e.OldFullPath.StartsWith(pair.Key))
                continue;

            pair.Value.Changed(_watcher, e.OldFullPath);
            pair.Value.Changed(_watcher, e.FullPath);
        }
    }

    private void StrikeFile(string path)
    {
        _files.Remove(path, out FileEntry _);
        _ties.Remove(path, out ObjectEntry _);
    }

    private void StrikerLoop()
    {
        while (true)
        {
            if (_abort)
                return;

            var now = DateTime.Now;

            foreach (var pair in _files)
            {
                if (now >= pair.Value.TimeoutAt)
                    StrikeFile(pair.Key);
            }

            foreach (var pair in _ties)
            {
                if (now >= pair.Value.TimeoutAt)
                    StrikeFile(pair.Key);
            }

            foreach (var pair in _objects)
            {
                if (now >= pair.Value.TimeoutAt)
                    _objects.Remove(pair.Key, out ObjectEntry _);
            }

            Thread.Sleep(5000);
        }
    }
}

public class FileEntry
{
    public DateTime TimeoutAt;
    public byte[] Bytes;

    public FileEntry(TimeSpan timeout, byte[] bytes)
    {
        TimeoutAt = DateTime.Now + timeout;
        Bytes = bytes;
    }

    public void Accessed(TimeSpan timeout)
    {
        TimeoutAt = DateTime.Now + timeout;
    }
}

public class ObjectEntry
{
    public DateTime TimeoutAt;
    public object Object;

    public ObjectEntry(TimeSpan timeout, object @object)
    {
        TimeoutAt = DateTime.Now + timeout;
        Object = @object;
    }

    public void Accessed(TimeSpan timeout)
    {
        TimeoutAt = DateTime.Now + timeout;
    }
}

public class SegmentHandle
{
    public delegate void OnChangedHandler(object sender, string path);
    public event OnChangedHandler? OnChanged;

    private Cache _cache;
    private string _path;
    private bool _started;

    public SegmentHandle(Cache cache, string path)
    {
        _cache = cache;
        _path = path;
    }

    public void Start()
    {
        _started = true;

        foreach (var file in Directory.EnumerateFiles(_path, "*.*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
        }))

            Changed(_cache, Path.GetFullPath(file));
    }

    public void Changed(object sender, string path)
    {
        if (!_started)
            return;

        OnChanged?.Invoke(sender, path);
    }
}
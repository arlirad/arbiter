using System.Runtime.Serialization;

namespace Arbiter;

public interface ISlottable
{
    public Task Deserialize(Stream source);
    public Task Serialize(Stream destination);
}

public class Slot<T>
{
    public int Index
    {
        get;
        private set;
    }
    public Slotter Owner
    {
        get;
        private set;
    }
    public T Value
    {
        get;
        private set;
    }

    public Slot(int index, Slotter owner, T value)
    {
        Index = index;
        Owner = owner;
        Value = value;
    }

    ~Slot()
    {
        Console.WriteLine("finalized");
    }
}

public class Slotter
{
    public int Count
    {
        get => (int)(_stream.Length / _recordSize);
    }

    private int _recordSize;
    private Stream _stream;
    private SemaphoreSlim _sem;
    private Dictionary<int, ISlottable> _cache;
    private Dictionary<int, ISlottable> _backing;

    public Slotter(Stream stream, int recordSize, bool create = false)
    {
        _recordSize = recordSize;
        _stream = stream;
        _sem = new SemaphoreSlim(1, 1);
        _cache = new Dictionary<int, ISlottable>();
    }

    public async Task SetAsync<T>(int index, ISlottable value)
    {
        await _sem.WaitAsync();

        _cache[index] = value;
        SeekTo(index);

        try
        {
            await value.Serialize(_stream);
        }
        catch
        {
            _sem.Release();
            throw;
        }

        _sem.Release();
        _ = _stream.FlushAsync();
    }

    public async Task<int> AppendAsync<T>(ISlottable value)
    {
        await _sem.WaitAsync();

        int index = Count;

        _cache[index] = value;
        SeekTo(index);

        try
        {
            await value.Serialize(_stream);
        }
        catch
        {
            _sem.Release();
            throw;
        }

        _sem.Release();
        _ = _stream.FlushAsync();

        return index;
    }

    public async Task<Slot<T>?> GetAsync<T>(int index) where T : ISlottable, new()
    {
        await _sem.WaitAsync();

        if (index >= Count)
        {
            _sem.Release();
            return null;
        }

        if (_cache.TryGetValue(index, out ISlottable? value))
        {
            _sem.Release();
            return new Slot<T>(index, this, (T)value);
        }

        SeekTo(index);

        var read = new T();

        await read.Deserialize(_stream);

        _cache[index] = read;

        _sem.Release();
        return new Slot<T>(index, this, (T)read);
    }

    private void SeekTo(int index)
    {
        _stream.Seek(index * _recordSize, SeekOrigin.Begin);
    }
}
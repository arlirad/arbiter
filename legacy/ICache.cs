namespace Arbiter.Infrastructure;

public interface ICache
{
    bool DropObject(string identifier);
    Stream? GetFile(string path);
    bool GetFile(string path, out Stream? stream);
    T? GetObject<T>(string identifier);
    bool GetObject<T>(string identifier, out T? value);
    T? GetTie<T>(string path);
    void SetObject<T>(string identifier, T obj, TimeSpan timeout);
    void SetTie<T>(string path, T obj);
    SegmentHandle WatchSegment(string path);
    void WriteDebugInfo(Stream stream);
}
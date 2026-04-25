namespace Arbiter.Api.Http;

public interface IFormFile
{
    string ContentType
    {
        get;
    }
    string FileName
    {
        get;
    }
    string Name
    {
        get;
    }
    long Length
    {
        get;
    }
    Stream OpenReadStream();
}

public class FormFile(Stream stream, string contentType, string fileName, string name) : IFormFile
{
    private readonly Stream _stream = stream;

    public string ContentType
    {
        get;
    } = contentType;
    public string FileName
    {
        get;
    } = fileName;
    public string Name
    {
        get;
    } = name;
    public long Length => _stream.Length;
    public Stream OpenReadStream() => _stream;
}
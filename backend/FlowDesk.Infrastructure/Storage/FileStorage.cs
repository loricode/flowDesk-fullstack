using Amazon.S3;
using Amazon.S3.Model;
using FlowDesk.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"Local" (desarrollo) o "S3".</summary>
    public string Provider { get; set; } = "Local";
    public string LocalRootPath { get; set; } = "storage";
    public string BucketName { get; set; } = "flowdesk-documents";
}

/// <summary>Almacenamiento en disco para desarrollo local, con protección contra path traversal.</summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalRootPath);

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (!File.Exists(path)) throw new FileNotFoundException("El archivo no existe en el almacenamiento.", key);
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Clave de almacenamiento inválida.");
        return full;
    }
}

/// <summary>Almacenamiento en AWS S3 con cifrado del lado del servidor.</summary>
public sealed class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private string Bucket => options.Value.BucketName;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        }, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        using var response = await s3.GetObjectAsync(Bucket, key, ct);
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }

    public Task DeleteAsync(string key, CancellationToken ct) => s3.DeleteObjectAsync(Bucket, key, ct);
}

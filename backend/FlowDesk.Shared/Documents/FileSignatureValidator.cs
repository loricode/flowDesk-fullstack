namespace FlowDesk.Shared.Documents;

public sealed record FileValidationResult(bool IsValid, string? ContentType, string? Error)
{
    public static FileValidationResult Ok(string contentType) => new(true, contentType, null);
    public static FileValidationResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Reglas de formato compartidas por la app de escritorio (validación previa al envío),
/// la API (validación al recibir) y el Worker (validación asíncrona). La extensión no basta:
/// se comprueba la firma binaria ("magic number") del contenido.
/// </summary>
public static class FileSignatureValidator
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    public const int HeaderLength = 8;

    private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
    };

    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] TiffLittleEndian = [0x49, 0x49, 0x2A, 0x00];
    private static readonly byte[] TiffBigEndian = [0x4D, 0x4D, 0x00, 0x2A];

    public static IReadOnlyCollection<string> AllowedExtensions => ExtensionToContentType.Keys;

    public static bool IsImage(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public static string? ContentTypeFromExtension(string fileName) =>
        ExtensionToContentType.TryGetValue(Path.GetExtension(fileName), out var ct) ? ct : null;

    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfSignature)) return "application/pdf";
        if (header.StartsWith(PngSignature)) return "image/png";
        if (header.StartsWith(JpegSignature)) return "image/jpeg";
        if (header.StartsWith(TiffLittleEndian) || header.StartsWith(TiffBigEndian)) return "image/tiff";
        return null;
    }

    public static FileValidationResult Validate(string fileName, long sizeBytes, ReadOnlySpan<byte> header)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return FileValidationResult.Fail("El archivo no tiene nombre.");

        var expected = ContentTypeFromExtension(fileName);
        if (expected is null)
            return FileValidationResult.Fail($"Formato no permitido. Use: {string.Join(", ", AllowedExtensions)}.");

        if (sizeBytes <= 0)
            return FileValidationResult.Fail("El archivo está vacío.");

        if (sizeBytes > MaxFileSizeBytes)
            return FileValidationResult.Fail($"El archivo supera el máximo de {MaxFileSizeBytes / 1024 / 1024} MB.");

        var detected = DetectContentType(header);
        if (detected is null || detected != expected)
            return FileValidationResult.Fail("El contenido del archivo no corresponde con su extensión.");

        return FileValidationResult.Ok(detected);
    }

    public static async Task<FileValidationResult> ValidateAsync(
        string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var header = new byte[HeaderLength];
        var read = await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
        if (content.CanSeek) content.Position = 0;
        var size = content.CanSeek ? content.Length : read;
        return Validate(fileName, size, header.AsSpan(0, read));
    }
}

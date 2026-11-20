using FlowDesk.Infrastructure.Security;
using FlowDesk.Shared.Documents;

namespace FlowDesk.Tests.Shared;

public class FileSignatureValidatorTests
{
    private static readonly byte[] Pdf = "%PDF-1.7"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];
    private static readonly byte[] Tiff = [0x49, 0x49, 0x2A, 0x00, 0, 0, 0, 0];

    public static TheoryData<string, byte[], string> ValidFiles => new()
    {
        { "contrato.pdf", Pdf, "application/pdf" },
        { "foto.PNG", Png, "image/png" },
        { "scan.jpeg", Jpeg, "image/jpeg" },
        { "scan.tif", Tiff, "image/tiff" },
    };

    [Theory]
    [MemberData(nameof(ValidFiles))]
    public void Accepts_known_formats(string name, byte[] header, string expected)
    {
        var result = FileSignatureValidator.Validate(name, 1024, header);
        Assert.True(result.IsValid, result.Error);
        Assert.Equal(expected, result.ContentType);
    }

    [Fact]
    public void Rejects_disguised_file()
    {
        var result = FileSignatureValidator.Validate("factura.pdf", 1024, Png);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("hoja.docx")]
    [InlineData("sin-extension")]
    public void Rejects_unsupported_extensions(string name) =>
        Assert.False(FileSignatureValidator.Validate(name, 1024, Pdf).IsValid);

    [Fact]
    public void Rejects_empty_and_oversized_files()
    {
        Assert.False(FileSignatureValidator.Validate("a.pdf", 0, Pdf).IsValid);
        Assert.False(FileSignatureValidator.Validate("a.pdf", FileSignatureValidator.MaxFileSizeBytes + 1, Pdf).IsValid);
    }

    [Fact]
    public async Task Validates_streams_and_rewinds_them()
    {
        using var stream = new MemoryStream([.. Pdf, 1, 2, 3]);
        var result = await FileSignatureValidator.ValidateAsync("a.pdf", stream);
        Assert.True(result.IsValid);
        Assert.Equal(0, stream.Position);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_verifies_and_uses_random_salt()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var a = hasher.Hash("Secreta123!");
        var b = hasher.Hash("Secreta123!");

        Assert.NotEqual(a, b);
        Assert.True(hasher.Verify("Secreta123!", a));
        Assert.False(hasher.Verify("otra", a));
        Assert.False(hasher.Verify("Secreta123!", "basura"));
    }
}

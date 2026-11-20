using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Documents;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Domain.Identity;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Services;
using FlowDesk.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Tests.Integration;

/// <summary>Pruebas de integración sobre SQLite en memoria: persistencia real, Outbox y Worker.</summary>
public sealed class DocumentProcessingTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly InMemoryStorage _storage = new();
    private FlowDeskDbContext _db = default!;
    private User _user = default!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new FlowDeskDbContext(new DbContextOptionsBuilder<FlowDeskDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _user = new User("op@test.local", "Operador", "hash", DateTimeOffset.UtcNow);
        _db.Users.Add(_user);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<(Expedient Expedient, Document Document)> UploadAsync(Expedient? expedient, string name, byte[] content)
    {
        expedient ??= Expedient.Create($"EXP-{Guid.NewGuid():N}"[..20], "T", "A", null, ExpedientPriority.Normal, _user.Id, null, DateTimeOffset.UtcNow);
        if (_db.Entry(expedient).State == EntityState.Detached) _db.Expedients.Add(expedient);

        var key = $"k/{Guid.NewGuid():N}";
        await _storage.SaveAsync(key, new MemoryStream(content), "application/pdf", default);
        var doc = expedient.AddDocument(name, "application/pdf", key, content.Length, DocumentSources.DesktopFile, _user.Id, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync();
        return (expedient, doc);
    }

    private DocumentProcessingService Service() =>
        new(_db, _storage, new AuditWriter(_db, new SystemCurrentUser(), new SystemClock()));

    [Fact]
    public async Task Saving_aggregate_writes_domain_events_to_outbox_in_same_transaction()
    {
        await UploadAsync(null, "a.pdf", "%PDF-1.7 uno"u8.ToArray());

        var types = await _db.OutboxMessages.Select(m => m.Type).ToListAsync();
        Assert.Contains(nameof(ExpedientCreated), types);
        Assert.Contains(nameof(DocumentUploaded), types);
    }

    [Fact]
    public async Task Valid_document_gets_hash_and_is_validated()
    {
        var (_, doc) = await UploadAsync(null, "a.pdf", "%PDF-1.7 uno"u8.ToArray());

        var result = await Service().ProcessAsync(doc.Id, 1, default);

        Assert.Equal(DocumentStatus.Validated, result!.Status);
        Assert.NotNull((await _db.DocumentVersions.SingleAsync(v => v.DocumentId == doc.Id)).Sha256);
    }

    [Fact]
    public async Task Identical_content_in_same_expedient_is_flagged_as_duplicate()
    {
        var bytes = "%PDF-1.7 mismo contenido"u8.ToArray();
        var (expedient, first) = await UploadAsync(null, "original.pdf", bytes);
        await Service().ProcessAsync(first.Id, 1, default);

        var (_, copy) = await UploadAsync(expedient, "copia.pdf", bytes);
        var result = await Service().ProcessAsync(copy.Id, 1, default);

        Assert.Equal(DocumentStatus.Invalid, result!.Status);
        Assert.Contains("original.pdf", result.Message);
    }

    [Fact]
    public async Task Processing_twice_is_a_no_op()
    {
        var (_, doc) = await UploadAsync(null, "a.pdf", "%PDF-1.7 x"u8.ToArray());
        Assert.NotNull(await Service().ProcessAsync(doc.Id, 1, default));
        Assert.Null(await Service().ProcessAsync(doc.Id, 1, default));
    }

    private sealed class InMemoryStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = [];

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            _files[key] = ms.ToArray();
        }

        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream(_files[key]));

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            _files.Remove(key);
            return Task.CompletedTask;
        }
    }
}

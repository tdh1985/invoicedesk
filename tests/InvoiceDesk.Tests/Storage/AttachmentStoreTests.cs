using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Tests.TestSupport;

namespace InvoiceDesk.Tests.Storage;

public class AttachmentStoreTests
{
    [Theory]
    [InlineData("virus.exe", 10)]
    [InlineData("noextension", 10)]
    [InlineData("big.pdf", AttachmentStore.MaxBytes + 1)]
    [InlineData("empty.pdf", 0)]
    public void rejects_bad_files(string name, long size) =>
        Assert.NotNull(AttachmentStore.ValidationError(name, size));

    [Theory]
    [InlineData("a.PDF")]
    [InlineData("b.jpeg")]
    [InlineData("c.jpg")]
    [InlineData("d.png")]
    [InlineData("e.webp")]
    public void accepts_receipt_types(string name) =>
        Assert.Null(AttachmentStore.ValidationError(name, 1000));

    [Fact]
    public async Task stage_rejects_invalid_file()
    {
        await using var env = await TestEnv.CreateAsync();
        var store = env.Get<AttachmentStore>();

        await Assert.ThrowsAsync<ValidationException>(() =>
            store.StageAsync(new MemoryStream([1, 2, 3]), "x.exe", 3));
    }

    [Fact]
    public async Task stage_then_import_copies_into_year_folder()
    {
        await using var env = await TestEnv.CreateAsync();
        var store = env.Get<AttachmentStore>();
        var bytes = "%PDF-1.4 test"u8.ToArray();

        var staged = await store.StageAsync(new MemoryStream(bytes), "Receipt March.pdf", bytes.Length);
        Assert.True(File.Exists(staged.TempPath));
        Assert.StartsWith("staging/", staged.RelativePath);

        var att = store.Import(staged.TempPath, staged.OriginalFileName, AttachmentKind.Receipt);

        Assert.StartsWith("attachments/2026/", att.StoredPath);
        Assert.EndsWith(".pdf", att.StoredPath);
        Assert.Equal("application/pdf", att.ContentType);
        Assert.Equal(bytes.Length, att.SizeBytes);
        Assert.Equal("Receipt March.pdf", att.OriginalFileName);
        Assert.Equal(AttachmentKind.Receipt, att.Kind);
        Assert.Equal(bytes, File.ReadAllBytes(store.FullPath(att)));
        Assert.True(File.Exists(staged.TempPath));
    }

    [Fact]
    public async Task delete_file_is_idempotent()
    {
        await using var env = await TestEnv.CreateAsync();
        var store = env.Get<AttachmentStore>();
        var staged = await store.StageAsync(new MemoryStream([1, 2, 3]), "photo.jpg", 3);
        var att = store.Import(staged.TempPath, staged.OriginalFileName, AttachmentKind.Receipt);

        store.DeleteFile(att);
        store.DeleteFile(att);

        Assert.False(File.Exists(store.FullPath(att)));
    }
}

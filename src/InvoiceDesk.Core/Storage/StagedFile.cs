// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Storage;

// a file picked in the ui but not saved yet, kept in staging so it can be previewed
public sealed record StagedFile(string TempPath, string OriginalFileName, long SizeBytes, string RelativePath)
{
    public string ContentType => AttachmentStore.ContentTypeFor(OriginalFileName);
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}

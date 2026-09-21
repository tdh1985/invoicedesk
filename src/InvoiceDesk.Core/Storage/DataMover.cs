// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Storage;

public sealed class DataMover(AppPaths paths)
{
    // copies rather than moves so the old folder stays behind as a spare
    public void MoveTo(string target)
    {
        var destination = Path.GetFullPath(target);
        if (DataLocation.SamePath(destination, paths.DataRoot))
            throw new ValidationException("Your data is already in that folder.");
        if (DataLocation.IsInside(destination, paths.DataRoot) || DataLocation.IsInside(paths.DataRoot, destination))
            throw new ValidationException("Choose a folder that isn't inside, or around, the current data folder.");
        if (DataLocation.HasData(destination))
            throw new ValidationException("That folder already has InvoiceDesk data. Use \"Use data from another PC\" to switch to it instead.");

        Directory.CreateDirectory(destination);
        var copy = new AppPaths(destination, paths.LocalRoot);
        SqliteCopy.To(paths.Database, copy.Database);
        CopyFolder(paths.Attachments, copy.Attachments);
        CopyFolder(paths.Exports, copy.Exports);
        CopyFolder(paths.Backups, copy.Backups);

        DataLocation.Save(paths.LocalRoot, destination);
    }

    public void UseExisting(string folder)
    {
        var found = FindDataFolder(folder)
                    ?? throw new ValidationException("No InvoiceDesk data was found there. Pick the folder that contains invoicedesk.db.");
        DataLocation.Save(paths.LocalRoot, found);
    }

    public void UseThisPc() => DataLocation.Save(paths.LocalRoot, null);

    // people often pick the parent folder, so look one level down as well
    public static string? FindDataFolder(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        if (DataLocation.HasData(folder)) return Path.GetFullPath(folder);
        var child = Path.Combine(folder, "InvoiceDesk");
        return DataLocation.HasData(child) ? Path.GetFullPath(child) : null;
    }

    static void CopyFolder(string from, string to)
    {
        if (!Directory.Exists(from)) return;
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }
}

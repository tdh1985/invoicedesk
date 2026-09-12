using System.IO;

namespace InvoiceDesk.App.Host;

public static class FileLog
{
    static readonly Lock Gate = new();

    public static string Folder { get; private set; } = "";

    public static void Initialise(string folder) => Folder = folder;

    public static void Write(Exception? ex, string context)
    {
        if (ex is null || Folder.Length == 0) return;
        try
        {
            var now = DateTime.Now;
            var entry = $"[{now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            lock (Gate) File.AppendAllText(Path.Combine(Folder, $"{now:yyyy-MM-dd}.log"), entry);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace InvoiceDesk.App.Host;

// the few things only windows itself can do: open files, explorer, clipboard, save dialog
public sealed class Desktop
{
    public void OpenFile(string path) => Start(path);

    public void OpenUrl(string url) => Start(url);

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Start(path);
    }

    public void ShowInFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    public bool CopyText(string text)
    {
        // the clipboard can be briefly held by another app
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }
        return false;
    }

    public string? SaveFileAs(string defaultName, string initialFolder)
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultName,
            InitialDirectory = initialFolder,
            Filter = "PDF document (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    static void Start(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}

// the main window handle, needed by the hidden webview that prints pdfs
public sealed class HostWindow
{
    public IntPtr Handle { get; set; }
}

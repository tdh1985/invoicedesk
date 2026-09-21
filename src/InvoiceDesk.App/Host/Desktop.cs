// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace InvoiceDesk.App.Host;

// shell and dialog calls the webview can't make on its own
public sealed class Desktop
{
    public void OpenFile(string path) => Start(path);

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Start(path);
    }

    public void ShowInFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

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

    public string? PickFolder(string title, string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder)) dialog.InitialDirectory = initialFolder;
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    // a fresh process reads the new data folder from the pointer file
    public void Restart()
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false });
        Application.Current.Shutdown();
    }

    static void Start(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}

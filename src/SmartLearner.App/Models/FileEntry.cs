using System.IO;
using MaterialDesignThemes.Wpf;

namespace SmartLearner.App.Models;

/// <summary>课程文件夹里的一个条目（文件或子文件夹）。</summary>
public sealed class FileEntry
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsFolder { get; init; }

    public long Size { get; init; }

    public string Extension => IsFolder ? string.Empty : Path.GetExtension(Name).ToLowerInvariant();

    public string SizeText => IsFolder ? "文件夹" : FormatSize(Size);

    public PackIconKind Icon => IsFolder
        ? PackIconKind.FolderOutline
        : Extension switch
        {
            ".pdf" => PackIconKind.FilePdfBox,
            ".docx" => PackIconKind.FileWordOutline,
            ".doc" => PackIconKind.FileWordOutline,
            ".md" => PackIconKind.NoteTextOutline,
            ".txt" => PackIconKind.NoteTextOutline,
            _ => PackIconKind.FileOutline
        };

    private static string FormatSize(long size) => size switch
    {
        < 1024 => $"{size} B",
        < 1024 * 1024 => $"{size / 1024.0:0.#} KB",
        _ => $"{size / 1024.0 / 1024.0:0.#} MB"
    };
}

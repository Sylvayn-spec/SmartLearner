using System.IO;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

/// <summary>
/// 课程文档的文件操作：列举、导入、新建笔记、删除（进回收站）。
/// 所有路径都限制在课程文件夹内，防止 ../ 越界。
/// </summary>
public sealed class DocumentService
{
    private readonly CourseService _courses;

    public DocumentService(CourseService courses) => _courses = courses;

    public string GetRoot(Course course) => _courses.GetCourseFolder(course.Name);

    public IReadOnlyList<FileEntry> List(Course course, string relativePath)
    {
        var directory = Resolve(course, relativePath);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<FileEntry>();
        }

        var folders = Directory.GetDirectories(directory).Select(path => new FileEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsFolder = true
        });

        var files = Directory.GetFiles(directory).Select(path => new FileEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            Size = new FileInfo(path).Length
        });

        return folders
            .OrderBy(f => f.Name, StringComparer.CurrentCulture)
            .Concat(files.OrderBy(f => f.Name, StringComparer.CurrentCulture))
            .ToList();
    }

    /// <summary>把相对路径解析成绝对路径，并确保不越出课程根目录。</summary>
    public string Resolve(Course course, string? relativePath)
    {
        var root = Path.GetFullPath(GetRoot(course));
        var combined = Path.GetFullPath(Path.Combine(root, relativePath ?? string.Empty));
        return combined.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? combined : root;
    }

    public string ToRelative(Course course, string fullPath)
    {
        var root = Path.GetFullPath(GetRoot(course));
        var relative = Path.GetRelativePath(root, fullPath);
        return relative == "." ? string.Empty : relative;
    }

    public string Import(Course course, string relativePath, string sourcePath)
    {
        // 导入的资料统一放到 TextBook
        var directory = Path.Combine(GetRoot(course), "TextBook");
        Directory.CreateDirectory(directory);

        var fileName = AppPaths.SanitizeFileName(Path.GetFileName(sourcePath));
        var target = Path.Combine(directory, fileName);
        var index = 1;
        while (File.Exists(target))
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            target = Path.Combine(directory, $"{stem} ({index++}){extension}");
        }

        File.Copy(sourcePath, target);
        return target;
    }

    public string CreateNote(Course course, string relativePath, string title)
    {
        // 笔记统一放到 Note
        var directory = Path.Combine(GetRoot(course), "Note");
        Directory.CreateDirectory(directory);

        var safeTitle = AppPaths.SanitizeFileName(title, "未命名笔记");
        var target = Path.Combine(directory, safeTitle + ".txt");
        var index = 1;
        while (File.Exists(target))
        {
            target = Path.Combine(directory, $"{safeTitle} ({index++}).txt");
        }

        File.WriteAllText(target, $"{title}{Environment.NewLine}{Environment.NewLine}");
        return target;
    }

    public void SaveText(string path, string text) => File.WriteAllText(path, text);

    public string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Logger.Error($"读取文件失败：{path}", ex);
            return null;
        }
    }

    /// <summary>移入回收站，而不是直接删除。</summary>
    public void MoveToTrash(FileEntry entry)
    {
        Directory.CreateDirectory(AppPaths.Trash);
        var target = Path.Combine(AppPaths.Trash, $"{DateTime.Now:yyyyMMddHHmmss}_{entry.Name}");

        if (entry.IsFolder)
        {
            Directory.Move(entry.FullPath, target);
        }
        else
        {
            File.Move(entry.FullPath, target);
        }
    }
}

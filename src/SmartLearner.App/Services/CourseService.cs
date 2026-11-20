using System.IO;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

/// <summary>
/// 课程的唯一真相源（D11）。负责读写 courses.json，并同步 Documents 下的课程文件夹。
/// </summary>
public sealed class CourseService
{
    private readonly List<Course> _courses = new();

    public IReadOnlyList<Course> Courses => _courses;

    public event EventHandler? Changed;

    public CourseService()
    {
        Load();
    }

    public void Load()
    {
        var file = JsonStore.Load(AppPaths.CoursesFile, () => new CourseFile());
        _courses.Clear();
        _courses.AddRange(file.Items);

        // 修复历史数据：课程存在但文件夹缺失时补齐
        foreach (var course in _courses)
        {
            EnsureCourseFolder(course.Name);
        }
    }

    public void Reload() => Load();

    public IReadOnlyList<Course> GetToday()
    {
        var today = DateTime.Today.DayOfWeek;
        return _courses
            .Where(c => c.Day == today)
            .OrderBy(c => c.Start)
            .ToList();
    }

    public IReadOnlyList<Course> GetByDay(DayOfWeek day) =>
        _courses.Where(c => c.Day == day).OrderBy(c => c.Start).ToList();

    /// <summary>校验一批待新增课程，返回第一条错误；通过返回 null。</summary>
    public string? Validate(IReadOnlyList<Course> incoming)
    {
        if (incoming.Count == 0)
        {
            return "至少添加一门课程";
        }

        foreach (var course in incoming)
        {
            if (string.IsNullOrWhiteSpace(course.Name))
            {
                return "课程名不能为空";
            }

            if (course.Name.Trim().Length > 30)
            {
                return "课程名不能超过 30 个字符";
            }

            if (course.End <= course.Start)
            {
                return $"「{course.Name}」的结束时间必须晚于开始时间";
            }
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var course in incoming)
        {
            if (!names.Add(course.Name.Trim()))
            {
                return $"「{course.Name}」重复添加了";
            }
        }

        foreach (var course in incoming)
        {
            if (_courses.Any(c => string.Equals(c.Name.Trim(), course.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return $"已经存在课程「{course.Name}」";
            }
        }

        // 时间允许重叠（例如同一时段的不同课程/实验），这里不再做冲突校验。
        return null;
    }

    /// <summary>校验单门课程（编辑时排除自身）。返回 null 表示通过。</summary>
    public string? ValidateSingle(Course candidate, string? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(candidate.Name))
        {
            return "课程名不能为空";
        }

        if (candidate.Name.Trim().Length > 30)
        {
            return "课程名不能超过 30 个字符";
        }

        if (candidate.End <= candidate.Start)
        {
            return "结束时间必须晚于开始时间";
        }

        if (_courses.Any(c =>
                c.Id != excludeId &&
                string.Equals(c.Name.Trim(), candidate.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return $"已经存在课程「{candidate.Name}」";
        }

        // 时间允许重叠，只校验重名。
        return null;
    }

    /// <summary>更新一门课程。返回 null 表示成功，否则返回错误信息。</summary>
    public string? Update(Course course)
    {
        var error = ValidateSingle(course, course.Id);
        if (error is not null)
        {
            return error;
        }

        var index = _courses.FindIndex(c => c.Id == course.Id);
        if (index < 0)
        {
            return "课程不存在";
        }

        _courses[index] = course;
        EnsureCourseFolder(course.Name);
        Save();
        return null;
    }

    /// <summary>删除课程。deleteFolder 为 true 时一并删除资料文件夹。</summary>
    public void Delete(Course course, bool deleteFolder)
    {
        if (!_courses.Remove(course))
        {
            return;
        }

        if (deleteFolder)
        {
            try
            {
                var folder = GetCourseFolder(course.Name);
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"删除课程文件夹失败：{course.Name}", ex);
            }
        }

        Save();
    }

    /// <summary>新增一批课程：写文件 + 建文件夹 + 通知。</summary>
    public void AddRange(IEnumerable<Course> courses)
    {
        var list = courses.ToList();
        foreach (var course in list)
        {
            course.Name = course.Name.Trim();
            EnsureCourseFolder(course.Name);
            _courses.Add(course);
        }

        Save();
    }

    public void Remove(Course course)
    {
        if (_courses.Remove(course))
        {
            Save();
        }
    }

    public string GetCourseFolder(string courseName) =>
        Path.Combine(AppPaths.Documents, AppPaths.SanitizeFileName(courseName));

    private void EnsureCourseFolder(string courseName)
    {
        try
        {
            var root = GetCourseFolder(courseName);
            Directory.CreateDirectory(root);
            var noteDir = Path.Combine(root, "Note");
            var bookDir = Path.Combine(root, "TextBook");
            Directory.CreateDirectory(noteDir);
            Directory.CreateDirectory(bookDir);
            MigrateRootFiles(root, noteDir, bookDir);
        }
        catch (Exception ex)
        {
            Logger.Error($"创建课程文件夹失败：{courseName}", ex);
        }
    }

    /// <summary>把早期放在课程根目录的文件归位：笔记 → Note，资料 → TextBook。</summary>
    private static void MigrateRootFiles(string root, string noteDir, string bookDir)
    {
        foreach (var file in Directory.GetFiles(root))
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            var target = extension is ".md" or ".txt"
                ? noteDir
                : extension is ".pdf" or ".docx"
                    ? bookDir
                    : null;

            if (target is null)
            {
                continue;
            }

            var destination = Path.Combine(target, Path.GetFileName(file));
            if (File.Exists(destination))
            {
                continue;
            }

            File.Move(file, destination);
            Logger.Info($"已归档：{Path.GetFileName(file)} → {Path.GetFileName(target)}");
        }
    }

    private void Save()
    {
        JsonStore.Save(AppPaths.CoursesFile, new CourseFile { Items = _courses });
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

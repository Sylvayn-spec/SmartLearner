using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;
using SmartLearner.App.Services;

namespace SmartLearner.App.ViewModels;

public partial class OrganizeViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settings;
    private readonly CourseService _courses;
    private readonly OrganizeService _organize;
    private readonly UiState _ui;

    private CancellationTokenSource? _cancellation;

    public UiState Ui => _ui;

    public MaterialDesignThemes.Wpf.PackIconKind FullscreenIcon =>
        _ui.IsReaderFullscreen
            ? MaterialDesignThemes.Wpf.PackIconKind.FullscreenExit
            : MaterialDesignThemes.Wpf.PackIconKind.Fullscreen;

    [ObservableProperty]
    private string? _selectedFile;

    [ObservableProperty]
    private string _fileName = "尚未选择文件";

    [ObservableProperty]
    private string _statusText = "支持 PDF / DOCX，AI 将整理成中文知识点笔记";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVisibility))]
    [NotifyPropertyChangedFor(nameof(EditorVisibility))]
    [NotifyPropertyChangedFor(nameof(EmptyVisibility))]
    private bool _hasResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVisibility))]
    [NotifyPropertyChangedFor(nameof(EditorVisibility))]
    private bool _isEditing;

    [ObservableProperty]
    private string _markdown = string.Empty;

    [ObservableProperty]
    private Course? _exportCourse;

    public ObservableCollection<Course> Courses { get; } = new();

    public Visibility PreviewVisibility => HasResult && !IsEditing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EditorVisibility => HasResult && IsEditing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => HasResult ? Visibility.Collapsed : Visibility.Visible;

    public bool CanGenerate => !IsBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    public event EventHandler<string>? HtmlRendered;

    public OrganizeViewModel(
        SettingsService settings,
        CourseService courses,
        OrganizeService organize,
        UiState ui)
    {
        _settings = settings;
        _courses = courses;
        _organize = organize;
        _ui = ui;

        _ui.PropertyChanged += OnUiStateChanged;
        _courses.Changed += OnCoursesChanged;
        LoadCourses();
    }

    private void OnUiStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UiState.IsReaderFullscreen))
        {
            OnPropertyChanged(nameof(FullscreenIcon));
        }
    }

    [RelayCommand]
    private void ToggleFullscreen() => _ui.ToggleReaderFullscreen();

    private void OnCoursesChanged(object? sender, EventArgs e) => LoadCourses();

    private void LoadCourses()
    {
        Courses.Clear();
        foreach (var course in _courses.Courses
                     .OrderBy(c => ((int)c.Day + 6) % 7)
                     .ThenBy(c => c.Start))
        {
            Courses.Add(course);
        }

        if (ExportCourse is null && Courses.Count > 0)
        {
            ExportCourse = Courses[0];
        }
    }

    [RelayCommand]
    private void PickFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要整理的课件",
            Filter = "课件文件|*.pdf;*.docx;*.txt;*.md|PDF|*.pdf|Word|*.docx|文本|*.txt;*.md"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SelectedFile = dialog.FileName;
        FileName = Path.GetFileName(dialog.FileName);
        HasResult = false;
        IsEditing = false;
        Progress = 0;
        StatusText = "已选择文件，点「开始整理」生成知识点";
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("SL_ORGANIZE_MOCK") == "1")
        {
            IsBusy = true;
            Progress = 40;
            StatusText = "正在整理第 1/1 段…";
            await Task.Delay(700);
            Markdown = MockMarkdown;
            Progress = 100;
            HasResult = true;
            Render();
            IsBusy = false;
            StatusText = "整理完成（示例数据）";
            return;
        }
#endif

        if (string.IsNullOrWhiteSpace(SelectedFile))
        {
            StatusText = "请先选择文件";
            return;
        }

        IsBusy = true;
        Progress = 0;
        HasResult = false;
        _cancellation = new CancellationTokenSource();

        try
        {
            var progress = new Progress<OrganizeProgress>(value =>
            {
                Progress = value.Total <= 0 ? 0 : value.Done * 100.0 / value.Total;
                StatusText = value.Message;
            });

            var markdown = await _organize.GenerateAsync(SelectedFile, progress, _cancellation.Token);
            Markdown = markdown;
            HasResult = true;
            Render();
            StatusText = "整理完成，可以编辑或导出";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            Logger.Error("整理失败", ex);
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand]
    private void ToggleEdit()
    {
        IsEditing = !IsEditing;
        if (!IsEditing)
        {
            Render();
        }
    }

    [RelayCommand]
    private void Export()
    {
        if (!HasResult)
        {
            StatusText = "还没有可导出的内容";
            return;
        }

        if (ExportCourse is null)
        {
            StatusText = "请先选择要导出的课程";
            return;
        }

        var dialog = new Views.InputDialog(
            "导出整理稿",
            "文件名",
            Path.GetFileNameWithoutExtension(FileName) + " 知识点")
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var folder = _courses.GetCourseFolder(ExportCourse.Name);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, AppPaths.SanitizeFileName(dialog.ResultText, "整理稿") + ".md");
            File.WriteAllText(path, Markdown);
            StatusText = $"已导出到 {path}";
        }
        catch (Exception ex)
        {
            Logger.Error("导出整理稿失败", ex);
            StatusText = $"导出失败：{ex.Message}";
        }
    }

    private void Render()
    {
        var title = Path.GetFileNameWithoutExtension(FileName);
        var html = MarkdownRenderer.Render(Markdown, title, _settings.Current.IsDarkMode);
        HtmlRendered?.Invoke(this, html);
    }

    partial void OnMarkdownChanged(string value)
    {
        if (HasResult && !IsEditing)
        {
            Render();
        }
    }

    /// <summary>把 Markdown 粗略转成纯文本，配合 .txt 笔记约定。</summary>
    private static string ToPlainText(string markdown)
    {
        var builder = new StringBuilder();
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("### "))
            {
                line = line[4..];
            }
            else if (line.StartsWith("## "))
            {
                line = line[3..];
            }
            else if (line.StartsWith("# "))
            {
                line = line[2..];
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                line = "· " + line[2..];
            }

            line = line.Replace("**", string.Empty).Replace("`", string.Empty);
            builder.AppendLine(line);
        }

        return builder.ToString().Trim();
    }

    public void Dispose()
    {
        _ui.PropertyChanged -= OnUiStateChanged;
        _courses.Changed -= OnCoursesChanged;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
    }

    private const string MockMarkdown = """
# 第一章 向量与矩阵

## 1.1 向量空间
- 向量（vector）是向量空间的元素
- 线性组合（linear combination）可以生成子空间
- 一组线性无关的向量构成基（basis）

## 1.2 矩阵运算
- 矩阵乘法满足结合律，但不满足交换律
- 单位矩阵（identity matrix）记为 I
- 行列式不为零时矩阵可逆

# 第二章 特征值与特征向量

## 2.1 定义
- 若 Ax = λx，则 λ 为特征值，x 为对应的特征向量
- 特征多项式 det(A − λI) = 0 的根即特征值

## 2.2 对角化
- 有 n 个线性无关特征向量时可对角化
- 实对称矩阵一定可以正交对角化
""";
}

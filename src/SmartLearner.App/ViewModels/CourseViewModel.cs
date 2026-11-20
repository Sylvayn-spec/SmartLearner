using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartLearner.App.Models;
using SmartLearner.App.Services;

namespace SmartLearner.App.ViewModels;

public enum PreviewMode
{
    None,
    Pdf,
    Markdown,
    Other,
    Rendered
}

public partial class CourseViewModel : ObservableObject, IDisposable
{
    private const string AssistantSystemPrompt =
        """
        你是一名专门回答学习问题的助手。请严格遵守以下规则：

        【角色与原则】
        1. 你的任务是解答学习类问题，包括概念解释、原理说明、解题思路、知识对比、方法步骤等。
        2. 准确性优先：不确定的内容必须标注“待核实”，不编造事实、数据、引用或出处。
        3. 不写废话、不写鸡汤、不复述我的问题。
        4. 如果我的问题信息不足，先问 1 到 2 个关键问题再回答，不要自行脑补。

        【输出格式：纯文本，禁止 Markdown】
        1. 只输出纯文本，不要使用任何 Markdown 语法。
        2. 禁止使用：星号、井号、反引号、大于号引用、竖线表格、短横线列表符号、方括号链接、加粗、斜体、代码块。
        3. 需要分点时，用中文数字加顿号，例如：一、二、三、；同一层级内用阿拉伯数字加顿号，例如：1、2、3、。
        4. 需要分小点时，用括号数字，例如：（1）（2）（3）。
        5. 不使用缩进、制表符和多余空行，段落之间空一行即可。
        6. 标题直接用文字写，不加任何符号装饰。

        【回答结构】
        1. 先用一句话直接回答问题的核心结论。
        2. 再分点展开，每点先写要点短句，必要时在同一个点内补充一句解释。
        3. 如果涉及概念对比，用“相同点：”“不同点：”这样的文字引导，逐条列出，不用表格。
        4. 如果涉及步骤或流程，用“第一步、第二步、第三步”这样的文字引导。
        5. 如果涉及公式或代码，单独起一行写出，前后不加反引号或代码块标记。
        6. 最后用一句话总结，或给出一个自测问题，二选一，不重复正文。

        【语言与粒度】
        1. 默认用中文回答，专业术语可保留英文原词。
        2. 每个分点尽量控制在两句话以内，避免长段落。
        3. 如果我要求“简短”，只保留核心结论和最关键的两到三个分点。
        4. 如果我要求“详细”，在每个分点下补充解释或例子，但不重复已说过的内容。

        【交互规则】
        1. 回答结束后，用一句话问我是否需要更简或更详，不要展开其他话题。
        2. 如果我的问题超出学习范畴，简短说明后仍按上述格式回答可回答的部分。

        【知乎查询】
        如果用户发送“问问知乎:[指定内容]”格式的问题，不要自行回答，应调用知乎搜索并返回最多三篇相关文章卡片（标题、摘要、作者、链接）。
        """;

    private readonly CourseService _courses;
    private readonly DocumentService _documents;
    private readonly SettingsService _settings;
    private readonly BailianCredentialService _bailian;
    private readonly BailianClient _ai;
    private readonly KnowledgeBaseService _knowledge;
    private readonly ChatHistoryService _chatHistory;
    private readonly ZhihuApiClient _zhihu;
    private readonly ZhihuCredentialService _zhihuCredentials;

    [ObservableProperty]
    private Course? _selectedCourse;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private FileEntry? _selectedEntry;

    [ObservableProperty]
    private string _breadcrumb = "请选择课程";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _previewTitle = string.Empty;

    [ObservableProperty]
    private string _markdownText = string.Empty;

    [ObservableProperty]
    private string _pdfUri = "about:blank";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoneVisibility))]
    [NotifyPropertyChangedFor(nameof(PdfVisibility))]
    [NotifyPropertyChangedFor(nameof(MarkdownVisibility))]
    [NotifyPropertyChangedFor(nameof(OtherVisibility))]
    [NotifyPropertyChangedFor(nameof(WebVisibility))]
    private PreviewMode _mode = PreviewMode.None;

    [ObservableProperty]
    private string? _htmlContent;

    [ObservableProperty]
    private bool _isMarkdownFile;

    [ObservableProperty]
    private bool _isMarkdownEditing;

    public string MarkdownEditText => IsMarkdownEditing ? "预览" : "编辑";

    partial void OnIsMarkdownEditingChanged(bool value) => OnPropertyChanged(nameof(MarkdownEditText));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DocsVisibility))]
    [NotifyPropertyChangedFor(nameof(AssistantVisibility))]
    private int _selectedTab;

    public Visibility DocsVisibility => SelectedTab == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AssistantVisibility => SelectedTab == 1 ? Visibility.Visible : Visibility.Collapsed;

    public UiState Ui { get; }

    public MaterialDesignThemes.Wpf.PackIconKind FullscreenIcon =>
        Ui.IsReaderFullscreen
            ? MaterialDesignThemes.Wpf.PackIconKind.FullscreenExit
            : MaterialDesignThemes.Wpf.PackIconKind.Fullscreen;

    public ObservableCollection<AssistantFileItem> NoteFiles { get; } = new();

    public ObservableCollection<AssistantFileItem> TextbookFiles { get; } = new();

    private IEnumerable<AssistantFileItem> AllFiles => NoteFiles.Concat(TextbookFiles);

    [ObservableProperty]
    private bool _selectAllNote;

    [ObservableProperty]
    private bool _selectAllTextbook;

    partial void OnSelectAllNoteChanged(bool value)
    {
        foreach (var file in NoteFiles)
        {
            file.IsSelected = value;
        }
    }

    partial void OnSelectAllTextbookChanged(bool value)
    {
        foreach (var file in TextbookFiles)
        {
            file.IsSelected = value;
        }
    }

    [ObservableProperty]
    private Course? _assistantCourse;

    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

    public ObservableCollection<ChatSession> Sessions { get; } = new();

    [ObservableProperty]
    private bool _isSelectionMode;

    partial void OnIsSelectionModeChanged(bool value)
    {
        foreach (var message in ChatMessages)
        {
            message.ShowSelection = value;
        }
    }

    [ObservableProperty]
    private ChatSession? _currentSession;

    public string ChatInputCount => $"{ChatInput.Length}/3000";

    partial void OnChatInputChanged(string value) => OnPropertyChanged(nameof(ChatInputCount));

    partial void OnCurrentSessionChanged(ChatSession? value)
    {
        LoadMessages(value);
        OnPropertyChanged(nameof(HasConversation));
    }

    public bool HasConversation => CurrentSession is not null;

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private bool _assistantBusy;

    public bool CanBuild => !AssistantBusy;

    partial void OnAssistantBusyChanged(bool value) => OnPropertyChanged(nameof(CanBuild));

    [ObservableProperty]
    private string _assistantStatus = "先勾选资料，再点「建库」";

    [ObservableProperty]
    private double _assistantProgress;

    [ObservableProperty]
    private bool _hasIndex;

    public ObservableCollection<Course> Courses { get; } = new();

    public ObservableCollection<FileEntry> Entries { get; } = new();

    public Visibility NoneVisibility => Mode == PreviewMode.None ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PdfVisibility => Mode == PreviewMode.Pdf ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MarkdownVisibility => Mode == PreviewMode.Markdown ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WebVisibility =>
        Mode is PreviewMode.Pdf or PreviewMode.Rendered ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OtherVisibility => Mode == PreviewMode.Other ? Visibility.Visible : Visibility.Collapsed;

    public bool HasCourses => Courses.Count > 0;

    public CourseViewModel(
        CourseService courses,
        DocumentService documents,
        UiState ui,
        SettingsService settings,
        BailianCredentialService bailian,
        BailianClient ai,
        KnowledgeBaseService knowledge,
        ChatHistoryService chatHistory,
        ZhihuApiClient zhihu,
        ZhihuCredentialService zhihuCredentials)
    {
        _courses = courses;
        _documents = documents;
        _settings = settings;
        _bailian = bailian;
        _ai = ai;
        _knowledge = knowledge;
        _chatHistory = chatHistory;
        _zhihu = zhihu;
        _zhihuCredentials = zhihuCredentials;
        Ui = ui;
        Ui.PropertyChanged += OnUiStateChanged;
        _courses.Changed += OnCoursesChanged;
        LoadCourses();

#if DEBUG
        if (Environment.GetEnvironmentVariable("SL_DEBUG_ASSISTANT_TAB") == "1")
        {
            SelectedTab = 1;
        }

        if (Environment.GetEnvironmentVariable("SL_ASSISTANT_MOCK") == "1")
        {
            HasIndex = true;
            AssistantStatus = "已建库：24 个片段 / 2 个文件";
            ChatMessages.Add(new ChatMessage
            {
                Role = ChatRole.User,
                Text = "这一章主要讲了什么？"
            });
            ChatMessages.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Text = "这一章围绕向量空间与矩阵展开：\n\n1. 向量是向量空间的元素，线性组合可以生成子空间 [1]\n2. 矩阵乘法满足结合律，但不满足交换律 [2]\n3. 行列式不为零时矩阵可逆 [2]",
                Sources = new[] { "Note1.txt · 第1页", "Week5_Assignment.pdf · 第2页" }
            });
        }

        if (Environment.GetEnvironmentVariable("SL_DEBUG_INDEX") == "1")
        {
            foreach (var file in AllFiles)
            {
                file.IsSelected = true;
            }

            _ = BuildIndexAsync();
        }

        if (Environment.GetEnvironmentVariable("SL_DEBUG_NEWCHAT") == "1")
        {
            NewChat();
        }

        if (Environment.GetEnvironmentVariable("SL_DEBUG_SELECT") == "1")
        {
            IsSelectionMode = true;
        }

        if (Environment.GetEnvironmentVariable("SL_DEBUG_EMPTYCHAT") == "1")
        {
            DeleteSessions(Sessions.ToList());
        }

        if (Environment.GetEnvironmentVariable("SL_DEBUG_DELETE") == "1")
        {
            foreach (var message in ChatMessages.Where(message => message.CanDelete))
            {
                message.IsSelected = true;
            }

            DeleteSelectedMessages();
        }

        var debugAsk = Environment.GetEnvironmentVariable("SL_DEBUG_ASK");
        if (!string.IsNullOrWhiteSpace(debugAsk))
        {
            ChatInput = debugAsk;
            _ = SendChatAsync();
        }

        var debugRemove = Environment.GetEnvironmentVariable("SL_DEBUG_REMOVE");
        if (!string.IsNullOrWhiteSpace(debugRemove))
        {
            foreach (var file in AllFiles.Where(f =>
                         string.Equals(f.Name, debugRemove, StringComparison.OrdinalIgnoreCase)))
            {
                file.IsSelected = true;
            }

            RemoveIndex();
        }
#endif
    }

    private void OnUiStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UiState.IsReaderFullscreen))
        {
            OnPropertyChanged(nameof(FullscreenIcon));
        }
    }

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

        OnPropertyChanged(nameof(HasCourses));

        if (SelectedCourse is null || !Courses.Contains(SelectedCourse))
        {
            SelectedCourse = Courses.FirstOrDefault();
        }
        else
        {
            RefreshEntries();
        }
    }

    partial void OnSelectedCourseChanged(Course? value)
    {
        CurrentPath = string.Empty;
        RefreshEntries();

        if (AssistantCourse is null)
        {
            AssistantCourse = value;
        }
    }

    partial void OnAssistantCourseChanged(Course? value)
    {
        ReloadAssistantFiles();
        LoadSessions();
    }

    partial void OnSelectedEntryChanged(FileEntry? value)
    {
        if (value is null || value.IsFolder)
        {
            Mode = PreviewMode.None;
            PreviewTitle = string.Empty;
            IsMarkdownFile = false;
            IsMarkdownEditing = false;
            HtmlContent = null;
            // WebView2 的 Source 不允许 null，清空时回到空白页。
            PdfUri = "about:blank";
            return;
        }

        PreviewTitle = value.Name;
        switch (value.Extension)
        {
            case ".pdf":
                PdfUri = new Uri(value.FullPath).AbsoluteUri;
                Mode = PreviewMode.Pdf;
                break;

            case ".md":
                MarkdownText = _documents.ReadText(value.FullPath) ?? string.Empty;
                IsMarkdownFile = true;
                IsMarkdownEditing = false;
                Mode = PreviewMode.Rendered;
                HtmlContent = MarkdownRenderer.Render(MarkdownText, value.Name, _settings.Current.IsDarkMode);
                break;

            case ".txt":
                MarkdownText = _documents.ReadText(value.FullPath) ?? string.Empty;
                IsMarkdownFile = false;
                IsMarkdownEditing = false;
                Mode = PreviewMode.Markdown;
                break;

            default:
                Mode = PreviewMode.Other;
                break;
        }
    }

    public void EnterFolder(FileEntry folder)
    {
        if (SelectedCourse is null || !folder.IsFolder)
        {
            return;
        }

        CurrentPath = _documents.ToRelative(SelectedCourse, folder.FullPath);
        RefreshEntries();
    }

    [RelayCommand]
    private void RefreshFiles()
    {
        RefreshEntries();
        ReloadAssistantFiles();
        StatusText = "已刷新";
    }

    [RelayCommand]
    private void GoUp()
    {
        if (SelectedCourse is null || string.IsNullOrEmpty(CurrentPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(CurrentPath);
        CurrentPath = parent ?? string.Empty;
        RefreshEntries();
    }

    public void RefreshEntries()
    {
        Entries.Clear();
        SelectedEntry = null;
        Mode = PreviewMode.None;

        if (SelectedCourse is null)
        {
            Breadcrumb = "请先添加课程";
            StatusText = string.Empty;
            return;
        }

        foreach (var entry in _documents.List(SelectedCourse, CurrentPath))
        {
            Entries.Add(entry);
        }

        Breadcrumb = string.IsNullOrEmpty(CurrentPath)
            ? SelectedCourse.Name
            : $"{SelectedCourse.Name} / {CurrentPath.Replace('\\', '/')}";

        StatusText = Entries.Count == 0
            ? "这个文件夹还是空的，导入一份课件或新建一条笔记吧"
            : $"{Entries.Count} 个条目";

#if DEBUG
        // 调试用：SL_DEBUG_SELECT_FIRST=pdf / md，自动选中首个同类型文件，便于截图验证。
        var pick = Environment.GetEnvironmentVariable("SL_DEBUG_SELECT_FIRST");
        if (!string.IsNullOrWhiteSpace(pick))
        {
            SelectedEntry = Entries.FirstOrDefault(e =>
                !e.IsFolder && e.Extension == "." + pick);
        }
#endif
    }

    [RelayCommand]
    private void ImportFiles()
    {
        if (SelectedCourse is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "导入课程文档",
            Filter = "支持的文档|*.pdf;*.docx|PDF|*.pdf|Word|*.docx|所有文件|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var count = 0;
        foreach (var file in dialog.FileNames)
        {
            try
            {
                _documents.Import(SelectedCourse, CurrentPath, file);
                count++;
            }
            catch (Exception ex)
            {
                Infrastructure.Logger.Error($"导入失败：{file}", ex);
            }
        }

        RefreshEntries();
        StatusText = $"已导入 {count} 个文件";
    }

    [RelayCommand]
    private void NewNote()
    {
        if (SelectedCourse is null)
        {
            return;
        }

        var dialog = new Views.InputDialog("新建笔记", "笔记名称", "新建笔记")
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var path = _documents.CreateNote(SelectedCourse, CurrentPath, dialog.ResultText);
        RefreshEntries();
        SelectedEntry = Entries.FirstOrDefault(e => e.FullPath == path);
        StatusText = $"已创建 {dialog.ResultText}.txt";
    }

    [RelayCommand]
    private void SaveNote()
    {
        if (SelectedEntry is null || Mode != PreviewMode.Markdown)
        {
            return;
        }

        _documents.SaveText(SelectedEntry.FullPath, MarkdownText);
        StatusText = $"已保存 {SelectedEntry.Name}";

        if (IsMarkdownFile)
        {
            IsMarkdownEditing = false;
            Mode = PreviewMode.Rendered;
            HtmlContent = MarkdownRenderer.Render(
                MarkdownText,
                SelectedEntry.Name,
                _settings.Current.IsDarkMode);
        }
    }

    /// <summary>Markdown 笔记在「渲染预览」和「源码编辑」之间切换。</summary>
    [RelayCommand]
    private void ToggleMarkdownEdit()
    {
        if (!IsMarkdownFile || SelectedEntry is null)
        {
            return;
        }

        if (Mode == PreviewMode.Rendered)
        {
            Mode = PreviewMode.Markdown;
            IsMarkdownEditing = true;
            return;
        }

        _documents.SaveText(SelectedEntry.FullPath, MarkdownText);
        IsMarkdownEditing = false;
        Mode = PreviewMode.Rendered;
        HtmlContent = MarkdownRenderer.Render(
            MarkdownText,
            SelectedEntry.Name,
            _settings.Current.IsDarkMode);
        StatusText = $"已保存 {SelectedEntry.Name}";
    }

    [RelayCommand]
    private void DeleteEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"确定删除「{SelectedEntry.Name}」吗？\n\n将移入 SmartLearner/.trash 回收站。",
            "删除",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        _documents.MoveToTrash(SelectedEntry);
        RefreshEntries();
        StatusText = "已移入回收站";
    }

    [RelayCommand]
    private void OpenInSystem()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SelectedEntry.FullPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"无法打开文件：{ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectDocsTab() => SelectedTab = 0;

    [RelayCommand]
    private void SelectAssistantTab() => SelectedTab = 1;

    [RelayCommand]
    private void ToggleFullscreen() => Ui.ToggleReaderFullscreen();

    /// <summary>课程助手「导入资料建库」：先校验百炼 Key，再进入建库流程。</summary>
    [RelayCommand]
    private void BuildKnowledgeBase()
    {
        if (!_bailian.HasKey)
        {
            MessageBox.Show(
                "请先设置阿里百炼 API Key。",
                "课程助手",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Ui.RequestNavigation("设置");
            return;
        }

        if (_bailian.IsExpired)
        {
            MessageBox.Show(
                "百炼 API Key 已过期，请更新后再建库。",
                "课程助手",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Ui.RequestNavigation("设置");
            return;
        }

        MessageBox.Show(
            "Key 校验通过。",
            "课程助手",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        SelectedTab = 1;
        ReloadAssistantFiles();
        AssistantStatus = "勾选资料后点「开始建库」";
    }

    [RelayCommand]
    private async Task BuildIndexAsync()
    {
        if (AssistantCourse is null)
        {
            AssistantStatus = "请先选择课程";
            return;
        }

        if (!EnsureBailianReady())
        {
            return;
        }

        var files = AllFiles.Where(f => f.IsSelected).Select(f => f.FullPath).ToList();
        if (files.Count == 0)
        {
            AssistantStatus = "请先勾选要导入的资料";
            return;
        }

        AssistantBusy = true;
        try
        {
            var progress = new Progress<IndexProgress>(value =>
            {
                AssistantProgress = value.Total <= 0 ? 0 : value.Done * 100.0 / value.Total;
                AssistantStatus = value.Message;
            });

            var knowledge = await _knowledge.IndexAsync(AssistantCourse, files, progress, CancellationToken.None);
            HasIndex = knowledge.Chunks.Count > 0;
            ReloadAssistantFiles();

            var skipped = knowledge.Files.Where(f => f.Chunks == 0).Select(f => f.File).Distinct().ToList();
            AssistantStatus = skipped.Count == 0
                ? $"建库完成：{knowledge.Chunks.Count} 个片段 / {knowledge.Files.Count} 个文件"
                : $"建库完成：{knowledge.Chunks.Count} 个片段；{string.Join("、", skipped)} 无文本层";
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Error("建库失败", ex);
            AssistantStatus = ex.Message;
        }
        finally
        {
            AssistantBusy = false;
        }
    }

    [RelayCommand]
    private void RemoveIndex()
    {
        if (AssistantCourse is null)
        {
            return;
        }

        var names = AllFiles
            .Where(f => f.IsSelected)
            .Select(f => f.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            AssistantStatus = "请先勾选要移除索引的文件";
            return;
        }

        _knowledge.RemoveFiles(AssistantCourse, names);
        ReloadAssistantFiles();

        var knowledge = _knowledge.Load(AssistantCourse);
        HasIndex = knowledge.Chunks.Count > 0;
        AssistantStatus = $"已移除 {names.Count} 个文件的索引";
    }

    [RelayCommand]
    private void ClearIndex()
    {
        if (AssistantCourse is null)
        {
            return;
        }

        _knowledge.Clear(AssistantCourse);
        ChatMessages.Clear();
        HasIndex = false;
        AssistantStatus = "已清空知识库";
        ReloadAssistantFiles();
    }

    [RelayCommand]
    private async Task SendChatAsync()
    {
        var question = ChatInput.Trim();
        if (question.Length == 0 || AssistantCourse is null || AssistantBusy || CurrentSession is null)
        {
            return;
        }

        var zhihuMatch = Regex.Match(question, @"^问问知乎\s*[:：]\s*(.+)$", RegexOptions.Singleline);

        if (!zhihuMatch.Success && !EnsureBailianReady())
        {
            return;
        }

        AppendMessage(new ChatMessage { Role = ChatRole.User, Text = question });
        ChatInput = string.Empty;
        AssistantBusy = true;

        try
        {
            if (zhihuMatch.Success)
            {
                await AnswerFromZhihuAsync(zhihuMatch.Groups[1].Value.Trim());
                return;
            }

            var knowledge = _knowledge.Load(AssistantCourse);
            if (knowledge.Chunks.Count == 0)
            {
                AppendMessage(new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Text = "知识库还是空的。请先在左侧勾选资料并点「开始建库」。"
                });
                return;
            }

            var hits = await _knowledge.SearchAsync(AssistantCourse, question, 4, CancellationToken.None);
            var usable = hits.Where(hit => hit.Score >= 0.35f).ToList();

            if (usable.Count == 0)
            {
                AppendMessage(new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Text = "知识库中没有找到相关内容。建议先导入相关课程资料，或换一种问法。"
                });
                return;
            }

            var context = string.Join(
                "\n",
                usable.Select((hit, index) =>
                    $"[{index + 1}]《{AssistantCourse.Name}》/ {hit.Chunk.File}" +
                    (hit.Chunk.Page > 0 ? $" / 第 {hit.Chunk.Page} 页" : string.Empty) +
                    $"：{hit.Chunk.Text}"));

            var answer = await _ai.ChatAsync(
                AssistantSystemPrompt,
                $"请只依据以下资料回答，引用时用 [编号] 标注；资料不足时说明缺什么，不要编造。\n\n资料：\n{context}\n\n问题：{question}",
                CancellationToken.None);

            var sources = usable
                .Select(hit => hit.Chunk.File + (hit.Chunk.Page > 0 ? $" · 第{hit.Chunk.Page}页" : string.Empty))
                .Distinct()
                .ToList();

            AppendMessage(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Text = answer.Trim(),
                Sources = sources
            });
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Error("书僮回答失败", ex);
            AppendMessage(new ChatMessage { Role = ChatRole.Assistant, Text = ex.Message });
        }
        finally
        {
            AssistantBusy = false;
        }
    }

    /// <summary>问问知乎:xxx —— 直接调用知乎搜索，返回最多三篇相关文章卡片。</summary>
    private async Task AnswerFromZhihuAsync(string query)
    {
        if (!_zhihuCredentials.HasKey)
        {
            AppendMessage(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Text = "还没有配置知乎 Access Key，请先到「设置」填写后再试。"
            });
            return;
        }

        try
        {
            var articles = await _zhihu.SearchAsync(query, 3, CancellationToken.None);
            var cards = articles.Take(3).Select(article => new ChatArticle
            {
                Title = article.Title,
                Url = article.Url,
                Excerpt = article.Summary,
                Author = article.Author
            }).ToList();

            if (cards.Count == 0)
            {
                AppendMessage(new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Text = $"没有找到与「{query}」相关的知乎内容，换个关键词试试。"
                });
                return;
            }

            AppendMessage(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Text = $"为你找到 {cards.Count} 篇知乎内容：",
                Articles = cards
            });
        }
        catch (ZhihuAuthException)
        {
            _zhihuCredentials.MarkExpired(true);
            AppendMessage(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Text = "知乎 Access Key 已过期或无效，请到「设置」更新后再试。"
            });
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Error("知乎搜索失败", ex);
            AppendMessage(new ChatMessage { Role = ChatRole.Assistant, Text = ex.Message });
        }
    }

    /// <summary>批量删除：勾选若干条消息后一次性移除，并同步写入会话文件。</summary>
    [RelayCommand]
    private void DeleteSelectedMessages()
    {
        if (CurrentSession is null)
        {
            return;
        }

        var indexes = ChatMessages
            .Select((message, index) => (message, index))
            .Where(item => item.message.IsSelected && item.message.CanDelete)
            .Select(item => item.index)
            .OrderByDescending(index => index)
            .ToList();

        foreach (var index in indexes)
        {
            ChatMessages.RemoveAt(index);
            if (index < CurrentSession.Messages.Count)
            {
                CurrentSession.Messages.RemoveAt(index);
            }
        }

        if (indexes.Count > 0)
        {
            CurrentSession.UpdatedAt = DateTime.Now;
            _chatHistory.Save(CurrentSession);
            AssistantStatus = $"已删除 {indexes.Count} 条消息";
        }

        IsSelectionMode = false;
        foreach (var message in ChatMessages)
        {
            message.IsSelected = false;
        }
    }

    [RelayCommand]
    private void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;

        if (!IsSelectionMode)
        {
            foreach (var message in ChatMessages)
            {
                message.IsSelected = false;
            }
        }
    }

    [RelayCommand]
    private void CopyMessage(ChatMessage? message)
    {
        if (message is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(message.PlainText);
            AssistantStatus = "已复制到剪贴板";
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Warn($"复制失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenArticle(ChatArticle? article)
    {
        if (article is null || string.IsNullOrWhiteSpace(article.Url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(article.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Warn($"打开知乎链接失败：{ex.Message}");
        }
    }

    private void LoadSessions()
    {
        Sessions.Clear();
        CurrentSession = null;
        ChatMessages.Clear();

        if (AssistantCourse is null)
        {
            return;
        }

        foreach (var session in _chatHistory.LoadAll().Where(s => s.Course == AssistantCourse.Name))
        {
            Sessions.Add(session);
        }

        if (Sessions.Count == 0)
        {
            CreateSession();
        }
        else
        {
            CurrentSession = Sessions[0];
        }
    }

    private void CreateSession()
    {
        if (AssistantCourse is null)
        {
            return;
        }

        var session = new ChatSession
        {
            Course = AssistantCourse.Name,
            Title = "新对话"
        };

        var greeting = new ChatMessage
        {
            Role = ChatRole.Assistant,
            CanDelete = false,
            Text = "你好，我是书僮，负责结合你的课程资料回答学习问题。可以输入:\"问问知乎:[问题]\""
        };

        session.Messages.Add(new ChatMessageData
        {
            Role = "assistant",
            Text = greeting.Text,
            CanDelete = false
        });

        _chatHistory.Save(session);
        Sessions.Insert(0, session);
        CurrentSession = session;
    }

    private void LoadMessages(ChatSession? session)
    {
        ChatMessages.Clear();
        if (session is null)
        {
            return;
        }

        foreach (var message in session.Messages)
        {
            ChatMessages.Add(new ChatMessage
            {
                Role = message.Role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                Text = message.Text,
                Sources = message.Sources,
                Articles = message.Articles,
                CanDelete = message.CanDelete,
                ShowSelection = IsSelectionMode
            });
        }
    }

    private void AppendMessage(ChatMessage message)
    {
        message.ShowSelection = IsSelectionMode;
        ChatMessages.Add(message);

        if (CurrentSession is null)
        {
            CreateSession();
        }

        if (CurrentSession is null)
        {
            return;
        }

        CurrentSession.Messages.Add(new ChatMessageData
        {
            Role = message.Role == ChatRole.Assistant ? "assistant" : "user",
            Text = message.Text,
            Sources = message.Sources.ToList(),
            Articles = message.Articles.ToList(),
            CanDelete = message.CanDelete
        });

        if (message.Role == ChatRole.User && CurrentSession.Title == "新对话")
        {
            CurrentSession.Title = message.Text.Length <= 18
                ? message.Text
                : message.Text[..18] + "…";
        }

        CurrentSession.UpdatedAt = DateTime.Now;
        _chatHistory.Save(CurrentSession);

        var index = Sessions.IndexOf(CurrentSession);
        if (index > 0)
        {
            Sessions.Move(index, 0);
        }
    }

    [RelayCommand]
    private void NewChat() => CreateSession();

    [RelayCommand]
    public void DeleteSession(object? parameter)
    {
        var session = parameter as ChatSession;
        if (session is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"确定删除对话「{session.Title}」吗？本地聊天记录和对话 ID 会一并删除。",
            "删除对话",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            var wasCurrent = ReferenceEquals(session, CurrentSession);
            _chatHistory.Delete(session.Id);
            Sessions.Remove(session);

            if (wasCurrent)
            {
                // 还有对话就自动切到下一个；没有就回到「当前无对话」空状态
                CurrentSession = Sessions.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Error("删除对话失败", ex);
            MessageBox.Show($"删除对话失败：{ex.Message}", "删除对话", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>批量删除对话：文件、ID 一并删除，并处理当前选中项。</summary>
    public void DeleteSessions(IReadOnlyList<ChatSession> sessions)
    {
        if (sessions.Count == 0)
        {
            return;
        }

        try
        {
            var wasCurrent = CurrentSession is not null && sessions.Contains(CurrentSession);

            foreach (var session in sessions.ToList())
            {
                _chatHistory.Delete(session.Id);
                Sessions.Remove(session);
            }

            if (wasCurrent)
            {
                CurrentSession = Sessions.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Error("批量删除对话失败", ex);
            MessageBox.Show($"批量删除失败：{ex.Message}", "删除对话", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DeleteChat()
    {
        if (CurrentSession is not null)
        {
            DeleteSession(CurrentSession);
        }
    }

    private bool EnsureBailianReady()
    {
        if (!_bailian.HasKey)
        {
            MessageBox.Show("请先设置阿里百炼 API Key。", "课程助手", MessageBoxButton.OK, MessageBoxImage.Information);
            Ui.RequestNavigation("设置");
            return false;
        }

        if (_bailian.IsExpired)
        {
            MessageBox.Show("百炼 API Key 已过期，请更新后再试。", "课程助手", MessageBoxButton.OK, MessageBoxImage.Warning);
            Ui.RequestNavigation("设置");
            return false;
        }

        return true;
    }

    private void ReloadAssistantFiles()
    {
        NoteFiles.Clear();
        TextbookFiles.Clear();
        SelectAllNote = false;
        SelectAllTextbook = false;

        if (AssistantCourse is null)
        {
            HasIndex = false;
            AssistantStatus = "请先选择课程";
            return;
        }

        var knowledge = _knowledge.Load(AssistantCourse);
        HasIndex = knowledge.Chunks.Count > 0;

        var indexed = knowledge.Files
            .GroupBy(f => f.File)
            .ToDictionary(g => g.Key, g => g.First().Chunks);

        var root = _documents.GetRoot(AssistantCourse);
        AddFolderFiles(Path.Combine(root, "Note"), NoteFiles, indexed);
        AddFolderFiles(Path.Combine(root, "TextBook"), TextbookFiles, indexed);

        var total = NoteFiles.Count + TextbookFiles.Count;
        AssistantStatus = HasIndex
            ? $"已建库：{knowledge.Chunks.Count} 个片段 / {knowledge.Files.Count} 个文件"
            : total == 0
                ? "Note 和 TextBook 里还没有可导入的资料"
                : "勾选资料后点「开始建库」";
    }

    private static void AddFolderFiles(
        string folder,
        ObservableCollection<AssistantFileItem> target,
        IReadOnlyDictionary<string, int> indexed)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                     .Where(p => new[] { ".pdf", ".docx", ".txt", ".md" }
                         .Contains(Path.GetExtension(p).ToLowerInvariant()))
                     .OrderBy(p => p))
        {
            var info = new FileInfo(path);
            var fileName = Path.GetFileName(path);
            target.Add(new AssistantFileItem
            {
                Name = fileName,
                FullPath = path,
                SizeText = info.Length < 1024 ? $"{info.Length} B" : $"{info.Length / 1024.0:0.#} KB",
                StatusText = indexed.TryGetValue(fileName, out var chunks)
                    ? (chunks > 0 ? "已索引" : "无文本层")
                    : "未建库"
            });
        }
    }

    public void Dispose()
    {
        Ui.PropertyChanged -= OnUiStateChanged;
        _courses.Changed -= OnCoursesChanged;
    }}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Services;

namespace SmartLearner.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ZhihuCredentialService _credentials;
    private readonly BailianCredentialService _bailian;

    [ObservableProperty]
    private string _dataRoot = AppPaths.Root;

    [ObservableProperty]
    private string _secretInput = string.Empty;

    [ObservableProperty]
    private string _bailianKeyInput = string.Empty;

    [ObservableProperty]
    private string _chatModel = "deepseek-flash";

    [ObservableProperty]
    private string _embeddingModel = "text-embedding-v4";

    [ObservableProperty]
    private string _ocrModel = "qwen-vl-ocr";

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private int _cacheTtlMinutes = 30;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public int[] TtlOptions { get; } = { 15, 30, 60 };

    public string SecretStateText => _credentials.HasKey
        ? (_credentials.IsExpired ? "已配置，但最近一次调用提示已失效" : "已配置（DPAPI 加密存储）")
        : "未配置";

    public string BailianStateText => _bailian.HasKey
        ? (_bailian.IsExpired ? "已配置，但最近一次调用提示已失效" : "已配置（DPAPI 加密存储）")
        : "未配置";

    public SettingsViewModel(
        SettingsService settings,
        ZhihuCredentialService credentials,
        BailianCredentialService bailian)
    {
        _settings = settings;
        _credentials = credentials;
        _bailian = bailian;
        _dataRoot = AppPaths.Root;
        _isDarkMode = settings.Current.IsDarkMode;
        _cacheTtlMinutes = settings.Current.CacheTtlMinutes;
        _chatModel = settings.Current.ChatModel;
        _embeddingModel = settings.Current.EmbeddingModel;
        _ocrModel = settings.Current.OcrModel;

        // 设置页回显当前 Access Key
        _secretInput = credentials.Key ?? string.Empty;
        _bailianKeyInput = bailian.Key ?? string.Empty;
    }

    [RelayCommand]
    private void SaveBailian()
    {
        if (string.IsNullOrWhiteSpace(BailianKeyInput))
        {
            StatusText = "请先填写百炼 API Key";
            return;
        }

        _bailian.SetKey(BailianKeyInput);
        _settings.Current.ChatModel = ChatModel.Trim();
        _settings.Current.EmbeddingModel = EmbeddingModel.Trim();
        _settings.Current.OcrModel = OcrModel.Trim();
        _settings.Save();

        // 保存后保持回显，不把输入框清空
        BailianKeyInput = _bailian.Key ?? string.Empty;
        StatusText = "百炼配置已保存到 AccessKey/bailian.json";
        OnPropertyChanged(nameof(BailianStateText));
    }

    [RelayCommand]
    private void ClearBailian()
    {
        _bailian.SetKey(null);
        BailianKeyInput = string.Empty;
        StatusText = "已清除百炼 API Key";
        OnPropertyChanged(nameof(BailianStateText));
    }

    [RelayCommand]
    private void SaveSecret()
    {
        if (string.IsNullOrWhiteSpace(SecretInput))
        {
            StatusText = "请先填写 Access Secret";
            return;
        }

        _credentials.SetKey(SecretInput);
        StatusText = "已保存到 AccessKey/zhihu.json，将在下次进入首页时生效";
        OnPropertyChanged(nameof(SecretStateText));
    }

    [RelayCommand]
    private void ClearSecret()
    {
        _credentials.SetKey(null);
        SecretInput = string.Empty;
        StatusText = "已清除本机保存的 Access Secret";
        OnPropertyChanged(nameof(SecretStateText));
    }

    [RelayCommand]
    private void ChangeDataRoot()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择数据存放目录",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings.SetDataRoot(dialog.FolderName);
        DataRoot = AppPaths.Root;
        StatusText = "数据目录已切换，新内容将写入新目录";
    }

    [RelayCommand]
    private void ResetDataRoot()
    {
        _settings.SetDataRoot(null);
        DataRoot = AppPaths.Root;
        StatusText = "已恢复默认目录";
    }

    [RelayCommand]
    private void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        _settings.SetDarkMode(IsDarkMode);
        ThemeService.Apply(IsDarkMode);
        StatusText = IsDarkMode ? "已切换到夜间模式" : "已切换到日间模式";
    }

    partial void OnCacheTtlMinutesChanged(int value)
    {
        if (value <= 0)
        {
            return;
        }

        _settings.Current.CacheTtlMinutes = value;
        _settings.Save();
    }
}

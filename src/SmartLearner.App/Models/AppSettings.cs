namespace SmartLearner.App.Models;

/// <summary>
/// 全局设置。持久化为 SmartLearner/settings.json。
/// </summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    /// <summary>数据根目录；为空表示使用默认（桌面/SmartLearner）。</summary>
    public string? DataRoot { get; set; }

    public bool IsDarkMode { get; set; }

    /// <summary>推送缓存存活时间（分钟）。</summary>
    public int CacheTtlMinutes { get; set; } = 30;

    /// <summary>关注关键词，上限 6 个。</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>DPAPI 加密后的知乎 Access Secret（Base64）。</summary>
    public string? ProtectedAccessSecret { get; set; }

    /// <summary>DPAPI 加密后的阿里百炼 API Key（Base64）。</summary>
    public string? ProtectedBailianKey { get; set; }

    /// <summary>百炼兼容 OpenAI 的接口前缀。</summary>
    public string ApiBase { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";

    public string ChatModel { get; set; } = "deepseek-flash";

    public string EmbeddingModel { get; set; } = "text-embedding-v4";

    /// <summary>视觉模型，用于扫描件 OCR。</summary>
    public string OcrModel { get; set; } = "qwen-vl-ocr";

    /// <summary>最近一次整理的文件路径。</summary>
    public string? LastOrganizedFile { get; set; }
}

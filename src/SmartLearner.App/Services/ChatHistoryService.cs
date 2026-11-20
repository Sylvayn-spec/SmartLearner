using System.IO;
using SmartLearner.App.Infrastructure;
using SmartLearner.App.Models;

namespace SmartLearner.App.Services;

/// <summary>
/// 对话会话的本地存储：SmartLearner/chat/&lt;id&gt;.json。
/// 每个会话一个文件，重开程序后按更新时间倒序恢复。
/// </summary>
public sealed class ChatHistoryService
{
    public string Directory => Path.Combine(AppPaths.Root, "chat");

    public IReadOnlyList<ChatSession> LoadAll()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return Array.Empty<ChatSession>();
            }

            var sessions = new List<ChatSession>();
            foreach (var file in System.IO.Directory.GetFiles(Directory, "*.json"))
            {
                var session = JsonStore.Load<ChatSession>(file, () => null!);
                if (session is not null)
                {
                    sessions.Add(session);
                }
            }

            return sessions
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Warn($"读取对话历史失败：{ex.Message}");
            return Array.Empty<ChatSession>();
        }
    }

    public void Save(ChatSession session)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            JsonStore.Save(Path.Combine(Directory, session.Id + ".json"), session);
        }
        catch (Exception ex)
        {
            Logger.Error($"保存对话失败：{session.Id}", ex);
        }
    }

    public void Delete(string id)
    {
        try
        {
            var path = Path.Combine(Directory, id + ".json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"删除对话失败：{id}，{ex.Message}");
        }
    }
}

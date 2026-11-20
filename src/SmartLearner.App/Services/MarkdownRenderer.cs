using System.Text;
using System.Text.RegularExpressions;

namespace SmartLearner.App.Services;

/// <summary>
/// 把模型输出的 Markdown 渲染成带目录的 HTML，供 WebView2 展示。
/// 只支持标题、列表、段落、加粗、行内代码——够用且不会引入额外依赖。
/// </summary>
public static class MarkdownRenderer
{
    private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex CodePattern = new(@"`(.+?)`", RegexOptions.Compiled);

    public static string Render(string markdown, string title, bool dark = false)
    {
        var body = new StringBuilder();
        var toc = new StringBuilder();
        var headingIndex = 0;
        var inList = false;

        void CloseList()
        {
            if (!inList)
            {
                return;
            }

            body.AppendLine("</ul>");
            inList = false;
        }

        void AddHeading(int level, string text)
        {
            var id = $"h{headingIndex++}";
            body.AppendLine($"<h{level} id=\"{id}\">{Inline(text)}</h{level}>");
            var cssClass = level switch { 1 => "l1", 2 => "l2", _ => "l3" };
            toc.AppendLine($"<a class=\"{cssClass}\" href=\"#{id}\">{Inline(text)}</a>");
        }

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                CloseList();
                continue;
            }

            if (line.StartsWith("### "))
            {
                CloseList();
                AddHeading(3, line[4..]);
            }
            else if (line.StartsWith("## "))
            {
                CloseList();
                AddHeading(2, line[3..]);
            }
            else if (line.StartsWith("# "))
            {
                CloseList();
                AddHeading(1, line[2..]);
            }
            else if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("· "))
            {
                if (!inList)
                {
                    body.AppendLine("<ul>");
                    inList = true;
                }

                body.AppendLine($"<li>{Inline(line[2..])}</li>");
            }
            else
            {
                CloseList();
                body.AppendLine($"<p>{Inline(line)}</p>");
            }
        }

        CloseList();

        var (paper, card, ink, subtle, lineColor, accent) = dark
            ? ("#1D1A16", "#282420", "#EFE7DA", "#A99C8B", "#3A342C", "#C0604F")
            : ("#F6F0E4", "#FFFCF6", "#2C2620", "#7C7060", "#E2D7C4", "#A23B2E");

        return $$"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8"/>
<title>{{Escape(title)}}</title>
<style>
  * { box-sizing: border-box; }
  body { margin:0; background:{{paper}}; color:{{ink}}; font-family:"Microsoft YaHei UI","Segoe UI",sans-serif; font-size:14px; line-height:1.9; }
  .wrap { display:flex; align-items:flex-start; }
  nav { width:250px; flex:0 0 250px; position:sticky; top:0; max-height:100vh; overflow:auto; padding:22px 14px; border-right:1px solid {{lineColor}}; background:{{card}}; }
  nav .cap { font-size:11px; letter-spacing:3px; color:{{subtle}}; margin:0 0 12px 8px; }
  nav a { display:block; color:{{ink}}; text-decoration:none; font-size:13px; padding:5px 8px; border-radius:6px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
  nav a:hover { background:rgba(162,59,46,.10); }
  nav a.l1 { font-weight:600; margin-top:6px; }
  nav a.l2 { padding-left:20px; color:{{subtle}}; }
  nav a.l3 { padding-left:34px; font-size:12px; color:{{subtle}}; }
  main { flex:1; min-width:0; padding:26px 36px 60px; max-width:920px; }
  @media (max-width: 760px) {
    nav { display:none; }
    .wrap { display:block; }
    main { padding:18px 20px 40px; max-width:none; }
  }
  h1,h2,h3 { font-family:SimSun,serif; line-height:1.4; }
  h1 { font-size:26px; border-bottom:1px solid {{lineColor}}; padding-bottom:8px; margin:6px 0 18px; }
  h2 { font-size:20px; margin:30px 0 10px; }
  h3 { font-size:16px; margin:22px 0 8px; }
  p { margin:10px 0; }
  ul { margin:8px 0 8px 4px; padding-left:20px; }
  li { margin:6px 0; }
  strong { color:{{accent}}; }
  code { background:rgba(162,59,46,.10); padding:1px 5px; border-radius:4px; font-family:Consolas,monospace; }
</style>
</head>
<body>
<div class="wrap">
  <nav>
    <div class="cap">目 录</div>
    {{toc}}
  </nav>
  <main>
    {{body}}
  </main>
</div>
</body>
</html>
""";
    }

    private static string Inline(string text)
    {
        var escaped = Escape(text);
        escaped = BoldPattern.Replace(escaped, "<strong>$1</strong>");
        escaped = CodePattern.Replace(escaped, "<code>$1</code>");
        return escaped;
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}

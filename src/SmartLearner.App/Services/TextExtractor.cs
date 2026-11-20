using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace SmartLearner.App.Services;

/// <summary>从 PDF / DOCX / 文本文件中抽取纯文本。</summary>
public static class TextExtractor
{
    public static string Extract(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => ExtractPdf(path),
            ".docx" => ExtractDocx(path),
            ".txt" or ".md" => File.ReadAllText(path),
            _ => throw new NotSupportedException($"暂不支持的文件类型：{extension}")
        };
    }

    private static string ExtractPdf(string path)
    {
        var builder = new StringBuilder();
        using var reader = new PdfReader(path);
        using var document = new PdfDocument(reader);

        for (var page = 1; page <= document.GetNumberOfPages(); page++)
        {
            builder.AppendLine($"[第 {page} 页]");
            builder.AppendLine(PdfTextExtractor.GetTextFromPage(document.GetPage(page)));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string ExtractDocx(string path)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            builder.AppendLine(paragraph.InnerText);
        }

        return builder.ToString();
    }
}

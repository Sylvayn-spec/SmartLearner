using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;

namespace SmartLearner.App.Services;

/// <summary>把 PDF 逐页渲染成 PNG，供视觉模型识别。</summary>
public static class PdfPageRenderer
{
    public static List<(int Page, byte[] Png)> Render(string path, int maxPages = 30)
    {
        var result = new List<(int, byte[])>();

        using var document = DocLib.Instance.GetDocReader(path, new PageDimensions(1240, 1754));
        var pageCount = Math.Min(document.GetPageCount(), maxPages);

        for (var index = 0; index < pageCount; index++)
        {
            using var page = document.GetPageReader(index);
            var width = page.GetPageWidth();
            var height = page.GetPageHeight();
            var raw = page.GetImage();

            if (width <= 0 || height <= 0 || raw.Length < width * height * 4)
            {
                continue;
            }

            var bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                raw,
                width * 4);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            result.Add((index + 1, stream.ToArray()));
        }

        return result;
    }
}

using System.Windows;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;

namespace SmartLearner.App.Services;

/// <summary>
/// 明暗主题切换（S-04）。
/// 通过替换 BundledTheme 实例重建 MD 调色板，并替换自定义的纸张/墨色色板。
/// </summary>
public static class ThemeService
{
    private const string PalettePrefix = "Themes/Palette.";

    public static void Apply(bool dark)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        try
        {
            SwapMaterialTheme(app, dark);
            SwapPalette(app, dark);
        }
        catch (Exception ex)
        {
            Infrastructure.Logger.Error("切换主题失败", ex);
        }
    }

    private static void SwapMaterialTheme(Application app, bool dark)
    {
        var dictionaries = app.Resources.MergedDictionaries;
        var current = dictionaries.OfType<BundledTheme>().FirstOrDefault();
        if (current is null)
        {
            return;
        }

        var index = dictionaries.IndexOf(current);
        dictionaries.RemoveAt(index);

        var replacement = new BundledTheme
        {
            BaseTheme = dark ? BaseTheme.Dark : BaseTheme.Light,
            PrimaryColor = PrimaryColor.Brown,
            SecondaryColor = SecondaryColor.Amber
        };

        dictionaries.Insert(index, replacement);
    }

    private static void SwapPalette(Application app, bool dark)
    {
        var dictionaries = app.Resources.MergedDictionaries;
        var current = dictionaries
            .FirstOrDefault(d => d.Source is not null &&
                                 d.Source.OriginalString.Contains(PalettePrefix, StringComparison.OrdinalIgnoreCase));

        var index = current is null ? dictionaries.Count : dictionaries.IndexOf(current);
        if (current is not null)
        {
            dictionaries.RemoveAt(index);
        }

        var uri = new Uri(
            $"pack://application:,,,/SmartLearner.App;component/Themes/Palette.{(dark ? "Dark" : "Light")}.xaml",
            UriKind.Absolute);

        dictionaries.Insert(index, new ResourceDictionary { Source = uri });
    }
}

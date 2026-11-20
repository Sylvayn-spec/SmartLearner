using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;

namespace SmartLearner.App.ViewModels;

/// <summary>侧边栏的一个导航项。</summary>
public sealed class NavItem
{
    public required string Label { get; init; }

    public required PackIconKind Icon { get; init; }

    public required ObservableObject Page { get; init; }
}

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLearner.App.Models;
using SmartLearner.App.Services;

namespace SmartLearner.App.ViewModels;

public partial class CourseDraft : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _day = "周一";

    [ObservableProperty]
    private string _start = "08:00";

    [ObservableProperty]
    private string _end = "09:40";
}

public partial class AddCourseViewModel : ObservableObject
{
    private readonly CourseService _service;
    private readonly Course? _editing;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<CourseDraft> Rows { get; } = new();

    public string[] DayOptions { get; } =
    {
        "周一", "周二", "周三", "周四", "周五", "周六", "周日"
    };

    public string[] TimeOptions { get; }

    public bool IsEditMode => _editing is not null;

    public string HeaderText => IsEditMode ? "编辑课程" : "添加课程";

    public Visibility AddRowVisibility => IsEditMode ? Visibility.Collapsed : Visibility.Visible;

    public AddCourseViewModel(CourseService service, Course? editing = null)
    {
        _service = service;
        _editing = editing;
        TimeOptions = BuildTimeOptions();

        Rows.Add(editing is null
            ? new CourseDraft()
            : new CourseDraft
            {
                Name = editing.Name,
                Day = editing.DayText,
                Start = $"{editing.Start.Hours:00}:{editing.Start.Minutes:00}",
                End = $"{editing.End.Hours:00}:{editing.End.Minutes:00}"
            });
    }

    [RelayCommand]
    private void AddRow() => Rows.Add(new CourseDraft());

    [RelayCommand]
    private void RemoveRow(CourseDraft? row)
    {
        if (row is null || Rows.Count <= 1)
        {
            return;
        }

        Rows.Remove(row);
    }

    /// <summary>校验并写入；失败时把错误写进 ErrorMessage。</summary>
    public bool TrySave()
    {
        var courses = new List<Course>();
        foreach (var row in Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                ErrorMessage = "请填写课程名";
                return false;
            }

            if (!TimeSpan.TryParse(row.Start, out var start) ||
                !TimeSpan.TryParse(row.End, out var end))
            {
                ErrorMessage = "时间格式不正确";
                return false;
            }

            courses.Add(new Course
            {
                Name = row.Name.Trim(),
                Day = ParseDay(row.Day),
                Start = start,
                End = end
            });
        }

        if (IsEditMode)
        {
            if (courses.Count != 1 || _editing is null)
            {
                ErrorMessage = "编辑时只能修改一门课程";
                return false;
            }

            courses[0].Id = _editing.Id;
            var updateError = _service.Update(courses[0]);
            if (updateError is not null)
            {
                ErrorMessage = updateError;
                return false;
            }

            return true;
        }

        var error = _service.Validate(courses);
        if (error is not null)
        {
            ErrorMessage = error;
            return false;
        }

        _service.AddRange(courses);
        return true;
    }

    private static DayOfWeek ParseDay(string text) => text switch
    {
        "周二" => DayOfWeek.Tuesday,
        "周三" => DayOfWeek.Wednesday,
        "周四" => DayOfWeek.Thursday,
        "周五" => DayOfWeek.Friday,
        "周六" => DayOfWeek.Saturday,
        "周日" => DayOfWeek.Sunday,
        _ => DayOfWeek.Monday
    };

    private static string[] BuildTimeOptions()
    {
        var options = new List<string>();
        for (var minutes = 7 * 60; minutes <= 22 * 60; minutes += 5)
        {
            options.Add($"{minutes / 60:00}:{minutes % 60:00}");
        }

        return options.ToArray();
    }
}

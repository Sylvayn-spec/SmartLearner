namespace SmartLearner.App.Models;

using System.Text.Json.Serialization;

/// <summary>
/// 一门课程。课程是唯一真相源，Documents 下的同名文件夹是它的投影。
/// </summary>
public sealed class Course
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>星期几（Monday..Sunday）。</summary>
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;

    public TimeSpan Start { get; set; } = new(8, 0, 0);

    public TimeSpan End { get; set; } = new(9, 40, 0);

    [JsonIgnore]
    public string TimeText => $"{Start:hh\\:mm} - {End:hh\\:mm}";

    [JsonIgnore]
    public string DayText => Day switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日"
    };
}

public sealed class CourseFile
{
    public int Version { get; set; } = 1;

    public List<Course> Items { get; set; } = new();
}

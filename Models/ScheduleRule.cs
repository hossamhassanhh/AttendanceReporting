using System.ComponentModel.DataAnnotations;

namespace AttendanceApp.Models;

public class ScheduleRule
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string LevelName { get; set; } = string.Empty;

    [Required]
    public string StartTime { get; set; } = "08:31";

    [Required]
    public string EndTime { get; set; } = "15:25";

    public bool IsActive { get; set; } = true;
}

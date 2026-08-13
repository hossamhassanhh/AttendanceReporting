using System.ComponentModel.DataAnnotations;

namespace AttendanceApp.Models;

public class AttendanceDaySetting
{
    [Key]
    public int Id { get; set; }

    public DateTime Date { get; set; }


    [Required]
    [MaxLength(30)]
    public string DayType { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

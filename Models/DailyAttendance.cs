using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceApp.Models;

public class DailyAttendance
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string EmployeeFinancialNo { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public DateTime? FirstPunch { get; set; }

    public DateTime? LastPunch { get; set; }

    [MaxLength(10)]
    public string? ScheduledStart { get; set; }

    [MaxLength(10)]
    public string? ScheduledEnd { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Unknown";

    public int? LeaveTypeId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(EmployeeFinancialNo))]
    public Employee? Employee { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public LeaveType? LeaveType { get; set; }
}

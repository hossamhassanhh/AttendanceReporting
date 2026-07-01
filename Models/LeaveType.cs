using System.ComponentModel.DataAnnotations;

namespace AttendanceApp.Models;

public class LeaveType
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    public string? NameAr { get; set; }

    public string? NameEn { get; set; }

    public string? BalanceType { get; set; }
}

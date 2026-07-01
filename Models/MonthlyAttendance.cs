using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceApp.Models;

public class MonthlyAttendance
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string EmployeeFinancialNo { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public int Day { get; set; }

    [MaxLength(500)]
    public string? Code { get; set; }

    [ForeignKey(nameof(EmployeeFinancialNo))]
    public Employee? Employee { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace AttendanceApp.Models;

public class Employee
{
    [Key]
    [MaxLength(20)]
    public string FinancialNo { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? JobTitle { get; set; }

    [MaxLength(500)]
    public string? Department { get; set; }

    [MaxLength(100)]
    public string? Level { get; set; }

    [MaxLength(10)]
    public string? ScheduleStart { get; set; }

    [MaxLength(10)]
    public string? ScheduleEnd { get; set; }

    [MaxLength(200)]
    public string? WorkLocation { get; set; }

    [MaxLength(200)]
    public string? JobStatus { get; set; }

    [MaxLength(20)]
    public string? ManagerFinancialNo { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? HireDate { get; set; }

    [MaxLength(100)]
    public string? ContractType { get; set; }

    public bool IsActive { get; set; } = true;
}

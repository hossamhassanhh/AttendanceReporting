using System.ComponentModel.DataAnnotations;

namespace AttendanceApp.Models;

public class DepartmentWorkflowConfig
{
    [Key]
    [MaxLength(500)]
    public string Department { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ManagerFinancialNo { get; set; }

    [MaxLength(20)]
    public string? ManagerDelegateFinancialNo { get; set; }

    [MaxLength(20)]
    public string? HrFinancialNo { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceApp.Models;

public class LeaveBalance
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string EmployeeFinancialNo { get; set; } = string.Empty;

    public int Year { get; set; }

    public double RegularLeave { get; set; }

    public double CasualLeave { get; set; }

    public double RestAllowance { get; set; }

    public double HolidayAllowance { get; set; }

    [ForeignKey(nameof(EmployeeFinancialNo))]
    public Employee? Employee { get; set; }
}

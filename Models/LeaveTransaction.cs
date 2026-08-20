using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceApp.Models;

public class LeaveTransaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string EmployeeFinancialNo { get; set; } = string.Empty;

    public int LeaveTypeId { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public double DaysCount { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Approved";

    [MaxLength(80)]
    public string EnteredBy { get; set; } = "admin";

    [MaxLength(20)]
    public string? ManagerFinancialNo { get; set; }

    public DateTime? ManagerApprovedAt { get; set; }

    public DateTime? HrApprovedAt { get; set; }

    [MaxLength(80)]
    public string? RejectedBy { get; set; }

    public DateTime? RejectedAt { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey(nameof(EmployeeFinancialNo))]
    public Employee? Employee { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public LeaveType? LeaveType { get; set; }
}

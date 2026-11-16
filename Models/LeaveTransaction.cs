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

    /// <summary>Direct (personal) manager - step after delegate acceptance.</summary>
    [MaxLength(20)]
    public string? DirectManagerFinancialNo { get; set; }

    public DateTime? DirectManagerApprovedAt { get; set; }

    /// <summary>Department manager - step after direct manager approval.</summary>
    [MaxLength(20)]
    public string? DepartmentManagerFinancialNo { get; set; }

    public DateTime? DepartmentManagerApprovedAt { get; set; }

    /// <summary>Legacy/compat: mirrors DirectManagerFinancialNo.</summary>
    [MaxLength(20)]
    public string? ManagerFinancialNo { get; set; }

    /// <summary>Manager's delegate who can approve on manager's behalf (if configured). Optional.</summary>
    [MaxLength(20)]
    public string? ManagerDelegateFinancialNo { get; set; }

    /// <summary>Employee's delegate chosen per-request (same department) who must approve first.</summary>
    [MaxLength(20)]
    public string? DelegateFinancialNo { get; set; }

    public DateTime? DelegateApprovedAt { get; set; }

    /// <summary>Legacy/compat: mirrors DirectManagerApprovedAt.</summary>
    public DateTime? ManagerApprovedAt { get; set; }

    /// <summary>Department HR employee assigned to final approval (if configured). Optional.</summary>
    [MaxLength(20)]
    public string? AssignedHrFinancialNo { get; set; }

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

namespace AttendanceApp.Models;

public class AttendanceData
{
    public int EmployeeCode { get; set; }
    public DateTime? FirstPunch { get; set; }
    public DateTime? LastPunch { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

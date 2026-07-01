namespace AttendanceApp.Models;

public class AttendanceRequest
{
    public List<int> EmployeeCodes { get; set; } = new();
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

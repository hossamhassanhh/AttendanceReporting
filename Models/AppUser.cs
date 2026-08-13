using System.ComponentModel.DataAnnotations;

namespace AttendanceApp.Models;

public class AppUser
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? DisplayNameAr { get; set; }

    [MaxLength(120)]
    public string? DisplayNameEn { get; set; }

    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = "Employee";

    [Required]
    [MaxLength(1000)]
    public string Permissions { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; } = true;

    public int SessionVersion { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

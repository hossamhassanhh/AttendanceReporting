using System.ComponentModel.DataAnnotations;

namespace AttendanceApp.Models;

public class SyncState
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    public long LastProcessedTransactionId { get; set; }

    public DateTime? LastSyncTime { get; set; }
}

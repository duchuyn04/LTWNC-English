using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class CardActionLog
{
    [Key]
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SetId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string CardIdsJson { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public DateTime? UndoneAt { get; set; }
}

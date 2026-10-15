using System.ComponentModel.DataAnnotations.Schema;

namespace BookonnectWorkerService.Models
{
  public class Preference
  {
    public int Id { get; set; }
    public int UserId { get; set; }

    [Column(TypeName = "VARCHAR(13)")]
    public string BookId { get; set; } = string.Empty;
    public bool IsLiked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Book Book { get; set; } = null!;
  }
}
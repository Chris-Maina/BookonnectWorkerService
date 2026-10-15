using System.ComponentModel.DataAnnotations.Schema;
namespace BookonnectWorkerService.Models
{
  public class User
  {
    public int ID { get; set; }
    [Column(TypeName = "VARCHAR(255)")]
    public string Name { get; set; } = String.Empty;
    [Column(TypeName = "VARCHAR(255)")]
    public string Email { get; set; } = String.Empty;
    public string? Image { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public ICollection<Preference> Preferences { get; set; } = new List<Preference>();
  }
}
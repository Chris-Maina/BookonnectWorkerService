using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookonnectWorkerService.Models
{
  public class Book
  {
    [Key]
    [Column(TypeName = "VARCHAR(13)")]
    public string ISBN13 { get; set; }  = string.Empty;

    [Column(TypeName = "VARCHAR(10)")]
    public string ISBN10 { get; set; }  = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string Categories { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime PublishedYear { get; set; } = DateTime.MinValue;
    public float AverageRating { get; set; } = 0.0f;
    public int PageCount { get; set; } = 0;
    public int RatingsCount { get; set; } = 0;
  }
}
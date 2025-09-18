using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class Category
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }
        [Required]
        [Column(TypeName = "nvarchar(50)"), MaxLength(50)]
        public string Name { get; set; }

        public ICollection<Recipe> Recipes { get; set; }

    }
}
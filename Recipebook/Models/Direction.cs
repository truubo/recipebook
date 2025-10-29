using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class Direction
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Recipe")]
        [Column(TypeName = "int")]
        public int RecipeId { get; set; }

        [Required]
        [Column(TypeName = "varchar(2000)")]
        public string StepDescription { get; set; } = default!;

        [Required]
        [Column(TypeName = "int")]
        public int StepNumber { get; set; }

    }
}

using System.ComponentModel.DataAnnotations;

namespace Recipebook.Models
{
    public enum SortType
    {
        [Display(Name = "Alphabetical (A-Z)")]
        AlphabeticalAsc,

        [Display(Name = "Alphabetical (Z-A)")]
        AlphabeticalDesc,
    }
}

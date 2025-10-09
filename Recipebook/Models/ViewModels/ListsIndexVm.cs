namespace Recipebook.Models.ViewModels
{
    public class ListsIndexVm
    {
        public IEnumerable<List> Lists { get; set; } = Enumerable.Empty<List>();
        public string? MyEmail { get; set; }
        public string? MyUserId { get; set; }
    }
}

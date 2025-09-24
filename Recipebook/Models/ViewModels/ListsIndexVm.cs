namespace Recipebook.Models.ViewModels
{
    public class ListsIndexVm
    {
        public IEnumerable<List> MyLists { get; set; } = Enumerable.Empty<List>();
        public IEnumerable<List> AllLists { get; set; } = Enumerable.Empty<List>();
        public string? MyEmail { get; set; }
        public string? MyUserId { get; set; }
    }
}

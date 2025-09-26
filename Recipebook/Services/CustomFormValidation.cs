using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Recipebook.Services
{
    public static class CustomFormValidation
    {
        public static bool FormValid(ModelStateDictionary modelState)
        {
            modelState.Remove("AuthorEmail");
            modelState.Remove("AuthorId");
            return modelState.IsValid;
        }
    }
}

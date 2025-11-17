namespace Recipebook.Helpers
{
    public static class TimeFormattingHelper
    {
        // Converts a total number of minutes into a readable time string (e.g., "1 hour 30 minutes")
        public static string FormatMinutes(int totalMinutes)
        {
            // If less than 60 minutes, just return minutes with correct pluralization
            if (totalMinutes < 60)
                return $"{totalMinutes} min";

            // Calculate full hours and remaining minutes
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            // If evenly divisible by 60, return only hours
            if (minutes == 0)
                return $"{hours} hr";

            // Otherwise, return both hours and minutes with correct pluralization
            return $"{hours} hr {minutes} min";
        }
    }
}
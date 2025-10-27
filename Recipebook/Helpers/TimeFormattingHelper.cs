namespace Recipebook.Helpers
{
    public static class TimeFormattingHelper
    {
        // Converts a total number of minutes into a readable time string (e.g., "1 hour 30 minutes")
        public static string FormatMinutes(int totalMinutes)
        {
            // If less than 60 minutes, just return minutes with correct pluralization
            if (totalMinutes < 60)
                return $"{totalMinutes} minute{(totalMinutes == 1 ? "" : "s")}";

            // Calculate full hours and remaining minutes
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            // If evenly divisible by 60, return only hours
            if (minutes == 0)
                return $"{hours} hour{(hours == 1 ? "" : "s")}";

            // Otherwise, return both hours and minutes with correct pluralization
            return $"{hours} hour{(hours == 1 ? "" : "s")} {minutes} minute{(minutes == 1 ? "" : "s")}";
        }
    }
}
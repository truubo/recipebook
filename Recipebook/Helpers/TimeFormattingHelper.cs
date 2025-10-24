namespace Recipebook.Helpers
{
    public static class TimeFormattingHelper
    {
        public static string FormatMinutes(int totalMinutes)
        {
            if (totalMinutes < 60)
                return $"{totalMinutes} minute{(totalMinutes == 1 ? "" : "s")}";

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            if (minutes == 0)
                return $"{hours} hour{(hours == 1 ? "" : "s")}";

            return $"{hours} hour{(hours == 1 ? "" : "s")} {minutes} minute{(minutes == 1 ? "" : "s")}";
        }
    }
}
using System;

namespace PhishingDataCollector.Utils
{
    public static class TimeStamp
    {
        public static DateTime Origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        public static DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            return Origin.AddSeconds(timestamp);
        }

        public static double ConvertToUnixTimestamp(DateTime date)
        {
            TimeSpan diff = date.ToUniversalTime() - Origin;
            return Math.Floor(diff.TotalSeconds);
        }
    }

}
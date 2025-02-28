/***
 *  This file is part of Dataset-Collector.

    Dataset-Collector is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Dataset-Collector is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Dataset-Collector.  If not, see <http://www.gnu.org/licenses/>. 
 * 
 * ***/
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
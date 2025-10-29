using System;

namespace Kotoban.Core.Utils
{
    public static class DateTimeUtils
    {
        public const string DisplayFormat = "yyyy-MM-dd HH:mm:ss 'UTC'";
        public const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

        public static string FormatForDisplay(DateTime dateTime)
        {
            return dateTime.ToString(DisplayFormat);
        }

        public static string FormatNullableForDisplay(DateTime? dateTime, string defaultValue = "なし")
        {
            return dateTime.HasValue ? dateTime.Value.ToString(DisplayFormat) : defaultValue;
        }

        public static string UtcNowTimestamp()
        {
            return DateTime.UtcNow.ToString(TimestampFormat);
        }
    }
}

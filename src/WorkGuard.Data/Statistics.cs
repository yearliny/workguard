using System.Globalization;
using System.Text;

namespace WorkGuard.Data;

public static class Statistics
{
    public static void Record(StoredState state, TimeSpan duration, DateTimeOffset end, TimeSpan continuous)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromSeconds(10)) return;
        var cursor = end - duration;
        while (cursor < end)
        {
            var midnight = new DateTimeOffset(cursor.Date.AddDays(1), cursor.Offset);
            var until = end < midnight ? end : midnight;
            var day = LocalStore.Day(state, DateOnly.FromDateTime(cursor.Date));
            day.ActiveSeconds += (until - cursor).TotalSeconds;
            var atSegmentEnd = continuous - (end - until);
            day.LongestSeconds = Math.Max(day.LongestSeconds, Math.Max(0, atSegmentEnd.TotalSeconds));
            cursor = until;
        }
    }

    public static string Csv(IEnumerable<DayStats> days)
    {
        var result = new StringBuilder("date,active_minutes,longest_session_minutes,eye_breaks,movement_breaks,office_breaks,maintenance_confirmed_minutes\r\n");
        foreach (var d in days.OrderBy(d => d.Date))
            result.AppendLine(string.Join(",", d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                (d.ActiveSeconds / 60).ToString("F2", CultureInfo.InvariantCulture),
                (d.LongestSeconds / 60).ToString("F2", CultureInfo.InvariantCulture),
                d.EyeBreaks.ToString(CultureInfo.InvariantCulture), d.MovementBreaks.ToString(CultureInfo.InvariantCulture),
                d.OfficeBreaks.ToString(CultureInfo.InvariantCulture),
                (d.MaintenanceSeconds.Values.Sum() / 60).ToString("F2", CultureInfo.InvariantCulture)));
        return result.ToString();
    }
}

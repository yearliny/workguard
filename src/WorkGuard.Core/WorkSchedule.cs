namespace WorkGuard.Core;

// Local wall time chooses calendar windows only; it never advances an activity timer.
public static class WorkSchedule
{
    public static bool IncludesDay(Preferences p, DayOfWeek day) => (p.WorkDays & (1 << (int)day)) != 0;

    public static bool Allows(Preferences p, DateTime localNow)
    {
        if (!p.WorkScheduleEnabled) return true;
        var minute = localNow.Hour * 60 + localNow.Minute;
        if (p.WorkStartMinute == p.WorkEndMinute) return false;
        if (p.WorkStartMinute < p.WorkEndMinute)
            return IncludesDay(p, localNow.DayOfWeek) && minute >= p.WorkStartMinute && minute < p.WorkEndMinute;
        // The after-midnight part belongs to the previous day's selected shift.
        var previousDay = (DayOfWeek)(((int)localNow.DayOfWeek + 6) % 7);
        return minute >= p.WorkStartMinute && IncludesDay(p, localNow.DayOfWeek) ||
            minute < p.WorkEndMinute && IncludesDay(p, previousDay);
    }

    public static DateTime? NextStart(Preferences p, DateTime localNow)
    {
        if (!p.WorkScheduleEnabled || p.WorkDays == 0 || p.WorkStartMinute == p.WorkEndMinute) return null;
        for (var offset = 0; offset < 8; offset++)
        {
            if (localNow.Date > DateTime.MaxValue.Date.AddDays(-offset)) break;
            var date = localNow.Date.AddDays(offset);
            var start = date.AddMinutes(p.WorkStartMinute);
            if (start > localNow && IncludesDay(p, date.DayOfWeek)) return start;
        }
        return null;
    }

    // Once per local date, offered only within 30 minutes. Suppression does not create a backlog.
    // A high-water date also prevents repeats after the clock is moved backwards.
    public static bool OfficeDue(Preferences p, DateTime localNow, DateOnly? lastOffered, bool officeCompletedToday)
    {
        var date = DateOnly.FromDateTime(localNow);
        if (!p.OfficeReminderEnabled || officeCompletedToday || lastOffered >= date || !Allows(p, localNow)) return false;
        var minutes = localNow.TimeOfDay.TotalMinutes;
        return minutes >= p.OfficeReminderMinute && minutes < p.OfficeReminderMinute + 30;
    }
}

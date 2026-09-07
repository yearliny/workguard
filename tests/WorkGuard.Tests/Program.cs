using WorkGuard.Core;
using WorkGuard.Data;

// Dependency-free deterministic behavior checks. Nonzero exit code fails CI.
var tests = new (string Name, Action Run)[]
{
    ("Quick setup preserves advanced preferences and custom rhythm", () =>
    {
        var initial = new Preferences { EyeIntervalMinutes = 35, MovementIntervalMinutes = 85, EyeReminders = false,
            StrictEyes = false, NeckMovements = true, ReduceMotion = true, QuietHoursEnabled = true, StartWithWindows = true };
        var result = new QuickSetup(null, true, false).ApplyTo(initial);
        True(result == initial with { StrictMode = true, SoundEnabled = false, OnboardingComplete = true });
    }),
    ("Quick setup presets do not silently enable strict mode or eye reminders", () =>
    {
        foreach (var minutes in new[] { 50, 60 })
        {
            var result = new QuickSetup(minutes, false, true).ApplyTo(new Preferences { EyeReminders = false });
            True(result.MovementIntervalMinutes == minutes && result.EyeIntervalMinutes == 20);
            True(!result.StrictMode && !result.EyeReminders && result.OnboardingComplete);
        }
    }),
    ("Reduce motion preference persists and defaults to normal", () => WithDirectory(path =>
    {
        var store = new LocalStore(path); var state = store.Load(); True(!state.Preferences.ReduceMotion);
        state.Preferences = state.Preferences with { ReduceMotion = true }; store.Save(state);
        True(new LocalStore(path).Load().Preferences.ReduceMotion);
    })),
    ("Strict scope is opt-in and never forces office training", () =>
    {
        var p = new Preferences(); True(!p.ShouldForce(BreakKind.Movement));
        p = p with { StrictMode = true }; True(p.ShouldForce(BreakKind.Movement)); True(p.ShouldForce(BreakKind.Eyes));
        True(!p.ShouldForce(BreakKind.Office)); True(!(p with { StrictEyes = false }).ShouldForce(BreakKind.Eyes));
        True(!(p with { EyeReminders = false }).ShouldForce(BreakKind.Eyes));
    }),
    ("Strict settings survive restart and old installs stay gentle", () => WithDirectory(path =>
    {
        var store = new LocalStore(path); var state = store.Load(); True(!state.Preferences.StrictMode);
        state.Preferences = state.Preferences with { StrictMode = true, StrictEyes = false, NeckMovements = true };
        store.Save(state); var p = new LocalStore(path).Load().Preferences;
        True(p.StrictMode && !p.StrictEyes && p.NeckMovements);
    })),
    ("Strict sessions reject skip and require every second", () =>
    {
        var s = new BreakSession(BreakKind.Movement, true, strict: true);
        s.Advance(Seconds(10)); s.Skip(); Equal(0, s.Index); Equal(Seconds(10), s.Observed);
        for (var i = 0; i < 169; i++) s.Advance(Seconds(1));
        True(!s.Finished); s.Skip(); True(!s.Finished); s.Advance(Seconds(1)); True(s.ActivityCompleted);
    }),
    ("Rest alternative preserves duration without awarding activity", () =>
    {
        var s = new BreakSession(BreakKind.Movement, true, strict: true);
        s.Advance(Seconds(10)); s.UseRestAlternative(); s.UseRestAlternative();
        Equal(Seconds(10), s.Observed); Equal(0, s.Index);
        for (var i = 0; i < 170; i++) s.Advance(Seconds(1));
        True(s.FullyCompleted && s.RestOnly && !s.ActivityCompleted);
    }),
    ("Strict interrupted sessions do not advance or gain credit", () =>
    {
        var s = new BreakSession(BreakKind.Eyes, true, strict: true);
        s.Advance(Seconds(600)); s.Paused = true; s.Advance(Seconds(10));
        Equal(TimeSpan.Zero, s.Observed); True(!s.FullyCompleted);
        s.Paused = false; s.Advance(Seconds(10)); True(!s.FullyCompleted);
    }),
    ("Strict delivery respects quiet time and natural absence", () =>
    {
        var e = new BreakEngine(new Preferences { StrictMode = true }); Work(e, 3600, quiet: true);
        Equal(Reminder.None, e.Advance(Seconds(1), Active(true)));
        Equal(Reminder.Movement, e.Advance(Seconds(1), Active()));
        e.Advance(Seconds(1), new(Seconds(300))); Equal(TimeSpan.Zero, e.Continuous);
    }),
    ("Every scene variant preserves duration and neck is optional", () =>
    {
        foreach (var kind in Enum.GetValues<BreakKind>()) foreach (var gentle in new[] { true, false })
        foreach (var neck in new[] { true, false }) foreach (var variant in new[] { 0, 1, 2, int.MaxValue })
            Equal(Programs.Duration(kind), Seconds(Programs.For(kind, gentle, variant, neck).Sum(x => x.Seconds)));
        True(!Programs.For(BreakKind.Movement, true).Any(x => x.Title.Contains("两侧")));
        True(Programs.For(BreakKind.Movement, true, neck: true).Any(x => x.Title.Contains("两侧")));
    }),
    ("Quiet hours include start and exclude end", () =>
    {
        var p = new Preferences { QuietHoursEnabled = true, QuietStartMinute = 720, QuietEndMinute = 780 };
        True(p.IsQuietTime(new TimeOnly(12, 0))); True(!p.IsQuietTime(new TimeOnly(13, 0)));
        True(!(p with { QuietHoursEnabled = false }).IsQuietTime(new TimeOnly(12, 30)));
    }),
    ("Overnight quiet hours and equal endpoints", () =>
    {
        var p = new Preferences { QuietHoursEnabled = true, QuietStartMinute = 1320, QuietEndMinute = 420 };
        True(p.IsQuietTime(new TimeOnly(23, 0))); True(p.IsQuietTime(new TimeOnly(6, 59)));
        True(!p.IsQuietTime(new TimeOnly(7, 0))); True(!p.IsQuietTime(new TimeOnly(12, 0)));
        True(!(p with { QuietEndMinute = 1320 }).IsQuietTime(new TimeOnly(23, 0)));
    }),
    ("Midnight tick splits active time into both dates", () =>
    {
        var state = new StoredState(); var end = new DateTimeOffset(2026, 9, 8, 0, 0, 2, TimeSpan.FromHours(8));
        Statistics.Record(state, Seconds(4), end, Seconds(104));
        Equal(2d, state.Days[0].ActiveSeconds); Equal(2d, state.Days[1].ActiveSeconds);
        Equal(102d, state.Days[0].LongestSeconds); Equal(104d, state.Days[1].LongestSeconds);
        Statistics.Record(state, Seconds(30), end, Seconds(200)); Equal(4d, state.Days.Sum(d => d.ActiveSeconds));
    }),
    ("CSV export is ordered and culture independent", () =>
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("fr-FR");
            var csv = Statistics.Csv([new DayStats { Date = new(2026, 9, 8), ActiveSeconds = 90 }, new DayStats { Date = new(2026, 9, 7) }]);
            True(csv.Contains("2026-09-08,1.50,0.00,0,0,0")); True(csv.IndexOf("2026-09-07", StringComparison.Ordinal) < csv.IndexOf("2026-09-08", StringComparison.Ordinal));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = before; }
    }),
    ("Valid backup restores without destroying corrupt original", () => WithDirectory(path =>
    {
        var store = new LocalStore(path); var state = store.Load(); LocalStore.Day(state, new(2026, 9, 7)).EyeBreaks = 5;
        store.Save(state); store.Save(state); File.WriteAllText(Path.Combine(path, "state.json"), "{broken");
        var reopened = new LocalStore(path); reopened.Load(); True(reopened.ReadOnly);
        var recovered = reopened.RestoreBackup(); Equal(5, recovered.Days.Single().EyeBreaks); True(!reopened.ReadOnly);
        Equal("{broken", File.ReadAllText(Directory.GetFiles(path, "state.json.preserved-*").Single()));
        reopened.Save(recovered); Equal(5, new LocalStore(path).Load().Days.Single().EyeBreaks);
    })),
    ("Invalid backup cannot replace current state", () => WithDirectory(path =>
    {
        var store = new LocalStore(path); store.Save(store.Load());
        var original = File.ReadAllText(Path.Combine(path, "state.json"));
        File.WriteAllText(Path.Combine(path, "state.json.bak"), "{broken"); ThrowsIo(() => store.RestoreBackup());
        Equal(original, File.ReadAllText(Path.Combine(path, "state.json")));
    })),
    ("Old preferences load new defaults without migration", () => WithDirectory(path =>
    {
        File.WriteAllText(Path.Combine(path, "state.json"), "{\"SchemaVersion\":1,\"Preferences\":{\"EyeIntervalMinutes\":25}}");
        var store = new LocalStore(path); var state = store.Load(); True(!store.ReadOnly);
        Equal(25, state.Preferences.EyeIntervalMinutes); True(state.Preferences.SoundEnabled); True(!state.Preferences.QuietHoursEnabled);
    })),
    ("Eye reminder becomes due at 20 minutes", () =>
    {
        var e = Engine(); Work(e, 1199); Equal(Reminder.Eyes, e.Advance(Seconds(1), Active()));
    }),
    ("Eye completion preserves body exposure", () =>
    {
        var e = Engine(); Work(e, 1200); True(e.Complete(BreakKind.Eyes, Seconds(20)));
        Equal(Seconds(1200), e.Continuous); Equal(TimeSpan.Zero, e.EyeExposure);
    }),
    ("Body reminder takes priority over eyes", () =>
    {
        var e = Engine(); Work(e, 3600, quiet: true);
        Equal(Reminder.Movement, e.Advance(Seconds(1), Active()));
    }),
    ("Natural rest resets exposure but awards no active time", () =>
    {
        var e = Engine(); Work(e, 1500); e.Advance(Seconds(1), new(Seconds(300)));
        Equal(TimeSpan.Zero, e.Continuous); Equal(Seconds(1500), e.TotalActive);
    }),
    ("Short idle freezes but does not reset exposure", () =>
    {
        var e = Engine(); Work(e, 1500); e.Advance(Seconds(1), new(Seconds(100)));
        Equal(Seconds(1500), e.Continuous);
    }),
    ("Meeting continues exposure even without input", () =>
    {
        var e = Engine(); Work(e, 100);
        for (var i = 0; i < 600; i++) Equal(Reminder.None, e.Advance(Seconds(1), new(Seconds(600), Quiet: true, KeepCountingWithoutInput: true)));
        Equal(Seconds(700), e.Continuous);
    }),
    ("Disable inference for readers", () =>
    {
        var e = new BreakEngine(new Preferences { InferNaturalRest = false });
        e.Advance(Seconds(1), new(Seconds(1800))); Equal(Seconds(1), e.Continuous);
    }),
    ("Quiet mode does not erase overdue reminder", () =>
    {
        var e = Engine(); Work(e, 4800, quiet: true);
        Equal(Reminder.Movement, e.Advance(Seconds(1), Active()));
    }),
    ("Snooze delays delivery without erasing exposure", () =>
    {
        var e = Engine(); Work(e, 3600, quiet: true); e.Snooze(Seconds(300));
        for (var i = 0; i < 299; i++) Equal(Reminder.None, e.Advance(Seconds(1), Active()));
        Equal(Reminder.Movement, e.Advance(Seconds(1), Active())); Equal(Seconds(3900), e.Continuous);
    }),
    ("Unexplained long gap is neither work nor rest", () =>
    {
        var e = Engine(); Work(e, 100); e.Advance(Seconds(7200), Active());
        Equal(Seconds(100), e.Continuous); Equal(Seconds(100), e.TotalActive);
    }),
    ("Observed sleep qualifies as absence", () =>
    {
        var e = Engine(); Work(e, 100); e.ObserveAbsence(Seconds(600));
        Equal(TimeSpan.Zero, e.Continuous); Equal(Seconds(100), e.TotalActive);
    }),
    ("Brief lock does not reset continuous work", () =>
    {
        var e = Engine(); Work(e, 100); e.ObserveAbsence(Seconds(30)); Equal(Seconds(100), e.Continuous);
    }),
    ("Locked samples never add active time", () =>
    {
        var e = Engine(); Work(e, 100);
        for (var i = 0; i < 300; i++) e.Advance(Seconds(1), new(TimeSpan.Zero, Unavailable: true));
        Equal(Seconds(100), e.TotalActive); Equal(TimeSpan.Zero, e.Continuous);
    }),
    ("Incomplete activity is not credited", () =>
    {
        var e = Engine(); Work(e, 500); True(!e.Complete(BreakKind.Movement, Seconds(179)));
        Equal(Seconds(500), e.Continuous);
    }),
    ("Completed body break resets both clocks", () =>
    {
        var e = Engine(); Work(e, 500); True(e.Complete(BreakKind.Movement, Seconds(180)));
        Equal(TimeSpan.Zero, e.Continuous); Equal(TimeSpan.Zero, e.EyeExposure); Equal(Seconds(500), e.Longest);
    }),
    ("Invalid samples cannot reverse time", () =>
    {
        var e = Engine(); Work(e, 100); e.Advance(Seconds(-5), Active()); Equal(Seconds(100), e.Continuous);
    }),
    ("Fractional tick durations accumulate accurately", () =>
    {
        var e = Engine(); for (var i = 0; i < 100; i++) e.Advance(TimeSpan.FromMilliseconds(125), Active());
        Equal(TimeSpan.FromMilliseconds(12500), e.TotalActive);
    }),
    ("Gentle and standard programs preserve advertised durations", () =>
    {
        foreach (var kind in Enum.GetValues<BreakKind>()) foreach (var gentle in new[] { true, false })
            Equal(Programs.Duration(kind), Seconds(Programs.For(kind, gentle).Sum(x => x.Seconds)));
    }),
    ("Session completes only after observed duration", () =>
    {
        var s = new BreakSession(BreakKind.Movement, true);
        for (var i = 0; i < 179; i++) s.Advance(Seconds(1));
        True(!s.Finished); s.Advance(Seconds(1)); True(s.FullyCompleted); Equal(Seconds(180), s.Observed);
    }),
    ("Skipped steps do not award completion", () =>
    {
        var s = new BreakSession(BreakKind.Movement, true); s.Skip();
        for (var i = 0; i < 150; i++) s.Advance(Seconds(1));
        True(s.Finished); True(!s.FullyCompleted);
    }),
    ("Sleep and paused session cannot auto-complete", () =>
    {
        var s = new BreakSession(BreakKind.Eyes, true); s.Advance(Seconds(600));
        Equal(TimeSpan.Zero, s.Observed); s.Paused = true; s.Advance(Seconds(5)); Equal(TimeSpan.Zero, s.Observed);
    }),
    ("Overshoot is capped to session length", () =>
    {
        var s = new BreakSession(BreakKind.Eyes, true);
        s.Advance(Seconds(9)); s.Advance(Seconds(9)); s.Advance(Seconds(9)); Equal(Seconds(20), s.Observed); True(s.FullyCompleted);
    }),
    ("Settings values are bounded", () =>
    {
        var p = new Preferences { EyeIntervalMinutes = -1, MovementIntervalMinutes = 10000, NaturalRestMinutes = 0 }.Validate();
        Equal(10, p.EyeIntervalMinutes); Equal(120, p.MovementIntervalMinutes); Equal(3, p.NaturalRestMinutes);
    }),
    ("Local settings and stats survive restart with backup", () => WithDirectory(path =>
    {
        var store = new LocalStore(path); var state = store.Load();
        LocalStore.Day(state, new DateOnly(2026, 9, 7)).MovementBreaks = 3; store.Save(state);
        state.Preferences = state.Preferences with { GentleOnly = false }; store.Save(state);
        var loaded = new LocalStore(path).Load(); Equal(3, loaded.Days.Single().MovementBreaks);
        True(!loaded.Preferences.GentleOnly); True(File.Exists(Path.Combine(path, "state.json.bak")));
    })),
    ("Corrupt state remains untouched", () => WithDirectory(path =>
    {
        var file = Path.Combine(path, "state.json"); File.WriteAllText(file, "{broken");
        var store = new LocalStore(path); var state = store.Load(); ThrowsIo(() => store.Save(state));
        True(store.ReadOnly); True(store.LoadWarning is not null); Equal("{broken", File.ReadAllText(file));
    })),
    ("Future schema remains untouched", () => WithDirectory(path =>
    {
        var file = Path.Combine(path, "state.json"); const string json = "{\"SchemaVersion\":99}"; File.WriteAllText(file, json);
        var store = new LocalStore(path); ThrowsIo(() => store.Save(store.Load())); True(store.ReadOnly); Equal(json, File.ReadAllText(file));
    })),
    ("Invalid counts are rejected", () => WithDirectory(path =>
    {
        File.WriteAllText(Path.Combine(path, "state.json"), "{\"Days\":[{\"Date\":\"2026-09-07\",\"EyeBreaks\":-1}]}");
        var store = new LocalStore(path); store.Load(); True(store.ReadOnly);
    })),
    ("Duplicate dates are rejected", () => WithDirectory(path =>
    {
        File.WriteAllText(Path.Combine(path, "state.json"), "{\"Days\":[{\"Date\":\"2026-09-07\"},{\"Date\":\"2026-09-07\"}]}");
        var store = new LocalStore(path); store.Load(); True(store.ReadOnly);
    }))
};

var failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
return failed == 0 ? 0 : 1;

static BreakEngine Engine() => new(new Preferences());
static ActivitySample Active(bool quiet = false) => new(TimeSpan.Zero, Quiet: quiet);
static TimeSpan Seconds(double n) => TimeSpan.FromSeconds(n);
static void Work(BreakEngine engine, int seconds, bool quiet = false)
{
    for (var i = 0; i < seconds; i++) engine.Advance(Seconds(1), Active(quiet));
}
static void True(bool value) { if (!value) throw new InvalidOperationException("Expected true"); }
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}, got {actual}");
}
static void WithDirectory(Action<string> run)
{
    var path = Path.Combine(Path.GetTempPath(), "WorkGuardTests-" + Guid.NewGuid());
    Directory.CreateDirectory(path);
    try { run(path); } finally { Directory.Delete(path, recursive: true); }
}

static void ThrowsIo(Action action) { try { action(); } catch (IOException) { return; } throw new InvalidOperationException("Expected IOException"); }

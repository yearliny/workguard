using WorkGuard.Core;
using WorkGuard.Data;

internal static class MaintenanceTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Maintenance behavior assertion failed."); }
    private static DateTimeOffset Start => new(2026, 9, 9, 9, 0, 0, TimeSpan.FromHours(8));
    private static MaintenanceSession Short() => new(MaintenancePlanner.Build(new(), new Dictionary<BodyArea, double>(), false));
    private static void Run(MaintenanceSession session, int seconds, DateTimeOffset? start = null)
    { var now = start ?? Start; for (var i = 0; i < seconds; i++) session.Advance(TimeSpan.FromSeconds(1), now.AddSeconds(i + 1)); }
    private static void DirectoryTest(Action<string> action)
    { var folder = Path.Combine(Path.GetTempPath(), "maintenance-" + Guid.NewGuid()); Directory.CreateDirectory(folder); try { action(folder); } finally { Directory.Delete(folder, true); } }
    public static (string Name, Action Run)[] All =>
    [
        ("Shoulder routine preserves order, two sets and manual duration", () =>
        {
            var plan = MaintenancePlanner.ShoulderNeck(new Preferences { NeckMovements = true }, true);
            Check(plan.Count == 8 && plan.Sum(s => s.TotalSeconds) == 490);
            Check(string.Join(",", plan.Select(s => s.Exercise.Id)) == "thoracic-turn,thoracic-extension,wall-slide,wall-slide,band-pull-apart,band-pull-apart,neck-retraction,shoulder-shrug");
            Check(plan[2].Exercise.Title.Contains("1/2") && plan[3].Exercise.Title.Contains("2/2"));
            var session = new MaintenanceSession(plan); Run(session, session.TotalSeconds);
            Check(session.FullyPracticed && !session.Confirmed);
            Check(session.Confirm([2]).Sum(c => c.Seconds) == 60);
            Check(session.Confirm([3]).Count == 0);
        }),
        ("Shoulder routine respects equipment, neck consent and blocked actions", () =>
        {
            var p = new Preferences();
            var plan = MaintenancePlanner.ShoulderNeck(p, false);
            Check(plan.All(s => s.Exercise.Area != BodyArea.Neck && s.Exercise.Id != "band-pull-apart"));
            p = p with { MaintenanceStanding = false, NeckMovements = true, MaintenanceBlockedExercises = ["neck-retraction", "shoulder-shrug"] };
            plan = MaintenancePlanner.ShoulderNeck(p, true);
            Check(plan.All(s => !s.Exercise.Standing && s.Exercise.Id != "neck-retraction" && s.Exercise.Id != "shoulder-shrug"));
            Check(MaintenancePlanner.ShoulderNeck(p with { MaintenanceExcludedAreas = BodyArea.All }, true).Count == 0);
            Check(MaintenanceCatalog.Allowed(new Preferences { NeckMovements = true }).All(e => !e.RoutineOnly));
        }),
        ("Maintenance restrictions also protect legacy movement and office fallback", () =>
        {
            var p = new Preferences { MaintenanceEnabled = true };
            Check(p.UseFreeRest(BreakKind.Movement) && !p.UseFreeRest(BreakKind.Office) && !p.UseFreeRest(BreakKind.Eyes));
            p = p with { MaintenanceEnabled = false, MaintenanceBlockedExercises = ["neck-turn"] };
            Check(p.UseFreeRest(BreakKind.Movement) && p.UseFreeRest(BreakKind.Office));
            Check(new Preferences { MaintenanceStanding = false }.UseFreeRest(BreakKind.Office));
            Check(new Preferences { MaintenanceExcludedAreas = BodyArea.Shoulders }.UseFreeRest(BreakKind.Movement));
            var free = new BreakSession(BreakKind.Office, false, neck: true, freeRest: true, standing: false);
            Check(free.Exercises.Count == 1 && free.Exercises.Single().Seconds == 420 && free.Exercises.Single().Scene == RestScene.Distance);
        }),
        ("Maintenance plan covers areas before repeating and excludes neck by default", () =>
        {
            var plan = MaintenancePlanner.Build(new(), new Dictionary<BodyArea, double>(), true);
            Check(plan.Sum(s => s.Seconds) == 480 && plan.Sum(s => s.TotalSeconds) == 520);
            Check(plan.Take(5).Select(s => s.Exercise.Area).Distinct().Count() == 5);
            Check(plan.All(s => s.Exercise.Area != BodyArea.Neck));
        }),
        ("Maintenance preferences filter exclusions, standing and blocked actions", () =>
        {
            var p = new Preferences { MaintenanceStanding = false, MaintenanceExcludedAreas = BodyArea.Thoracic,
                NeckMovements = true, MaintenanceBlockedExercises = ["neck-turn", "neck-nod"] };
            var plan = MaintenancePlanner.Build(p, new Dictionary<BodyArea, double>(), true);
            Check(plan.All(s => !s.Exercise.Standing && s.Exercise.Area is not (BodyArea.Thoracic or BodyArea.Neck)));
            Check(MaintenancePlanner.Build(p with { MaintenanceExcludedAreas = BodyArea.All }, new Dictionary<BodyArea, double>(), true).Count == 0);
        }),
        ("Maintenance concentrated mode resumes remaining daily coverage", () =>
        {
            var covered = new Dictionary<BodyArea, double> { [BodyArea.Thoracic] = 60, [BodyArea.Shoulders] = 60 };
            var plan = MaintenancePlanner.Build(new(), covered, true);
            Check(plan.Sum(s => s.Seconds) == 360 && plan.First().Exercise.Area == BodyArea.Hips);
            covered[BodyArea.Hips] = 500;
            Check(MaintenancePlanner.Build(new(), covered, false).Count == 0);
            Check(MaintenancePlanner.Build(new(), new Dictionary<BodyArea, double>(), false).Sum(s => s.Seconds) == 120);
        }),
        ("Maintenance preparation, pause and long gaps do not credit practice", () =>
        {
            var session = Short(); Run(session, 5); Check(session.ObservedPractice == 0 && !session.Preparing);
            Run(session, 10); Check(session.ObservedPractice == 10);
            session.Paused = true; Run(session, 20); Check(session.ObservedPractice == 10);
            session.Paused = false; session.Advance(TimeSpan.FromSeconds(11), Start); Check(session.ObservedPractice == 10);
            Check(session.Confirm([0]).Count == 0 && !session.Confirmed);
        }),
        ("Maintenance side switch and total remaining share the same clock", () =>
        {
            var session = new MaintenanceSession([new(MaintenanceCatalog.All.First(e => e.Id == "thoracic-turn"), 60)]);
            Run(session, 34); Check(session.Side == 1 && session.TotalRemaining == 31);
            Run(session, 1); Check(session.Side == 2 && session.TotalRemaining == 30);
            Run(session, 30); Check(session.FullyPracticed && session.TotalRemaining == 0 && session.ObservedPractice == 60);
        }),
        ("Maintenance playback alone grants no confirmation and confirmation is idempotent", () =>
        {
            var session = Short(); Run(session, session.TotalSeconds);
            Check(session.FullyPracticed && !session.Confirmed);
            Check(session.Confirm([0, 1]).Sum(x => x.Seconds) == 120);
            Check(session.Confirmed && session.Confirm([0, 1]).Count == 0);
        }),
        ("Maintenance partial and unchecked actions grant only selected observed seconds", () =>
        {
            var session = Short(); Run(session, 15); session.Skip(); Run(session, 65);
            Check(session.Finished && !session.FullyPracticed && session.ObservedPractice == 70);
            Check(session.Confirm([0]).Sum(x => x.Seconds) == 10);
            var noPractice = Short(); Run(noPractice, noPractice.TotalSeconds); Check(noPractice.Confirm([]).Count == 0);
        }),
        ("Replacing maintenance action discards its old practice and redoes preparation", () =>
        {
            var session = Short(); Run(session, 20);
            Check(session.Replace(MaintenanceCatalog.All.First(e => e.Id == "ankle-pump")));
            Check(session.ObservedPractice == 0 && session.Preparing && session.TotalRemaining == session.TotalSeconds);
            Run(session, session.TotalSeconds);
            Check(session.Confirm([0]).All(c => c.ExerciseId == "ankle-pump"));
        }),
        ("Maintenance practice is allocated across midnight before delayed confirmation", () =>
        {
            var session = new MaintenanceSession([new(MaintenanceCatalog.All[0], 10)]);
            Run(session, 15, new(2026, 9, 9, 23, 59, 50, TimeSpan.FromHours(8)));
            var credits = session.Confirm([0]);
            Check(credits.Count == 2 && credits.Single(x => x.Date == new DateOnly(2026, 9, 9)).Seconds == 5);
            Check(credits.Single(x => x.Date == new DateOnly(2026, 9, 10)).Seconds == 5);
        }),
        ("Maintenance stopping cannot grant a full flow or subsequent time", () =>
        {
            var session = Short(); Run(session, 20); session.Stop(); Run(session, 100);
            Check(session.Finished && session.Stopped && !session.FullyPracticed && session.ObservedPractice == 15);
        }),
        ("Only fully followed two-minute maintenance resets exposure", () =>
        {
            var engine = new BreakEngine(new()); for (var i = 0; i < 10; i++) engine.Advance(TimeSpan.FromSeconds(1), new(TimeSpan.Zero));
            Check(!engine.CompleteMaintenance(false, TimeSpan.FromMinutes(8)) && engine.Continuous.TotalSeconds == 10);
            Check(!engine.CompleteMaintenance(true, TimeSpan.FromSeconds(119)) && engine.Continuous.TotalSeconds == 10);
            Check(engine.CompleteMaintenance(true, TimeSpan.FromSeconds(120)) && engine.Continuous == TimeSpan.Zero);
        }),
        ("Maintenance v1 migration preserves history and v2 roundtrip persists exclusions", () => DirectoryTest(path =>
        {
            File.WriteAllText(Path.Combine(path, "state.json"), "{\"SchemaVersion\":1,\"Days\":[{\"Date\":\"2026-09-09\",\"EyeBreaks\":3}]}");
            var store = new LocalStore(path); var state = store.Load();
            Check(!store.ReadOnly && state.SchemaVersion == 2 && state.Days.Single().EyeBreaks == 3 && !state.Preferences.MaintenanceEnabled);
            state.Preferences = state.Preferences with { MaintenanceBlockedExercises = ["neck-turn"], MaintenanceGoalMinutes = 9 };
            state.Days.Single().MaintenanceSeconds[BodyArea.Shoulders] = 72.5; store.Save(state);
            var restored = new LocalStore(path).Load();
            Check(restored.Days.Single().MaintenanceSeconds[BodyArea.Shoulders] == 72.5 && restored.Preferences.MaintenanceBlockedExercises.Single() == "neck-turn");
            Check(Statistics.Csv(restored.Days).Contains("maintenance_confirmed_minutes"));
        })),
        ("Malformed maintenance totals preserve original data in read-only mode", () => DirectoryTest(path =>
        {
            foreach (var value in new[] { "null", "{\"Shoulders\":-1}", "{\"All\":10}", "{\"3\":10}", "{\"Shoulders\":1e300}" })
            {
                var text = "{\"SchemaVersion\":2,\"Days\":[{\"Date\":\"2026-09-09\",\"MaintenanceSeconds\":" + value + "}]}";
                File.WriteAllText(Path.Combine(path, "state.json"), text);
                var store = new LocalStore(path); store.Load(); Check(store.ReadOnly && File.ReadAllText(Path.Combine(path, "state.json")) == text);
            }
        })),
        ("Maintenance values normalize unknown blocked IDs and goal range", () =>
        {
            var p = new Preferences { MaintenanceGoalMinutes = 99, MaintenanceBlockedExercises = ["unknown", "neck-turn", "neck-turn"] }.Validate();
            Check(p.MaintenanceGoalMinutes == 10 && p.MaintenanceBlockedExercises.Length == 1);
            Check(new Preferences { MaintenanceGoalMinutes = 0, MaintenanceBlockedExercises = null! }.Validate().MaintenanceGoalMinutes == 5);
        })
    ];
}

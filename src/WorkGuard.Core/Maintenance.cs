namespace WorkGuard.Core;

[Flags]
public enum BodyArea { None = 0, Thoracic = 1, Shoulders = 2, Hips = 4, BackLegs = 8, Ankles = 16, Neck = 32, All = 63 }
public enum MovementDemo { Rotate, OpenChest, Reach, Shoulders, March, HipExtension, Hamstring, SitStand, Ankle, Calf, NeckTurn, Nod }

public sealed record MaintenanceExercise(string Id, string Title, BodyArea Area, string Setup,
    string Cue, MovementDemo Demo, bool Standing = false, bool Bilateral = false)
{
    public const int ContentVersion = 1;
}

public static class MaintenanceCatalog
{
    public static IReadOnlyList<MaintenanceExercise> All { get; } = Array.AsReadOnly(new MaintenanceExercise[]
    {
        new("thoracic-turn", "胸椎轻旋转", BodyArea.Thoracic, "坐稳，双脚着地，双臂轻抱胸前。", "骨盆保持稳定，胸口缓慢转向一侧，再回正。只到舒服的范围。", MovementDemo.Rotate, Bilateral: true),
        new("chest-open", "轻轻打开胸口", BodyArea.Thoracic, "坐稳，双手轻放大腿，肩膀放松。", "胸口轻轻向前上方打开，再放松。不要仰头或用力挺腰。", MovementDemo.OpenChest),
        new("scapular-reach", "肩胛向前，再回来", BodyArea.Shoulders, "坐稳，双臂在胸前舒适高度向前伸。", "双手轻轻向前送，让肩胛滑动，再回到自然位置。不耸肩。", MovementDemo.Reach),
        new("scapular-release", "肩胛轻收与放松", BodyArea.Shoulders, "坐稳或站稳，双臂自然下垂。", "轻轻让肩胛靠拢，再完全放松。不用力夹紧，不憋气。", MovementDemo.Shoulders),
        new("seated-march", "坐姿交替抬膝", BodyArea.Hips, "用稳固、无滚轮的椅子，坐稳并扶住椅侧。", "一侧脚轻轻离地，再缓慢放下，左右交替。保持身体稳定。", MovementDemo.March),
        new("standing-march", "扶稳交替抬膝", BodyArea.Hips, "站在牢固桌面旁，一手扶稳。", "轻轻抬起一侧膝盖，再放下，左右交替。不追求高度。", MovementDemo.March, Standing: true),
        new("hip-extension", "扶稳轻轻向后伸腿", BodyArea.Hips, "面对牢固桌面，双手扶稳，身体直立。", "一条腿小幅向后移，再回到原位。不要挺腰或前倾。", MovementDemo.HipExtension, Standing: true, Bilateral: true),
        new("hamstring", "温和活动腿后侧", BodyArea.BackLegs, "坐稳，一脚向前放，脚跟着地，膝盖留一点弯曲。", "从髋部轻轻前倾，出现温和牵拉感就停住，再回正。不低头弓背，不追求触脚。", MovementDemo.Hamstring, Bilateral: true),
        new("sit-stand", "扶稳缓慢坐站", BodyArea.Hips, "确认椅子稳固、无滚轮，必要时扶稳。", "按自己的节奏站起，再慢慢坐下。膝部不适就停止。", MovementDemo.SitStand, Standing: true),
        new("ankle-pump", "脚踝轻轻活动", BodyArea.Ankles, "坐稳，一脚稍向前，腿部保持舒适支撑。", "脚尖缓慢向前，再轻轻勾回。幅度舒服就好。", MovementDemo.Ankle, Bilateral: true),
        new("calf-raise", "扶稳抬起脚跟", BodyArea.Ankles, "站在牢固桌面旁，双手扶稳。", "缓慢抬起脚跟，再轻轻放下。保持平衡，不必踮到最高。", MovementDemo.Calf, Standing: true),
        new("neck-turn", "舒适范围内转头", BodyArea.Neck, "坐稳，肩膀放松，目光平视。", "缓慢转向一侧，再回正。不绕颈，不用手压头。出现不适立即停止。", MovementDemo.NeckTurn, Bilateral: true),
        new("neck-nod", "轻轻点头，再回正", BodyArea.Neck, "坐稳，让头部处于自然位置。", "做很小幅度的点头，再回到自然位置。不用力后压头部。", MovementDemo.Nod)
    });
    public static string Label(BodyArea area) => area switch
    {
        BodyArea.Thoracic => "胸椎", BodyArea.Shoulders => "肩胛", BodyArea.Hips => "髋部",
        BodyArea.BackLegs => "腿后侧", BodyArea.Ankles => "踝与小腿", BodyArea.Neck => "颈部", _ => "身体"
    };
    public static IReadOnlyList<MaintenanceExercise> Allowed(Preferences p) => All.Where(e =>
        (p.MaintenanceExcludedAreas & e.Area) == 0 && (e.Area != BodyArea.Neck || p.NeckMovements) &&
        (!e.Standing || p.MaintenanceStanding) && !p.MaintenanceBlockedExercises.Contains(e.Id)).ToArray();
}

public sealed record MaintenanceStep(MaintenanceExercise Exercise, int Seconds)
{
    public const int PreparationSeconds = 5;
    public int TotalSeconds => Seconds + PreparationSeconds;
}

public static class MaintenancePlanner
{
    // Dates and recorded coverage are supplied by the caller; no clock or platform dependencies.
    // Duration is actual practice time. Preparation is separately included in the displayed estimate.
    public static IReadOnlyList<MaintenanceStep> Build(Preferences preferences,
        IReadOnlyDictionary<BodyArea, double> covered, bool concentrated, int? practiceBudget = null)
    {
        var p = preferences.Validate();
        var allowed = MaintenanceCatalog.Allowed(p);
        var remaining = Math.Max(0, p.MaintenanceGoalMinutes * 60 - (int)Math.Floor(covered.Values.Sum()));
        var budget = Math.Min(remaining, practiceBudget ?? (concentrated ? remaining : 120));
        if (allowed.Count == 0 || budget <= 0) return [];
        var planned = allowed.Select(x => x.Area).Distinct().ToDictionary(a => a, a => covered.GetValueOrDefault(a));
        var steps = new List<MaintenanceStep>();
        while (budget > 0)
        {
            var area = planned.OrderBy(x => x.Value).ThenBy(x => (int)x.Key).First().Key;
            var candidates = allowed.Where(e => e.Area == area).ToArray();
            var exercise = candidates[((int)(planned[area] / 60)) % candidates.Length];
            var seconds = Math.Min(60, budget);
            steps.Add(new(exercise, seconds)); planned[area] += seconds; budget -= seconds;
        }
        return steps;
    }
}

public sealed record MaintenanceCredit(DateOnly Date, BodyArea Area, string ExerciseId, double Seconds);

public sealed class MaintenanceSession
{
    private readonly List<MaintenanceStep> _steps;
    private readonly Dictionary<(int Index, DateOnly Date), double> _practice = [];
    private readonly HashSet<int> _skipped = [];
    public MaintenanceSession(IEnumerable<MaintenanceStep> steps)
    {
        _steps = steps.ToList();
        if (_steps.Count == 0 || _steps.Any(s => s.Seconds is < 1 or > 60)) throw new ArgumentException("A nonempty practice plan is required.");
        TotalSeconds = _steps.Sum(s => s.TotalSeconds);
    }
    public IReadOnlyList<MaintenanceStep> Steps => _steps.AsReadOnly();
    public int Index { get; private set; }
    public bool Finished => Index >= _steps.Count;
    public bool Paused { get; set; }
    public bool Confirmed { get; private set; }
    public bool Stopped { get; private set; }
    public double StepElapsed { get; private set; }
    public double TimelineSeconds { get; private set; }
    public int TotalSeconds { get; }
    public double TotalRemaining => Math.Max(0, TotalSeconds - TimelineSeconds);
    public bool Preparing => !Finished && StepElapsed < MaintenanceStep.PreparationSeconds;
    public MaintenanceStep Current => _steps[Math.Min(Index, _steps.Count - 1)];
    public double PracticeElapsed => Math.Max(0, StepElapsed - MaintenanceStep.PreparationSeconds);
    public double ObservedPractice => _practice.Values.Sum();
    public bool FullyPracticed => Finished && !Stopped && _skipped.Count == 0 && ObservedPractice >= _steps.Sum(s => s.Seconds) - .001;
    public double PracticedAt(int index) => _practice.Where(x => x.Key.Index == index).Sum(x => x.Value);
    public int Side => Current.Exercise.Bilateral && PracticeElapsed >= Current.Seconds / 2.0 ? 2 : 1;
    public string Phase => Preparing ? "准备姿势" : Current.Exercise.Bilateral ? (Side == 1 ? "左侧" : "右侧") : "缓慢跟练";

    public void Advance(TimeSpan elapsed, DateTimeOffset end)
    {
        if (Paused || Finished || Confirmed || elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(10)) return;
        var left = elapsed.TotalSeconds;
        var cursor = end - elapsed;
        while (left > .000001 && !Finished)
        {
            var boundary = Preparing ? MaintenanceStep.PreparationSeconds : Current.TotalSeconds;
            var take = Math.Min(left, boundary - StepElapsed);
            if (!Preparing)
            {
                var until = cursor.AddSeconds(take);
                while (cursor < until)
                {
                    var midnight = new DateTimeOffset(cursor.Date.AddDays(1), cursor.Offset);
                    var segmentEnd = until < midnight ? until : midnight;
                    var key = (Index, DateOnly.FromDateTime(cursor.Date));
                    _practice[key] = _practice.GetValueOrDefault(key) + (segmentEnd - cursor).TotalSeconds;
                    cursor = segmentEnd;
                }
            }
            else cursor = cursor.AddSeconds(take);
            StepElapsed += take; TimelineSeconds += take; left -= take;
            if (StepElapsed >= Current.TotalSeconds - .000001) { Index++; StepElapsed = 0; }
        }
    }
    public void Skip()
    {
        if (Finished || Confirmed) return;
        _skipped.Add(Index);
        TimelineSeconds += Current.TotalSeconds - StepElapsed;
        Index++; StepElapsed = 0;
    }
    // A replacement must be supplied from the currently allowed catalog by the controller.
    public bool Replace(MaintenanceExercise replacement)
    {
        if (Finished || Confirmed || replacement.Id == Current.Exercise.Id) return false;
        // Discard practice on the replaced step; it must never credit the new exercise.
        foreach (var key in _practice.Keys.Where(k => k.Index == Index).ToArray()) _practice.Remove(key);
        TimelineSeconds -= StepElapsed; StepElapsed = 0;
        _steps[Index] = new(replacement, Current.Seconds);
        return true;
    }
    public void Stop() { if (!Confirmed) { Stopped = true; Index = _steps.Count; StepElapsed = 0; } }
    public IReadOnlyList<MaintenanceCredit> Confirm(IEnumerable<int> selected)
    {
        if (!Finished || Confirmed) return [];
        Confirmed = true;
        var indices = selected.ToHashSet();
        return _practice.Where(x => indices.Contains(x.Key.Index) && x.Value > 0).Select(x =>
            new MaintenanceCredit(x.Key.Date, _steps[x.Key.Index].Exercise.Area, _steps[x.Key.Index].Exercise.Id, x.Value)).ToArray();
    }
}

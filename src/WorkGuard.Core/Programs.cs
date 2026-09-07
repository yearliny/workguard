namespace WorkGuard.Core;

public sealed record Exercise(string Title, string Instruction, int Seconds, string Glyph);

public static class Programs
{
    public static TimeSpan Duration(BreakKind kind) => TimeSpan.FromSeconds(kind switch
    {
        BreakKind.Eyes => 20, BreakKind.Movement => 180, _ => 420
    });

    // General movement prompts, not treatment protocols or high-intensity training.
    public static IReadOnlyList<Exercise> For(BreakKind kind, bool gentle) => kind switch
    {
        BreakKind.Eyes => [new("目光，放远一点", "看向窗外或远处，让眼睛离开屏幕。自然眨眼，不必盯着倒计时。", 20, "◉")],
        BreakKind.Movement =>
        [
            new("离开座位", "慢慢站起，在安全、平整的地方走动。需要时扶稳桌面。", 30, "↗"),
            new("肩膀放松", "放松肩膀，轻轻让肩胛骨向后靠拢，再放松。不耸肩，不用力夹紧。", 30, "↔"),
            new("换个姿势", "轻松站立或慢走，让头颈保持自然位置。不要用力拉头或绕颈。", 30, "◇"),
            new("手腕松一松", "放松双手，缓慢张开、合拢手指，在舒适范围内轻轻活动手腕。", 30, "✧"),
            gentle
                ? new("继续走一走", "按舒服的速度走动。不方便站立时，可调整坐姿并轻轻活动手脚。", 30, "↗")
                : new("缓慢坐站", "使用稳固、无滚轮的椅子，缓慢坐下再起立。膝部不适就跳过。", 30, "↕"),
            new("带着轻松回来", "再走几步，看看远处。准备好了，再回到工作。", 30, "○")
        ],
        _ =>
        [
            new("先走一分钟", "离开座位，用舒适的速度走动，让身体逐渐活动起来。", 60, "↗"),
            gentle
                ? new("轻松活动", "继续慢走或轻轻调整站姿。以舒适为准，不追求幅度。", 60, "◇")
                : new("缓慢坐站", "用稳固、无滚轮的椅子，按自己的节奏坐下、起立。可以随时休息。", 60, "↕"),
            gentle
                ? new("肩背放松", "放松肩膀，轻轻收拢再放松肩胛骨。呼吸自然。", 60, "↔")
                : new("墙壁俯卧撑", "面对牢固墙面，双手扶墙，缓慢屈肘靠近后推回。肩腕不适就跳过。", 60, "↔"),
            new("再走一走", "在平整、无障碍的地方行走，注意脚下，不看屏幕。", 60, "↗"),
            new("手与肩放松", "舒展手指，轻轻活动手腕，放松双肩。动作保持舒适。", 60, "✧"),
            gentle
                ? new("换一个位置", "走到窗边或另一个安全位置。不能站立时，调整身体支撑。", 60, "◇")
                : new("扶稳提踵", "扶着稳固桌面，缓慢抬起脚跟、放下。保持平衡，不适就停止。", 60, "↕"),
            new("慢下来", "慢走，恢复自然呼吸。看看远处，让这几分钟成为真正的休息。", 60, "○")
        ]
    };
}

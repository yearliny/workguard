namespace WorkGuard.Windows.Views;

internal static class ReminderCopy
{
    public static string Title(DeliveryReason reason) => reason switch
    {
        DeliveryReason.Setup => "等待快速配置",
        DeliveryReason.Unavailable => "已锁屏或休眠",
        DeliveryReason.Resting => "休息时间",
        DeliveryReason.Settings => "正在调整偏好",
        DeliveryReason.Meeting => "会议中 · 提醒静默",
        DeliveryReason.Paused => "提醒已暂停",
        DeliveryReason.OutsideSchedule => "工作时段之外",
        DeliveryReason.QuietHours => "安静时段",
        DeliveryReason.Fullscreen => "全屏应用 · 提醒静默",
        DeliveryReason.Idle => "暂未检测到输入",
        DeliveryReason.ReminderOpen => "休息邀请已送达",
        DeliveryReason.Returning => "给你一点准备时间",
        _ => "提醒已开启"
    };

    public static string Detail(ReminderGate gate, Preferences p, BreakEngine engine, DateTime now, TimeSpan pause) => gate.Reason switch
    {
        DeliveryReason.Setup => "完成快速配置后，自动提醒才会开启。",
        DeliveryReason.Unavailable => "回来后留出 15 秒缓冲；中断的活动需要手动继续。",
        DeliveryReason.Resting => "跟随当前流程即可。暂停与未完整结束不会计为完整活动。",
        DeliveryReason.Settings => "关闭设置后留出 15 秒缓冲，再恢复到期提醒。",
        DeliveryReason.Meeting => "从托盘关闭会议模式后恢复；会议期间仍累计用屏估计。",
        DeliveryReason.Paused => $"约 {Math.Ceiling(pause.TotalMinutes)} 分钟后恢复；暂停不清零连续计时。",
        DeliveryReason.OutsideSchedule => WorkSchedule.NextStart(p, now) is { } next
            ? $"下个提醒时段 {next:MM-dd HH:mm} 开始；期间仍按原规则估计用屏。" : "当前没有可用的提醒时段，可在「我的安排」中调整。",
        DeliveryReason.QuietHours => $"{Clock(p.QuietEndMinute)} 后恢复；安静时段不清零连续计时。",
        DeliveryReason.Fullscreen => "离开全屏后留出 15 秒缓冲；全屏期间仍累计用屏估计。",
        DeliveryReason.Idle => "没有输入不等于离席；恢复输入后留出 15 秒缓冲。",
        DeliveryReason.ReminderOpen => "可从屏幕角落开始休息，也可以继续手头的事。",
        DeliveryReason.Returning => $"{Math.Ceiling(gate.Remaining.TotalSeconds)} 秒后恢复到期提醒。连续计时保持不变。",
        _ => engine.SnoozeRemaining > TimeSpan.Zero
            ? $"提醒已推迟，至少 {Math.Ceiling(engine.SnoozeRemaining.TotalMinutes)} 分钟后再提醒。"
            : p.StrictMode ? "到点自动全屏。会议、工作安排与静默规则仍然有效。" : "到点在屏幕角落轻提醒，不抢走键盘焦点。"
    };

    public static string Clock(int minute) => $"{minute / 60:00}:{minute % 60:00}";
    public static string Days(Preferences p)
    {
        if (p.WorkDays == 62) return "周一至周五";
        if (p.WorkDays == 127) return "每天";
        var names = new[] { "日", "一", "二", "三", "四", "五", "六" };
        return string.Join("、", new[] { 1, 2, 3, 4, 5, 6, 0 }.Where(i => WorkSchedule.IncludesDay(p, (DayOfWeek)i)).Select(i => "周" + names[i]));
    }
}

namespace SharpSnip.Core;

public enum AppState
{
    Idle, ModeMenu, ElementPicking, RegionPicking, WindowPicking, FullScreenPicking,
    FullScreenInstant, Captured, Editing, Exporting,
}

public enum PickMode { Menu, Element, Region, FixedRegion, Window, FullScreen }

/// <summary>The blueprint state machine (§1). Illegal transitions are refused, never thrown.</summary>
public sealed class AppStateMachine
{
    private static readonly AppState[] Pickers =
        { AppState.ElementPicking, AppState.RegionPicking, AppState.WindowPicking, AppState.FullScreenPicking };

    private static readonly Dictionary<AppState, AppState[]> Allowed = Build();

    private static Dictionary<AppState, AppState[]> Build()
    {
        var d = new Dictionary<AppState, AppState[]>
        {
            [AppState.Idle] = new[] { AppState.ModeMenu, AppState.FullScreenInstant },
            [AppState.ModeMenu] = Pickers.Append(AppState.Idle).ToArray(),
            [AppState.FullScreenInstant] = new[] { AppState.Captured, AppState.Exporting, AppState.Idle },
            [AppState.Captured] = new[] { AppState.Editing, AppState.Exporting, AppState.Idle },
            [AppState.Editing] = new[] { AppState.Exporting, AppState.Idle },
            [AppState.Exporting] = new[] { AppState.Idle },
        };
        foreach (var p in Pickers)
            d[p] = Pickers.Where(o => o != p).Concat(new[] { AppState.ModeMenu, AppState.Captured, AppState.Idle }).ToArray();
        return d;
    }

    public AppState State { get; private set; } = AppState.Idle;

    public bool TryTransition(AppState to)
    {
        if (!Allowed[State].Contains(to)) return false;
        State = to;
        return true;
    }

    public void ForceIdle() => State = AppState.Idle;

    public static AppState ForMode(PickMode mode) => mode switch
    {
        PickMode.Menu => AppState.ModeMenu,
        PickMode.Element => AppState.ElementPicking,
        PickMode.Region or PickMode.FixedRegion => AppState.RegionPicking,
        PickMode.Window => AppState.WindowPicking,
        _ => AppState.FullScreenPicking,
    };
}

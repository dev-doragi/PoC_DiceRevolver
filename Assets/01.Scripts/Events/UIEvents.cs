public struct GameModeChangedEvent
{
    public GameMode NewMode;
}

public class ReloadRequestedEvent
{
    public object NewMode { get; }

    public ReloadRequestedEvent(GameMode mode)
    {
        NewMode = mode;
    }

    public ReloadRequestedEvent(DiceMode mode)
    {
        NewMode = mode;
    }
}

public class DiceModeChangedEvent
{
    public DiceMode NewMode { get; }

    public DiceModeChangedEvent(DiceMode mode)
    {
        NewMode = mode;
    }
}

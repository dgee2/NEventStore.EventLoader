namespace NEventStore.EventLoader
{
    public interface ICanProcessEvent<TClass, in TEvent>
    {
        TClass ProcessEvent(TClass person, TEvent @event);
    }
}
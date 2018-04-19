namespace easyLib.DB
{
    public interface IDatumAccessor<T>: IDatumGetter<T>, IDatumSetter<T>
    { }


    public interface IDatumAccessor : IDatumAccessor<IDatum>
    { }
}

namespace easyLib.DB
{
    /*
     * Version: 1
     */ 
    public interface IDatumAccessor<T>: IDatumGetter<T>, IDatumSetter<T>
    { }
    //-------------------------------------------------------------------


    /*
     * Version: 1
     */ 
    public interface IDatumAccessor : IDatumAccessor<IDatum>
    { }
}

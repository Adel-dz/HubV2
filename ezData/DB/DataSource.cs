/*
 * Version: 1
 */  
namespace easyLib.DB
{
    public interface IDataSource<T>
    {
        uint ID { get; }    //nothhrow
        IDatumProvider DataProvider { get; }    //nothrow
    }
}

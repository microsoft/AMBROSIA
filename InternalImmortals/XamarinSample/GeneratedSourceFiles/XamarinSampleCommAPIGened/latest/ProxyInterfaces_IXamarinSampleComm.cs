
using System;
using Ambrosia;
using System.Threading.Tasks;
using static Ambrosia.StreamCommunicator;

namespace XamarinSampleCommAPI
{
    /// <summary>
    // Generated from IXamarinSampleComm by the proxy generation.
    // This is the API that any immortal implementing the interface must be a subtype of.
    /// </summary>
    public interface IXamarinSampleComm
    {
        Task<Boolean> DetAddItemAsync(CommInterfaceClasses.Item p_0);
        Task<Boolean> DetUpdateItemAsync(CommInterfaceClasses.Item p_0);
        Task<Boolean> DetDeleteItemAsync(System.String p_0);
        Task ImpAddItemAsync(CommInterfaceClasses.Item p_0);
        Task ImpUpdateItemAsync(CommInterfaceClasses.Item p_0);
        Task ImpDeleteItemAsync(System.String p_0);
        Task<CommInterfaceClasses.Item> GetItemAsync(System.String p_0);
        Task<CommInterfaceClasses.Item[]> GetItemsAsync(System.Boolean p_0);
    }

    /// <summary>
    // Generated from IXamarinSampleComm by the proxy generation.
    // This is the API that is used to call a immortal that implements
    /// </summary>
    [Ambrosia.InstanceProxy(typeof(IXamarinSampleComm))]
    public interface IXamarinSampleCommProxy
    {
        Task<Boolean> DetAddItemAsync(CommInterfaceClasses.Item p_0);
        void DetAddItemFork(CommInterfaceClasses.Item p_0);
        Task<Boolean> DetUpdateItemAsync(CommInterfaceClasses.Item p_0);
        void DetUpdateItemFork(CommInterfaceClasses.Item p_0);
        Task<Boolean> DetDeleteItemAsync(System.String p_0);
        void DetDeleteItemFork(System.String p_0);
        void ImpAddItemFork(CommInterfaceClasses.Item p_0);
        void ImpUpdateItemFork(CommInterfaceClasses.Item p_0);
        void ImpDeleteItemFork(System.String p_0);
        Task<CommInterfaceClasses.Item> GetItemAsync(System.String p_0);
        void GetItemFork(System.String p_0);
        Task<CommInterfaceClasses.Item[]> GetItemsAsync(System.Boolean p_0);
        void GetItemsFork(System.Boolean p_0);
    }
}
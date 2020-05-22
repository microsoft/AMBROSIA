using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Ambrosia;
using CommInterfaceClasses;

namespace XamarinSampleCommAPI
{
    public interface IXamarinSampleComm
    {
        // First, we begin with calls to update state which come from replayable sources
        bool DetAddItem(CommInterfaceClasses.Item item);
        bool DetUpdateItem(CommInterfaceClasses.Item item);
        bool DetDeleteItem(string id);

        // Now we have the impulse versions of functions to handle non-replayable callers, like 
        // UI code, note that return values are not allowed.
        [ImpulseHandler]
        void ImpAddItem(CommInterfaceClasses.Item item);
        [ImpulseHandler]
        void ImpUpdateItem(CommInterfaceClasses.Item item);
        [ImpulseHandler]
        void ImpDeleteItem(string id);

        // Now the public functions which return state. Note that these MUST 
        // be called async to get the return values, which is the point.
        CommInterfaceClasses.Item GetItem(string id);
        CommInterfaceClasses.Item[] GetItems(bool forceRefresh = false);
    }
}

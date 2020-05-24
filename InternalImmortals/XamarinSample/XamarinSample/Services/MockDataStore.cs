using Ambrosia;
using CommInterfaceClasses;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using XamarinSample.Models;
using XamarinSampleCommAPI;

namespace XamarinSample.Services
{
    public class MockDataStore : IDataStore<Item>
    {
        [DataContract]
        public class AmbrosiaMockDataStore : Immortal<IXamarinSampleCommProxy>, IXamarinSampleComm
        {
            [DataMember]
            List<Item> items;
            [DataMember]
            bool _initialized;
            bool _normalOp;

            static public AmbrosiaMockDataStore myDataStore = null;

            public AmbrosiaMockDataStore()
            {
            }

            protected override async Task<bool> OnFirstStart()
            {
                items = new List<Item>()
                {
                    new Item { Id = Guid.NewGuid().ToString(), Text = "First item", Description="This is an item description." },
                    new Item { Id = Guid.NewGuid().ToString(), Text = "Second item", Description="This is an item description." },
                    new Item { Id = Guid.NewGuid().ToString(), Text = "Third item", Description="This is an item description." },
                    new Item { Id = Guid.NewGuid().ToString(), Text = "Fourth item", Description="This is an item description." },
                    new Item { Id = Guid.NewGuid().ToString(), Text = "Fifth item", Description="This is an item description." },
                    new Item { Id = Guid.NewGuid().ToString(), Text = "Sixth item", Description="This is an item description." }
                };
                _initialized = true;
                if (_normalOp)
                {
                    myDataStore = this;
                }
                return true;
            }

            protected override void BecomingPrimary()
            {
                _normalOp = true;
                if (_initialized)
                {
                    myDataStore = this;
                }
            }

            public async Task<bool> DetAddItemAsync(Item item)
            {
                lock (items)
                {
                    items.Add(item);
                }
                return true;
            }

            public async Task<bool> DetUpdateItemAsync(Item item)
            {
                lock (items)
                {
                    var oldItem = items.Where((Item arg) => arg.Id == item.Id).FirstOrDefault();
                    items.Remove(oldItem);
                    items.Add(item);
                }
                return true;
            }

            public async Task<bool> DetDeleteItemAsync(string id)
            {
                lock (items)
                {
                    var oldItem = items.Where((Item arg) => arg.Id == id).FirstOrDefault();
                    items.Remove(oldItem);
                }
                return true;
            }

            public async Task ImpAddItemAsync(Item item)
            {
                lock (items)
                {
                    items.Add(item);
                }
            }

            public async Task ImpUpdateItemAsync(Item item)
            {
                lock (items)
                {
                    var oldItem = items.Where((Item arg) => arg.Id == item.Id).FirstOrDefault();
                    items.Remove(oldItem);
                    items.Add(item);
                }
            }

            public async Task ImpDeleteItemAsync(string id)
            {
                lock (items)
                {
                    var oldItem = items.Where((Item arg) => arg.Id == id).FirstOrDefault();
                    items.Remove(oldItem);
                }
            }

            public void TunnelAddItem(Item item)
            {
                thisProxy.ImpAddItemFork(item);
            }

            public void TunnelUpdateItem(Item item)
            {
                thisProxy.ImpUpdateItemFork(item);
            }

            public void TunnelDeleteItem(string id)
            {
                thisProxy.ImpDeleteItemFork(id);
            }

            public async Task<Item> GetItemAsync(string id)
            {
                lock (items)
                {
                    return items.FirstOrDefault(s => s.Id == id);
                }
            }

            public async Task<Item[]> GetItemsAsync(bool forceRefresh = false)
            {
                lock (items)
                {
                    return items.ToArray();
                }
            }
        }

        //readonly List<Item> items;
        IDisposable _container;


        public MockDataStore()
        {
            new Thread(new ThreadStart(() => _container = AmbrosiaFactory.Deploy<IXamarinSampleComm>("MockDataStore", new AmbrosiaMockDataStore(), 1001, 1000))).Start();
            while (AmbrosiaMockDataStore.myDataStore == null) ;
        }

        public async Task<bool> AddItemAsync(Item item)
        {
            AmbrosiaMockDataStore.myDataStore.TunnelAddItem(item);
            return await Task.FromResult(true);
        }

        public async Task<bool> UpdateItemAsync(Item item)
        {
            AmbrosiaMockDataStore.myDataStore.TunnelUpdateItem(item);
            return await Task.FromResult(true);
        }

        public async Task<bool> DeleteItemAsync(string id)
        {
            AmbrosiaMockDataStore.myDataStore.TunnelDeleteItem(id);
            return await Task.FromResult(true);
        }

        public async Task<Item> GetItemAsync(string id)
        {
            return await AmbrosiaMockDataStore.myDataStore.GetItemAsync(id);
        }

        public async Task<IEnumerable<Item>> GetItemsAsync(bool forceRefresh = false)
        {
            return await AmbrosiaMockDataStore.myDataStore.GetItemsAsync(forceRefresh);
        }
    }
}
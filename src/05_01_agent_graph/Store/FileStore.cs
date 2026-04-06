using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Models;
using Newtonsoft.Json;

namespace FourthDevs.AgentGraph.Store
{
    public sealed class FileStore<T> where T : IEntity
    {
        private List<T> _items = new List<T>();
        private readonly string _filePath;
        private bool _loaded;
        private readonly object _lock = new object();

        public FileStore(string name, string dataDir)
        {
            _filePath = Path.Combine(dataDir, name + ".json");
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded) return;
                if (File.Exists(_filePath))
                {
                    var raw = File.ReadAllText(_filePath);
                    _items = JsonConvert.DeserializeObject<List<T>>(raw) ?? new List<T>();
                }
                _loaded = true;
            }
        }

        private void Persist()
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(_items, Formatting.Indented));
        }

        public Task<T> Add(T item)
        {
            EnsureLoaded();
            _items.Add(item);
            Persist();
            return Task.FromResult(item);
        }

        public Task<T> Update(string id, Action<T> patch)
        {
            EnsureLoaded();
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item == null) return Task.FromResult<T>(default(T));
            patch(item);
            Persist();
            return Task.FromResult(item);
        }

        public Task<T> GetById(string id)
        {
            EnsureLoaded();
            return Task.FromResult(_items.FirstOrDefault(i => i.Id == id));
        }

        public Task<List<T>> Find(Func<T, bool> predicate)
        {
            EnsureLoaded();
            return Task.FromResult(_items.Where(predicate).ToList());
        }

        public Task<List<T>> All()
        {
            EnsureLoaded();
            return Task.FromResult(new List<T>(_items));
        }
    }
}

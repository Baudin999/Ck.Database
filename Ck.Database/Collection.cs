using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace Ck.Database
{
    public class Collection<T> : ICollectionBase
    {
        private readonly string _filePath;
        private readonly List<T> _items;
        private readonly IdFarm _idFarm;
        private readonly Database _database;

        public Collection(string databasePath, IdFarm idFarm, Database database)
        {
            var collectionFileName = $"{typeof(T).Name}.json";
            _filePath = Path.Combine(databasePath, collectionFileName);
            _items = new List<T>();
            _idFarm = idFarm;
            _database = database;
        }

        // ICollectionBase implementation
        public void Add(object item)
        {
            if (item is T typedItem && !_items.Contains(typedItem))
            {
                _items.Add(typedItem);
            }
        }

        public void Remove(object item)
        {
            if (item is T typedItem)
            {
                _items.Remove(typedItem);
            }
        }

        public void RemoveById(int id)
        {
            var item = FindById(id);
            if (item != null)
            {
                _items.Remove((T)item);
            }
        }

        public object FindById(int id)
        {
            foreach (var item in _items)
            {
                var idValue = GetIdValue(item);
                if (idValue == id)
                {
                    return item;
                }
            }
            return null;
        }

        public IEnumerable GetAll()
        {
            return _items;
        }

        public void Load()
        {
            if (File.Exists(_filePath))
            {
                var content = File.ReadAllText(_filePath);
                var loadedItems = JsonConvert.DeserializeObject<List<T>>(content);
                if (loadedItems != null)
                {
                    _items.Clear();
                    _items.AddRange(loadedItems);
                }
            }
        }

        public void Save()
        {
            var content = JsonConvert.SerializeObject(_items, Formatting.Indented);
            File.WriteAllText(_filePath, content);
        }

        // Original methods
        public void Add(T item)
        {
            if (!_items.Contains(item))
            {
                _items.Add(item);
            }
        }

        public void Remove(T item)
        {
            _items.Remove(item);
        }

        public T FindByIdTyped(int id)
        {
            foreach (var item in _items)
            {
                var idValue = GetIdValue(item);
                if (idValue == id)
                {
                    return item;
                }
            }
            return default;
        }

        public IEnumerable<T> GetAllTyped()
        {
            return _items;
        }

        private int GetIdValue(T item)
        {
            var type = typeof(T);
            var idField = type.GetField("Id") ?? (MemberInfo)type.GetProperty("Id");

            if (idField != null)
            {
                if (idField is FieldInfo fieldInfo)
                {
                    return (int)fieldInfo.GetValue(item);
                }
                else if (idField is PropertyInfo propertyInfo)
                {
                    return (int)propertyInfo.GetValue(item);
                }
            }
            return 0;
        }
    }
}

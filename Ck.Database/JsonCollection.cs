﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Ck.Database
{
    public interface IJsonCollection
    {
        void Save();
    }
    
    public interface IJsonCollection<T> : IJsonCollection
    {
        void Add(T entity);
        void Remove(int id);
        T Find(int id);
        List<T> FindAll();
    }

    public class JsonCollection<T> : IJsonCollection<T>
    {
        private readonly Schema _schema;
        private readonly Metadata _metadata;
        private readonly Dictionary<int, T> _entities;
        private readonly string _filePath;
        private readonly IdFarm _idFarm;

        public JsonCollection(Schema schema, IdFarm idFarm)
        {
            var typeName = typeof(T).Name;
            _filePath =  Path.Combine(schema.DatabasePath, $"{typeName}.json");
            _schema = schema;
            _metadata = _schema.GetMetadataByType(typeof(T)) ?? throw new Exception("Invalid Metadata schema");
            _idFarm = idFarm;
            _entities = new Dictionary<int, T>();

            Load(); // Load existing entities from file
        }

        public void Add(T entity)
        {
            int id = _metadata.GetId(entity);

            if (id <= 0)
            {
                id = _idFarm.GetNextId();
                _metadata.SetId(entity, id);
            }

            _entities[id] = entity;
        }

        public void Remove(int id)
        {
            _entities.Remove(id);
        }

        public T? Find(int id)
        {
            _entities.TryGetValue(id, out var entity);
            return entity;
        }

        public List<T> FindAll()
        {
            return _entities.Values.ToList();
        }
        
        public void Reset()
        {
            // Clear the current collection
            _entities.Clear();

            // Reload entities from file
            Load();
        }


        public void Save()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new MetadataContractResolver(_schema)
            };
            var json = JsonConvert.SerializeObject(_entities.Values.ToList(), settings);
            File.WriteAllText(_filePath, json);
        }

        private void Load()
        {
            if (File.Exists(_filePath))
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ContractResolver = new MetadataContractResolver(_schema)
                };
                var json = File.ReadAllText(_filePath);
                var entities = JsonConvert.DeserializeObject<List<T>>(json, settings);
                foreach (var entity in entities)
                {
                    int id = _metadata.GetId(entity);
                    if (id > 0)
                    {
                        _entities[id] = entity;
                    }
                }
            }
        }
    }
}

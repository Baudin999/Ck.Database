using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ck.Database
{
    public class JsonDatabase : IDisposable
    {
        private readonly string _databasePath;
        private readonly IdFarm _idFarm;
        private readonly Dictionary<string, object> _collections;
        private Schema _schema;
        private HashSet<int> _processedEntities = new HashSet<int>();

        public JsonDatabase(string databasePath)
        {
            _databasePath = databasePath;
            if (!Directory.Exists(_databasePath)) Directory.CreateDirectory(_databasePath);

            _idFarm = new IdFarm(databasePath);
            _collections = new Dictionary<string, object>();
            _schema = Schema.LoadFromJson(_databasePath);
        }

        public void Store<T>(T entity)
        {
            var processedEntities = new HashSet<object>();
            StoreEntity(entity, processedEntities);
        }

        private void StoreEntity<T>(T entity, HashSet<object> processedEntities)
        {
            if (entity == null || processedEntities.Contains(entity))
                return;

            processedEntities.Add(entity);

            var type = typeof(T);
            var metadata = ValidateType(type);
            var typeName = metadata.Name;

            var collection = GetOrCreateCollection<T>();

            // Assign Id if necessary
            int id = metadata.GetId(entity);
            if (id <= 0)
            {
                id = _idFarm.GetNextId();
                metadata.SetId(entity, id);
            }
            else
            {
                // Remove existing entity with the same Id
                collection.Remove(id);
            }

            // Store referenced entities
            StoreReferencedEntities(entity, metadata, processedEntities);

            collection.Add(entity);
            collection.Save();
        }

        private void StoreReferencedEntities(object entity, Metadata metadata, HashSet<object> processedEntities)
        {
            // Store reference fields
            foreach (var refField in metadata.ReferenceFields)
            {
                var value = metadata.GetMemberValue(entity, refField.Name);
                if (value != null && !processedEntities.Contains(value))
                {
                    var method = typeof(JsonDatabase)
                        .GetMethod(nameof(StoreEntity), BindingFlags.NonPublic | BindingFlags.Instance)
                        .MakeGenericMethod(refField.Type);
                    method.Invoke(this, new object[] { value, processedEntities });
                }
            }

            // Store collection references
            foreach (var colRef in metadata.CollectionReferences)
            {
                var value = metadata.GetMemberValue(entity, colRef.Name) as IEnumerable;
                if (value != null)
                {
                    foreach (var item in value)
                    {
                        if (item != null && !processedEntities.Contains(item))
                        {
                            var method = typeof(JsonDatabase)
                                .GetMethod(nameof(StoreEntity), BindingFlags.NonPublic | BindingFlags.Instance)
                                .MakeGenericMethod(colRef.ItemType);
                            method.Invoke(this, new object[] { item, processedEntities });
                        }
                    }
                }
            }
        }

        public T Find<T>(int id)
        {
            var collection = GetOrCreateCollection<T>();
            var entity = collection.Find(id);
            if (entity != null && !_processedEntities.Contains(id))
            {
                var metadata = ValidateType(typeof(T));
                ResolveEntityReferences(entity, metadata);
            }

            return entity;
        }

        private void ResolveEntityReferences(object entity, Metadata metadata)
        {
            ResolveEntityReferencesRecursive(entity, metadata);
        }

        private void ResolveEntityReferencesRecursive(object entity, Metadata metadata)
        {
            var id = metadata.GetId(entity);
            if (entity == null || _processedEntities.Contains(id))
                return;

            _processedEntities.Add(id);

            // Resolve reference fields
            foreach (var refField in metadata.ReferenceFields)
            {
                var value = metadata.GetMemberValue(entity, refField.Name);
                if (value != null)
                {
                    var refmetadata = _schema.GetMetadataByType(refField.Type);
                    refmetadata.Validate();
                    int refId = refmetadata.GetId(value);

                    var method = typeof(JsonDatabase)
                        .GetMethod(nameof(Find))
                        .MakeGenericMethod(refField.Type);
                    var referencedEntity = method.Invoke(this, new object[] { refId });

                    metadata.SetMemberValue(entity, refField.Name, referencedEntity);

                    // Recursively resolve references
                    var refMetadata = ValidateType(refField.Type);
                    ResolveEntityReferencesRecursive(referencedEntity, refMetadata);
                }
            }

            // Resolve collection references
            foreach (var colRef in metadata.CollectionReferences)
            {
                var value = metadata.GetMemberValue(entity, colRef.Name) as IEnumerable;
                if (value != null)
                {
                    var resolvedListType = typeof(List<>).MakeGenericType(colRef.ItemType);
                    var resolvedList = (IList)Activator.CreateInstance(resolvedListType);
                    var colRefMetadata = _schema.GetMetadataByType(colRef.ItemType);

                    foreach (var item in value)
                    {
                        if (item != null)
                        {
                            int itemId = colRefMetadata.GetId(item);

                            var method = typeof(JsonDatabase)
                                .GetMethod(nameof(Find))
                                .MakeGenericMethod(colRef.ItemType);
                            var resolvedItem = method.Invoke(this, new object[] { itemId });

                            resolvedList.Add(resolvedItem);

                            // Recursively resolve references
                            var itemMetadata = ValidateType(colRef.ItemType);
                            ResolveEntityReferencesRecursive(resolvedItem, itemMetadata);
                        }
                    }

                    metadata.SetMemberValue(entity, colRef.Name, resolvedList);
                }
            }
        }

        public List<T> FindAll<T>()
        {
            var collection = GetOrCreateCollection<T>();
            var entities = collection.FindAll();

            var metadata = ValidateType(typeof(T));
            foreach (var entity in entities)
            {
                ResolveEntityReferences(entity, metadata);
            }

            return entities;
        }

        public void Delete<T>(int id)
        {
            var collection = GetOrCreateCollection<T>();
            collection.Remove(id);
            collection.Save();
        }

        private JsonCollection<T> GetOrCreateCollection<T>()
        {
            var type = typeof(T);
            var typeName = type.Name;

            if (!_collections.TryGetValue(typeName, out var collectionObj))
            {
                // Ensure metadata exists for type T
                if (!_schema.Contains(type))
                {
                    _schema = MetadataBuilder.BuildSchema(type, _schema);
                    _schema.SaveToJson(_databasePath);
                }

                var metadata = _schema.GetMetadataByType(type);
                string filePath = Path.Combine(_databasePath, $"{typeName}.json");

                var collection = new JsonCollection<T>(_schema, _idFarm);
                _collections[typeName] = collection;
                return collection;
            }
            else
            {
                return (JsonCollection<T>)collectionObj;
            }
        }

        private Metadata ValidateType(Type type)
        {
            if (!_schema.Contains(type))
            {
                _schema = MetadataBuilder.BuildSchema(type, _schema);
                _schema.SaveToJson(_databasePath);
            }

            return _schema.GetMetadataByType(type);
        }

        public void Dispose()
        {
            foreach (var collectionObj in _collections.Values)
            {
                var collectionType = collectionObj.GetType();
                var saveMethod = collectionType.GetMethod("Save");
                saveMethod.Invoke(collectionObj, null);
            }

            _schema.SaveToJson(_databasePath);
        }
    }
}
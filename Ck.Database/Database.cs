using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ck.Database
{
    public class Database : IDisposable
    {
        private readonly string _databasePath;
        private readonly IdFarm _idFarm;
        private readonly Dictionary<string, object> _collections;
        private Schema _schema;
        private Dictionary<string, Metadata> _metadataDict;
        public HashSet<int> _loadedIds = new HashSet<int>();

        public Database(string databasePath)
        {
            _databasePath = databasePath;
            if (!Directory.Exists(_databasePath)) Directory.CreateDirectory(_databasePath);

            _idFarm = new IdFarm(databasePath);
            _collections = new Dictionary<string, object>();
            _schema = Schema.LoadFromJson(_databasePath);
            _metadataDict = _schema.ToDictionary(m => m.Name);
        }

        public void Store<T>(T entity)
        {
            var processedEntities = new HashSet<object>();
            Store(entity, processedEntities);
        }

        private void Store<T>(T entity, HashSet<object> processedEntities)
        {
            if (entity == null || processedEntities.Contains(entity))
                return;

            processedEntities.Add(entity);

            var type = typeof(T);
            var typeName = type.Name;
            ValidateType<T>(typeName, type);

            var metadata = _metadataDict[typeName];
            var collection = GetOrCreateCollection<T>(typeName);

            // Assign Id if necessary
            var idMember = GetIdMember(typeof(T));
            int id = GetIdValue(entity, idMember);
            if (id == 0)
            {
                id = _idFarm.GetNextId();
                SetIdValue(entity, idMember, id);
            }
            else
            {
                // Remove existing entity with the same Id
                var existingEntity = collection.FirstOrDefault(e => GetIdValue(e, idMember) == id);
                if (existingEntity != null)
                {
                    collection.Remove(existingEntity);
                }
            }

            // Store referenced entities
            StoreReferencedEntities(entity, metadata, processedEntities);

            collection.Add(entity);
            SaveCollection(collection, typeName);
        }
        public T Find<T>(int id)
        {
            var type = typeof(T);
            var typeName = type.Name;
            ValidateType<T>(typeName, type);

            var collection = GetOrCreateCollection<T>(typeName);

            var idMember = GetIdMember(typeof(T));
            var entity = collection.FirstOrDefault(e => GetIdValue(e, idMember) == id);

            if (entity != null)
            {
                // Resolve references
                var metadata = _metadataDict[typeName];
                ResolveEntityReferences(entity, metadata);
            }

            return entity;
        }

        public List<T> FindAll<T>()
        {

            var type = typeof(T);
            var typeName = type.Name;
            ValidateType<T>(typeName, type);

            var collection = GetOrCreateCollection<T>(typeName);

            // Resolve references for all entities
            var metadata = _metadataDict[typeName];
            foreach (var entity in collection)
            {
                ResolveEntityReferences(entity, metadata);
            }

            return collection.ToList();
        }

        public void Delete<T>(int id)
        {
            var type = typeof(T);
            var typeName = type.Name;
            ValidateType<T>(typeName, type);

            var collection = GetOrCreateCollection<T>(typeName);

            var idMember = GetIdMember(typeof(T));
            var entity = collection.FirstOrDefault(e => GetIdValue(e, idMember) == id);

            if (entity != null)
            {
                collection.Remove(entity);
                SaveCollection(collection, typeName);

                // Remove the ID from the loaded IDs set
                _loadedIds.Remove(id);
            }
        }


        private List<T> GetOrCreateCollection<T>(string typeName)
        {
            if (!_collections.TryGetValue(typeName, out var collectionObj))
            {
                string filePath = Path.Combine(_databasePath, $"{typeName}.json");
                List<T> collection;
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = GetJsonSettings();
                    collection = JsonConvert.DeserializeObject<List<T>>(json, settings);
                }
                else
                {
                    collection = new List<T>();
                }

                _collections[typeName] = collection;
                return collection;
            }
            else
            {
                return (List<T>)collectionObj;
            }
        }

        private void SaveCollection<T>(List<T> collection, string typeName)
        {
            string filePath = Path.Combine(_databasePath, $"{typeName}.json");
            var settings = GetJsonSettings();
            var json = JsonConvert.SerializeObject(collection, Formatting.Indented, settings);
            File.WriteAllText(filePath, json);
        }

        private void StoreReferencedEntities(object entity, Metadata metadata, HashSet<object> processedEntities)
        {
            // Store reference fields
            foreach (var refField in metadata.ReferenceFields)
            {
                var memberInfo = GetMemberInfo(entity.GetType(), refField.Name);
                var value = GetMemberValue(entity, memberInfo);
                if (value != null && !processedEntities.Contains(value))
                {
                    var method = typeof(Database)
                        .GetMethod(nameof(Store), BindingFlags.NonPublic | BindingFlags.Instance)
                        .MakeGenericMethod(refField.Type);
                    method.Invoke(this, new object[] { value, processedEntities });
                }
            }

            // Store collection references
            foreach (var colRef in metadata.CollectionReferences)
            {
                var memberInfo = GetMemberInfo(entity.GetType(), colRef.Name);
                var value = GetMemberValue(entity, memberInfo) as IEnumerable;
                if (value != null)
                {
                    foreach (var item in value)
                    {
                        if (item != null && !processedEntities.Contains(item))
                        {
                            var method = typeof(Database)
                                .GetMethod(nameof(Store), BindingFlags.NonPublic | BindingFlags.Instance)
                                .MakeGenericMethod(colRef.ItemType);
                            method.Invoke(this, new object[] { item, processedEntities });
                        }
                    }
                }
            }
        }


        private void ResolveEntityReferences(object entity, Metadata metadata)
        {
            var idMember = GetIdMember(entity.GetType());
            int entityId = GetIdValue(entity, idMember);

            if (_loadedIds.Contains(entityId))
            {
                return;
            }
            else
            {
                _loadedIds.Add(entityId);
            }

            // Resolve reference fields
            foreach (var refField in metadata.ReferenceFields)
            {
                var memberInfo = GetMemberInfo(entity.GetType(), refField.Name);
                var referenceValue = GetMemberValue(entity, memberInfo);

                if (referenceValue != null)
                {
                    var idMemberRef = GetIdMember(refField.Type);
                    int refId = GetIdValue(referenceValue, idMemberRef);

                    var method = typeof(Database).GetMethod(nameof(Find)).MakeGenericMethod(refField.Type);
                    var referencedEntity = method.Invoke(this, new object[] { refId });
                    SetMemberValue(entity, memberInfo, referencedEntity);
                }
            }

            // Resolve collection references
            foreach (var colRef in metadata.CollectionReferences)
            {
                var memberInfo = GetMemberInfo(entity.GetType(), colRef.Name);
                var collectionValue = GetMemberValue(entity, memberInfo) as IEnumerable;

                if (collectionValue != null)
                {
                    var resolvedListType = typeof(List<>).MakeGenericType(colRef.ItemType);
                    var resolvedList = (IList)Activator.CreateInstance(resolvedListType);

                    var method = typeof(Database).GetMethod(nameof(Find)).MakeGenericMethod(colRef.ItemType);

                    foreach (var item in collectionValue)
                    {
                        if (item != null)
                        {
                            var idMemberItem = GetIdMember(colRef.ItemType);
                            int itemId = GetIdValue(item, idMemberItem);

                            var resolvedItem = method.Invoke(this, new object[] { itemId });
                            resolvedList.Add(resolvedItem);
                        }
                    }

                    SetMemberValue(entity, memberInfo, resolvedList);
                }
            }
        }

        private MemberInfo GetMemberInfo(Type type, string memberName)
        {
            return type.GetMember(memberName, BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
        }

        private object GetMemberValue(object obj, MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.GetValue(obj),
                PropertyInfo prop => prop.GetValue(obj),
                _ => null,
            };
        }

        private void SetMemberValue(object obj, MemberInfo member, object value)
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(obj, value);
                    break;
                case PropertyInfo prop:
                    if (prop.CanWrite)
                        prop.SetValue(obj, value);
                    break;
            }
        }

        private MemberInfo GetIdMember(Type type)
        {
            var idMember = type.GetMember("Id", BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => (m.MemberType == MemberTypes.Field && ((FieldInfo)m).FieldType == typeof(int))
                                     || (m.MemberType == MemberTypes.Property &&
                                         ((PropertyInfo)m).PropertyType == typeof(int)));
            if (idMember == null)
                throw new InvalidOperationException(
                    $"Type {type.Name} does not have an Id field or property of type int.");

            return idMember;
        }

        private int GetIdValue(object entity, MemberInfo idMember)
        {
            return idMember switch
            {
                PropertyInfo prop => (int)prop.GetValue(entity),
                FieldInfo field => (int)field.GetValue(entity),
                _ => throw new InvalidOperationException("Id member is neither a field nor a property."),
            };
        }

        private void SetIdValue(object entity, MemberInfo idMember, int value)
        {
            switch (idMember)
            {
                case PropertyInfo prop:
                    prop.SetValue(entity, value);
                    break;
                case FieldInfo field:
                    field.SetValue(entity, value);
                    break;
                default:
                    throw new InvalidOperationException("Id member is neither a field nor a property.");
            }
        }

        private JsonSerializerSettings GetJsonSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new MetadataContractResolver(_metadataDict)
            };
        }

        private void ValidateType<T>(string typeName, Type type)
        {
            if (!_metadataDict.ContainsKey(typeName))
            {
                _schema = MetadataBuilder.BuildSchema(type, _schema);
                _metadataDict = _schema.ToDictionary(m => m.Name);

                _schema.SaveToJson(_databasePath);
            }
        }

        public void Close()
        {
            _schema.SaveToJson(_databasePath);
            _collections.Clear();
            _metadataDict.Clear();
        }

        public void Dispose()
        {
            Close();
        }
    }

    public class MetadataContractResolver : DefaultContractResolver
    {
        private readonly Dictionary<string, Metadata> _metadataDict;

        public MetadataContractResolver(Dictionary<string, Metadata> metadataDict)
        {
            _metadataDict = metadataDict;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var jsonProperty = base.CreateProperty(member, memberSerialization);
            var declaringTypeName = member.DeclaringType.Name;

            if (_metadataDict.TryGetValue(declaringTypeName, out var metadata))
            {
                // Handle reference fields
                if (metadata.ReferenceFields.Any(f => f.Name == member.Name))
                {
                    jsonProperty.ShouldSerialize = instance => true;
                    jsonProperty.Converter = new ReferenceFieldConverter();
                }

                // Handle collection references
                else if (metadata.CollectionReferences.Any(f => f.Name == member.Name))
                {
                    jsonProperty.ShouldSerialize = instance => true;
                    jsonProperty.Converter = new CollectionReferenceConverter();
                }
            }

            return jsonProperty;
        }
    }

    // ReferenceFieldConverter.cs
    public class ReferenceFieldConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            // This converter is only for reference fields
            return true;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Serialize reference fields as Ids
            if (value != null)
            {
                var idMember = value.GetType().GetMember("Id", BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => (m.MemberType == MemberTypes.Field && ((FieldInfo)m).FieldType == typeof(int))
                                         || (m.MemberType == MemberTypes.Property &&
                                             ((PropertyInfo)m).PropertyType == typeof(int)));

                if (idMember != null)
                {
                    var idValue = idMember.MemberType == MemberTypes.Field
                        ? ((FieldInfo)idMember).GetValue(value)
                        : ((PropertyInfo)idMember).GetValue(value);

                    writer.WriteValue(idValue);
                }
                else
                {
                    writer.WriteNull();
                }
            }
            else
            {
                writer.WriteNull();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            // Create a placeholder object with only the Id set
            if (reader.TokenType == JsonToken.Integer)
            {
                int id = Convert.ToInt32(reader.Value);

                var placeholder = Activator.CreateInstance(objectType);
                var idMember = objectType.GetMember("Id", BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => (m.MemberType == MemberTypes.Field && ((FieldInfo)m).FieldType == typeof(int))
                                         || (m.MemberType == MemberTypes.Property &&
                                             ((PropertyInfo)m).PropertyType == typeof(int)));

                if (idMember != null)
                {
                    if (idMember.MemberType == MemberTypes.Field)
                        ((FieldInfo)idMember).SetValue(placeholder, id);
                    else
                        ((PropertyInfo)idMember).SetValue(placeholder, id);
                }

                return placeholder;
            }
            else
            {
                return null;
            }
        }
    }


    // CollectionReferenceConverter.cs
    public class CollectionReferenceConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            // This converter is only for collection references
            return true;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Serialize collection references as arrays of Ids
            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        var idMember = item.GetType().GetMember("Id", BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m =>
                                (m.MemberType == MemberTypes.Field && ((FieldInfo)m).FieldType == typeof(int))
                                || (m.MemberType == MemberTypes.Property &&
                                    ((PropertyInfo)m).PropertyType == typeof(int)));

                        if (idMember != null)
                        {
                            var idValue = idMember.MemberType == MemberTypes.Field
                                ? ((FieldInfo)idMember).GetValue(item)
                                : ((PropertyInfo)idMember).GetValue(item);

                            writer.WriteValue(idValue);
                        }
                        else
                        {
                            writer.WriteNull();
                        }
                    }
                    else
                    {
                        writer.WriteNull();
                    }
                }

                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            // Create a list of placeholder objects with only the Ids set
            if (reader.TokenType == JsonToken.StartArray)
            {
                var listType = typeof(List<>).MakeGenericType(objectType.GetGenericArguments()[0]);
                var list = (IList)Activator.CreateInstance(listType);

                var elementType = objectType.GetGenericArguments()[0];

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndArray)
                        break;

                    if (reader.TokenType == JsonToken.Integer)
                    {
                        int id = Convert.ToInt32(reader.Value);

                        var placeholder = Activator.CreateInstance(elementType);
                        var idMember = elementType.GetMember("Id", BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(m =>
                                (m.MemberType == MemberTypes.Field && ((FieldInfo)m).FieldType == typeof(int))
                                || (m.MemberType == MemberTypes.Property &&
                                    ((PropertyInfo)m).PropertyType == typeof(int)));

                        if (idMember != null)
                        {
                            if (idMember.MemberType == MemberTypes.Field)
                                ((FieldInfo)idMember).SetValue(placeholder, id);
                            else
                                ((PropertyInfo)idMember).SetValue(placeholder, id);
                        }

                        list.Add(placeholder);
                    }
                    else
                    {
                        list.Add(null);
                    }
                }

                return list;
            }
            else
            {
                return null;
            }
        }
    }
}
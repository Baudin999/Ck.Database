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
    public class Database : IDisposable
    {
        private readonly string _databasePath;
        private readonly IdFarm _idFarm;
        private readonly Dictionary<string, object> _collections;
        private readonly HashSet<int> _loadedIds = [];
        private Schema _schema;

        public Database(string databasePath)
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
            Store(entity, processedEntities);
        }

        private async void Store<T>(T entity, HashSet<object> processedEntities)
        {
            if (entity == null || processedEntities.Contains(entity))
                return;

            processedEntities.Add(entity);

            var type = typeof(T);
            var metadata = ValidateType<T>(type);
            var typeName = metadata.Name;

            var collection = await GetOrCreateCollection<T>(typeName);

            // Assign Id if necessary
            //var idMember = GetIdMember(typeof(T));
            int id = metadata.GetId(entity);
            if (id <= 0)
            {
                id = _idFarm.GetNextId();
                metadata.SetId(entity, id);
            }
            else
            {
                // Remove existing entity with the same Id
                var existingEntity = collection.FirstOrDefault(e => metadata.GetId(e) == id);
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

        public async Task<T> Find<T>(int id)
        {
            var type = typeof(T);
            var metadata = ValidateType<T>(type);
            var typeName = metadata.Name;

            var collection = await GetOrCreateCollection<T>(typeName);

            var idMember = GetIdMember(typeof(T));
            var entity = collection.FirstOrDefault(e => GetIdValue(e, idMember) == id);

            if (entity != null)
            {
                ResolveEntityReferences(entity, metadata);
            }

            return entity;
        }

        public async Task<List<T>> FindAll<T>()
        {
            var type = typeof(T);
            var metadata = ValidateType<T>(type);
            var typeName = metadata.Name;

            var collection = await GetOrCreateCollection<T>(typeName);

            // Resolve references for all entities
            foreach (var entity in collection)
            {
                ResolveEntityReferences(entity, metadata);
            }

            return collection.ToList();
        }

        public async void Delete<T>(int id)
        {
            var type = typeof(T);
            var metadata = ValidateType<T>(type);
            var typeName = metadata.Name;

            var collection = await GetOrCreateCollection<T>(typeName);

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


        private async Task<List<T>> GetOrCreateCollection<T>(string typeName)
        {
            if (!_collections.TryGetValue(typeName, out var collectionObj))
            {
                string filePath = Path.Combine(_databasePath, $"{typeName}.json");
                List<T> collection;
                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath);
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


        private async void ResolveEntityReferences(object entity, Metadata metadata)
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
                    var task = (Task)method.Invoke(this, new object[] { refId });

                    // Await the Task to ensure proper handling of the asynchronous operation
                    await task;

                    // If the method has a return type, you need to get the result from the Task
                    var resultProperty = task.GetType().GetProperty("Result");
                    var referencedEntity = resultProperty?.GetValue(task);

                    // Set the value to the member
                    SetMemberValue(entity, memberInfo, referencedEntity);
                }
            }

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

                            // Invoke the method and await the Task result
                            var task = (Task)method.Invoke(this, new object[] { itemId });
                            await task; // Ensure the task completes

                            // Retrieve the resolved item from the Task's result
                            var resultProperty = task.GetType().GetProperty("Result");
                            var resolvedItem = resultProperty?.GetValue(task);

                            resolvedList.Add(resolvedItem);
                        }
                    }

                    SetMemberValue(entity, memberInfo, resolvedList);
                }
            }

        }

        private MemberInfo? GetMemberInfo(Type type, string memberName)
        {
            return type.GetMember(memberName, BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
        }

        private object? GetMemberValue(object obj, MemberInfo member)
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
                ContractResolver = new MetadataContractResolver(_schema)
            };
        }

        private Metadata ValidateType<T>(Type type)
        {
            if (!_schema.Contains(type))
            {
                _schema = MetadataBuilder.BuildSchema(type, _schema);
                _schema.SaveToJson(_databasePath);
            }

            return _schema.GetMetadataByType(type);
        }

        public void Close()
        {
            _schema.SaveToJson(_databasePath);
            _collections.Clear();
        }

        public void Dispose()
        {
            Close();
        }
    }

    public class MetadataContractResolver : DefaultContractResolver
    {
        private readonly Schema _schema;

        public MetadataContractResolver(Schema schema)
        {
            _schema = schema;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var jsonProperty = base.CreateProperty(member, memberSerialization);
            var metadata = _schema.GetMetadataByType(member.DeclaringType);
            if (metadata is not null)
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
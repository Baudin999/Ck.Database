using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;

namespace Ck.Database
{
    public static class MetadataBuilder
    {
        public static Schema BuildSchema(Type t, Schema existingSchema = null)
        {
            var schema = existingSchema ?? new Schema();

            // Check if the type is a runtime type
            if (t.IsGenericType && t.FullName == null)
            {
                // Break out if it's an anonymous or runtime-generated type
                return schema;
            }

            if (schema.Contains(t.Name))
            {
                // If metadata for this type already exists in the schema, return the existing schema
                return schema;
            }

            // Create metadata for the type
            var metadata = new Metadata { Name = t.Name, Type = t };

            // Add this type's metadata to the schema
            schema.Add(metadata);

            foreach (var memberInfo in t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (memberInfo.MemberType == MemberTypes.Field)
                {
                    HandleField((FieldInfo)memberInfo, metadata, schema);
                }
                else if (memberInfo.MemberType == MemberTypes.Property)
                {
                    HandleProperty((PropertyInfo)memberInfo, metadata, schema);
                }
            }

            // Cache getters and setters after building metadata
            metadata.CacheMemberAccessors();

            return schema;
        }

        private static void HandleProperty(PropertyInfo propertyInfo, Metadata metadata, Schema schema)
        {
            if (propertyInfo.Name == "Id" && propertyInfo.PropertyType == typeof(int))
            {
                metadata.IsEntity = true;

                // get a compiled lambda to get the Id of the entity
            }

            if (IsGenericReference(propertyInfo.PropertyType, out var genericArgument))
            {
                // Build metadata for the generic argument type and add it to the schema
                var collectionMetadata = BuildSchema(genericArgument, schema);

                metadata.CollectionReferences.Add(new MetadataCollectionReference
                {
                    Name = propertyInfo.Name,
                    CollectionType = propertyInfo.PropertyType,
                    ItemType = genericArgument,
                    Metadata = schema.First(m => m.Name == genericArgument.Name),
                    IsRequired = IsRequired(propertyInfo.PropertyType) ? "true" : "false"
                });
            }
            else if (IsReference(propertyInfo.PropertyType))
            {
                // Build metadata for the reference type and add it to the schema
                BuildSchema(propertyInfo.PropertyType, schema);

                metadata.ReferenceFields.Add(new MetadataReferenceField
                {
                    Name = propertyInfo.Name,
                    Type = propertyInfo.PropertyType,
                    IsRequired = IsRequired(propertyInfo.PropertyType) ? "true" : "false"
                });
            }
            else
            {
                // Handle regular property
                metadata.Fields.Add(new MetadataField
                {
                    Name = propertyInfo.Name,
                    Type = propertyInfo.PropertyType,
                    IsRequired = IsRequired(propertyInfo.PropertyType)
                });
            }
        }

        private static void HandleField(FieldInfo fieldInfo, Metadata metadata, Schema schema)
        {
            if (fieldInfo.Name == "Id" && fieldInfo.FieldType == typeof(int))
            {
                metadata.IsEntity = true;
            }

            if (IsGenericReference(fieldInfo.FieldType, out var genericArgument))
            {
                // Build metadata for the generic argument type and add it to the schema
                var collectionMetadata = BuildSchema(genericArgument, schema);

                metadata.CollectionReferences.Add(new MetadataCollectionReference
                {
                    Name = fieldInfo.Name,
                    CollectionType = fieldInfo.FieldType,
                    ItemType = genericArgument,
                    Metadata = schema.First(m => m.Name == genericArgument.Name),
                    IsRequired = IsRequired(fieldInfo.FieldType) ? "true" : "false"
                });
            }
            else if (IsReference(fieldInfo.FieldType))
            {
                // Build metadata for the reference type and add it to the schema
                BuildSchema(fieldInfo.FieldType, schema);

                metadata.ReferenceFields.Add(new MetadataReferenceField
                {
                    Name = fieldInfo.Name,
                    Type = fieldInfo.FieldType,
                    IsRequired = IsRequired(fieldInfo.FieldType) ? "true" : "false"
                });
            }
            else
            {
                // Handle regular field
                metadata.Fields.Add(new MetadataField
                {
                    Name = fieldInfo.Name,
                    Type = fieldInfo.FieldType,
                    IsRequired = IsRequired(fieldInfo.FieldType)
                });
            }
        }

        private static bool IsReference(Type type)
        {
            // Get the Id property if it exists
            var idMember = type.GetMember("Id", BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();

            if (idMember is null) return false;
            if (idMember is PropertyInfo propertyInfo) return propertyInfo.PropertyType == typeof(int);
            if (idMember is FieldInfo fieldInfo) return fieldInfo.FieldType == typeof(int);
            return false;
        }

        private static bool IsGenericReference(Type type, out Type genericArgument)
        {
            if (type.IsGenericType && type.GetGenericArguments().Length == 1)
            {
                genericArgument = type.GetGenericArguments()[0];
                return IsReference(genericArgument);
            }

            genericArgument = null;
            return false;
        }

        private static bool IsRequired(Type type)
        {
            // A type is required if it is not nullable
            return !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                   && !type.IsClass; // Non-nullable value types and non-nullable reference types
        }
    }

    public class Metadata
    {
        public Type Type;
        public string Name;
        public bool IsEntity = false;
        public List<MetadataField> Fields = new List<MetadataField>();
        public List<MetadataReferenceField> ReferenceFields = new List<MetadataReferenceField>();
        public List<MetadataCollectionReference> CollectionReferences = new List<MetadataCollectionReference>();
        
        // [JsonIgnore]
        // public Schema Schema;

        // Caching member infos and compiled lambdas
        [JsonIgnore]
        private readonly Dictionary<string, Func<object, object>> _getters =
            new Dictionary<string, Func<object, object>>();

        [JsonIgnore]
        private readonly Dictionary<string, Action<object, object>> _setters =
            new Dictionary<string, Action<object, object>>();

        // Id getter and setter
        [JsonIgnore] private Func<object, int> _getIdFunc = null;

        [JsonIgnore] private Action<object, int> _setIdFunc = null;
        
        // Save<T> caching
        [JsonIgnore] private Action<object> _saveFunc = null;

        public Metadata()
        {
            // Initialize the dictionaries in the constructor
        }

        public void Validate()
        {
            
        }


        public void CacheMemberAccessors()
        {
            if (IsEntity)
            {
                // Cache Id getter and setter
                CacheIdAccessors();
            }
            
            // Cache getters and setters for fields
            foreach (var field in Fields)
            {
                var memberInfo = Type.GetMember(field.Name, BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault();
                CacheMemberAccessor(memberInfo, field.Name);
            }

            // Cache getters and setters for reference fields
            foreach (var refField in ReferenceFields)
            {
                var memberInfo = Type.GetMember(refField.Name, BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault();
                CacheMemberAccessor(memberInfo, refField.Name);
            }

            // Cache getters and setters for collection references
            foreach (var colRef in CollectionReferences)
            {
                var memberInfo = Type.GetMember(colRef.Name, BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault();
                CacheMemberAccessor(memberInfo, colRef.Name);
            }
        }

        private void CacheMemberAccessor(MemberInfo memberInfo, string memberName)
        {
            if (memberInfo == null) return;

            var parameter = Expression.Parameter(typeof(object), "entity");
            var castEntity = Expression.Convert(parameter, Type);

            // Getter
            var getter = Expression.Lambda<Func<object, object>>(
                Expression.Convert(
                    memberInfo.MemberType == MemberTypes.Property
                        ? Expression.Property(castEntity, (PropertyInfo)memberInfo)
                        : Expression.Field(castEntity, (FieldInfo)memberInfo),
                    typeof(object)
                ),
                parameter
            ).Compile();

            _getters[memberName] = getter;

            // Setter
            var valueParameter = Expression.Parameter(typeof(object), "value");
            var memberAccess = memberInfo.MemberType == MemberTypes.Property
                ? Expression.Property(castEntity, (PropertyInfo)memberInfo)
                : Expression.Field(castEntity, (FieldInfo)memberInfo);

            var assign = Expression.Assign(
                memberAccess,
                Expression.Convert(valueParameter, memberAccess.Type)
            );

            var setter = Expression.Lambda<Action<object, object>>(
                assign,
                parameter,
                valueParameter
            ).Compile();

            _setters[memberName] = setter;
        }

        private void CacheIdAccessors()
        {
            var idMember = Type.GetMember("Id", BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
            if (idMember == null)
                throw new InvalidOperationException($"Id member not found on type '{Name}'.");

            // Getter
            var parameter = Expression.Parameter(typeof(object), "entity");
            var castEntity = Expression.Convert(parameter, Type);
            var memberAccess = idMember.MemberType == MemberTypes.Property
                ? Expression.Property(castEntity, (PropertyInfo)idMember)
                : Expression.Field(castEntity, (FieldInfo)idMember);

            _getIdFunc = Expression.Lambda<Func<object, int>>(
                Expression.Convert(memberAccess, typeof(int)),
                parameter
            ).Compile();

            // Setter
            var valueParameter = Expression.Parameter(typeof(int), "value");
            var assign = Expression.Assign(
                memberAccess,
                Expression.Convert(valueParameter, memberAccess.Type)
            );

            _setIdFunc = Expression.Lambda<Action<object, int>>(
                assign,
                parameter,
                valueParameter
            ).Compile();
        }

        public object GetMemberValue(object entity, string memberName)
        {
            if (_getters.TryGetValue(memberName, out var getter))
            {
                return getter(entity);
            }

            throw new InvalidOperationException($"Getter not found for member '{memberName}' in type '{Name}'.");
        }

        public void SetMemberValue(object entity, string memberName, object value)
        {
            if (_setters.TryGetValue(memberName, out var setter))
            {
                setter(entity, value);
            }
            else
            {
                throw new InvalidOperationException($"Setter not found for member '{memberName}' in type '{Name}'.");
            }
        }

        public int GetId(object entity)
        {
            if (_getIdFunc != null)
                return _getIdFunc(entity);

            throw new InvalidOperationException($"Id getter not cached for type '{Name}'.");
        }

        public void SetId(object entity, int id)
        {
            if (_setIdFunc != null)
                _setIdFunc(entity, id);
            else
                throw new InvalidOperationException($"Id setter not cached for type '{Name}'.");
        }


        public override string ToString()
        {
            var fieldsString = Fields.Any()
                ? string.Join(Environment.NewLine, Fields.Select(f => $"  - {f}"))
                : "    None";

            var referenceFieldsString = ReferenceFields.Any()
                ? string.Join(Environment.NewLine, ReferenceFields.Select(rf => $"  - {rf}"))
                : "    None";

            var collectionReferencesString = CollectionReferences.Any()
                ? string.Join(Environment.NewLine, CollectionReferences.Select(cr => $"  - {cr}"))
                : "    None";

            return $@"
Metadata:
  Name: {Name}
  IsEntity: {IsEntity}
  Fields:
{fieldsString}
  ReferenceFields:
{referenceFieldsString}
  CollectionReferences:
{collectionReferencesString}";
        }
    }

    public class MetadataField
    {
        public string Name;
        public bool IsRequired;
        public Type Type;

        public override string ToString()
        {
            return $"Name: {Name}, Type: {Type.Name}, IsRequired: {IsRequired}";
        }
    }

    public class MetadataReferenceField
    {
        public string Name;
        public string IsRequired;
        public Type Type;

        public override string ToString()
        {
            return $"Name: {Name}, Type: {Type.Name}, IsRequired: {IsRequired}";
        }
    }

    public class MetadataCollectionReference
    {
        public string Name;
        public Type CollectionType;
        public Type ItemType;
        public Metadata Metadata;
        public string IsRequired;

        public override string ToString()
        {
            return
                $"Name: {Name}, CollectionType: {CollectionType.Name}, ItemType: {ItemType.Name}, IsRequired: {IsRequired}";
        }
    }

    public class Schema : List<Metadata>
    {
        public string DatabasePath;
        public bool Contains(string name) => this.Any(m => m.Name == name);
        public bool Contains(Type type) => this.Any(m => m.Type == type);

        public Metadata? GetMetadataByType(Type? type)
        {
            if (type is null) return null;
            var result = this.FirstOrDefault(m => m.Name == type.Name);
            if (result is null)
            {
                MetadataBuilder.BuildSchema(type, this);
                return this.FirstOrDefault(m => m.Name == type.Name);
            }
            else
            {
                
                return result;
            }
        }

        public override string ToString()
        {
            return this.Any()
                ? string.Join(Environment.NewLine, this.Select(m => m.ToString()))
                : "Schema is empty.";
        }

        public void SaveToJson(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
            var filePath = Path.Combine(directoryPath, "Schema.json");
            var json = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto // Preserve type information
            });
            File.WriteAllText(filePath, json);
        }

        public static Schema LoadFromJson(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
            var filePath = Path.Combine(directoryPath, "Schema.json");
            if (!File.Exists(filePath)) return new Schema { DatabasePath = directoryPath };
            var json = File.ReadAllText(filePath);
            var schema = JsonConvert.DeserializeObject<Schema>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto // Preserve type information
            }) ?? new Schema { DatabasePath = directoryPath };

            schema.DatabasePath = directoryPath;
            foreach (var metadata in schema)
            {
                metadata.CacheMemberAccessors();
                
            }
            return schema;
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Ck.Database
{
    public static class MetadataBuilder
    {
        public static Schema BuildSchema(Type t, Schema existingSchema = null)
        {
            var schema = existingSchema ?? new Schema();

            if (schema.Contains(t.Name))
            {
                // If metadata for this type already exists in the schema, return the existing schema
                return schema;
            }

            // Create metadata for the type
            var metadata = new Metadata { Name = t.Name };

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

            // Add this type's metadata to the schema
            schema.Add(metadata);
            return schema;
        }

        private static void HandleProperty(PropertyInfo propertyInfo, Metadata metadata, Schema schema)
        {
            if (propertyInfo.Name == "Id" && propertyInfo.PropertyType == typeof(int))
            {
                metadata.IsEntity = true;
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
        public string Name;
        public bool IsEntity = false;
        public List<MetadataField> Fields = new List<MetadataField>();
        public List<MetadataReferenceField> ReferenceFields = new List<MetadataReferenceField>();
        public List<MetadataCollectionReference> CollectionReferences = new List<MetadataCollectionReference>();
        public Schema Schema = new Schema();

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

            var schemaString = Schema.Any()
                ? string.Join(Environment.NewLine, Schema.Select(m => $"  - {m.Name}"))
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
{collectionReferencesString}
  Schema:
{schemaString}";
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
            return $"Name: {Name}, CollectionType: {CollectionType.Name}, ItemType: {ItemType.Name}, IsRequired: {IsRequired}";
        }
    }

    public class Schema : List<Metadata>
    {
        public bool Contains(string name) => this.Any(m => m.Name == name);

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
            if (!File.Exists(filePath)) return new Schema();
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Schema>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto // Preserve type information
            }) ?? new Schema();
        }
    }


}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ck.Database;


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
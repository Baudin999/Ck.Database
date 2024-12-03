namespace Ck.Database;

public struct EntityKey
{
    public string TypeName { get; }
    public int Id { get; }

    public EntityKey(string typeName, int id)
    {
        TypeName = typeName;
        Id = id;
    }

    public override bool Equals(object obj)
    {
        return obj is EntityKey other &&
               TypeName == other.TypeName &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        unchecked // Allow overflow, as it is harmless in this context
        {
            int hash = 17;
            hash = hash * 31 + (TypeName != null ? TypeName.GetHashCode() : 0);
            hash = hash * 31 + Id.GetHashCode();
            return hash;
        }
    }
}


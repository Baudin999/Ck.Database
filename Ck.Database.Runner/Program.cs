namespace Ck.Database.Runner;

class Program
{
    static void Main(string[] args)
    {
        var schema = MetadataBuilder.BuildSchema(typeof(Person));
        
        Console.WriteLine(schema);
    }
}

public class Person
{
    public int Id;
    public int Number { get; set; }

    public Inventory Inventory;
}

public class Inventory
{
    public int Id { get; set; }
    
    public List<InventoryItem> Items { get; set; }
}

public class InventoryItem
{
    public int Id;
    public string Name;
}
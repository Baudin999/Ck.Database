namespace Ck.Database.Tests;


// Define your entities
public class Foo
{
    public int Id;
    public string Name { get; set; }
    public Bar Bar;
    public List<Drink> Somethings { get; set; }
}

public class Bar
{
    public int Id { get; set; }
    public string Street;
    public Drink Drink;
}

public class Drink
{
    public string Name;
    public int Id;
}
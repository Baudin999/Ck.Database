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

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Book> Books { get; set; }
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; }
    public Author Author { get; set; }
}

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

public class CircleAuthor
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<CircleBook> Books { get; set; }
}

public class CircleBook
{
    public int Id { get; set; }
    public string Title { get; set; }
    public CircleAuthor CircleAuthor { get; set; }
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
    public Publication Publication { get; set; }
    public List<Reviewer> Reviewers { get; set; }
}

public class Publication
{
    public int Id { get; set; }
    public string PublisherName { get; set; }
    public DateTime PublishDate { get; set; }
}

public class Reviewer
{
    public int Id { get; set; }
    public string ReviewerName { get; set; }
    public string Review { get; set; }
}


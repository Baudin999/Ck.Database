namespace Ck.Database.Tests;

public class TestAuthors
{
    
    [Fact]
    public void StoreAutors()
    {
        var author = new Author
        {
            Name = "John Doe",
            Books = new List<Book>
            {
                new Book { Title = "Book 1" },
                new Book { Title = "Book 2" }
            }
        };
        
        // Set the Author reference in each Book
        foreach (var book in author.Books)
        {
            book.Author = author;
        }

        var Path = @"C:\TEMP\TestDbAuthors";
        var database = new Database(Path);
        database.Store(author);
        var authorId = author.Id;

        database.Dispose();
        database = new Database(Path);
        var secondAuthor = database.Find<Author>(authorId);
        Assert.Equal(2, secondAuthor.Books.Count);

        secondAuthor.Books.Remove(secondAuthor.Books[1]);
        database.Store(secondAuthor);
        
        database.Dispose();
        database = new Database(Path);
        var thirdAuthor = database.Find<Author>(authorId);
        Assert.Single(thirdAuthor.Books);

    }
}
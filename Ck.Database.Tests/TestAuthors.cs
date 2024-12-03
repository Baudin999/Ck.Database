namespace Ck.Database.Tests;

public class TestAuthors
{
    
    [Fact]
    public async void StoreAutors()
    {
        var path = @"C:\TEMP\TestDbAuthors";
        if (Directory.Exists(path)) Directory.Delete(path, true);
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

        var database = new Database(path);
        database.Store(author);
        var authorId = author.Id;

        database.Dispose();
        database = new Database(path);
        var secondAuthor = await database.Find<Author>(authorId);
        Assert.Equal(2, secondAuthor.Books.Count);

        secondAuthor.Books.Remove(secondAuthor.Books[1]);
        database.Store(secondAuthor);
        
        database.Dispose();
        database = new Database(path);
        var thirdAuthor = await database.Find<Author>(authorId);
        Assert.Single(thirdAuthor.Books);

    }
}
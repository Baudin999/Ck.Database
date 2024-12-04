namespace Ck.Database.Tests;

public class TestAuthors
{
    
    [Fact]
    public async void StoreAutors()
    {
        var path = @"C:\TEMP\TestDbAuthors";
        if (Directory.Exists(path)) Directory.Delete(path, true);
        var author = new CircleAuthor
        {
            Name = "John Doe",
            Books = new List<CircleBook>
            {
                new CircleBook { Title = "Book 1" },
                new CircleBook { Title = "Book 2" }
            }
        };
        
        // Set the Author reference in each Book
        foreach (var book in author.Books)
        {
            book.CircleAuthor = author;
        }

        var database = new Database(path);
        database.Store(author);
        var authorId = author.Id;
        
        database.Dispose();
        database = new Database(path);
        var secondAuthor = database.Find<CircleAuthor>(authorId);
        Assert.Equal(2, secondAuthor.Books.Count);
        
        secondAuthor.Books.Remove(secondAuthor.Books[1]);
        database.Store(secondAuthor);
        
        database.Dispose();
        database = new Database(path);
        var thirdAuthor = database.Find<CircleAuthor>(authorId);
        Assert.Single(thirdAuthor.Books);

    }
}
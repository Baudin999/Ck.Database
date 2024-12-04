namespace Ck.Database.Tests;

public class AuthorFixture : IDisposable
{
    public string Path { get; }
    public JsonDatabase JsonDatabase { get; }

    public AuthorFixture()
    {
        Path = @"C:\TEMP\Db003";
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
        JsonDatabase = new JsonDatabase(Path);

        // Setup initial data
        var publication = new Publication
        {
            PublisherName = "Bloomsbury",
            PublishDate = new DateTime(1997, 6, 26)
        };

        var reviewer1 = new Reviewer
        {
            ReviewerName = "John Smith",
            Review = "A magical journey for all ages!"
        };

        var reviewer2 = new Reviewer
        {
            ReviewerName = "Jane Doe",
            Review = "A timeless classic!"
        };

        var book = new Book
        {
            Title = "Harry Potter and the Philosopher's Stone",
            Publication = publication,
            Reviewers = new List<Reviewer> { reviewer1, reviewer2 }
        };

        var author = new Author
        {
            Name = "J.K. Rowling",
            Books = new List<Book> { book }
        };

        // Store objects
        JsonDatabase.Store(author);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }
}
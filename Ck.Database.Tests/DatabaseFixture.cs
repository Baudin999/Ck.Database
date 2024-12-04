namespace Ck.Database.Tests;

public class DatabaseFixture : IDisposable
{
    public string Path { get; }
    public JsonDatabase JsonDatabase { get; }

    public DatabaseFixture()
    {
        Path = @"C:\TEMP\TestDb";
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
        JsonDatabase = new JsonDatabase(Path);

        // Setup initial data
        var foo = new Foo
        {
            Name = "Carlos's World",
            Bar = new Bar
            {
                Street = "Weezenhof 6134",
                Drink = new Drink { Name = "White wine" }
            },
            Somethings = new List<Drink>
            {
                new Drink { Name = "Gin & Tonic" },
                new Drink { Name = "Beer" },
                new Drink { Name = "Whiskey" },
            }
        };
        JsonDatabase.Store(foo);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }
}

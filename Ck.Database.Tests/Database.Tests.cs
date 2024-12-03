namespace Ck.Database.Tests;

public class Database_Tests: IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public Database_Tests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async void TestDatabase_Load()
    {
        var database = _fixture.Database;

        var foos = await database.FindAll<Foo>();
        var foo = foos.FirstOrDefault();

        Assert.NotNull(foo);
        Assert.Equal(3, foo.Somethings.Count);
    }

}



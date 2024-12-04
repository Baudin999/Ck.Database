
namespace Ck.Database.Tests
{
    public class JsonDatabaseTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fixture;

        public JsonDatabaseTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void ShouldCreateExpectedFilesInDatabaseDirectory()
        {
            // Arrange
            var expectedFiles = new[]
            {
                "Foo.json",
                "Bar.json",
                "Drink.json",
                "Schema.json",
                "IdFarm.json"
            };

            // Act
            var files = Directory.GetFiles(_fixture.Path);

            // Assert
            foreach (var fileName in expectedFiles)
            {
                Assert.Contains(Path.Combine(_fixture.Path, fileName), files);
            }
        }

        [Fact]
        public void ShouldPersistFooDataCorrectly()
        {
            // Arrange
            var fooPath = Path.Combine(_fixture.Path, "Foo.json");

            // Act
            var fooContent = File.ReadAllText(fooPath);

            // Assert
            Assert.Contains("Carlos's World", fooContent);
        }

        [Fact]
        public void ShouldPersistIdFarmCorrectly()
        {
            // Arrange
            var idFarmPath = Path.Combine(_fixture.Path, "IdFarm.json");

            // Act
            var idFarmContent = File.ReadAllText(idFarmPath);

            // Assert
            Assert.Contains("6", idFarmContent); // Assuming 5 entities are created and the next ID is 6
        }

        [Fact]
        public void ShouldPersistBarDataCorrectly()
        {
            // Arrange
            var barPath = Path.Combine(_fixture.Path, "Bar.json");

            // Act
            var barContent = File.ReadAllText(barPath);

            // Assert
            Assert.Contains("Weezenhof 6134", barContent);
        }

        [Fact]
        public void ShouldPersistDrinkDataCorrectly()
        {
            // Arrange
            var drinkPath = Path.Combine(_fixture.Path, "Drink.json");

            // Act
            var drinkContent = File.ReadAllText(drinkPath);

            // Assert
            Assert.Contains("Gin & Tonic", drinkContent);
            Assert.Contains("Beer", drinkContent);
            Assert.Contains("Whiskey", drinkContent);
            Assert.Contains("White wine", drinkContent);
        }
    }
}


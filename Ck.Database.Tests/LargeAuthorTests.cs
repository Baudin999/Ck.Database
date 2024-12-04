

using System.IO;
using Xunit;

namespace Ck.Database.Tests
{
    public class LargeAuthorTests : IClassFixture<AuthorFixture>
    {
        private readonly AuthorFixture _fixture;

        public LargeAuthorTests(AuthorFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void ShouldCreateExpectedFilesInDatabaseDirectory()
        {
            // Arrange
            var expectedFiles = new[]
            {
                "Author.json",
                "Book.json",
                "Publication.json",
                "Reviewer.json",
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
        public void ShouldPersistAuthorDataCorrectly()
        {
            // Arrange
            var authorPath = Path.Combine(_fixture.Path, "Author.json");

            // Act
            var authorContent = File.ReadAllText(authorPath);

            // Assert
            Assert.Contains("J.K. Rowling", authorContent);
        }

        [Fact]
        public void ShouldPersistBookDataCorrectly()
        {
            // Arrange
            var bookPath = Path.Combine(_fixture.Path, "Book.json");

            // Act
            var bookContent = File.ReadAllText(bookPath);

            // Assert
            Assert.Contains("Harry Potter and the Philosopher's Stone", bookContent);
        }

        [Fact]
        public void ShouldPersistPublicationDataCorrectly()
        {
            // Arrange
            var publicationPath = Path.Combine(_fixture.Path, "Publication.json");

            // Act
            var publicationContent = File.ReadAllText(publicationPath);

            // Assert
            Assert.Contains("Bloomsbury", publicationContent);
            Assert.Contains("1997-06-26", publicationContent);
        }

        [Fact]
        public void ShouldPersistReviewerDataCorrectly()
        {
            // Arrange
            var reviewerPath = Path.Combine(_fixture.Path, "Reviewer.json");

            // Act
            var reviewerContent = File.ReadAllText(reviewerPath);

            // Assert
            Assert.Contains("John Smith", reviewerContent);
            Assert.Contains("A magical journey for all ages!", reviewerContent);
            Assert.Contains("Jane Doe", reviewerContent);
            Assert.Contains("A timeless classic!", reviewerContent);
        }

        [Fact]
        public void ShouldPersistIdFarmCorrectly()
        {
            // Arrange
            var idFarmPath = Path.Combine(_fixture.Path, "IdFarm.json");

            // Act
            var idFarmContent = File.ReadAllText(idFarmPath);

            // Assert
            Assert.Contains("5", idFarmContent); // 5 entities stored
        }
    }
}

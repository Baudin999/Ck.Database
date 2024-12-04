using System.IO;
using Xunit;

namespace Ck.Database.Tests
{
    public class DatabaseEngineTests : IDisposable
    {
        private readonly string _testDirectory;

        public DatabaseEngineTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "CkDatabaseTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public void ShouldStoreAndRetrieveEntityCorrectly()
        {
            // Arrange
            var database = new Database(_testDirectory);

            var author = new Author
            {
                Name = "J.K. Rowling"
            };

            // Act
            database.Store(author);
            var retrievedAuthor = database.Find<Author>(author.Id);

            // Assert
            Assert.NotNull(retrievedAuthor);
            Assert.Equal("J.K. Rowling", retrievedAuthor.Name);
        }

        [Fact]
        public void ShouldFindAllEntitiesCorrectly()
        {
            // Arrange
            var database = new Database(_testDirectory);

            var author1 = new Author { Name = "J.K. Rowling" };
            var author2 = new Author { Name = "George R.R. Martin" };

            database.Store(author1);
            database.Store(author2);

            // Act
            var authors = database.FindAll<Author>();

            // Assert
            Assert.NotNull(authors);
            Assert.Equal(2, authors.Count);
            Assert.Contains(authors, a => a.Name == "J.K. Rowling");
            Assert.Contains(authors, a => a.Name == "George R.R. Martin");
        }

        [Fact]
        public void ShouldDeleteEntityCorrectly()
        {
            // Arrange
            var database = new Database(_testDirectory);

            var author = new Author
            {
                Name = "J.K. Rowling"
            };

            database.Store(author);

            // Act
            database.Delete<Author>(author.Id);
            var deletedAuthor = database.Find<Author>(author.Id);

            // Assert
            Assert.Null(deletedAuthor);
        }

        [Fact]
        public void ShouldUpdateEntityCorrectly()
        {
            // Arrange
            var database = new Database(_testDirectory);

            var author = new Author
            {
                Name = "J.K. Rowling"
            };

            database.Store(author);

            // Act
            author.Name = "Robert Galbraith"; // Update the name
            database.Store(author);

            var updatedAuthor = database.Find<Author>(author.Id);

            // Assert
            Assert.NotNull(updatedAuthor);
            Assert.Equal("Robert Galbraith", updatedAuthor.Name);
        }

        [Fact]
        public void ShouldHandleComplexRelationships()
        {
            // Arrange
            var database = new Database(_testDirectory);

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

            database.Store(author);

            // Act
            var retrievedAuthor = database.Find<Author>(author.Id);
            var retrievedBooks = database.FindAll<Book>();
            var retrievedReviewers = database.FindAll<Reviewer>();

            // Assert
            Assert.NotNull(retrievedAuthor);
            Assert.Equal("J.K. Rowling", retrievedAuthor.Name);

            Assert.NotNull(retrievedBooks);
            Assert.Single(retrievedBooks);
            Assert.Equal("Harry Potter and the Philosopher's Stone", retrievedBooks[0].Title);

            Assert.NotNull(retrievedReviewers);
            Assert.Equal(2, retrievedReviewers.Count);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}

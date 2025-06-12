// XmlApi.Tests/XmlApiControllerTests.cs - All tests and test infrastructure in one file.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using bd2proj;
using bd2proj.Models;
using Microsoft.Data.SqlClient; // Ensure this namespace is correct for your models

namespace bd2proj.Tests
{
    // CustomWebApplicationFactory allows us to configure the test host
    // and override services, like connection strings.
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override IHostBuilder CreateHostBuilder()
        {
            // Create a default host builder for the application.
            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    // Use the specified startup class from the main API project.
                    webBuilder.UseStartup<TStartup>();
                });
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Configure the web host specifically for testing.
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                // Clear existing configuration sources.
                conf.Sources.Clear();

                // Build a configuration to ensure the controller gets the 'TestConnection' string.
                // We're providing the connection string directly here for the IConfiguration.
                var inMemoryConfiguration = new Dictionary<string, string>
                {
                    {"ConnectionStrings:DefaultConnection", "Server=localhost,1433;Database=XmlDb_Test;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;"}
                };
                conf.AddInMemoryCollection(inMemoryConfiguration);

                // For testing, we ensure that the TestConnection is used.
                // The controller's constructor takes IConfiguration, and this setup
                // makes sure the correct connection string is loaded.
            });

            // Ensure the application environment is set to "Development" for consistency.
            builder.UseEnvironment("Development");
        }
    }

    // DatabaseFixture manages the lifecycle of the test database.
    // It creates and cleans up the database table for all tests within the collection.
    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly string _connectionString;
        private readonly string _masterConnectionString;
        private readonly string _testDbName;

        public DatabaseFixture()
        {
            // Directly specify the connection string here, removing the need for appsettings.json
            _connectionString = "Server=localhost,1433;Database=XmlDb_Test;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";
            
            // Extract the database name from the connection string.
            var builder = new SqlConnectionStringBuilder(_connectionString);
            _testDbName = builder.InitialCatalog;
            builder.InitialCatalog = "master"; // Connect to master to create/drop the test database.
            _masterConnectionString = builder.ConnectionString;
        }

        public async Task InitializeAsync()
        {
            // Ensure the test database exists and is empty before tests run.
            await DropAndCreateDatabaseAsync();
            await CreateXmlDocumentsTableAsync();
        }

        public async Task DisposeAsync()
        {
            // Clean up the test database after all tests in the collection have run.
            await DropDatabaseAsync();
        }

        private async Task DropAndCreateDatabaseAsync()
        {
            using var conn = new SqlConnection(_masterConnectionString);
            await conn.OpenAsync();

            // Disconnect all users from the database before dropping.
            // This is crucial to avoid "database in use" errors.
            var disconnectCmd = new SqlCommand($@"
                ALTER DATABASE [{_testDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE IF EXISTS [{_testDbName}];
            ", conn);
            try
            {
                await disconnectCmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (ex.Number == 3702)
            {
                // Error 3702: Cannot drop the database because it is currently in use.
                // This might happen if there are lingering connections.
                Console.WriteLine($"Warning: Could not drop database initially (Error {ex.Number}). Retrying or ignoring.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during database drop: {ex.Message}");
            }

            // Create the test database.
            var createDbCmd = new SqlCommand($"CREATE DATABASE [{_testDbName}]", conn);
            await createDbCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Database {_testDbName} created.");
        }

        private async Task DropDatabaseAsync()
        {
            using var conn = new SqlConnection(_masterConnectionString);
            await conn.OpenAsync();

            var disconnectCmd = new SqlCommand($@"
                ALTER DATABASE [{_testDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE IF EXISTS [{_testDbName}];
            ", conn);
            try
            {
                await disconnectCmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Error during database teardown: {ex.Message}");
            }
            Console.WriteLine($"Database {_testDbName} dropped.");
        }

        private async Task CreateXmlDocumentsTableAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand($@"
                CREATE TABLE XmlDocuments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(255),
                    XmlContent NVARCHAR(MAX) NOT NULL,
                    CreateData DATETIME NOT NULL DEFAULT GETDATE()
                );
            ", conn);
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("XmlDocuments table created.");
        }

        public async Task ClearTableAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = new SqlCommand("DELETE FROM XmlDocuments; DBCC CHECKIDENT('XmlDocuments', RESEED, 0);", conn);
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("XmlDocuments table cleared and ID reseeded.");
        }
    }

    // Collection definition to apply the DatabaseFixture to all tests in this collection.
    [CollectionDefinition("Database Collection")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        // This class has no code, and is used to apply the ICollectionFixture interface.
        // All tests in this collection will share the same DatabaseFixture instance.
    }

    // Main test class for XmlApiController.
    // IClassFixture<CustomWebApplicationFactory> initializes the web application host.
    // ICollectionFixture<DatabaseFixture> ensures the database setup/teardown is handled for the collection.
    public class XmlApiControllerTests : IClassFixture<CustomWebApplicationFactory<bd2proj.Program>>, IAsyncLifetime // bd2proj.Program is your Startup class
    {
        private readonly HttpClient _client;
        private readonly DatabaseFixture _dbFixture;

        public XmlApiControllerTests(CustomWebApplicationFactory<bd2proj.Program> factory, DatabaseFixture dbFixture)
        {
            _client = factory.CreateClient();
            _dbFixture = dbFixture;
        }

        // InitializeAsync runs before *each* test method.
        // It's used here to ensure the table is cleared for isolated test runs.
        public async Task InitializeAsync()
        {
            await _dbFixture.ClearTableAsync();
        }

        // DisposeAsync runs after *each* test method.
        public Task DisposeAsync()
        {
            return Task.CompletedTask; // No specific per-test cleanup needed beyond InitializeAsync
        }

        // Helper to post an XML document and return the posted model.
        private async Task<XmlDocumentModel> PostSampleXmlDocument(string name, string xmlContent)
        {
            var doc = new XmlDocumentModel
            {
                Name = name,
                XmlContent = xmlContent,
                CreateData = DateTime.UtcNow // Using UtcNow for consistency
            };

            var response = await _client.PostAsJsonAsync("/api/XmlApi", doc);
            response.EnsureSuccessStatusCode(); // Throws if not a success status code
            var createdDoc = await response.Content.ReadFromJsonAsync<XmlDocumentModel>();
            Assert.NotNull(createdDoc);
            return createdDoc;
        }

        #region GET AllXmls Tests
        [Fact]
        public async Task GetAllXmls_ReturnsNotFound_WhenNoDocumentsExist()
        {
            // Arrange (DB is clear by InitializeAsync)

            // Act
            var response = await _client.GetAsync("/api/XmlApi");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("No documents found", content);
        }

        [Fact]
        public async Task GetAllXmls_ReturnsOkWithDocuments_WhenDocumentsExist()
        {
            // Arrange
            await PostSampleXmlDocument("Doc1", "<root><item>1</item></root>");
            await PostSampleXmlDocument("Doc2", "<data><value>test</value></data>");

            // Act
            var response = await _client.GetAsync("/api/XmlApi");

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            var xmls = await response.Content.ReadFromJsonAsync<List<XmlDocumentModel>>();
            Assert.NotNull(xmls);
            Assert.Equal(2, xmls.Count);
            Assert.Contains(xmls, d => d.Name == "Doc1");
            Assert.Contains(xmls, d => d.Name == "Doc2");
        }
        #endregion

        #region GET By Id Tests
        [Fact]
        public async Task GetXmlDocument_ReturnsOk_WhenDocumentExists()
        {
            // Arrange
            var createdDoc = await PostSampleXmlDocument("SpecificDoc", "<info><id>abc</id></info>");

            // Act
            var response = await _client.GetAsync($"/api/XmlApi/{createdDoc.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            var doc = await response.Content.ReadFromJsonAsync<XmlDocumentModel>();
            Assert.NotNull(doc);
            Assert.Equal(createdDoc.Id, doc.Id);
            Assert.Equal("SpecificDoc", doc.Name);
            Assert.Equal("<info><id>abc</id></info>", doc.XmlContent);
        }

        [Fact]
        public async Task GetXmlDocument_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange (DB is clear)
            var nonExistentId = 999;

            // Act
            var response = await _client.GetAsync($"/api/XmlApi/{nonExistentId}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Requested Document was not found in DB", content);
            Assert.Contains($"\"requestedId\":{nonExistentId}", content);
        }
        #endregion

        #region POST Tests
        [Fact]
        public async Task PostXmlDocument_ReturnsCreatedAtAction_WithNewDocument()
        {
            // Arrange
            var newDoc = new XmlDocumentModel
            {
                Name = "NewDocument",
                XmlContent = "<data><item attr='val'/></data>",
                CreateData = DateTime.Parse("2024-01-01T10:00:00Z").ToUniversalTime()
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/XmlApi", newDoc);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdDoc = await response.Content.ReadFromJsonAsync<XmlDocumentModel>();
            Assert.NotNull(createdDoc);
            Assert.True(createdDoc.Id > 0); // Id should be assigned by DB
            Assert.Equal(newDoc.Name, createdDoc.Name);
            Assert.Equal(newDoc.XmlContent, createdDoc.XmlContent);
            Assert.Equal(newDoc.CreateData, createdDoc.CreateData);

            // Verify location header
            Assert.NotNull(response.Headers.Location);
            Assert.Contains($"/api/XmlApi/{createdDoc.Id}", response.Headers.Location.OriginalString);

            // Verify it can be retrieved
            var getResponse = await _client.GetAsync($"/api/XmlApi/{createdDoc.Id}");
            getResponse.EnsureSuccessStatusCode();
            var retrievedDoc = await getResponse.Content.ReadFromJsonAsync<XmlDocumentModel>();
            Assert.NotNull(retrievedDoc);
            Assert.Equal(createdDoc.Id, retrievedDoc.Id);
        }
        #endregion

        #region PUT Tests
        [Fact]
        public async Task PutXmlDocument_UpdatesXmlContent_WhenDocumentAndXPathAreValid()
        {
            // Arrange
            var initialXml = "<root><settings><value>oldValue</value></settings></root>";
            var createdDoc = await PostSampleXmlDocument("DocToUpdate", initialXml);

            var updateReq = new XmlUpdateRequest
            {
                XPath = "/root/settings/value",
                NewValue = "newValue"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/XmlApi/{createdDoc.Id}", updateReq);

            // Assert
            response.EnsureSuccessStatusCode(); // Expect 200 OK
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("XML updated", content.Trim('"')); // Controller returns Ok("XML updated") as a string

            // Verify the update by retrieving the document
            var getResponse = await _client.GetAsync($"/api/XmlApi/{createdDoc.Id}");
            getResponse.EnsureSuccessStatusCode();
            var updatedDoc = await getResponse.Content.ReadFromJsonAsync<XmlDocumentModel>();
            Assert.NotNull(updatedDoc);
            Assert.Contains("<value>newValue</value>", updatedDoc.XmlContent);
        }

        [Fact]
        public async Task PutXmlDocument_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            var nonExistentId = 999;
            var updateReq = new XmlUpdateRequest { XPath = "/root", NewValue = "new" };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/XmlApi/{nonExistentId}", updateReq);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Document Not Found", content);
            Assert.Contains($"\"requestedId\":{nonExistentId}", content);
        }

        [Fact]
        public async Task PutXmlDocument_ReturnsNotFound_WhenXPathElementDoesNotExist()
        {
            // Arrange
            var initialXml = "<root><item>value</item></root>";
            var createdDoc = await PostSampleXmlDocument("DocXPathTest", initialXml);

            var updateReq = new XmlUpdateRequest
            {
                XPath = "/root/nonexistentNode", // Invalid XPath
                NewValue = "new"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/XmlApi/{createdDoc.Id}", updateReq);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("XPath element not found", content);
        }
        #endregion

        #region DELETE Tests
        [Fact]
        public async Task DeleteXmlDocument_ReturnsNoContent_WhenDocumentExists()
        {
            // Arrange
            var createdDoc = await PostSampleXmlDocument("DocToDelete", "<data/>");

            // Act
            var response = await _client.DeleteAsync($"/api/XmlApi/{createdDoc.Id}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // Verify deletion
            var getResponse = await _client.GetAsync($"/api/XmlApi/{createdDoc.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteXmlDocument_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            var nonExistentId = 999;

            // Act
            var response = await _client.DeleteAsync($"/api/XmlApi/{nonExistentId}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        #endregion

        #region SEARCH Tests
        [Fact]
        public async Task SearchXmlDocument_ReturnsMatchingDocuments_ByName()
        {
            // Arrange
            await PostSampleXmlDocument("SpecificName1", "<xml/>");
            await PostSampleXmlDocument("OtherName", "<xml/>");
            await PostSampleXmlDocument("SpecificName2", "<xml/>");

            // Act
            var response = await _client.GetAsync("/api/XmlApi/search?name=SpecificName1");

            // Assert
            response.EnsureSuccessStatusCode();
            var results = await response.Content.ReadFromJsonAsync<List<XmlDocumentModel>>();
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal("SpecificName1", results[0].Name);
        }

        [Fact]
        public async Task SearchXmlDocument_ReturnsMatchingDocuments_ByNode()
        {
            // Arrange
            await PostSampleXmlDocument("DocA", "<root><item>1</item></root>");
            await PostSampleXmlDocument("DocB", "<data><value>test</value></data>");
            await PostSampleXmlDocument("DocC", "<another/>");

            // Act
            var response = await _client.GetAsync("/api/XmlApi/search?node=item");

            // Assert
            response.EnsureSuccessStatusCode();
            var results = await response.Content.ReadFromJsonAsync<List<XmlDocumentModel>>();
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal("DocA", results[0].Name);
        }

        [Fact]
        public async Task SearchXmlDocument_ReturnsMatchingDocuments_ByAttribute()
        {
            // Arrange
            await PostSampleXmlDocument("DocX", "<root><element key='val'/></root>");
            await PostSampleXmlDocument("DocY", "<root><another/></root>");

            // Act
            var response = await _client.GetAsync("/api/XmlApi/search?attribute=key");

            // Assert
            response.EnsureSuccessStatusCode();
            var results = await response.Content.ReadFromJsonAsync<List<XmlDocumentModel>>();
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal("DocX", results[0].Name);
        }

        [Fact]
        public async Task SearchXmlDocument_ReturnsMatchingDocuments_ByAttributeValue()
        {
            // Arrange
            await PostSampleXmlDocument("DocP", "<root><item id='unique'/></root>");
            await PostSampleXmlDocument("DocQ", "<root><item id='common'/></root>");
            await PostSampleXmlDocument("DocR", "<root><element id='common'/></root>");

            // Act
            var response = await _client.GetAsync("/api/XmlApi/search?attributeValue=unique");

            // Assert
            response.EnsureSuccessStatusCode();
            var results = await response.Content.ReadFromJsonAsync<List<XmlDocumentModel>>();
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal("DocP", results[0].Name);
        }

        [Fact]
        public async Task SearchXmlDocument_ReturnsEmptyList_WhenNoMatch()
        {
            // Arrange
            await PostSampleXmlDocument("Doc1", "<root><item>1</item></root>");

            // Act
            var response = await _client.GetAsync("/api/XmlApi/search?name=NonExistentName");

            // Assert
            response.EnsureSuccessStatusCode(); // Expect 200 OK with empty list
            var results = await response.Content.ReadFromJsonAsync<List<XmlDocumentModel>>();
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchXmlDocument_CombinesCriteria()
        {
            // Arrange
            await PostSampleXmlDocument("Invoice_2023", "<invoice><item id='A'/></invoice>");
            await PostSampleXmlDocument("Order_2023", "<order><item id='B'/></order>");
            await PostSampleXmlDocument("Invoice_2024", "<invoice><product id='C'/></invoice>");

            // Act: Search for name "Invoice" and node "item"
            var response = await _client.GetAsync("/api/XmlApi/search?name=Invoice_2023&node=item");

            // Assert
            response.EnsureSuccessStatusCode();
            var results = await response.Content.ReadFromJsonAsync<List<XmlDocumentModel>>();
            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal("Invoice_2023", results[0].Name);
        }
        #endregion

        #region XSL Transform Tests
        [Fact]
        public async Task XslTransform_ReturnsHtmlContent_WhenDocumentExistsAndTransformSuccessful()
        {
            // Arrange
            var xmlContent = "<data><message>Hello World</message></data>";
            var createdDoc = await PostSampleXmlDocument("XslTestDoc", xmlContent);

            // Act
            var response = await _client.GetAsync($"/api/XmlApi/{createdDoc.Id}/transform");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            var htmlContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("<html>", htmlContent);
            Assert.Contains("<h1>XML Content</h1>", htmlContent);
            Assert.Contains("<data><message>Hello World</message></data>", htmlContent); // Assuming basic copy-of transform
        }

        [Fact]
        public async Task XslTransform_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            var nonExistentId = 999;

            // Act
            var response = await _client.GetAsync($"/api/XmlApi/{nonExistentId}/transform");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task XslTransform_ReturnsBadRequest_WhenXmlContentIsEmpty()
        {
            // Arrange
            var createdDoc = await PostSampleXmlDocument("EmptyXml", ""); // Empty XML content

            // Act
            var response = await _client.GetAsync($"/api/XmlApi/{createdDoc.Id}/transform");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("XML content is missing or empty.", content);
        }
        #endregion
    }
}

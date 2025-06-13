using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using bd2proj.Controllers;
using bd2proj.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace bd2proj.Tests.Controllers
{
    public class XmlApiControllerTests : IDisposable
    {
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly string _connectionString;
        private readonly SqlConnection _testConnection;

        public XmlApiControllerTests()
        {
            _connectionString = "Server=localhost,1433;Database=XmlDb_Test;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";
            
            _mockConfig = new Mock<IConfiguration>();
            _testConnection = new SqlConnection(_connectionString);
            _testConnection.Open();
        }

        public void Dispose()
        {
            _testConnection.Close();
            _testConnection.Dispose();
        }

        private async Task<int> InsertTestDocument(string name, string xmlContent, DateTime createData)
        {
            using var cmd = new SqlCommand(
                "INSERT INTO XmlDocuments (Name, XmlContent, CreateData) OUTPUT INSERTED.Id VALUES (@Name, @XmlContent, @CreateData)",
                _testConnection);
            cmd.Parameters.AddWithValue("@Name", (object?)name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@XmlContent", xmlContent);
            cmd.Parameters.AddWithValue("@CreateData", createData);
            return (int)await cmd.ExecuteScalarAsync();
        }

        [Fact]
        public async Task GetAllXmls_ReturnsEmptyList_WhenNoDocuments()
        {
            // Arrange
            // Clear any existing data
            using var clearCmd = new SqlCommand("DELETE FROM XmlDocuments", _testConnection);
            await clearCmd.ExecuteNonQueryAsync();

            // Properly setup the mock configuration
            var mockConfigSection = new Mock<IConfigurationSection>();
            mockConfigSection.Setup(x => x.Value).Returns(_connectionString);
            _mockConfig.Setup(x => x.GetSection("ConnectionStrings:DefaultConnection"))
                .Returns(mockConfigSection.Object);

            var controller = new XmlApiController(_mockConfig.Object);

            // Act
            var result = await controller.GetAllXmls();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("No documents found", (notFoundResult.Value as dynamic)?.Message);
        }

        [Fact]
        public async Task GetAllXmls_ReturnsAllDocuments()
        {
            // Arrange
            var testData = new List<XmlDocumentModel>
            {
                new XmlDocumentModel { Name = "Test1", XmlContent = "<root><test>1</test></root>", CreateData = DateTime.Now },
                new XmlDocumentModel { Name = "Test2", XmlContent = "<root><test>2</test></root>", CreateData = DateTime.Now.AddDays(-1) }
            };

            foreach (var doc in testData)
            {
                await InsertTestDocument(doc.Name, doc.XmlContent, doc.CreateData);
            }

            var controller = new XmlApiController(_mockConfig.Object);

            // Act
            var result = await controller.GetAllXmls();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedDocs = Assert.IsType<List<XmlDocumentModel>>(okResult.Value);
            Assert.Equal(2, returnedDocs.Count);
        }
        
        [Fact]
        public async Task GetXmlDocument_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            var controller = new XmlApiController(_mockConfig.Object);
            var nonExistentId = 999;

            // Act
            var result = await controller.GetXmlDocument(nonExistentId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Requested Document was not found in DB", (notFoundResult.Value as dynamic)?.Message);
            Assert.Equal(nonExistentId, (notFoundResult.Value as dynamic)?.RequestedId);
        }

        [Fact]
        public async Task GetXmlDocument_ReturnsDocument_WhenExists()
        {
            // Arrange
            var testDoc = new XmlDocumentModel
            {
                Name = "Test Document",
                XmlContent = "<root><node>value</node></root>",
                CreateData = DateTime.Now
            };
            var id = await InsertTestDocument(testDoc.Name, testDoc.XmlContent, testDoc.CreateData);

            var controller = new XmlApiController(_mockConfig.Object);

            // Act
            var result = await controller.GetXmlDocument(id);

            // Assert
            var okResult = Assert.IsType<XmlDocumentModel>(result.Value);
            Assert.Equal(id, okResult.Id);
            Assert.Equal(testDoc.Name, okResult.Name);
            Assert.Equal(testDoc.XmlContent, okResult.XmlContent);
            Assert.Equal(testDoc.CreateData, okResult.CreateData, TimeSpan.FromSeconds(1));
        }
    }
}
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using bd2proj.Models;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace bd2proj.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XmlApiController : ControllerBase
    {
        private readonly string _connectionString;

        public XmlApiController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<XmlDocumentModel>> GetXmlDocument(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = new SqlCommand("SELECT Id, Name, XmlContent, CreateData FROM XmlDocuments WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new XmlDocumentModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                    XmlContent = reader.GetString(2),
                    CreateData = reader.GetDateTime(3)
                };
            }

            return NotFound(new { Message = "Requested Document was not found in DB", RequestedId = id });
        }

        [HttpPost]
        public async Task<ActionResult<XmlDocumentModel>> PostXmlDocument([FromBody] XmlDocumentModel doc)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("INSERT INTO XmlDocuments (Name, XmlContent, CreateData) OUTPUT INSERTED.Id VALUES (@Name, @XmlContent, @CreateData)", conn);
            cmd.Parameters.AddWithValue("@Name", (object?)doc.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@XmlContent", doc.XmlContent);
            cmd.Parameters.AddWithValue("@CreateData", doc.CreateData);

            var newId = (int)await cmd.ExecuteScalarAsync();
            doc.Id = newId;

            return CreatedAtAction(nameof(GetXmlDocument), new { id = newId }, doc);
        }
        
        [HttpPut("{id}")]
        
        public async Task<ActionResult<XmlDocumentModel>> PutXmlDocument(int id, [FromBody] XmlUpdateRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("SELECT XmlContent FROM XmlDocuments WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            var xmlContent = (string?)await cmd.ExecuteScalarAsync();

            if (xmlContent == null)
            {
                return NotFound(new { Message = "Document Not Found", RequestedId = id });
            }

            var xml = XDocument.Parse(xmlContent);
            var element = xml.XPathSelectElement(req.XPath);
            if (element == null)
                return NotFound(new { Message = "XPath element not found" });

            element.Value = req.NewValue;
            var updatedXml = xml.ToString();

            // Update in DB
            var updateCmd = new SqlCommand("UPDATE XmlDocuments SET XmlContent = @XmlContent WHERE Id = @Id", conn);
            updateCmd.Parameters.AddWithValue("@XmlContent", updatedXml);
            updateCmd.Parameters.AddWithValue("@Id", id);

            await updateCmd.ExecuteNonQueryAsync();

            return Ok("XML updated");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteXmlDocument(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("DELETE FROM XmlDocuments WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
                return NotFound();

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<XmlDocumentModel>>> SearchXmlDocument(
            [FromQuery] string? name,
            [FromQuery] string? node,
            [FromQuery] string? attribute,
            [FromQuery] string? attributeValue)
        {
            var results = new List<XmlDocumentModel>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand("SELECT Id, Name, XmlContent, CreateData FROM XmlDocuments", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var doc = new XmlDocumentModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                    XmlContent = reader.GetString(2),
                    CreateData = reader.GetDateTime(3)
                };

                var xml = XDocument.Parse(doc.XmlContent);

                if (name != null && doc.Name != name)
                    continue;

                if (node != null && !xml.Descendants(node).Any())
                    continue;

                if (attribute != null && !xml.Descendants().Any(e => e.Attribute(attribute) != null))
                    continue;

                if (attributeValue != null && !xml.Descendants().Any(e => e.Attributes().Any(a => a.Value == attributeValue)))
                    continue;

                results.Add(doc);
            }

            return results;
        }

        [HttpGet("{id}/transform")]
        public async Task<IActionResult> XslTransform(int id)
        {
            var result = await GetXmlDocument(id);
            if (result.Result is NotFoundObjectResult)
                return result.Result;

            var xmlModel = result.Value;
            if (xmlModel == null || string.IsNullOrWhiteSpace(xmlModel.XmlContent))
                return BadRequest("XML content is missing or empty.");

            var xsltPath = Path.Combine("XSL", "XSLT/UniversalTransformation.xsl");
            if (!System.IO.File.Exists(xsltPath))
                return NotFound("XSLT file not found.");

            var transform = new XslCompiledTransform();
            using var xsltReader = XmlReader.Create(xsltPath);
            transform.Load(xsltReader);

            using var xmlReader = XmlReader.Create(new StringReader(xmlModel.XmlContent));
            using var writer = new StringWriter();
            transform.Transform(xmlReader, null, writer);

            return Content(writer.ToString(), "text/html");
        }
    }
}
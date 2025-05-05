using Microsoft.AspNetCore.Mvc;
using bd2proj.Models;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using System.Xml.XPath;

namespace bd2proj.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XmlApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public XmlApiController(AppDbContext context)
        {
            _context = context;
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<XmlDocumentModel>> GetXmlDocument(int id)
        {
            var xmlDocumentModel = await _context.XmlDocuments.FindAsync(id);
            if (xmlDocumentModel == null)
            {
                return NotFound(new { Message = "The requested XML Document was not found in the Database", RequestedId = id});
            }
            return xmlDocumentModel;
        }

        [HttpPost]
        public async Task<ActionResult<XmlDocumentModel>> PostXmlDocument([FromBody] XmlDocumentModel xmlDocumentModel)
        {
            _context.XmlDocuments.Add(xmlDocumentModel);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetXmlDocument), new { id = xmlDocumentModel.Id }, xmlDocumentModel);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<XmlDocumentModel>>> SearchXmlDocument([FromQuery] string? name, [FromQuery] string? node, [FromQuery] string? attribute, [FromQuery] string? attributeValue)
        {
            var allDocs = await _context.XmlDocuments.ToListAsync();

            var lookedFor = allDocs.Where(doc =>
            {
                var xml = XDocument.Parse(doc.XmlContent);
                if (name != null && doc.Name != null && doc.Name != name)
                {
                    return false;
                }

                if (node != null && !xml.Descendants(node).Any())
                {
                    return false;
                }

                if (attribute != null)
                {
                    bool hasAttribute = xml.Descendants()
                        .Any(el => el.Attributes().Any(attr => attr.Name.LocalName == attribute));
                    if (!hasAttribute)
                        return false;
                }

                if (attributeValue != null)
                {
                    bool hasAttributeValue = xml.Descendants()
                        .Any(el => el.Attributes().Any(attr => attr.Value == attributeValue));
                    if (!hasAttributeValue)
                        return false;
                }
                
                return true;
            });

            return lookedFor.ToList();
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<XmlDocumentModel>> PutXmlDocument(int id, [FromBody] XmlUpdateRequest req)
        {
            var doc = await _context.XmlDocuments.FindAsync(id);
            if (doc == null)
            {
                return NotFound(new {Message = "The requested XML Document was not found in the Database", RequestedId = id});
            }
            
            var xml = XDocument.Parse(doc.XmlContent);
            var element = xml.XPathSelectElement(req.XPath);

            if (element == null)
            {
                return NotFound(new { Message = "Element not found" });
            }
            
            element.Value = req.NewValue;
            doc.XmlContent = xml.ToString();
            _context.SaveChanges();

            return Ok("XML updated");
        }
        
        [HttpDelete("{id}")]
        public async Task<ActionResult<XmlDocumentModel>> DeleteXmlDocument(int id)
        {
            var xmlDocument = await _context.XmlDocuments.FindAsync(id);
            if (xmlDocument == null)
            {
                return NotFound();
            }

            _context.XmlDocuments.Remove(xmlDocument);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
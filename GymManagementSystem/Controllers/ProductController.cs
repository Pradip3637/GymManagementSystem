using GymManagement.Domain.Models;
using GymManagementSystem.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _service;

        public ProductController(IProductService service)
        {
            _service = service;
        }

        // GET: api/Product
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _service.GetAllProductsAsync();
            return Ok(products);
        }

        // GET: api/Product/5
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _service.GetProductByIdAsync(id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        // POST: api/Product
        [HttpPost]
        public async Task<IActionResult> Add(Product product)
        {
            var created = await _service.AddProductAsync(product);
            return Ok(created);
        }

        // PUT: api/Product
        [HttpPut]
        public async Task<IActionResult> Update(Product product)
        {
            var updated = await _service.UpdateProductAsync(product);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        // DELETE: api/Product/5
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _service.DeleteProductAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
        public class NotifyDto
        {
            public string Email { get; set; } = "";
        }

        // POST: api/Product/{id}/notify
        [HttpPost("{id:guid}/notify")]
        public async Task<IActionResult> Notify(Guid id, [FromBody] NotifyDto dto)
        {
            var email = dto.Email?.Trim() ?? "";
            if (string.IsNullOrEmpty(email)) return BadRequest("Email is required.");

            var products = await _service.GetAllProductsAsync();
            var product = products.FirstOrDefault(p => p.Id == id);
            if (product == null) return NotFound();

            var list = (product.NotifyEmails ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!list.Contains(email, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(email);
                product.NotifyEmails = string.Join(",", list);
                await _service.UpdateProductAsync(product);
            }
            return Ok();
        }
    }
    
}

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
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteProductAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
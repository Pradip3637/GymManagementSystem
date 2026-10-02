
using GymManagement.DataAcces.Interfaces;
using GymManagement.Domain.Models;
using GymManagementSystem.Business.Interfaces;



namespace GymManagementSystem.Business.Services
{
    namespace GymManagementSystem.Business.Services
    {
        public class ProductService : IProductService
        {
            private readonly IProductRepository _repo;
            public ProductService(IProductRepository repo) => _repo = repo;

            public Task<IEnumerable<Product>> GetAllProductsAsync() => _repo.GetAllAsync();
            public Task<Product?> GetProductByIdAsync(int id) => _repo.GetByIdAsync(id);
            public Task<Product> AddProductAsync(Product product) => _repo.AddAsync(product);
            public Task<Product?> UpdateProductAsync(Product product) => _repo.UpdateAsync(product);
            public Task<bool> DeleteProductAsync(int id) => _repo.DeleteAsync(id);
        }
    }
}

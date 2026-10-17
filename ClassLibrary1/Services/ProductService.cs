using GymManagement.DataAcces.Interfaces;
using GymManagement.Domain.Models;
using GymManagementSystem.Business.Interfaces;

namespace GymManagementSystem.Business.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    private readonly IEmailService _email;

    public ProductService(IProductRepository repo, IEmailService email)
    {
        _repo = repo;
        _email = email;
    }

    public Task<IEnumerable<Product>> GetAllProductsAsync() => _repo.GetAllAsync();
    public Task<Product?> GetProductByIdAsync(Guid id) => _repo.GetByIdAsync(id);
    public Task<Product> AddProductAsync(Product product) => _repo.AddAsync(product);

    public async Task<Product?> UpdateProductAsync(Product product)
    {
        // read the OLD values before saving
        var existing = await _repo.GetByIdAsync(product.Id);
        var oldQty = existing?.Quantity ?? 0;
        var emails = existing?.NotifyEmails;

        // keep the saved emails if the caller did not send any
        if (string.IsNullOrWhiteSpace(product.NotifyEmails))
            product.NotifyEmails = emails;

        var updated = await _repo.UpdateAsync(product);
        if (updated == null) return null;

        // back in stock: 0 -> above 0
        if (oldQty <= 0 && updated.Quantity > 0 && !string.IsNullOrWhiteSpace(emails))
        {
            var sentAny = false;
            var addresses = emails.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(e => e.Trim());

            foreach (var email in addresses)
            {
                try
                {
                    await _email.SendAsync(email, $"{updated.Name} is back in stock!",
                        $"<h3>{updated.Name} is back in stock!</h3><p>Order now before it sells out.</p>");
                    Console.WriteLine($"Email sent to {email}");
                    sentAny = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Email to {email} failed: {ex.Message}");
                }
            }

            if (sentAny)
            {
                updated.NotifyEmails = null;   // so nobody gets it twice
                await _repo.UpdateAsync(updated);
            }
        }

        return updated;
    }

    public Task<bool> DeleteProductAsync(Guid id) => _repo.DeleteAsync(id);
}
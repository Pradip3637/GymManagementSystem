using GymManagement.DataAcces.Data;
using GymManagement.DataAcces.Interfaces;
using GymManagement.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.DataAcces.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly GymDbContext _context;
        public ProductRepository(GymDbContext context) => _context = context;

        public async Task<IEnumerable<Product>> GetAllAsync()
            => await _context.Products.AsNoTracking().ToListAsync();

        public async Task<Product?> GetByIdAsync(Guid id)
            => await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

        public async Task<Product> AddAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task<Product?> UpdateAsync(Product product)
        {
            var existing = await _context.Products.FindAsync(product.Id);
            if (existing == null) return null;

            existing.Name = product.Name;
            existing.Price = product.Price;
            existing.Category = product.Category;
            existing.Location = product.Location;
            existing.Image = product.Image;
            existing.Rating = product.Rating;
            existing.Quantity = product.Quantity;            // new
            existing.NotifyEmails = product.NotifyEmails;    // new

            await _context.SaveChangesAsync();
            return existing;
        }
        public async Task<bool> DeleteAsync(Guid id)
        {
            var existing = await _context.Products.FindAsync(id);
            if (existing == null) return false;
            _context.Products.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

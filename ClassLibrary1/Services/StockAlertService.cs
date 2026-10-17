using GymManagement.Domain.Models;
using Microsoft.Extensions.Options;

namespace GymManagementSystem.Business.Services
{
    public class StockAlertSettings
    {
        public string WebsiteOwnerEmail { get; set; } = "";
        public string DefaultProductOwnerEmail { get; set; } = "";
    }

    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody);
    }

    public interface IStockAlertService
    {
        Task NotifyOutOfStockAsync(Product product);
    }

    public class StockAlertService : IStockAlertService
    {
        private readonly IEmailService _email;
        private readonly StockAlertSettings _settings;

        public StockAlertService(IEmailService email, IOptions<StockAlertSettings> settings)
        {
            _email = email;
            _settings = settings.Value;
        }

        public async Task NotifyOutOfStockAsync(Product product)
        {
            var subject = $"Out of stock: {product.Name}";
            var htmlBody =
                $"<h3>Out of stock</h3>" +
                $"<p>The product <b>{product.Name}</b> is now out of stock.</p>" +
                $"<p>Id: {product.Id}<br>" +
                $"Category: {product.Category}<br>" +
                $"Location: {product.Location}<br>" +
                $"Quantity: 0</p>";

            var productOwner = _settings.DefaultProductOwnerEmail;

            try
            {
                await _email.SendAsync(productOwner, subject, htmlBody);
                await _email.SendAsync(_settings.WebsiteOwnerEmail, subject, htmlBody);
            }
            catch
            {
                // An email problem must never stop the customer's order.
            }
        }
    }
}
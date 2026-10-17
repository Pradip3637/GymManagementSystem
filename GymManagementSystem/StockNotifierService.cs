using GymManagementSystem.Business.Interfaces;
using GymManagementSystem.Business.Services;

public class StockNotifierService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    public StockNotifierService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"Stock check at {DateTime.Now:T}");
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var products = await productService.GetAllProductsAsync();
                Console.WriteLine($"Products loaded: {products.Count()}, with emails and stock: {products.Count(p => p.Quantity > 0 && !string.IsNullOrWhiteSpace(p.NotifyEmails))}");
                var inStock = products
                    .Where(p => p.Quantity > 0 && !string.IsNullOrWhiteSpace(p.NotifyEmails))
                    .ToList();

                foreach (var p in inStock)
                {
                    var sentAny = false;
                    var addresses = p.NotifyEmails!
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var to in addresses)
                    {
                        try
                        {
                            await email.SendAsync(to, $"{p.Name} is back in stock!",
                                $"<h3>{p.Name} is back in stock!</h3><p>Order now before it sells out.</p>");
                            Console.WriteLine($"Email sent to {to}");
                            sentAny = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Email to {to} failed: {ex.Message}");
                        }
                    }

                    if (sentAny)
                    {
                        p.NotifyEmails = null;                 // so nobody gets it twice
                        await productService.UpdateProductAsync(p);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Stock check error: " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
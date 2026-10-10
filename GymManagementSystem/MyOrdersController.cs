using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

// New controller: "My orders" (order history with details).
// Put this file in your API project's Controllers folder.
// It reads from your Orders, OrderItems, Payment and Address tables.
// Needs the connection string "DefaultConnection" in appsettings.json.

[ApiController]
[Route("api/[controller]")]
public class MyOrdersController : ControllerBase
{
    private readonly string _connectionString;

    public MyOrdersController(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");
    }

    public class OrderLineDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class OrderFullDto
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string OrderStatus { get; set; } = "";
        public decimal SubTotal { get; set; }
        public decimal ShippingCharge { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string AddressLine2 { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Country { get; set; } = "";
        public string ZipCode { get; set; } = "";
        public List<OrderLineDto> Items { get; set; } = new();
    }

    private static string Str(SqlDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    // GET api/myorders/user/1
    // Every order of the user (newest first) with its items, address and payment.
    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<List<OrderFullDto>>> GetByUser(int userId)
    {
        var orders = new List<OrderFullDto>();

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        // 1) The orders, with payment and delivery address
        await using (var cmd = new SqlCommand(
            @"SELECT o.OrderId, o.OrderDate, o.OrderStatus, o.SubTotal, o.ShippingCharge, o.TaxAmount, o.TotalAmount,
                     p.PaymentMethod, p.PaymentStatus,
                     a.FullName, a.PhoneNumber, a.AddressLine1, a.AddressLine2, a.City, a.State, a.Country, a.ZipCode
              FROM Orders o
              LEFT JOIN Payment p ON p.OrderId = o.OrderId
              LEFT JOIN Address a ON a.AddressId = o.AddressId
              WHERE o.UserId = @UserId
              ORDER BY o.OrderId DESC", con))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                orders.Add(new OrderFullDto
                {
                    OrderId = r.GetInt32(0),
                    OrderDate = r.GetDateTime(1),
                    OrderStatus = Str(r, 2),
                    SubTotal = r.GetDecimal(3),
                    ShippingCharge = r.IsDBNull(4) ? 0 : r.GetDecimal(4),
                    TaxAmount = r.IsDBNull(5) ? 0 : r.GetDecimal(5),
                    TotalAmount = r.GetDecimal(6),
                    PaymentMethod = Str(r, 7),
                    PaymentStatus = Str(r, 8),
                    FullName = Str(r, 9),
                    PhoneNumber = Str(r, 10),
                    AddressLine1 = Str(r, 11),
                    AddressLine2 = Str(r, 12),
                    City = Str(r, 13),
                    State = Str(r, 14),
                    Country = Str(r, 15),
                    ZipCode = Str(r, 16)
                });
            }
        }

        // 2) The items of those orders
        if (orders.Count > 0)
        {
            var byId = orders.ToDictionary(o => o.OrderId);

            await using var cmd = new SqlCommand(
                @"SELECT oi.OrderId, oi.ProductId, oi.Quantity, oi.UnitPrice, oi.TotalPrice
                  FROM OrderItems oi
                  JOIN Orders o ON o.OrderId = oi.OrderId
                  WHERE o.UserId = @UserId
                  ORDER BY oi.OrderItemId", con);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (byId.TryGetValue(r.GetInt32(0), out var order))
                {
                    order.Items.Add(new OrderLineDto
                    {
                        ProductId = r.GetInt32(1),
                        Quantity = r.GetInt32(2),
                        UnitPrice = r.GetDecimal(3),
                        TotalPrice = r.GetDecimal(4)
                    });
                }
            }
        }

        return Ok(orders);
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

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
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class OrderFullDto
    {
        public Guid OrderId { get; set; }
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

    private static string S(SqlDataReader r, int i) =>
        r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)) ?? "";

    // GET api/myorders/user/{userId}  -> all orders for the user, newest first
    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<List<OrderFullDto>>> GetByUser(Guid userId)
    {
        var orders = new List<OrderFullDto>();
        var byId = new Dictionary<Guid, OrderFullDto>();

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        // 1) orders + address + payment
        await using (var cmd = new SqlCommand(
            @"SELECT o.OrderId, o.OrderDate, o.OrderStatus,
                     o.SubTotal, o.ShippingCharge, o.TaxAmount, o.TotalAmount,
                     p.PaymentMethod, p.PaymentStatus,
                     a.FullName, a.PhoneNumber, a.AddressLine1, a.AddressLine2,
                     a.City, a.State, a.Country, a.ZipCode
              FROM Orders o
              LEFT JOIN Payment p ON p.OrderId = o.OrderId
              LEFT JOIN Address a ON a.AddressId = o.AddressId
              WHERE o.UserId = @UserId
              ORDER BY o.OrderDate DESC", con))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var o = new OrderFullDto
                {
                    OrderId = r.GetGuid(0),
                    OrderDate = r.GetDateTime(1),
                    OrderStatus = S(r, 2),
                    SubTotal = r.GetDecimal(3),
                    ShippingCharge = r.GetDecimal(4),
                    TaxAmount = r.GetDecimal(5),
                    TotalAmount = r.GetDecimal(6),
                    PaymentMethod = S(r, 7),
                    PaymentStatus = S(r, 8),
                    FullName = S(r, 9),
                    PhoneNumber = S(r, 10),
                    AddressLine1 = S(r, 11),
                    AddressLine2 = S(r, 12),
                    City = S(r, 13),
                    State = S(r, 14),
                    Country = S(r, 15),
                    ZipCode = S(r, 16)
                };
                orders.Add(o);
                byId[o.OrderId] = o;
            }
        }

        // 2) items for all of those orders
        await using (var cmd = new SqlCommand(
            @"SELECT i.OrderId, i.ProductId, i.Quantity, i.UnitPrice, i.TotalPrice
              FROM OrderItems i
              JOIN Orders o ON o.OrderId = i.OrderId
              WHERE o.UserId = @UserId", con))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (byId.TryGetValue(r.GetGuid(0), out var o))
                {
                    o.Items.Add(new OrderLineDto
                    {
                        ProductId = r.GetGuid(1),
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
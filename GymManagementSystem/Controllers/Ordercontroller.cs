using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

// Put this in your ASP.NET Core API project (Controllers folder).
// Saves into your Orders, OrderItems and Payment tables in one transaction.
// Needs a connection string named "DefaultConnection" in appsettings.json.

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IEmailService _email;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IConfiguration config, IEmailService email, ILogger<OrderController> logger)
    {
        _email = email;
        _logger = logger;
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");
    }

    public class OrderItemDto
    {
        public int ProductId { get; set; }
        [Range(1, 1000)] public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class PlaceOrderDto
    {
        public int UserId { get; set; }
        public int AddressId { get; set; }
        public decimal SubTotal { get; set; }
        public decimal ShippingCharge { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        [Required, MinLength(1)] public List<OrderItemDto> Items { get; set; } = new();
        [Required] public string PaymentMethod { get; set; } = "";
        public string? TransactionId { get; set; }
    }

    // POST api/order
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] PlaceOrderDto dto)
    {
        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();
        await using var tx = con.BeginTransaction();

        try
        {
            // 1) Orders
            const string orderSql = @"INSERT INTO Orders
                (UserId, AddressId, SubTotal, ShippingCharge, TaxAmount, TotalAmount, OrderStatus)
                 VALUES (@UserId, @AddressId, @SubTotal, @ShippingCharge, @TaxAmount, @TotalAmount,'Order Placed'););
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int orderId;
            await using (var cmd = new SqlCommand(orderSql, con, tx))
            {
                cmd.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd.Parameters.AddWithValue("@AddressId", dto.AddressId);
                cmd.Parameters.AddWithValue("@SubTotal", dto.SubTotal);
                cmd.Parameters.AddWithValue("@ShippingCharge", dto.ShippingCharge);
                cmd.Parameters.AddWithValue("@TaxAmount", dto.TaxAmount);
                cmd.Parameters.AddWithValue("@TotalAmount", dto.TotalAmount);
                orderId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 1b) First tracking step
            await ExecAsync(con, tx,
                "INSERT INTO OrderStatusHistory (OrderId, Status, Note) VALUES (@Id, 'Order Placed', 'We have received your order.')",
                ("@Id", orderId));

            // 2) OrderItems (TotalPrice is a computed column, so it is not inserted)
            foreach (var item in dto.Items)
            {
                await using var cmd = new SqlCommand(
                    @"INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice)
                      VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)", con, tx);
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                cmd.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                await cmd.ExecuteNonQueryAsync();
            }

            // 3) Payment (stays 'Pending' until you connect a real payment gateway)
            await using (var cmd = new SqlCommand(
                @"INSERT INTO Payment (OrderId, PaymentMethod, TransactionId, Amount, PaymentStatus)
                  VALUES (@OrderId, @PaymentMethod, @TransactionId, @Amount, 'Pending')", con, tx))
            {
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                cmd.Parameters.AddWithValue("@PaymentMethod", dto.PaymentMethod);
                cmd.Parameters.AddWithValue("@TransactionId", (object?)dto.TransactionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Amount", dto.TotalAmount);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            // Email problems must never break a saved order
            await SendConfirmationAsync(con, orderId, dto);

            return Ok(new { orderId });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
    // =====================================================================
    // PASTE THIS INSIDE your OrderController class (NOT at the end of the file).
    // Put it directly ABOVE this line in your file:
    //     // GET api/order/test-email?to=you@gmail.com ...
    // Do not paste these two comment lines.
    // =====================================================================

    // ================= ORDER TRACKING =================

    // The order moves forward through this list (it can never go back)
    private static readonly string[] Statuses =
    {
        "Order Placed", "Payment Confirmed", "Order Confirmed", "Processing", "Packed",
        "Ready to Ship", "Picked Up", "In Transit", "Out for Delivery", "Delivered"
    };

    // These steps also send an email to the customer
    private static readonly string[] EmailStatuses =
        { "Order Confirmed", "Picked Up", "Out for Delivery", "Delivered" };

    public class OrderSummaryDto
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = "";
    }

    public class HistoryDto
    {
        public string Status { get; set; } = "";
        public string? Note { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class TrackingDto : OrderSummaryDto
    {
        public List<HistoryDto> History { get; set; } = new();
    }

    public class UpdateStatusDto
    {
        [Required] public string Status { get; set; } = "";
        public string? Note { get; set; }
    }

    // GET api/order/user/1   -> the user's orders (newest first)
    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<List<OrderSummaryDto>>> GetByUser(int userId)
    {
        var list = new List<OrderSummaryDto>();

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        await using var cmd = new SqlCommand(
            @"SELECT OrderId, OrderDate, TotalAmount, OrderStatus
              FROM Orders WHERE UserId = @UserId ORDER BY OrderId DESC", con);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new OrderSummaryDto
            {
                OrderId = r.GetInt32(0),
                OrderDate = r.GetDateTime(1),
                TotalAmount = r.GetDecimal(2),
                OrderStatus = r.IsDBNull(3) ? "" : r.GetString(3)
            });
        }
        return Ok(list);
    }

    // GET api/order/5/tracking?userId=1   -> one order with its status history
    [HttpGet("{id:int}/tracking")]
    public async Task<ActionResult<TrackingDto>> GetTracking(int id, [FromQuery] int? userId)
    {
        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        TrackingDto? result = null;

        await using (var cmd = new SqlCommand(
            @"SELECT OrderId, OrderDate, TotalAmount, OrderStatus FROM Orders
              WHERE OrderId = @Id AND (@UserId IS NULL OR UserId = @UserId)", con))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                result = new TrackingDto
                {
                    OrderId = r.GetInt32(0),
                    OrderDate = r.GetDateTime(1),
                    TotalAmount = r.GetDecimal(2),
                    OrderStatus = r.IsDBNull(3) ? "" : r.GetString(3)
                };
            }
        }

        if (result == null) return NotFound();

        await using (var cmd = new SqlCommand(
            "SELECT Status, Note, CreatedDate FROM OrderStatusHistory WHERE OrderId = @Id ORDER BY HistoryId", con))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.History.Add(new HistoryDto
                {
                    Status = r.GetString(0),
                    Note = r.IsDBNull(1) ? null : r.GetString(1),
                    CreatedDate = r.GetDateTime(2)
                });
            }
        }

        return Ok(result);
    }

    // PUT api/order/5/status      body: { "status": "Packed", "note": "optional" }
    // Used by the shop owner / admin to move the order to its next step.
    // NOTE: add login/authorization before you publish the site.
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
    {
        var newIndex = Array.FindIndex(Statuses,
            s => s.Equals(dto.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (newIndex < 0)
            return BadRequest("Status must be one of: " + string.Join(", ", Statuses));
        var newStatus = Statuses[newIndex];

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        string? current;
        await using (var cmd = new SqlCommand("SELECT OrderStatus FROM Orders WHERE OrderId = @Id", con))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            var value = await cmd.ExecuteScalarAsync();
            if (value == null) return NotFound();
            current = value == DBNull.Value ? null : (string)value;
        }

        var currentIndex = Array.FindIndex(Statuses,
            s => s.Equals(current, StringComparison.OrdinalIgnoreCase));
        if (newIndex <= currentIndex)
            return BadRequest($"The order is already at '{current}'. A status can only move forward.");

        await using var tx = con.BeginTransaction();
        try
        {
            await ExecAsync(con, tx, "UPDATE Orders SET OrderStatus = @S WHERE OrderId = @Id",
                ("@S", newStatus), ("@Id", id));

            await ExecAsync(con, tx,
                "INSERT INTO OrderStatusHistory (OrderId, Status, Note) VALUES (@Id, @S, @N)",
                ("@Id", id), ("@S", newStatus), ("@N", dto.Note));

            // Payment becomes "Paid" when payment is confirmed (COD: when delivered)
            await ExecAsync(con, tx,
                @"UPDATE Payment SET PaymentStatus = 'Paid'
                  WHERE OrderId = @Id AND PaymentStatus <> 'Paid'
                    AND (@S = 'Payment Confirmed' OR (@S = 'Delivered' AND PaymentMethod = 'COD'))",
                ("@Id", id), ("@S", newStatus));

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        if (EmailStatuses.Contains(newStatus))
            await SendStatusEmailAsync(id, newStatus);

        return Ok(new { orderId = id, status = newStatus });
    }

    private static async Task ExecAsync(SqlConnection con, SqlTransaction tx, string sql,
        params (string Name, object? Value)[] args)
    {
        await using var cmd = new SqlCommand(sql, con, tx);
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SendStatusEmailAsync(int orderId, string status)
    {
        try
        {
            string name = "";
            string? email = null;

            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            await using (var cmd = new SqlCommand(
                @"SELECT a.FullName, a.Email FROM Orders o
                  JOIN Address a ON a.AddressId = o.AddressId WHERE o.OrderId = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", orderId);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    name = r.GetString(0);
                    email = r.IsDBNull(1) ? null : r.GetString(1);
                }
            }
            if (string.IsNullOrWhiteSpace(email)) return;

            var text = status switch
            {
                "Order Confirmed" => "Your order has been confirmed and will be prepared shortly.",
                "Picked Up" => "Your package has been picked up by our delivery partner.",
                "Out for Delivery" => "Your order is out for delivery and will reach you today.",
                "Delivered" => "Your order has been delivered. Enjoy!",
                _ => "Your order status was updated."
            };

            var html = $@"
                <div style='font-family:Arial,sans-serif;max-width:520px;margin:auto'>
                  <h2 style='color:#18392d'>Hi {System.Net.WebUtility.HtmlEncode(name)},</h2>
                  <p>{text}</p>
                  <p><b>Order #{orderId}</b> &mdash; {status}</p>
                  <p style='color:#666;font-size:12px'>Coco Republic &ndash; goodness from the inside out.</p>
                </div>";

            await _email.SendAsync(email, $"Order #{orderId}: {status}", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send status email for order {OrderId}", orderId);
        }
    }

    // GET api/order/test-email?to=you@gmail.com  (can be opened directly in the browser)
    // Sends a test message so you can check the SMTP settings from Swagger.
    // Remove this method before you publish the site (anyone could use it to send mail).
    [HttpGet("test-email")]
    public async Task<IActionResult> TestEmail([FromQuery] string to)
    {
        try
        {
            await _email.SendAsync(to, "Test email from Coco Republic",
                "<p>Your SMTP settings work. 🎉</p>");
            return Ok("Email sent to " + to);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private async Task SendConfirmationAsync(SqlConnection con, int orderId, PlaceOrderDto dto)
    {
        try
        {
            string name = "";
            string? email = null;

            await using (var cmd = new SqlCommand(
                "SELECT FullName, Email FROM Address WHERE AddressId = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", dto.AddressId);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    name = r.GetString(0);
                    email = r.IsDBNull(1) ? null : r.GetString(1);
                }
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning(
                    "No email saved for address {AddressId}, so no confirmation email was sent for order {OrderId}.",
                    dto.AddressId, orderId);
                return;
            }
            var itemCount = dto.Items.Sum(i => i.Quantity);

            var html = $@"
<div style='margin:0; padding:30px 15px; background:#f4f7f5; font-family:Arial,Helvetica,sans-serif;'>

    <div style='max-width:560px; margin:0 auto; background:#ffffff; 
                border-radius:16px; overflow:hidden; 
                box-shadow:0 4px 18px rgba(0,0,0,0.08);'>

        <!-- Header -->
        <div style='background:#18392d; padding:30px 25px; text-align:center;'>

            <div style='font-size:28px; font-weight:bold; color:#ffffff;'>
                Coco Republic
            </div>

            <div style='margin-top:7px; font-size:13px; 
                        color:#d9e8df; letter-spacing:0.5px;'>
                Goodness from the inside out.
            </div>

        </div>


        <!-- Main Content -->
        <div style='padding:35px 30px;'>

            <div style='text-align:center; margin-bottom:25px;'>

                <div style='display:inline-block; width:52px; height:52px;
                            line-height:52px; border-radius:50%;
                            background:#e8f5ed; color:#23804d;
                            font-size:26px;'>
                    ✓
                </div>

                <h2 style='margin:18px 0 8px; color:#18392d; 
                           font-size:24px;'>
                    Thank you for your order!
                </h2>

                <p style='margin:0; color:#555; font-size:15px;'>
                    Hi <b>{System.Net.WebUtility.HtmlEncode(name)}</b>,
                    your order has been received successfully.
                </p>

            </div>


            <!-- Delivery Message -->
            <div style='background:#f4faf6; border-left:4px solid #2e7d52;
                        padding:15px 16px; border-radius:6px;
                        margin-bottom:25px;'>

                <p style='margin:0; color:#4d5b53; font-size:14px; 
                          line-height:1.6;'>
                    We have received your order and will confirm delivery
                    within one business day.
                </p>

            </div>


            <!-- Order Details Title -->
            <div style='font-size:16px; font-weight:bold; 
                        color:#18392d; margin-bottom:10px;'>
                Order Details
            </div>


            <!-- Order Details Table -->
            <table style='width:100%; border-collapse:separate;
                          border-spacing:0; border:1px solid #e2e8e4;
                          border-radius:10px; overflow:hidden;'>

                <tr>
                    <td style='padding:14px 15px; background:#fafcfb;
                               border-bottom:1px solid #e2e8e4;
                               color:#68736d; font-size:14px;'>
                        Order number
                    </td>

                    <td style='padding:14px 15px; background:#fafcfb;
                               border-bottom:1px solid #e2e8e4;
                               text-align:right; color:#18392d;
                               font-weight:bold; font-size:14px;'>
                        #{orderId}
                    </td>
                </tr>


                <tr>
                    <td style='padding:14px 15px;
                               border-bottom:1px solid #e2e8e4;
                               color:#68736d; font-size:14px;'>
                        Items
                    </td>

                    <td style='padding:14px 15px;
                               border-bottom:1px solid #e2e8e4;
                               text-align:right; color:#333;
                               font-size:14px;'>
                        {itemCount}
                    </td>
                </tr>


                <tr>
                    <td style='padding:14px 15px;
                               border-bottom:1px solid #e2e8e4;
                               color:#68736d; font-size:14px;'>
                        Payment
                    </td>

                    <td style='padding:14px 15px;
                               border-bottom:1px solid #e2e8e4;
                               text-align:right; color:#333;
                               font-size:14px;'>
                        {System.Net.WebUtility.HtmlEncode(dto.PaymentMethod)}
                    </td>
                </tr>


                <tr>
                    <td style='padding:16px 15px; background:#f4faf6;
                               color:#18392d; font-size:15px;
                               font-weight:bold;'>
                        Total
                    </td>

                    <td style='padding:16px 15px; background:#f4faf6;
                               text-align:right; color:#23804d;
                               font-size:18px; font-weight:bold;'>
                        ₹{dto.TotalAmount:N0}
                    </td>
                </tr>

            </table>


            <!-- Footer Message -->
            <div style='text-align:center; margin-top:28px;'>

                <p style='margin:0; color:#555; font-size:14px;'>
                    Thank you for choosing
                    <b style='color:#18392d;'>Coco Republic</b>.
                </p>

            </div>

        </div>


        <!-- Footer -->
        <div style='background:#f7f9f8; border-top:1px solid #edf0ee;
                    padding:20px; text-align:center;'>

            <p style='margin:0; color:#89928d; font-size:12px;'>
                Coco Republic – goodness from the inside out.
            </p>

            <p style='margin:7px 0 0; color:#a1aaa5; font-size:11px;'>
                This is an automated order confirmation email.
            </p>

        </div>

    </div>

</div>";

            await _email.SendAsync(email, $"Your Coco Republic order #{orderId} is confirmed", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send confirmation email for order {OrderId}", orderId);
        }
    }
}
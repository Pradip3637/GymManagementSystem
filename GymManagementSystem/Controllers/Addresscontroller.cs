using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

// Put this in your ASP.NET Core API project (Controllers folder).
// Needs a connection string named "DefaultConnection" in appsettings.json.
// If you use Entity Framework or Dapper, keep the routes and JSON shape the same
// and swap the SQL for your own data access.

[ApiController]
[Route("api/[controller]")]
public class AddressController : ControllerBase
{
    private readonly string _connectionString;

    public AddressController(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");
    }

    public class AddressDto
    {
        public int AddressId { get; set; }
        public int UserId { get; set; }
        [Required, StringLength(100)] public string FullName { get; set; } = "";
        [Required, StringLength(15)] public string PhoneNumber { get; set; } = "";
        [Required, StringLength(200)] public string AddressLine1 { get; set; } = "";
        [StringLength(200)] public string? AddressLine2 { get; set; }
        [Required, StringLength(100)] public string City { get; set; } = "";
        [Required, StringLength(100)] public string State { get; set; } = "";
        [Required, StringLength(100)] public string Country { get; set; } = "";
        [Required, StringLength(20)] public string ZipCode { get; set; } = "";
        [EmailAddress, StringLength(150)] public string? Email { get; set; }
        public bool IsDefault { get; set; }
    }

    // GET api/address/user/1
    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<List<AddressDto>>> GetByUser(int userId)
    {
        var list = new List<AddressDto>();

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        const string sql = @"SELECT AddressId, UserId, FullName, PhoneNumber, AddressLine1, AddressLine2,
                                    City, State, Country, ZipCode, IsDefault, Email
                             FROM Address
                             WHERE UserId = @UserId AND IsDeleted = 0
                             ORDER BY IsDefault DESC, AddressId DESC";

        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new AddressDto
            {
                AddressId = r.GetInt32(0),
                UserId = r.GetInt32(1),
                FullName = r.GetString(2),
                PhoneNumber = r.GetString(3),
                AddressLine1 = r.GetString(4),
                AddressLine2 = r.IsDBNull(5) ? null : r.GetString(5),
                City = r.GetString(6),
                State = r.GetString(7),
                Country = r.GetString(8),
                ZipCode = r.GetString(9),
                IsDefault = !r.IsDBNull(10) && r.GetBoolean(10),
                Email = r.IsDBNull(11) ? null : r.GetString(11)
            });
        }

        return Ok(list);
    }

    // DELETE api/address/5?userId=1
    // Soft delete: the row stays in SQL (old orders still point to it) but is hidden.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int userId)
    {
        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        await using var cmd = new SqlCommand(
            @"UPDATE Address SET IsDeleted = 1, IsDefault = 0
              WHERE AddressId = @Id AND UserId = @UserId AND IsDeleted = 0", con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0 ? NoContent() : NotFound();
    }

    // POST api/address
    [HttpPost]
    public async Task<ActionResult<AddressDto>> Create([FromBody] AddressDto dto)
    {
        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();
        await using var tx = con.BeginTransaction();

        try
        {
            if (dto.IsDefault)
            {
                await using var clear = new SqlCommand(
                    "UPDATE Address SET IsDefault = 0 WHERE UserId = @UserId", con, tx);
                clear.Parameters.AddWithValue("@UserId", dto.UserId);
                await clear.ExecuteNonQueryAsync();
            }

            const string sql = @"INSERT INTO Address
                (UserId, FullName, PhoneNumber, AddressLine1, AddressLine2, City, State, Country, ZipCode, IsDefault, Email)
                VALUES
                (@UserId, @FullName, @PhoneNumber, @AddressLine1, @AddressLine2, @City, @State, @Country, @ZipCode, @IsDefault, @Email);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            await using var cmd = new SqlCommand(sql, con, tx);
            cmd.Parameters.AddWithValue("@UserId", dto.UserId);
            cmd.Parameters.AddWithValue("@FullName", dto.FullName);
            cmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);
            cmd.Parameters.AddWithValue("@AddressLine1", dto.AddressLine1);
            cmd.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City", dto.City);
            cmd.Parameters.AddWithValue("@State", dto.State);
            cmd.Parameters.AddWithValue("@Country", dto.Country);
            cmd.Parameters.AddWithValue("@ZipCode", dto.ZipCode);
            cmd.Parameters.AddWithValue("@Email", (object?)dto.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsDefault", dto.IsDefault);

            dto.AddressId = (int)(await cmd.ExecuteScalarAsync())!;
            await tx.CommitAsync();
            return Ok(dto);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
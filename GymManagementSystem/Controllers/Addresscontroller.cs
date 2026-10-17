using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

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
        public Guid AddressId { get; set; }
        public Guid UserId { get; set; }
        [Required, StringLength(100)] public string FullName { get; set; } = "";
        [Required, StringLength(15)] public string PhoneNumber { get; set; } = "";
        [Required, StringLength(200)] public string AddressLine1 { get; set; } = "";
        [StringLength(200)] public string? AddressLine2 { get; set; }
        [Required, StringLength(50)] public string City { get; set; } = "";
        [Required, StringLength(50)] public string State { get; set; } = "";
        [Required, StringLength(50)] public string Country { get; set; } = "";
        [Required, StringLength(10)] public string ZipCode { get; set; } = "";
        [EmailAddress, StringLength(100)] public string? Email { get; set; }
        public bool IsDefault { get; set; }
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<List<AddressDto>>> GetByUser(Guid userId)
    {
        var list = new List<AddressDto>();
        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        const string sql = @"SELECT AddressId, UserId, FullName, PhoneNumber, AddressLine1, AddressLine2,
                                    City, State, Country, ZipCode, IsDefault, Email
                             FROM Address
                             WHERE UserId = @UserId AND IsDeleted = 0
                             ORDER BY IsDefault DESC, CreatedDate DESC";

        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new AddressDto
            {
                AddressId = r.GetGuid(0),
                UserId = r.GetGuid(1),
                FullName = r.GetString(2),
                PhoneNumber = r.IsDBNull(3) ? "" : r.GetString(3),
                AddressLine1 = r.GetString(4),
                AddressLine2 = r.IsDBNull(5) ? null : r.GetString(5),
                City = r.IsDBNull(6) ? "" : r.GetString(6),
                State = r.IsDBNull(7) ? "" : r.GetString(7),
                Country = r.IsDBNull(8) ? "" : r.GetString(8),
                ZipCode = r.IsDBNull(9) ? "" : r.GetString(9),
                IsDefault = !r.IsDBNull(10) && r.GetBoolean(10),
                Email = r.IsDBNull(11) ? null : r.GetString(11)
            });
        }
        return Ok(list);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid userId)
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

            dto.AddressId = Guid.NewGuid();

            const string sql = @"INSERT INTO Address
                (AddressId, UserId, FullName, PhoneNumber, AddressLine1, AddressLine2, City, State, Country, ZipCode, IsDefault, Email)
                VALUES
                (@AddressId, @UserId, @FullName, @PhoneNumber, @AddressLine1, @AddressLine2, @City, @State, @Country, @ZipCode, @IsDefault, @Email);";

            await using var cmd = new SqlCommand(sql, con, tx);
            cmd.Parameters.AddWithValue("@AddressId", dto.AddressId);
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

            await cmd.ExecuteNonQueryAsync();
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
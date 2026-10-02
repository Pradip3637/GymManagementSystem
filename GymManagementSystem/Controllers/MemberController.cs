using GymManagementSystem.API.Models;
using GymManagementSystem.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/Member_Table")]
    public class MemberController : ControllerBase
    {
        private readonly IMember_Tableervice _Member_Tableervice;

        public MemberController(IMember_Tableervice Member_Tableervice)
        {
            _Member_Tableervice = Member_Tableervice;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var Member_Table = await _Member_Tableervice.GetAllMember_TableAsync();
            return Ok(Member_Table);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var member = await _Member_Tableervice.GetMemberByIdAsync(id);
            if (member == null) return NotFound();
            return Ok(member);
        }

        [HttpPost]
        public async Task<IActionResult> AddMemberAsync([FromBody] Member member)
        {
            var created = await _Member_Tableervice.AddMemberAsync(member);
            return CreatedAtAction(nameof(Index), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMemberAsync(int id, [FromBody] Member member)
        {
            if (id != member.Id) return BadRequest("ID mismatch");

            var existing = await _Member_Tableervice.GetMemberByIdAsync(id);
            if (existing == null) return NotFound();

            var updated = await _Member_Tableervice.UpdateMemberAsync(member);
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMemberAsync(int id)
        {
            var deleted = await _Member_Tableervice.DeleteMemberAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
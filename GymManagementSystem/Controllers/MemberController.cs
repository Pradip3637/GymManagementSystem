using GymManagementSystem.API.Models;
using GymManagementSystem.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/Members")]
    public class MemberController : ControllerBase
    {
        private readonly IMemberService _memberService;

        public MemberController(IMemberService memberService)
        {
            _memberService = memberService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var members = await _memberService.GetAllMembersAsync();
            return Ok(members);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var member = await _memberService.GetMemberByIdAsync(id);
            if (member == null) return NotFound();
            return Ok(member);
        }

        [HttpPost]
        public async Task<IActionResult> AddMemberAsync([FromBody] Member member)
        {
            var created = await _memberService.AddMemberAsync(member);
            return CreatedAtAction(nameof(Index), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMemberAsync(int id, [FromBody] Member member)
        {
            if (id != member.Id) return BadRequest("ID mismatch");

            var existing = await _memberService.GetMemberByIdAsync(id);
            if (existing == null) return NotFound();

            var updated = await _memberService.UpdateMemberAsync(member);
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMemberAsync(int id)
        {
            var deleted = await _memberService.DeleteMemberAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
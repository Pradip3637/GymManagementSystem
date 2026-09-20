using System.Collections.Generic;
using System.Threading.Tasks;
using GymManagementSystem.API.Models;

namespace GymManagementSystem.Business.Interfaces
{
    public interface IMemberService
    {
        Task<IEnumerable<Member>> GetAllMembersAsync();
        Task<Member> GetMemberByIdAsync(int id);
        Task<Member> AddMemberAsync(Member member);
        Task<Member> UpdateMemberAsync(Member member);
        Task<bool> DeleteMemberAsync(int id);
    }
}
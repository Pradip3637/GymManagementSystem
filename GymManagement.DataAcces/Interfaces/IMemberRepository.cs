using GymManagementSystem.API.Models;

namespace GymManagement.DataAcces.Repositories
{
    public interface IMemberRepository
    {
        Task<IEnumerable<Member>> GetAllMember_TableAsync();
        Task<Member> GetMemberByIdAsync(int id);
        Task<Member> AddMemberAsync(Member member);
        Task<Member> UpdateMemberAsync(Member member);
        Task<bool> DeleteMemberAsync(int id);
    }
}
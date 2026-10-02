using GymManagement.DataAcces.Data;
using GymManagementSystem.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.DataAcces.Repositories
{
    public class MemberRepository : IMemberRepository
    {
        public GymDbContext _dbContext;

        public MemberRepository(GymDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<IEnumerable<Member>> GetAllMember_TableAsync()
        {
            var list = await _dbContext.Member_Table.ToListAsync();
            return list;
        }

        public async Task<Member> GetMemberByIdAsync(int id)
        {
            return  _dbContext.Member_Table.Find(id);
            //return _dbContext.Member_Table.Where(x => x.Id == id).FirstOrDefault();
        }

        public async Task<Member> AddMemberAsync(Member? member)
        {
            _dbContext.Add(member);
            _dbContext.SaveChanges();
            return member;
        }
        public async Task<Member> UpdateMemberAsync(Member member)
        {
            _dbContext.Member_Table.Update(member);
            _dbContext.SaveChanges();
            return member;
        }

        public async Task<bool> DeleteMemberAsync(int id)
        {
            var member = _dbContext.Member_Table.Find(id);
            if (member == null) return false;

            _dbContext.Member_Table.Remove(member);
            _dbContext.SaveChanges();
            return true;
        }
    }
}

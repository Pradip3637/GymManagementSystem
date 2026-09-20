using GymManagement.DataAcces.Data;
using GymManagementSystem.API.Models;
using GymManagementSystem.Business.Interfaces;

namespace GymManagement.DataAcces.Repositories
{
    public class MemberRepository : IMemberRepository
    {
        public GymDbContext _dbContext;

        public MemberRepository(GymDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<IEnumerable<Member>> GetAllMembersAsync()
        {
            var list = _dbContext.Members.ToList();
            return list;
        }

        public async Task<Member> GetMemberByIdAsync(int id)
        {
            return  _dbContext.Members.Find(id);
            //return _dbContext.Members.Where(x => x.Id == id).FirstOrDefault();
        }

        public async Task<Member> AddMemberAsync(Member? member)
        {
            _dbContext.Add(member);
            _dbContext.SaveChanges();
            return member;
        }
        public async Task<Member> UpdateMemberAsync(Member member)
        {
            _dbContext.Members.Update(member);
            _dbContext.SaveChanges();
            return member;
        }

        public async Task<bool> DeleteMemberAsync(int id)
        {
            var member = _dbContext.Members.Find(id);
            if (member == null) return false;

            _dbContext.Members.Remove(member);
            _dbContext.SaveChanges();
            return true;
        }
    }
}

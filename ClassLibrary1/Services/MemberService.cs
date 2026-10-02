using GymManagement.DataAcces.Repositories;
using GymManagementSystem.API.Models;
using GymManagementSystem.Business.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymManagementSystem.Business.Services
{
    public class Member_Tableervice1 : IMember_Tableervice
    {
        private readonly IMemberRepository _repository;
        public Member_Tableervice1(IMemberRepository repository)
        {
            _repository = repository;
        }
        public Task<IEnumerable<Member>> GetAllMember_TableAsync()
        {
            return _repository.GetAllMember_TableAsync();
        }

        public async Task<Member> GetMemberByIdAsync(int id)
        {
            return await _repository.GetMemberByIdAsync(id);
        }
        public async Task<Member> AddMemberAsync(Member member)
        {
            return await _repository.AddMemberAsync(member);
        }
        public async Task<Member> UpdateMemberAsync(Member member)
        {
            return await _repository.UpdateMemberAsync(member);
        }

        public async Task<bool> DeleteMemberAsync(int id)
        {
            return await _repository.DeleteMemberAsync(id);
        }
    }
}
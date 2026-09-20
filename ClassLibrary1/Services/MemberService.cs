using GymManagementSystem.API.Models;
using GymManagementSystem.Business.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymManagementSystem.Business.Services
{
    public class MemberService1 : IMemberService
    {
        private readonly IMemberRepository _repository;
        public MemberService1(IMemberRepository repository)
        {
            _repository = repository;
        }
        public Task<IEnumerable<Member>> GetAllMembersAsync()
        {
            return _repository.GetAllMembersAsync();
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
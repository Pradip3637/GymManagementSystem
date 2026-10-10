namespace GymManagementSystem.API.Models
{
    public class Member
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime JoinDate { get; set; } = DateTime.Now;
        public string? MembershipPlan { get; set; }
    }
}

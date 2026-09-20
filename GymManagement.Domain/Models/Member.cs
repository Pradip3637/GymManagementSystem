namespace GymManagementSystem.API.Models
{
    public class Member
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public DateTime JoinDate { get; set; }
        public string? MembershipPlan { get; set; }
    }
}

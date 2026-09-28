namespace EventTicketing.Api.Entities;

public class Role
{
    public int Id { get; set; }
    public required string Name { get; set; } // Buyer, Organizer, GateStaff, Accountant, Admin

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

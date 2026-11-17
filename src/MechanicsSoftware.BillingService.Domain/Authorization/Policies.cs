namespace MechanicsSoftware.BillingService.Domain.Authorization;

public static class Policies
{
    public const string Staff = nameof(Staff);

    public const string CustomerOrStaff = nameof(CustomerOrStaff);

    public const string CustomerRole = "CUSTOMER";

    public static readonly string[] StaffRoles = ["ADMIN", "ATTENDANT", "MECHANIC"];

    public static readonly string[] AllRoles = [.. StaffRoles, CustomerRole];
}

namespace MechanicsSoftware.BillingService.Domain.Authorization;

/// <summary>
/// Authorization policies and the role values carried in the platform JWT <c>role</c> claim.
/// Role values must match the tokens issued by mechanics-software (staff) and mechanics-lambda (CUSTOMER).
/// </summary>
public static class Policies
{
    public const string Staff = nameof(Staff);

    public const string CustomerOrStaff = nameof(CustomerOrStaff);

    public const string CustomerRole = "CUSTOMER";

    public static readonly string[] StaffRoles = ["ADMIN", "ATTENDANT", "MECHANIC"];

    public static readonly string[] AllRoles = [.. StaffRoles, CustomerRole];
}

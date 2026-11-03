using MechanicsSoftware.BillingService.Application.UseCases.Budgets.Commands;
using MechanicsSoftware.BillingService.Domain.Entities;
using MechanicsSoftware.BillingService.Domain.Enums;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.UnitTests.Helpers;

public static class BudgetFixtures
{
    public static BudgetItemDraft ServiceDraft(int unitPriceInCents = 15000, int quantity = 1) =>
        new(BudgetItemType.Service, Guid.NewGuid(), "Oil change", new Money(unitPriceInCents), quantity);

    public static BudgetItemDraft PartDraft(int unitPriceInCents = 4500, int quantity = 2) =>
        new(BudgetItemType.Part, Guid.NewGuid(), "Oil filter", new Money(unitPriceInCents), quantity);

    public static readonly BudgetRequester StaffRequester = BudgetRequester.Staff(Guid.NewGuid());

    public static Budget CreateBudget(Guid? serviceOrderId = null, Guid? customerId = null) =>
        Budget.Create(serviceOrderId ?? Guid.NewGuid(), customerId ?? Guid.NewGuid(), [ServiceDraft(), PartDraft()]);

    public static Budget CreateApprovedBudget(Guid? customerId = null)
    {
        var budget = CreateBudget(customerId: customerId);
        budget.Approve();
        return budget;
    }

    public static GenerateBudgetItem ServiceItem(int unitPriceInCents = 15000, int quantity = 1) =>
        new(BudgetItemType.Service, Guid.NewGuid(), "Oil change", unitPriceInCents, quantity);

    public static GenerateBudgetItem PartItem(int unitPriceInCents = 4500, int quantity = 2) =>
        new(BudgetItemType.Part, Guid.NewGuid(), "Oil filter", unitPriceInCents, quantity);
}

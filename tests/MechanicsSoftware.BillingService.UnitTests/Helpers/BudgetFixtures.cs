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

    public static Budget CreateBudget(Guid? serviceOrderId = null) =>
        Budget.Create(serviceOrderId ?? Guid.NewGuid(), [ServiceDraft(), PartDraft()]);

    public static GenerateBudgetItem ServiceItem(int unitPriceInCents = 15000, int quantity = 1) =>
        new(BudgetItemType.Service, Guid.NewGuid(), "Oil change", unitPriceInCents, quantity);

    public static GenerateBudgetItem PartItem(int unitPriceInCents = 4500, int quantity = 2) =>
        new(BudgetItemType.Part, Guid.NewGuid(), "Oil filter", unitPriceInCents, quantity);
}

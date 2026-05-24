namespace Restaurant.Domain.Enums;

public static class CashMovementKinds
{
  public static readonly CashMovementType[] OutflowTypes =
  [
      CashMovementType.ProviderPayment,
      CashMovementType.PettyCash,
      CashMovementType.Refund,
      CashMovementType.OtherOutflow,
      CashMovementType.AdjustmentOut,
  ];

    public static bool IsOutflow(CashMovementType type) =>
        type is CashMovementType.ProviderPayment
            or CashMovementType.PettyCash
            or CashMovementType.Refund
            or CashMovementType.OtherOutflow
            or CashMovementType.AdjustmentOut;
}

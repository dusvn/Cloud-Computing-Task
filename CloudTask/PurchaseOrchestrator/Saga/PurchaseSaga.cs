using Common.Models;
using PurchaseOrchestrator.Saga.Commands;

namespace PurchaseOrchestrator.Saga
{
    /// <summary>
    /// One row of the saga: in <paramref name="State"/> execute <paramref name="Command"/>,
    /// then go to <paramref name="OnSuccess"/> or <paramref name="OnFailure"/> (business rejection).
    /// <paramref name="BeforePivot"/>: if the command keeps failing technically, the saga may give up and compensate.
    /// </summary>
    internal sealed record SagaStep(SagaState State, ISagaCommand Command, SagaState OnSuccess, SagaState OnFailure, bool BeforePivot);

    /// <summary>
    /// The purchase saga as a table of steps:
    ///
    ///   Started      --ReserveBook-->     BookReserved   (rejected: Failed, nothing to undo)
    ///   BookReserved --ChargeAccount-->   Confirming     (rejected: Compensating)
    ///   Confirming   --ConfirmPurchase--> Completed      (pivot: only retried)
    ///   Compensating --Refund+Release-->  Compensated
    /// </summary>
    internal static class PurchaseSaga
    {
        /// <summary>Technical failures tolerated before the pivot; then the saga compensates instead of retrying forever.</summary>
        public const int MaxAttemptsBeforePivot = 5;

        private static readonly Dictionary<SagaState, SagaStep> Steps = new SagaStep[]
        {
            new(SagaState.Started,      new ReserveBookCommand(),     SagaState.BookReserved, SagaState.Failed,       BeforePivot: true),
            new(SagaState.BookReserved, new ChargeAccountCommand(),   SagaState.Confirming,   SagaState.Compensating, BeforePivot: true),
            new(SagaState.Confirming,   new ConfirmPurchaseCommand(), SagaState.Completed,    SagaState.Failed,       BeforePivot: false),
            new(SagaState.Compensating, new CompensateCommand(new RefundAccountCommand(), new ReleaseBookCommand()),
                                                                      SagaState.Compensated,  SagaState.Failed,       BeforePivot: false),
        }.ToDictionary(s => s.State);

        /// <summary>The step to run in this state; null when the saga is finished.</summary>
        public static SagaStep? StepFor(SagaState state) => Steps.GetValueOrDefault(state);
    }
}

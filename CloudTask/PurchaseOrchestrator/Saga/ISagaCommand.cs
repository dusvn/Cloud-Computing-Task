using Common.Models;

namespace PurchaseOrchestrator.Saga
{
    /// <summary>
    /// Command pattern: one saga step = one command object that knows which participant to call and how.
    /// The orchestrator only knows "current state → command → next state" (see <see cref="PurchaseSaga"/>).
    ///
    /// Contract for every command:
    ///  - idempotent: it sends the saga's TransactionId, so executing it again returns the first outcome;
    ///  - business outcome (ok / rejected) is the returned <see cref="StepResult"/>;
    ///  - technical problems (timeout, failover) are thrown, and the orchestrator retries the same command.
    /// </summary>
    internal interface ISagaCommand
    {
        string Name { get; }

        Task<StepResult> ExecuteAsync(OrderRecord saga);
    }
}

using Common.Models;

namespace PurchaseOrchestrator.Saga.Commands
{
    /// <summary>
    /// Composite command: runs the compensations in reverse order of the forward steps (refund, then release).
    /// Each part is idempotent, so after a crash the whole composite can simply run again.
    /// </summary>
    internal sealed class CompensateCommand : ISagaCommand
    {
        private readonly ISagaCommand[] _compensations;

        public CompensateCommand(params ISagaCommand[] compensations) => _compensations = compensations;

        public string Name => "Compensate(" + string.Join(", ", _compensations.Select(c => c.Name)) + ")";

        public async Task<StepResult> ExecuteAsync(OrderRecord saga)
        {
            foreach (ISagaCommand compensation in _compensations)
            {
                StepResult result = await compensation.ExecuteAsync(saga);
                if (!result.Succeeded)
                    return StepResult.Fail($"{compensation.Name} failed: {result.Message}");
            }
            return StepResult.Ok("Money refunded and books released.");
        }
    }
}

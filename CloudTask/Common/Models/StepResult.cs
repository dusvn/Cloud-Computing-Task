namespace Common.Models
{
    /// <summary>
    /// Business outcome of one saga command at a participant (Bookstore / Bank).
    /// Technical problems (timeout, failover) are NOT a StepResult: they are exceptions and the step is retried.
    /// </summary>
    public class StepResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>ReserveAsync: total amount (price × quantity) of the reservation.</summary>
        public double Amount { get; set; }

        public static StepResult Ok(string message = "", double amount = 0) => new() { Succeeded = true, Message = message, Amount = amount };
        public static StepResult Fail(string message) => new() { Succeeded = false, Message = message };
    }
}

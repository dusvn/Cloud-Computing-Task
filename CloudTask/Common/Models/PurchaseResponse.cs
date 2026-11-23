namespace Common.Models
{
    /// <summary>
    /// Outcome of a purchase request, carrying an HTTP-style status code and a human readable message.
    /// </summary>
    public class PurchaseResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;

        public bool IsAccepted => StatusCode == 202;

        /// <summary>200 - purchase finished successfully.</summary>
        public static PurchaseResponse Ok(string message = "Purchase completed.") => Create(200, message);

        /// <summary>202 - request passed validation and was accepted for processing.</summary>
        public static PurchaseResponse Accepted(string message = "Purchase request accepted.") => Create(202, message);

        /// <summary>400 - request is invalid.</summary>
        public static PurchaseResponse BadRequest(string message) => Create(400, message);

        /// <summary>409 - request was valid but could not be completed (e.g. not enough stock or money).</summary>
        public static PurchaseResponse Conflict(string message = "Purchase could not be completed.") => Create(409, message);

        private static PurchaseResponse Create(int statusCode, string message) =>
            new() { StatusCode = statusCode, Message = message };
    }
}

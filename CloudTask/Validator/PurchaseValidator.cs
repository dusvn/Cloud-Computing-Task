using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Common.Models;

namespace Validator
{
    /// <summary>
    /// Validates a <see cref="Purchase"/>. Single entry point: <see cref="Validate"/>.
    /// Every check returns an error message, or null when the check passes.
    /// </summary>
    internal sealed class PurchaseValidator
    {
        private const int MaxBookNameLength = 100;
        private const int MaxNameLength = 50;
        private const int MaxEmailLength = 254;
        private const int MinQuantity = 1;
        private const int MaxQuantity = 1000;

        private static readonly Regex EmailPattern =
            new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

        /// <summary>400 with the first problem found, otherwise 202 Accepted.</summary>
        public PurchaseResponse Validate(Purchase? purchase)
        {
            string? error = FindFirstError(purchase);
            return error is null
                ? PurchaseResponse.Accepted()
                : PurchaseResponse.BadRequest(error);
        }

        private string? FindFirstError(Purchase? purchase)
        {
            if (purchase is null) return "Purchase is required.";

            return CheckUserId(purchase.UserId)
                ?? CheckBookId(purchase.BookId)
                ?? CheckBookName(purchase.BookName)
                ?? CheckFirstName(purchase.FirstName)
                ?? CheckLastName(purchase.LastName)
                ?? CheckQuantity(purchase.Quantity)
                ?? CheckEmail(purchase.Email);
        }

        private string? CheckUserId(Guid userId) => CheckId(userId, "UserId");

        private string? CheckBookId(Guid bookId) => CheckId(bookId, "BookId");

        private string? CheckBookName(string? bookName) => CheckText(bookName, "BookName", MaxBookNameLength);

        private string? CheckFirstName(string? firstName) => CheckText(firstName, "FirstName", MaxNameLength);

        private string? CheckLastName(string? lastName) => CheckText(lastName, "LastName", MaxNameLength);

        private string? CheckQuantity(int quantity) =>
            IsInRange(quantity, MinQuantity, MaxQuantity)
                ? null
                : $"Quantity must be between {MinQuantity} and {MaxQuantity}.";

        private string? CheckEmail(string? email)
        {
            if (IsBlank(email)) return "Email is required.";
            if (IsTooLong(email, MaxEmailLength)) return $"Email must be at most {MaxEmailLength} characters.";
            if (!MatchesEmailPattern(email)) return "Email format is invalid.";
            return null;
        }

        private string? CheckId(Guid id, string fieldName) =>
            id == Guid.Empty ? $"{fieldName} is required and must be a valid UUID." : null;

        private string? CheckText(string? value, string fieldName, int maxLength)
        {
            if (IsBlank(value)) return $"{fieldName} is required.";
            if (IsTooLong(value, maxLength)) return $"{fieldName} must be at most {maxLength} characters.";
            return null;
        }

        private static bool IsBlank([NotNullWhen(false)] string? value) => string.IsNullOrWhiteSpace(value);

        private static bool IsTooLong(string value, int maxLength) => value.Length > maxLength;

        private static bool IsInRange(int value, int min, int max) => value >= min && value <= max;

        private static bool MatchesEmailPattern(string email) => EmailPattern.IsMatch(email.Trim());
    }
}

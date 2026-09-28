using System.Net.Mail;
using EvoOffer.Services;

namespace EvoOffer.Models;

public static class ContactDataValue
{
    public static string EmailValidationMessage => LocalizationService.Get("EmailValidation");

    public static bool IsValidEmail(string? text)
    {
        var email = text?.Trim();
        // Contact details are optional, but a supplied address must be valid.
        if (string.IsNullOrEmpty(email))
            return true;

        return !email.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(email, out var address)
            && string.Equals(address.Address, email, StringComparison.Ordinal)
            && address.Host.Contains('.')
            && !address.Host.EndsWith('.');
    }

    // Keep identifiers as strings so leading zeros and long numbers survive saving.
    public static string DigitsOnly(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : new string(text.Where(char.IsAsciiDigit).ToArray());
}

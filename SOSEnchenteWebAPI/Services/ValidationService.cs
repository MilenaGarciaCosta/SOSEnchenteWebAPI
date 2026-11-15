using System.Text.RegularExpressions;

namespace SOSEnchenteWebAPI.Services;

public interface IValidationService
{
    bool IsValidEmail(string email);
    bool IsValidPhoneNumber(string phoneNumber);
    string GetEmailValidationErrorMessage();
    string GetPhoneNumberValidationErrorMessage();
}

public class ValidationService : IValidationService
{
    private const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";

    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim();

        if (email.Length > 254)
            return false;

        if (!Regex.IsMatch(email, EmailPattern))
            return false;

        if (email.StartsWith(".") || email.EndsWith("."))
            return false;

        if (email.Contains(".."))
            return false;

        return true;
    }

    public bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        phoneNumber = phoneNumber.Trim();

        string digitsOnly = Regex.Replace(phoneNumber, @"\D", "");

        if (digitsOnly.Length < 8 || digitsOnly.Length > 15)
            return false;

        if (!Regex.IsMatch(phoneNumber, @"^[\d\s+\-().]+$"))
            return false;

        return true;
    }

    public string GetEmailValidationErrorMessage()
    {
        return "Email inválido. Por favor, forneça um email válido.";
    }

    public string GetPhoneNumberValidationErrorMessage()
    {
        return "Número de telefone inválido. Forneça um número com 8 a 15 dígitos.";
    }
}

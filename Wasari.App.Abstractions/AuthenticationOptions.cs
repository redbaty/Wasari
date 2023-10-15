namespace Wasari.App.Abstractions;

public class AuthenticationOptions
{
    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
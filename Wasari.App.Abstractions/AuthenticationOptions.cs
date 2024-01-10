namespace Wasari.App.Abstractions;

public class AuthenticationOptions
{
    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    public string AnonymousBasicAuthHeader { get; set; } = "Y3Jfd2ViOg==";
    
    public string AuthenticatedBasicAuthHeader { get; set; } = "b2VkYXJteHN0bGgxanZhd2ltbnE6OWxFaHZIWkpEMzJqdVY1ZFc5Vk9TNTdkb3BkSnBnbzE=";
}
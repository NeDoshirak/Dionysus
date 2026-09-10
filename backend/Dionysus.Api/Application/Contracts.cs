public record Credentials(string Email, string Password);
public record CodeRequest(string Email, string Code);
public record ResetRequest(string Email);
public record ResetConfirm(string Email, string Code, string NewPassword);
public record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

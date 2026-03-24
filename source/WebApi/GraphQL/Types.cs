namespace Project.WebApi.GraphQL;

// ── Inputs ────────────────────────────────────────────────────────────────────

public record RegisterInput(string Username, string Email, string Password);

public record LoginInput(string Login, string Password);

public record ResendConfirmationInput(string Email);

public record ForgotPasswordInput(string Email);

public record ResetPasswordInput(string NewPassword);

public record UpdateUsernameInput(string Username);

// ── Output types ──────────────────────────────────────────────────────────────

public record AuthenticatedUserType(Guid Id, string Username, string Email);

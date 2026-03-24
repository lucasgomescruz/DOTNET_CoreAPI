using Project.Application.Common.Localizers;

namespace Project.Application.Features.Commands.ConfirmPasswordReset;

public class ConfirmPasswordResetCommandValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    private readonly CultureLocalizer _localizer;

    public ConfirmPasswordResetCommandValidator(CultureLocalizer localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(_localizer.Text("RequiredField", "Token"));

        RuleFor(x => x.Request)
            .NotNull().WithMessage(_localizer.Text("RequiredField", "Request"))
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.NewPassword)
                    .NotEmpty().WithMessage(_localizer.Text("RequiredField", "Password"))
                    .MinimumLength(8).WithMessage(_localizer.Text("PasswordMinLength", 8))
                    .Matches("[A-Z]").WithMessage(_localizer.Text("PasswordUppercase"))
                    .Matches("[a-z]").WithMessage(_localizer.Text("PasswordLowercase"))
                    .Matches("[0-9]").WithMessage(_localizer.Text("PasswordNumber"))
                    .Matches("[^a-zA-Z0-9]").WithMessage(_localizer.Text("PasswordSpecialCharacter"));
            });
    }
}

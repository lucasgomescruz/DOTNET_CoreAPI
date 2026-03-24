using Project.Application.Common.Localizers;

namespace Project.Application.Features.Commands.ResendConfirmation;

public class ResendConfirmationCommandValidator : AbstractValidator<ResendConfirmationCommand>
{
    private readonly CultureLocalizer _localizer;

    public ResendConfirmationCommandValidator(CultureLocalizer localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.Request)
            .NotNull().WithMessage(_localizer.Text("RequiredField", "Request"))
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.Email)
                    .NotEmpty().WithMessage(_localizer.Text("RequiredField", "Email"))
                    .EmailAddress().WithMessage(_localizer.Text("InvalidEmail", "Email"));
            });
    }
}

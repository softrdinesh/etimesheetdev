using ETimeSheet.Application.Models;
using FluentValidation;

namespace ETimeSheet.Application.Validators.MasterAdmins;

/// <summary>
/// Shape-level validation for the client remove payload. Whether the client
/// exists and is not already removed is a database question, answered by
/// <c>MasterAdminService</c>.
/// </summary>
public class ClientDeleteRequestValidator : AbstractValidator<ClientDeleteRequest>
{
    public ClientDeleteRequestValidator()
    {
        RuleFor(request => request.ClientId)
            .GreaterThan(0)
            .WithMessage("ClientId is required and must be greater than 0.");
    }
}

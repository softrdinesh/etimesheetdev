using ETimeSheet.Application.Models;
using ETimeSheet.Application.Validators.MasterAdmins;

namespace ETimeSheet.Tests.Unit.Validators;

[Trait("Category", "Unit")]
public class ClientDeleteRequestValidatorTests
{
    private readonly ClientDeleteRequestValidator _validator = new();

    [Fact]
    public void APositiveClientIdPasses()
    {
        Assert.True(_validator.Validate(new ClientDeleteRequest { ClientId = 1 }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AClientIdThatIsNotPositiveIsRejected(int clientId)
    {
        var result = _validator.Validate(new ClientDeleteRequest { ClientId = clientId });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ClientDeleteRequest.ClientId));
    }
}

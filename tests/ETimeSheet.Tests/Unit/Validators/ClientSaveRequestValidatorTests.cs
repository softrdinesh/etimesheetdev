using ETimeSheet.Application.Models;
using ETimeSheet.Application.Validators.MasterAdmins;
using ETimeSheet.Tests.TestData;

namespace ETimeSheet.Tests.Unit.Validators;

/// <summary>
/// Shape rules for the client add/edit payload. Whether the country has a
/// currency, or the client exists, is the service's question - not tested here.
/// </summary>
[Trait("Category", "Unit")]
public class ClientSaveRequestValidatorTests
{
    private readonly ClientSaveRequestValidator _validator = new();

    private IReadOnlyList<string> FailedFields(ClientSaveRequest request) =>
        _validator.Validate(request).Errors.Select(error => error.PropertyName).Distinct().ToList();

    [Fact]
    public void AValidRequestPasses()
    {
        Assert.True(_validator.Validate(MasterAdminTestData.ValidRequest()).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(15)]
    public void ClientIdMayBeAbsentZeroOrAnId(int? clientId)
    {
        Assert.True(_validator.Validate(MasterAdminTestData.ValidRequest(clientId: clientId)).IsValid);
    }

    [Fact]
    public void ANegativeClientIdIsRejected()
    {
        Assert.Contains(
            nameof(ClientSaveRequest.ClientId),
            FailedFields(MasterAdminTestData.ValidRequest(clientId: -1)));
    }

    [Fact]
    public void OnlyTheRequiredFieldsAreNeeded()
    {
        var request = new ClientSaveRequest
        {
            ClientName = "Acme",
            BillingEmail = "billing@acme.test",
            PhoneNumber = "123",
            PaymentTermsDays = 0,
            CountryId = MasterAdminTestData.IndiaCountryId,
        };

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(nameof(ClientSaveRequest.ClientName))]
    [InlineData(nameof(ClientSaveRequest.BillingEmail))]
    [InlineData(nameof(ClientSaveRequest.PhoneNumber))]
    public void ARequiredTextFieldCannotBeBlank(string field)
    {
        foreach (var blank in new[] { null, "", "   " })
        {
            var request = MasterAdminTestData.ValidRequest();
            typeof(ClientSaveRequest).GetProperty(field)!.SetValue(request, blank);

            Assert.Contains(field, FailedFields(request));
        }
    }

    [Fact]
    public void PaymentTermsDaysIsRequiredAndNotNegative()
    {
        var missing = MasterAdminTestData.ValidRequest();
        missing.PaymentTermsDays = null;

        var negative = MasterAdminTestData.ValidRequest();
        negative.PaymentTermsDays = -1;

        Assert.Contains(nameof(ClientSaveRequest.PaymentTermsDays), FailedFields(missing));
        Assert.Contains(nameof(ClientSaveRequest.PaymentTermsDays), FailedFields(negative));
    }

    /// <summary>The currency is derived from the country, so the country is required.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public void CountryIdIsRequiredAndPositive(int? countryId)
    {
        Assert.Contains(
            nameof(ClientSaveRequest.CountryId),
            FailedFields(MasterAdminTestData.ValidRequest(countryId: countryId)));
    }

    [Fact]
    public void BillingEmailMustBeAnEmailAddress()
    {
        var request = MasterAdminTestData.ValidRequest();
        request.BillingEmail = "not-an-email";

        Assert.Contains(nameof(ClientSaveRequest.BillingEmail), FailedFields(request));
    }

    [Fact]
    public void AlternateEmailIsOptionalButMustBeAnEmailWhenSent()
    {
        var absent = MasterAdminTestData.ValidRequest();
        absent.AlternateEmail = null;

        var malformed = MasterAdminTestData.ValidRequest();
        malformed.AlternateEmail = "nope";

        Assert.True(_validator.Validate(absent).IsValid);
        Assert.Contains(nameof(ClientSaveRequest.AlternateEmail), FailedFields(malformed));
    }

    /// <summary>
    /// Each limit is the column's length: one character over is a named 400
    /// rather than a SQL truncation error and a 500.
    /// </summary>
    [Theory]
    [InlineData(nameof(ClientSaveRequest.ClientName), 50)]
    [InlineData(nameof(ClientSaveRequest.PhoneNumber), 150)]
    [InlineData(nameof(ClientSaveRequest.AlternatePhoneNumber), 150)]
    [InlineData(nameof(ClientSaveRequest.TaxIdentifier), 50)]
    public void TextFieldsAreLimitedToTheirColumnLength(string field, int maximum)
    {
        var property = typeof(ClientSaveRequest).GetProperty(field)!;

        var atLimit = MasterAdminTestData.ValidRequest();
        property.SetValue(atLimit, new string('x', maximum));

        var overLimit = MasterAdminTestData.ValidRequest();
        property.SetValue(overLimit, new string('x', maximum + 1));

        Assert.DoesNotContain(field, FailedFields(atLimit));
        Assert.Contains(field, FailedFields(overLimit));
    }

    [Theory]
    [InlineData(nameof(ClientSaveRequest.BillingEmail))]
    [InlineData(nameof(ClientSaveRequest.AlternateEmail))]
    public void EmailsAreLimitedTo150Characters(string field)
    {
        var request = MasterAdminTestData.ValidRequest();
        typeof(ClientSaveRequest).GetProperty(field)!
            .SetValue(request, new string('a', 140) + "@acme.test"); // 150 characters

        var tooLong = MasterAdminTestData.ValidRequest();
        typeof(ClientSaveRequest).GetProperty(field)!
            .SetValue(tooLong, new string('a', 141) + "@acme.test"); // 151 characters

        Assert.DoesNotContain(field, FailedFields(request));
        Assert.Contains(field, FailedFields(tooLong));
    }

    [Fact]
    public void BillingAddressHasNoLengthLimit()
    {
        var request = MasterAdminTestData.ValidRequest();
        request.BillingAddress = new string('x', 10_000);

        Assert.True(_validator.Validate(request).IsValid);
    }
}

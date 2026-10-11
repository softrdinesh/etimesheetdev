using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Services.Implementations;
using ETimeSheet.Shared.Exceptions;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ETimeSheet.Tests.Unit.Services;

/// <summary>
/// <see cref="MasterAdminService"/> - the client setup list, add/edit and remove.
/// <para>
/// The code generation and the currency derivation happen inside the stored
/// procedures, so they are proven by the integration tests. What is left here is
/// what the service itself decides: the 400 for a country with no currency, the
/// 404s, and what a remove writes.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class MasterAdminServiceTests
{
    private readonly Mock<IMasterAdminRepository> _repository = new(MockBehavior.Strict);

    /// <summary>
    /// Strict with no setup: none of these tests sends a logo, so any upload
    /// attempt fails the test.
    /// </summary>
    private readonly Mock<IDocumentUploader> _uploader = new(MockBehavior.Strict);
    private readonly FixedDateTimeProvider _clock = new(TimeLogTestData.Now);

    /// <summary>
    /// The billing email is free unless a test says otherwise - the strict
    /// repository mock would otherwise fail every save on the uniqueness check.
    /// </summary>
    public MasterAdminServiceTests() =>
        _repository
            .Setup(repository => repository.BillingEmailExistsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

    private MasterAdminService CreateService() =>
        new(_repository.Object, _uploader.Object, _clock, NullLogger<MasterAdminService>.Instance);

    private void CountryHasCurrency(bool hasCurrency) =>
        _repository
            .Setup(repository => repository.HasCurrencyForCountryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasCurrency);

    private void SaveReturns(ClientSaveDetail? row) =>
        _repository
            .Setup(repository => repository.SaveClientAsync(
                It.IsAny<ClientSaveRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

    private void ClientExists(bool exists, string? avatar = null) =>
        _repository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists
                ? new TimesheetClientSetup { ClientId = 4, ClientCode = "CL0004", Avatar = avatar }
                : null);

    // ---------------------------------------------------------------- list

    [Fact]
    public async Task ListReturnsEveryRowWithItsCurrency()
    {
        _repository
            .Setup(repository => repository.GetClientListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ClientListDetail
                {
                    ClientId = 1, ClientCode = "CL0001", ClientName = "Acme",
                    CurrencyId = 102, CurrencyName = "Indian Rupee", CurrencySymbol = "₹", CountryId = 103,
                },
                new ClientListDetail { ClientId = 2, ClientCode = "CL0002", ClientName = "Beta" },
            });

        var result = await CreateService().GetClientListAsync();

        Assert.Equal(new[] { "CL0001", "CL0002" }, result.Select(client => client.ClientCode));

        var first = result[0];
        Assert.Equal(102, first.CurrencyId);
        Assert.Equal("Indian Rupee", first.CurrencyName);
        Assert.Equal("₹", first.CurrencySymbol);
        Assert.Equal(103, first.CountryId);
    }

    [Fact]
    public async Task AnEmptyListIsAnEmptyListNotAnError()
    {
        _repository
            .Setup(repository => repository.GetClientListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ClientListDetail>());

        Assert.Empty(await CreateService().GetClientListAsync());
    }

    // ---------------------------------------------------------------- save

    [Fact]
    public async Task SaveReturnsTheRowTheProcedureSaved()
    {
        CountryHasCurrency(true);
        SaveReturns(MasterAdminTestData.SavedRow(clientId: 9, clientCode: "CL0009"));

        var result = await CreateService().SaveClientAsync(MasterAdminTestData.ValidRequest());

        Assert.Equal(9, result.ClientId);
        Assert.Equal("CL0009", result.ClientCode);
        Assert.Equal(MasterAdminTestData.IndiaCurrencyId, result.CurrencyId);
        Assert.Equal("Indian Rupee", result.CurrencyName);
        Assert.Equal("₹", result.CurrencySymbol);
    }

    [Fact]
    public async Task SavePassesTheRequestAndTokenStraightToTheRepository()
    {
        using var cancellation = new CancellationTokenSource();
        var request = MasterAdminTestData.ValidRequest(clientId: 4);
        CountryHasCurrency(true);
        ClientExists(true, avatar: "https://docs.test/current-logo.png");
        SaveReturns(MasterAdminTestData.SavedRow());

        await CreateService().SaveClientAsync(request, cancellation.Token);

        // An edit with no new logo keeps the avatar the client already has.
        _repository.Verify(
            repository => repository.SaveClientAsync(request, "https://docs.test/current-logo.png", cancellation.Token),
            Times.Once);
    }

    [Fact]
    public async Task SaveChecksTheCurrencyOfTheRequestedCountry()
    {
        CountryHasCurrency(true);
        SaveReturns(MasterAdminTestData.SavedRow());

        await CreateService().SaveClientAsync(
            MasterAdminTestData.ValidRequest(countryId: MasterAdminTestData.UnitedStatesCountryId));

        _repository.Verify(
            repository => repository.HasCurrencyForCountryAsync(
                MasterAdminTestData.UnitedStatesCountryId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The procedure would refuse this too, but only with a SQL error - a 500.
    /// The service asks first so the caller gets a 400 naming the field, and
    /// the procedure is never called.
    /// </summary>
    [Fact]
    public async Task SaveRejectsACountryWithNoCurrencyBeforeCallingTheProcedure()
    {
        CountryHasCurrency(false);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().SaveClientAsync(
                MasterAdminTestData.ValidRequest(countryId: MasterAdminTestData.CountryWithoutCurrencyId)));

        Assert.True(exception.Failures.ContainsKey(nameof(ClientSaveRequest.CountryId)));
        _repository.Verify(
            repository => repository.SaveClientAsync(
                It.IsAny<ClientSaveRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Refused before anything is uploaded or saved - the strict uploader mock
    /// and the Never below are what prove it.
    /// </summary>
    [Fact]
    public async Task EditingAClientThatIsMissingOrDeletedIsNotFound()
    {
        CountryHasCurrency(true);
        ClientExists(false);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().SaveClientAsync(MasterAdminTestData.ValidRequest(clientId: 77)));

        Assert.Contains("77", exception.Message);
        _repository.Verify(
            repository => repository.SaveClientAsync(
                It.IsAny<ClientSaveRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------- remove

    [Fact]
    public async Task RemoveMarksTheClientDeletedAndStampsTheDate()
    {
        var client = MasterAdminTestData.Client(clientId: 3, clientCode: "CL0003");

        _repository
            .Setup(repository => repository.GetForUpdateAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _repository
            .Setup(repository => repository.UpdateAsync(client, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().RemoveClientAsync(new ClientDeleteRequest { ClientId = 3 });

        Assert.True(client.IsDeleted);
        Assert.Equal(TimeLogTestData.Now, client.DeleteDate);
        _repository.Verify(
            repository => repository.UpdateAsync(client, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A soft delete changes nothing else: the code stays spent, and the rest of
    /// the row is history.
    /// </summary>
    [Fact]
    public async Task RemoveLeavesTheRestOfTheRowAlone()
    {
        var client = MasterAdminTestData.Client(clientId: 3, clientCode: "CL0003", clientName: "Keep Me");

        _repository
            .Setup(repository => repository.GetForUpdateAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _repository
            .Setup(repository => repository.UpdateAsync(client, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().RemoveClientAsync(new ClientDeleteRequest { ClientId = 3 });

        Assert.Equal("CL0003", client.ClientCode);
        Assert.Equal("Keep Me", client.ClientName);
        Assert.Equal(MasterAdminTestData.IndiaCurrencyId, client.CurrencyCode);
    }

    /// <summary>
    /// The repository hides deleted clients, so this covers both an id that never
    /// existed and a client removed already - a second remove is a 404, and the
    /// first DeleteDate is never overwritten.
    /// </summary>
    [Fact]
    public async Task RemovingAClientThatIsMissingOrAlreadyRemovedIsNotFound()
    {
        _repository
            .Setup(repository => repository.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimesheetClientSetup?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().RemoveClientAsync(new ClientDeleteRequest { ClientId = 404 }));

        _repository.Verify(
            repository => repository.UpdateAsync(It.IsAny<TimesheetClientSetup>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

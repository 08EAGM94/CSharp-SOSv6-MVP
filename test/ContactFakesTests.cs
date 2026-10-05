using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using test.Fakes;

namespace test;

public class ContactFakesTests
{
    [Fact]
    public void FakeContactChildrenService_ExposesTheChildrenContractAndNotTheSelectOne()
    {
        var service = new FakeContactChildrenService();

        Assert.IsAssignableFrom<IEnterpriseChildrenService<ContactDTO>>(service);
        Assert.IsNotAssignableFrom<ISelectService<ContactDTO>>(service);
    }

    [Fact]
    public async Task FakeContactChildrenService_CountsEveryCallAndRecordsTheExactDtoReceived()
    {
        var service = new FakeContactChildrenService();
        var added = new ContactDTO { EnterpriseId = 1, FullName = "Contacto nuevo" };
        var requested = new ContactDTO { Id = 2 };
        var enterprise = new ContactDTO { EnterpriseId = 3 };
        var forSelect = new ContactDTO { EnterpriseId = 4 };
        var updated = new ContactDTO { Id = 5, FullName = "Contacto editado" };
        var visibility = new ContactDTO { Id = 6, Visibility = "DISABLED" };

        await service.AddAsyncChild(added);
        await service.GetAsyncChild(requested);
        await service.GetAsyncChildrenByEnterprise(enterprise);
        await service.GetAsyncChildrenByEnterForSelect(forSelect);
        await service.UpdateAsyncChild(updated);
        await service.UpdateAsyncVisibility(visibility);

        Assert.Equal(1, service.AddAsyncChildCalls);
        Assert.Same(added, service.LastAddedDto);
        Assert.Equal(1, service.GetAsyncChildCalls);
        Assert.Same(requested, service.LastRequestedDto);
        Assert.Equal(1, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Same(enterprise, service.LastEnterpriseDto);
        Assert.Equal(1, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Same(forSelect, service.LastForSelectDto);
        Assert.Equal(1, service.UpdateAsyncChildCalls);
        Assert.Same(updated, service.LastUpdatedDto);
        Assert.Equal(1, service.UpdateAsyncVisibilityCalls);
        Assert.Same(visibility, service.LastVisibilityDto);
        Assert.Equal(6, service.TotalCalls);
    }

    [Fact]
    public async Task FakeContactChildrenService_ReturnsTheProgrammedResults()
    {
        var service = new FakeContactChildrenService();
        var child = new ContactDTO { Id = 1, FullName = "Contacto almacenado", Visibility = "ENABLED" };
        var all = new List<ContactDTO>
        {
            child,
            new() { Id = 2, FullName = "Contacto deshabilitado", Visibility = "DISABLED" }
        };
        var forSelect = new List<ContactDTO> { child };
        service.ChildResult = child;
        service.ChildrenResult = all;
        service.ForSelectResult = forSelect;

        Assert.Same(child, await service.GetAsyncChild(new ContactDTO { Id = 1 }));
        Assert.Same(all, await service.GetAsyncChildrenByEnterprise(new ContactDTO { EnterpriseId = 1 }));
        Assert.Same(forSelect, await service.GetAsyncChildrenByEnterForSelect(new ContactDTO { EnterpriseId = 1 }));
    }

    [Fact]
    public async Task FakeContactChildrenService_PropagatesTheProgrammedFailureAndStillCountsTheCall()
    {
        var service = new FakeContactChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException("No existe un contacto con ese identificador.")
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddAsyncChild(new ContactDTO { Id = 1 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsyncChild(new ContactDTO { Id = 1 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsyncChildrenByEnterprise(new ContactDTO { EnterpriseId = 1 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsyncChildrenByEnterForSelect(new ContactDTO { EnterpriseId = 1 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsyncChild(new ContactDTO { Id = 1 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsyncVisibility(new ContactDTO { Id = 1 }));

        Assert.Equal(6, service.TotalCalls);
    }

    [Fact]
    public void FakeContactChildrenService_ReportsNoCallsBeforeTheFirstInvocation()
    {
        var service = new FakeContactChildrenService();

        Assert.Equal(0, service.TotalCalls);
        Assert.Null(service.LastAddedDto);
        Assert.Null(service.LastRequestedDto);
        Assert.Null(service.LastEnterpriseDto);
        Assert.Null(service.LastForSelectDto);
        Assert.Null(service.LastUpdatedDto);
        Assert.Null(service.LastVisibilityDto);
    }

    [Fact]
    public void FakeContactSelectService_ExposesTheSelectContractAndNotTheChildrenOne()
    {
        var service = new FakeContactSelectService();

        Assert.IsAssignableFrom<ISelectService<ContactDTO>>(service);
        Assert.IsNotAssignableFrom<IEnterpriseChildrenService<ContactDTO>>(service);
    }

    [Fact]
    public async Task FakeContactSelectService_CountsTheCallAndReturnsTheProgrammedResult()
    {
        var service = new FakeContactSelectService();
        var stored = new List<ContactDTO> { new() { Id = 1, FullName = "Contacto habilitado", Visibility = "ENABLED" } };
        service.SelectResult = stored;

        var result = await service.GetAsyncInfoForSelects();

        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
        Assert.Same(stored, result);
    }

    [Fact]
    public async Task FakeContactSelectService_PropagatesTheProgrammedFailure()
    {
        var service = new FakeContactSelectService { ExceptionToThrow = new InvalidOperationException("fallo programado") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsyncInfoForSelects());

        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
    }
}
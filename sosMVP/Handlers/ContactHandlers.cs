using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace SosMVP.Handlers;

public static class ContactHandlers
{
    public static async Task<IResult> InsertContactAsync(IEnterpriseChildrenService<ContactDTO> enterpriseChildrenService, ContactDTO dto)
    {
        await enterpriseChildrenService.AddAsyncChild(dto);

        return Results.StatusCode(StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetContactAsync(IEnterpriseChildrenService<ContactDTO> enterpriseChildrenService, int id)
    {
        var contact = await enterpriseChildrenService.GetAsyncChild(new ContactDTO { Id = id });

        return Results.Ok(contact);
    }

    public static async Task<IResult> GetContactsByEnterpriseAsync(IEnterpriseChildrenService<ContactDTO> enterpriseChildrenService, int enterpriseId)
    {
        var contacts = await enterpriseChildrenService.GetAsyncChildrenByEnterprise(new ContactDTO { EnterpriseId = enterpriseId });

        return Results.Ok(contacts);
    }

    public static async Task<IResult> UpdateContactAsync(IEnterpriseChildrenService<ContactDTO> enterpriseChildrenService, int id, ContactDTO dto)
    {
        dto.Id = id;

        await enterpriseChildrenService.UpdateAsyncChild(dto);

        return Results.NoContent();
    }

    public static async Task<IResult> UpdateContactVisibilityAsync(IEnterpriseChildrenService<ContactDTO> enterpriseChildrenService, int id, ContactDTO dto)
    {
        var visibilityDto = new ContactDTO { Id = id, Visibility = dto.Visibility };

        await enterpriseChildrenService.UpdateAsyncVisibility(visibilityDto);

        return Results.NoContent();
    }

    public static async Task<IResult> GetContactsByEnterpriseForSelectAsync(IEnterpriseChildrenService<ContactDTO> enterpriseChildrenService, int enterpriseId)
    {
        var contacts = await enterpriseChildrenService.GetAsyncChildrenByEnterForSelect(new ContactDTO { EnterpriseId = enterpriseId });

        return Results.Ok(contacts);
    }

    public static async Task<IResult> GetContactsForSelectAsync(ISelectService<ContactDTO> selectService)
    {
        var contacts = await selectService.GetAsyncInfoForSelects();

        return Results.Ok(contacts);
    }
}
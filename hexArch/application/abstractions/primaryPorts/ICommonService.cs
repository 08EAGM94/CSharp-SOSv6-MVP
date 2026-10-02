using HexArch.Application.DTOs;

namespace HexArch.Application.Abstractions.PrimaryPorts;

public interface ICommonService<TDTO> where TDTO : class
{
    Task AddAsyncInfo(TDTO dto, ContactDTO? contact = null);

    Task<TDTO> GetAsyncInfo(TDTO dto);

    Task<IEnumerable<TDTO>> GetAsyncAllInfo();

    Task UpdateAsyncInfo(TDTO dto);

    Task UpdateAsyncVisibility(TDTO dto);
}
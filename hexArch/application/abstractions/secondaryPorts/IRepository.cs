using HexArch.Domain.Entities;

namespace HexArch.Application.Abstractions.SecondaryPorts;

public interface IRepository<TEntity, TDTO> where TEntity : class
{
    Task AddAsyncInfo(TEntity entity, ContactEntity? contact = null);

    Task<TDTO> GetAsyncInfo(TEntity entity);

    Task<IEnumerable<TDTO>> GetAsyncAllInfo();

    Task UpdateAsyncInfo(TEntity entity);

    Task UpdateAsyncVisibility(TDTO dto);
}
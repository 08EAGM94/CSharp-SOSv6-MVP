namespace HexArch.Application.Abstractions.SecondaryPorts;

public interface IByEnterpriseRepository<TEntity, TDTO>
{
    Task AddAsyncChild(TEntity entity);

    Task<TDTO> GetAsyncChild(TEntity entity);

    Task<IEnumerable<TDTO>> GetAsyncChildrenByEnterForSelect(TEntity entity);

    Task<IEnumerable<TDTO>> GetAsyncChildrenByEnterprise(TEntity entity);

    Task UpdateAsyncChild(TEntity entity);

    Task UpdateAsyncVisibility(TDTO dto);
}
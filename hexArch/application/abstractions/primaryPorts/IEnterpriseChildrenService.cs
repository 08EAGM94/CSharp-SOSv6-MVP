namespace HexArch.Application.Abstractions.PrimaryPorts;

public interface IEnterpriseChildrenService<TDTO>
{
    Task AddAsyncChild(TDTO dto);

    Task<TDTO> GetAsyncChild(TDTO dto);

    Task<IEnumerable<TDTO>> GetAsyncChildrenByEnterForSelect(TDTO dto);

    Task<IEnumerable<TDTO>> GetAsyncChildrenByEnterprise(TDTO dto);

    Task UpdateAsyncChild(TDTO dto);

    Task UpdateAsyncVisibility(TDTO dto);
}
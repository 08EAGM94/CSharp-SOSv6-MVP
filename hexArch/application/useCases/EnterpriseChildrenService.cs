using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace HexArch.Application.UseCases;

public class EnterpriseChildrenService<TEntity, TDTO> : IEnterpriseChildrenService<TDTO>
{
    private readonly IByEnterpriseRepository<TEntity, TDTO> _repository;
    private readonly IMapper<TDTO, TEntity> _toEntityMapper;

    public EnterpriseChildrenService(IByEnterpriseRepository<TEntity, TDTO> repository, IMapper<TDTO, TEntity> toEntityMapper)
    {
        _repository = repository;
        _toEntityMapper = toEntityMapper;
    }

    public Task AddAsyncChild(TDTO dto)
    {
        return _repository.AddAsyncChild(_toEntityMapper.Map(dto));
    }

    public Task<TDTO> GetAsyncChild(TDTO dto)
    {
        return _repository.GetAsyncChild(_toEntityMapper.Map(dto));
    }

    public Task<IEnumerable<TDTO>> GetAsyncChildrenByEnterForSelect(TDTO dto)
    {
        return _repository.GetAsyncChildrenByEnterForSelect(_toEntityMapper.Map(dto));
    }

    public Task<IEnumerable<TDTO>> GetAsyncChildrenByEnterprise(TDTO dto)
    {
        return _repository.GetAsyncChildrenByEnterprise(_toEntityMapper.Map(dto));
    }

    public Task UpdateAsyncChild(TDTO dto)
    {
        return _repository.UpdateAsyncChild(_toEntityMapper.Map(dto));
    }

    public Task UpdateAsyncVisibility(TDTO dto)
    {
        Type dtoType = dto!.GetType();
        int? id = dtoType.GetProperty("Id")?.GetValue(dto) as int?;
        string? visibility = dtoType.GetProperty("Visibility")?.GetValue(dto) as string;

        string errors = "";
        if (id is null || id < 1)
        {
            errors += "El identificador debe ser un número mayor que cero.\n";
        }
        if (visibility is null || (visibility != "ENABLED" && visibility != "DISABLED"))
        {
            errors += "La visibilidad debe ser ENABLED o DISABLED.\n";
        }

        if (errors.Length > 0)
        {
            throw new HexArchApplicationException(errors);
        }

        return _repository.UpdateAsyncVisibility(dto);
    }
}
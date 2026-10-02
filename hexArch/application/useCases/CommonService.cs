using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace HexArch.Application.UseCases;

public class CommonService<TEntity, TDTO> : ICommonService<TDTO> where TEntity : class where TDTO : class
{
    private readonly IRepository<TEntity, TDTO> _repository;
    private readonly IMapper<TDTO, TEntity> _toEntityMapper;
    private readonly IMapper<ContactDTO, ContactEntity> _toContactEntityMapper;

    public CommonService(
        IRepository<TEntity, TDTO> repository,
        IMapper<TDTO, TEntity> toEntityMapper,
        IMapper<ContactDTO, ContactEntity> toContactEntityMapper)
    {
        _repository = repository;
        _toEntityMapper = toEntityMapper;
        _toContactEntityMapper = toContactEntityMapper;
    }

    public Task AddAsyncInfo(TDTO dto, ContactDTO? contact = null)
    {
        return _repository.AddAsyncInfo(
            _toEntityMapper.Map(dto),
            contact is null ? null : _toContactEntityMapper.Map(contact));
    }

    public Task<TDTO> GetAsyncInfo(TDTO dto)
    {
        return _repository.GetAsyncInfo(_toEntityMapper.Map(dto));
    }

    public Task<IEnumerable<TDTO>> GetAsyncAllInfo()
    {
        return _repository.GetAsyncAllInfo();
    }

    public Task UpdateAsyncInfo(TDTO dto)
    {
        return _repository.UpdateAsyncInfo(_toEntityMapper.Map(dto));
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
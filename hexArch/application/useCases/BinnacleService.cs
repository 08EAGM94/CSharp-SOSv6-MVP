using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace HexArch.Application.UseCases;

public class BinnacleService : IBinnacleService
{
    private readonly IBinnacleRepository _repository;
    private readonly IMapper<BinnacleDTO, BinnacleEntity> _toEntityMapper;

    public BinnacleService(IBinnacleRepository repository, IMapper<BinnacleDTO, BinnacleEntity> toEntityMapper)
    {
        _repository = repository;
        _toEntityMapper = toEntityMapper;
    }

    public Task AddAsyncInfo(BinnacleDTO dto)
    {
        return _repository.AddAsyncInfo(
            _toEntityMapper.Map(dto));
    }

    public Task<BinnacleDTO> GetAsyncInfo(BinnacleDTO dto)
    {
        return _repository.GetAsyncInfo(_toEntityMapper.Map(dto));
    }

    public Task<PaginationResult> GetAsyncAllInfo(BinnacleDTO? dto = null, int? page = null, int? elemsKey = null, Dictionary<string, string>? binnFilter = null, string? controllerAction = null)
    {
        return _repository.GetAsyncAllInfo(
            dto is null ? null : _toEntityMapper.Map(dto),
            page,
            elemsKey,
            binnFilter,
            controllerAction
        );
    }

    public Task UpdateAsyncInfo(BinnacleDTO dto)
    {
        return _repository.UpdateAsyncInfo(_toEntityMapper.Map(dto));
    }

    public Task UpdateAsyncVisibility(BinnacleDTO dto)
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

    public Task FollowupPartialAsync(BinnacleDTO entity)
    {
        return _repository.FollowupPartialAsync(_toEntityMapper.Map(entity));
    }

    public Task ResetActivitiesAsync(BinnacleDTO entity)
    {
        return _repository.ResetActivitiesAsync(_toEntityMapper.Map(entity));
    }

    public Task CancelBinnacleAsync(BinnacleDTO entity)
    {
        return _repository.CancelBinnacleAsync(_toEntityMapper.Map(entity));
    }

    public Task FinishBinnacleAsync(BinnacleDTO entity)
    {
        return _repository.FinishBinnacleAsync(_toEntityMapper.Map(entity));
    }
}
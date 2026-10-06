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
            _toEntityMapper.Map(NormalizeForInsert(dto)));
    }

    private static BinnacleDTO NormalizeForInsert(BinnacleDTO dto)
    {
        return new BinnacleDTO
        {
            Id = dto.Id,
            UserId = dto.UserId,
            ContactId = dto.ContactId,
            Service = dto.Service,
            DeviceId = dto.DeviceId,
            Amount = dto.Amount,
            ActivitiesDone = null,
            Hints = null,
            CustomerSignature = dto.CustomerSignature,
            Status = null,
            StartingDate = dto.StartingDate,
            EndDate = dto.EndDate,
            Visibility = dto.Visibility,
            DatesType = dto.DatesType,
            LeftDay = dto.LeftDay,
            RightDay = dto.RightDay,
            EnterpriseId = dto.EnterpriseId,
            CancelDesc = null,
            User = dto.User,
            Contact = dto.Contact,
            Device = dto.Device
        };
    }

    public Task<BinnacleDTO> GetAsyncInfo(BinnacleDTO dto)
    {
        return _repository.GetAsyncInfo(_toEntityMapper.Map(dto));
    }

    public Task<PaginationResult> GetAsyncAllInfo(BinnacleDTO? dto = null, int? page = null, int? elemsKey = null, Dictionary<string, string>? binnFilter = null, string? controllerAction = null)
    {
        if (!IsValidPagination(page, elemsKey))
        {
            throw new HexArchApplicationException("La página y los elementos por página deben ser números mayores que cero.");
        }

        return _repository.GetAsyncAllInfo(
            dto is null ? null : _toEntityMapper.Map(dto),
            page,
            elemsKey,
            binnFilter,
            controllerAction
        );
    }

    private static bool IsValidPagination(int? page, int? elemsKey)
    {
        return page is >= 1 && elemsKey is >= 1;
    }

    private static bool IsValidStatus(string? status)
    {
        return status is "en proceso" or "falta confirmar" or "cancelado" or "finalizado";
    }

    private static bool IsValidVisibility(string? visibility)
    {
        return visibility is "ENABLED" or "DISABLED";
    }

    public Task UpdateAsyncInfo(BinnacleDTO dto)
    {
        Type dtoType = dto!.GetType();
        int? id = dtoType.GetProperty("Id")?.GetValue(dto) as int?;
        string? status = dtoType.GetProperty("Status")?.GetValue(dto) as string;

        string errors = "";
        if (id is null || id < 1)
        {
            errors += "El identificador debe ser un número mayor que cero.\n";
        }
        if (!IsValidStatus(status))
        {
            errors += "La visibilidad debe ser en proceso o falta confirmar o cancelado o finalizado.\n";
        }

        if (errors.Length > 0)
        {
            throw new HexArchApplicationException(errors);
        }
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
        if (!IsValidVisibility(visibility))
        {
            errors += "La visibilidad debe ser ENABLED o DISABLED.\n";
        }

        if (errors.Length > 0)
        {
            throw new HexArchApplicationException(errors);
        }

        return _repository.UpdateAsyncVisibility(dto);
    }

    public Task FollowupPartialAsync(BinnacleDTO dto)
    {
        return _repository.FollowupPartialAsync(_toEntityMapper.Map(dto));
    }

    public Task ResetActivitiesAsync(BinnacleDTO dto)
    {
        return _repository.ResetActivitiesAsync(_toEntityMapper.Map(dto));
    }

    public Task CancelBinnacleAsync(BinnacleDTO dto)
    {
        return _repository.CancelBinnacleAsync(_toEntityMapper.Map(dto));
    }

    public Task FinishBinnacleAsync(BinnacleDTO dto)
    {
        return _repository.FinishBinnacleAsync(_toEntityMapper.Map(dto));
    }
}
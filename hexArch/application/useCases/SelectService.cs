using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;

namespace HexArch.Application.UseCases;

public class SelectService<TDTO> : ISelectService<TDTO>
{
    private readonly ISelectRepository<TDTO> _repository;

    public SelectService(ISelectRepository<TDTO> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<TDTO>> GetAsyncInfoForSelects()
    {
        return _repository.GetAsyncInfoForSelects();
    }
}
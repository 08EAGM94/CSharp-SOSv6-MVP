namespace HexArch.Application.Abstractions.SecondaryPorts;

public interface ISelectRepository<TDTO>
{
    Task<IEnumerable<TDTO>> GetAsyncInfoForSelects();
}
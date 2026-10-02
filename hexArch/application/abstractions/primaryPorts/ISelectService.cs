namespace HexArch.Application.Abstractions.PrimaryPorts;

public interface ISelectService<TDTO>
{
    Task<IEnumerable<TDTO>> GetAsyncInfoForSelects();
}
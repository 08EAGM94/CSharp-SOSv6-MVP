namespace HexArch.Application.Abstractions.Mapper;

public interface IMapper<TIn, TOut>
{
    TOut Map(TIn obj);
}
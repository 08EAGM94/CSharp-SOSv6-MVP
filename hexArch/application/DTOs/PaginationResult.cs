namespace HexArch.Application.DTOs;

public class PaginationResult
{
    public List<BinnacleDTO> Elements { get; set; } = [];
    public int ActualPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalRegisters { get; set; }
}

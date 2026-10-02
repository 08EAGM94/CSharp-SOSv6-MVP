namespace HexArch.Application.DTOs;

public class DeviceDTO
{
    public int? Id { get; set; }
    public int? EnterpriseId { get; set; }
    public int? TypeId { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public int? InventoryNumber { get; set; }
    public string? Visibility { get; set; }
    public TypeDTO? Type { get; set; }
}
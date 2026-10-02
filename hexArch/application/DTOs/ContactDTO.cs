namespace HexArch.Application.DTOs;

public class ContactDTO
{
    public int? Id { get; set; }
    public int? EnterpriseId { get; set; }
    public string? FullName { get; set; }
    public string? Visibility { get; set; }
    public EnterpriseDTO? Enterprise { get; set; }
}
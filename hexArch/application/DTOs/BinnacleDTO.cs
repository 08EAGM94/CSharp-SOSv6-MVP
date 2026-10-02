namespace HexArch.Application.DTOs;

public class BinnacleDTO
{
    public int? Id { get; set; }
    public int? UserId { get; set; }
    public int? ContactId { get; set; }
    public string? Service { get; set; }
    public int? DeviceId { get; set; }
    public float? Amount { get; set; }
    public string? ActivitiesDone { get; set; }
    public string? Hints { get; set; }
    public string? CustomerSignature { get; set; }
    public string? Status { get; set; }
    public DateOnly? StartingDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Visibility { get; set; }
    public string? DatesType { get; set; }
    public DateOnly? LeftDay { get; set; }
    public DateOnly? RightDay { get; set; }
    public int? EnterpriseId { get; set; }
    public string? CancelDesc { get; set; }
    public UserDTO? User { get; set; }
    public ContactDTO? Contact { get; set; }
    public DeviceDTO? Device { get; set; }
}
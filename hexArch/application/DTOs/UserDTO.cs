namespace HexArch.Application.DTOs;

public class UserDTO
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Surname { get; set; }
    public string? Nickname { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
    public string? Signature { get; set; }
    public string? Visibility { get; set; }
    public string? ConfPwd { get; set; }
    public string? AdminNickname { get; set; }
    public string? AdminPwd { get; set; }
}
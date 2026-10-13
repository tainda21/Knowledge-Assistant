namespace WebAPI.Models;

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}

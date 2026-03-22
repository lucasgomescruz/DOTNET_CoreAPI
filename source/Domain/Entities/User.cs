namespace Project.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Username { get; private set; } = string.Empty;
        public string HashedPassword { get; set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public Guid RoleId { get; set; }
        public virtual Role? Role { get; set; }

        private User( ) { }

        public User(string username, string password, string email, Guid roleId)
        {
            Username = username;
            HashedPassword = HashPassword(password);
            Email = email;
            RoleId = roleId;
        }

        public User(string username, string password, string email, Guid roleId, bool isHashed)
        {
            Username = username;
            HashedPassword = isHashed ? password : HashPassword(password);
            Email = email;
            RoleId = roleId;
        }

        public void UpdateUsername(string newUsername)
        {
            if (string.IsNullOrWhiteSpace(newUsername))
                throw new ArgumentException("Username cannot be empty", nameof(newUsername));

            Username = newUsername.Trim();
        }

        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, HashedPassword);
        }
    }
}

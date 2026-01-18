using System;

namespace Web.Server.Entities
{
    public class AuthToken
    {
        public int ID { get; set; }
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public DateTime LastRefreshed { get; set; }
    }
}

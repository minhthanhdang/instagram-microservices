using Domain.Rules;

namespace Users.Domain.Rules;

public class UsernameLengthRule : LengthRule
{
	public UsernameLengthRule(string username) : base(username, "Username", 1, 20) { }
}
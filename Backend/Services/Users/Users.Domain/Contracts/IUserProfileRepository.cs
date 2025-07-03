using Users.Domain.Models;

namespace Users.Domain.Contracts;

public interface IUserProfileRepository
{
	Task<UserProfile?> GetUserProfileByIdAsync(string userId, bool updateLock,
		CancellationToken cancellationToken = default);

	Task<UserProfile?> GetUserProfileByUsernameAsync(string handle, bool updateLock,
		CancellationToken cancellationToken = default);

	Task AddUserProfileAsync(UserProfile userProfile);
	Task<bool> IsUsernameAvailable(string handle, CancellationToken cancellationToken = default);
	void Track(UserProfile userProfile);
}
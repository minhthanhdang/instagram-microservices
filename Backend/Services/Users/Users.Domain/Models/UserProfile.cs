using Domain;
using Users.Domain.DomainEvents;
using Users.Domain.Rules;

namespace Users.Domain.Models;

public class UserProfile : DomainEntity
{
	private UserProfile() { }

	private UserProfile(string userId, string? displayName, string username, ImageFile? avatar)
	{
		CheckRule(new UsernameLengthRule(username));

		Id = userId;
		DisplayName = displayName;
		Description = string.Empty;
		Email = null;
		Username = username;
		Avatar = avatar;
		IsPrivate = false;
		CreateDate = DateTimeOffset.UtcNow;
		Biography = string.Empty;

		AddDomainEvent(new UserCreatedDomainEvent(this));

		if (Avatar != null) AddDomainEvent(new UserProfileAvatarUpdatedDomainEvent(this, null));
	}

	public string Id { get; private set; }
	public string? DisplayName { get; private set; }
	public string? Description { get; private set; }
	public string Username { get; private set; }
	public ImageFile? Avatar { get; private set; }
	
	public string? Biography { get; private set; }

	public string? Email { get; private set; }
	public bool IsPrivate { get; private set; }
	public DateTimeOffset CreateDate { get; private set; }

	public long Version { get; set; }
	
	

	public void UpdateInfo(string username, string description, string displayName, string email, string? biography)
	{
		CheckRule(new UsernameLengthRule(username));

		Username = username;
		DisplayName = displayName;
		Description = description;
		Email = email;
		Biography = biography;
	}

	public void UpdateAvatar(ImageFile? avatar)
	{
		if (Avatar?.ImageFileId == avatar?.ImageFileId) return;

		var oldAvatar = Avatar;
		Avatar = avatar;
		Version++;

		AddDomainEvent(new UserProfileAvatarUpdatedDomainEvent(this, oldAvatar));
		AddUniqueDomainEvent(new UserProfileUpdatedDomainEvent(this));
	}

	public static UserProfile Create(string userId, string? displayName, string username, ImageFile? avatar)
	{
		return new UserProfile(userId, displayName, username, avatar);
	}
}
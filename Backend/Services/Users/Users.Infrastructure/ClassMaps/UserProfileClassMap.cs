using Infrastructure.MongoDb;
using MongoDB.Bson.Serialization;
using Users.Domain.Models;

namespace Users.Infrastructure.ClassMaps;

public class UserProfileClassMap : MongoClassMap<UserProfile>
{
	public override void RegisterClassMap(BsonClassMap<UserProfile> classMap)
	{
		classMap.AutoMap();

		classMap.MapIdMember(x => x.Id);

		classMap.MapMember(x => x.DisplayName);

		classMap.MapMember(x => x.Description);

		classMap.MapMember(x => x.Username).SetIgnoreIfNull(true);

		classMap.MapMember(x => x.Email);

		classMap.MapMember(x => x.Avatar);

		classMap.MapMember(x => x.CreateDate);

		classMap.MapMember(x => x.Version);

		classMap.SetIgnoreExtraElements(true);
	}
}
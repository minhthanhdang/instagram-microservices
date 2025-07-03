using Domain;

namespace Users.Domain.Models;

public class ImageFile : ValueObject
{
	private ImageFile(Guid imageFileId, string url)
	{
		ImageFileId = imageFileId;
		Url = url;
	}

	public Guid ImageFileId { get; private set; }
	public string Url { get; private set; }

	public static ImageFile Create(Guid imageFileId, string url)
	{
		return new ImageFile(imageFileId, url);
	}
}
namespace Shared.Utilities;

public static class TypeExtensions
{
	public static string GetGenericTypeName(this Type type)
	{
		string typeName;

		if (type.IsGenericType)
		{
			var genericTypes = string.Join(",", type.GetGenericArguments().Select(t => t.Name));
			typeName = $"{type.Name.Remove(type.Name.IndexOf('`'))}<{genericTypes}>";
		}
		else
		{
			typeName = type.Name;
		}

		return typeName;
	}
}
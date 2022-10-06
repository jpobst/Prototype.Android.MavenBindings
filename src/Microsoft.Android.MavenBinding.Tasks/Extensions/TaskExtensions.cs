using System;
using System.Linq;
using Microsoft.Build.Framework;

namespace Prototype.Android.MavenBinding.Tasks
{
	static class TaskExtensions
	{
		public static T [] OrEmpty<T> (this T []? value)
		{
			return value ?? Enumerable.Empty<T> ().ToArray ();
		}

		public static string GetMetadataOrDefault (this ITaskItem item, string name, string defaultValue)
		{
			var value = item.GetMetadata (name);

			if (string.IsNullOrWhiteSpace (value))
				return defaultValue;

			return value;
		}

		public static string? GetRequiredMetadata (this ITaskItem item, string name, LogWrapper log)
		{
			var value = item.GetMetadata (name);

			if (string.IsNullOrWhiteSpace (value)) {
				log.LogError ("Item is missing required metadata '{0}'", name);
				return null;
			}

			return value;
		}

		public static bool HasMetadata (this ITaskItem item, string name)
			=> item.MetadataNames.OfType<string> ().Contains (name, StringComparer.OrdinalIgnoreCase);
	}
}

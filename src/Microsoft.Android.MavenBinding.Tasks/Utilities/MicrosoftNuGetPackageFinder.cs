using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Prototype.Android.MavenBinding.Tasks;

namespace XamPrototype.Android.MavenBinding.Tasks
{
	class MicrosoftNuGetPackageFinder
	{
		PackageListFile? package_list;

		public MicrosoftNuGetPackageFinder (string mavenCacheDir, LogWrapper log)
		{
			var file = Path.Combine (mavenCacheDir, "microsoft-packages.json");

			if (!File.Exists (file)) {
				log.LogMessage ("'microsoft-packages.json' file not found, Android NuGet suggestions will not be provided");
				return;
			}

			try {
				var json = File.ReadAllText (file);
				package_list = JsonConvert.DeserializeObject<PackageListFile> (json);
			} catch (Exception ex) {
				log.LogMessage ("There was an error reading 'microsoft-packages.json', Android NuGet suggestions will not be provided: {0}", ex);
			}
		}

		public string? GetNuGetPackage (string javaId)
		{
			return package_list?.Packages?.FirstOrDefault (p => p.JavaId?.Equals (javaId, StringComparison.OrdinalIgnoreCase) == true)?.NuGetId;
		}

		public class PackageListFile
		{
			[JsonProperty ("packages")]
			public List<Package>? Packages { get; set; }
		}

		public class Package
		{
			[JsonProperty ("javaId")]
			public string? JavaId { get; set; }

			[JsonProperty ("nugetId")]
			public string? NuGetId { get; set; }
		}

	}
}

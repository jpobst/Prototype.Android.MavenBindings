using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MavenNet.Models;
using NuGet.ProjectModel;

namespace Prototype.Android.MavenBinding.Tasks
{
	public class NuGetPackageVersionFinder
	{
		LockFile lock_file;
		Dictionary<string, Artifact> cache = new Dictionary<string, Artifact> ();
		Regex tag = new Regex ("artifact_versioned=(?<GroupId>.+)?:(?<ArtifactId>.+?):(?<Version>.+)\\s?", RegexOptions.Compiled);
		Regex tag2 = new Regex ("artifact=(?<GroupId>.+)?:(?<ArtifactId>.+?):(?<Version>.+)\\s?", RegexOptions.Compiled);

		NuGetPackageVersionFinder (LockFile lockFile)
		{
			lock_file = lockFile;
		}

		public static NuGetPackageVersionFinder? Create (string filename, LogWrapper log)
		{
			try {
				var lock_file_format = new LockFileFormat ();
				var lock_file = lock_file_format.Read (filename);
				return new NuGetPackageVersionFinder (lock_file);
			} catch (Exception e) {
				log.LogError (e.Message);
				return null;
			}
		}

		public Artifact? GetJavaInformation (string library, string version, LogWrapper log)
		{
			// Check if we already have this one in the cache
			var dictionary_key = $"{library.ToLowerInvariant ()}:{version}";

			if (cache.TryGetValue (dictionary_key, out var artifact))
				return artifact;

			// Find the LockFileLibrary
			var nuget = lock_file.GetLibrary (library, new NuGet.Versioning.NuGetVersion (version));

			if (nuget is null) {
				log.LogError ("Could not find NuGet package '{0}' version '{1}' in lock file. Ensure NuGet Restore has run since this <PackageReference> was added.", library, version);
				return null;
			}

			foreach (var path in lock_file.PackageFolders)
				if (CheckFilePath (path.Path, nuget) is Artifact art) {
					cache.Add (dictionary_key, art);
					return art;
				}

			return null;
		}

		Artifact? CheckFilePath (string nugetPackagePath, LockFileLibrary package)
		{
			// Check NuGet tags
			var nuspec = package.Files.FirstOrDefault (f => f.EndsWith (".nuspec", StringComparison.OrdinalIgnoreCase));

			if (nuspec is null)
				return null;

			nuspec = Path.Combine (nugetPackagePath, package.Path, nuspec);

			if (!File.Exists (nuspec))
				return null;

			var reader = new NuGet.Packaging.NuspecReader (nuspec);
			var tags = reader.GetTags ();

			// Try the first tag format
			var match = tag.Match (tags);

			// Try the second tag format
			if (!match.Success)
				match = tag2.Match (tags);

			if (!match.Success)
				return null;

			// TODO: Define a well-known file that can be included in the package like "java-package.txt"

			return new Artifact (match.Groups ["GroupId"].Value, match.Groups ["ArtifactId"].Value, match.Groups ["Version"].Value);
		}
	}
}

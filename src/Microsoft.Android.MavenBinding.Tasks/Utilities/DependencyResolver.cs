using System.Collections.Generic;
using System.IO;
using System.Linq;
using MavenNet.Models;
using Microsoft.Build.Framework;
using XamPrototype.Android.MavenBinding.Tasks;

namespace Prototype.Android.MavenBinding.Tasks
{
	class DependencyResolver
	{
		public List<Artifact> artifacts = new List<Artifact> ();

		NuGetPackageVersionFinder? finder;

		public DependencyResolver (string? lockFile, LogWrapper log)
		{
			if (File.Exists (lockFile))
				finder = NuGetPackageVersionFinder.Create (lockFile!, log);
		}

		public bool IsDependencySatisfied (Dependency dependency, MicrosoftNuGetPackageFinder packages, LogWrapper log)
		{
			if (!dependency.Version.HasValue ()) {
				log.LogWarning ("Could not determine needed version of Maven dependency '{0}:{1}' (possibly due to not understanding a parent POM). Validation of this dependency will be skipped, but it still needs to be fulfilled.", dependency.GroupId, dependency.ArtifactId);
				return true;
			}

			var dep_versions = MavenVersionRange.Parse (dependency.Version);

			// TODO: Various fixups / parent POM 
			var satisfied = artifacts.Any (a =>
				a.GroupId == dependency.GroupId
				&& a.Id == dependency.ArtifactId
				&& dep_versions.Any (r => r.ContainsVersion (MavenVersion.Parse (a.Versions.First ())))
			);

			if (!satisfied) {
				if (packages.GetNuGetPackage ($"{dependency.GroupId}:{dependency.ArtifactId}") is string nuget)
					log.LogError ("Maven dependency '{0}:{1}' version '{2}' is not satisfied. Microsoft maintains the NuGet package '{3}' that could fulfill this dependency.", dependency.GroupId, dependency.ArtifactId, dependency.Version, nuget);
				else
					log.LogError ("Maven dependency '{0}:{1}' version '{2}' is not satisfied.", dependency.GroupId, dependency.ArtifactId, dependency.Version);
			}

			return satisfied;
		}

		public void AddMavenAndroidLibraries (ITaskItem []? tasks, LogWrapper log)
		{
			foreach (var task in tasks.OrEmpty ()) {
				var id = task.GetMetadataOrDefault ("ArtifactSpec", task.ItemSpec);
				var version = task.GetRequiredMetadata ("Version", log);

				if (version is null)
					continue;

				if (version != null && MavenExtensions.ParseArtifact (id, version, log) is Artifact art) {
					log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from AndroidMavenLibrary '{3}'", art.GroupId, art.Id, art.Versions.FirstOrDefault (), task.ItemSpec);
					artifacts.Add (art);
				}
			}
		}

		public void AddPackageReferences (ITaskItem []? tasks, LogWrapper log)
		{
			foreach (var task in tasks.OrEmpty ()) {

				// See if JavaArtifact/JavaVersion overrides were used
				if (TryParseJavaArtifactAndVersion ("PackageReference", task, log))
					continue;

				// Try parsing the NuGet metadata for Java version information instead
				var artifact = finder?.GetJavaInformation (task.ItemSpec, task.GetMetadataOrDefault ("Version", string.Empty), log);

				if (artifact != null) {
					log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from PackageReference '{3}'", artifact.GroupId, artifact.Id, artifact.Versions.FirstOrDefault (), task.ItemSpec);
					artifacts.Add (artifact);

					continue;
				}

				log.LogMessage ("No Java artifact information found for PackageReference '{0}'", task.ItemSpec);
			}
		}

		public void AddProjectReferences (ITaskItem []? tasks, LogWrapper log)
		{
			foreach (var task in tasks.OrEmpty ()) {
				// See if JavaArtifact/JavaVersion overrides were used
				if (TryParseJavaArtifactAndVersion ("ProjectReference", task, log))
					continue;

				// There currently is no alternate way to figure this out. Perhaps in
				// the future we could somehow parse the project to find it automatically?
			}
		}

		// "type" is PackageReference or ProjectReference
		// Returns "true" if JavaArtifact/JavaVersion is used, even if it was used incorrectly and is useless.
		// This is so the caller will know to try alternate methods if neither JavaArtifact or JavaVersion were specified.
		bool TryParseJavaArtifactAndVersion (string type, ITaskItem task, LogWrapper log)
		{
			var item_name = task.ItemSpec;

			// Convert "../../src/blah/Blah.csproj" to "Blah.csproj"
			if (type == "ProjectReference")
				item_name = Path.GetFileName (item_name);

			var has_artifact = task.HasMetadata ("JavaArtifact");
			var has_version = task.HasMetadata ("JavaVersion");

			if (has_artifact && !has_version) {
				log.LogError ("'JavaVersion' is required when using 'JavaArtifact' for {0} '{1}'.", type, item_name);
				return true;
			}

			if (!has_artifact && has_version) {
				log.LogError ("'JavaArtifact' is required when using 'JavaVersion' for {0} '{1}'.", type, item_name);
				return true;
			}

			if (has_artifact && has_version) {
				var id = task.GetMetadata ("JavaArtifact");
				var version = task.GetMetadata ("JavaVersion");

				if (string.IsNullOrWhiteSpace (id)) {
					log.LogError ("'JavaArtifact' cannot be empty for {0} '{1}'.", type, item_name);
					return true;
				}

				if (string.IsNullOrWhiteSpace (version)) {
					log.LogError ("'JavaVersion' cannot be empty for {0} '{1}'.", type, item_name);
					return true;
				}

				if (MavenExtensions.ParseArtifact (id, version, log) is Artifact art) {
					log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from {3} '{4}' (JavaArtifact)", art.GroupId, art.Id, art.Versions.FirstOrDefault (), type, item_name);
					artifacts.Add (art);
				}

				return true;
			}

			return false;
		}

		public void AddIgnoredDependency (ITaskItem []? tasks, LogWrapper log)
		{
			foreach (var task in tasks.OrEmpty ()) {
				var id = task.ItemSpec;
				var version = task.GetRequiredMetadata ("Version", log);

				if (version is null)
					continue;

				if (version != null && MavenExtensions.ParseArtifact (id, version, log) is Artifact art) {
					log.LogMessage ("Ignoring Java dependency '{0}:{1}' version '{2}'", art.GroupId, art.Id, art.Versions.FirstOrDefault ());
					artifacts.Add (art);
				}
			}
		}
	}
}

using System.Linq;
using MavenNet.Models;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Prototype.Android.MavenBinding.Tasks
{
	public class MavenDependencyVerifierTask : Task
	{
		public ITaskItem []? ResolvedAndroidMavenLibraries { get; set; }
		public ITaskItem []? ResolvedAndroidMavenParentLibraries { get; set; }
		public ITaskItem []? PackageReferences { get; set; }
		public ITaskItem []? ProjectReferences { get; set; }
		public ITaskItem []? IgnoredMavenDependencies { get; set; }
		[Required]
		public string ProjectAssetsLockFile { get; set; } = null!;
		public LogWrapper? Logger { get; set; }

		public override bool Execute ()
		{
			// Build a list of every available dependency we know about
			var log = Logger ??= new MSBuildLogWrapper (Log);
			var resolver = new DependencyResolver (ProjectAssetsLockFile, log);

			resolver.AddMavenAndroidLibraries (ResolvedAndroidMavenLibraries, log);
			resolver.AddPackageReferences (PackageReferences, log);
			resolver.AddProjectReferences (ProjectReferences, log);
			resolver.AddIgnoredDependency (IgnoredMavenDependencies, log);

			// Read POM files so we know which dependencies must be satisfied
			foreach (var library in ResolvedAndroidMavenLibraries.OrEmpty ()) {
				// Parse POM file
				var pom_file = library.GetRequiredMetadata ("ArtifactPom", log);

				if (pom_file is null)
					continue;

				var pom = MavenExtensions.ParsePom (pom_file);
				Project? parent_pom = null;

				// Load up the parent POM if needed
				if (pom.GetParentPom () is Artifact parent_artifact) {
					var parent_id = $"{parent_artifact.GroupId}:{parent_artifact.Id}";
					var parent = ResolvedAndroidMavenParentLibraries.OrEmpty ().FirstOrDefault (i =>
						i.ItemSpec.Equals (parent_id, System.StringComparison.OrdinalIgnoreCase) &&
						i.GetMetadataOrDefault ("Version", "-1").Equals (parent_artifact.Versions.FirstOrDefault (), System.StringComparison.Ordinal));

					if (parent is null) {
						log.LogMessage ("Could not find needed parent POM task item: '{0}' version '{1}'.", parent_id, parent_artifact.Versions.FirstOrDefault ());
					} else {
						var parent_file = parent.GetRequiredMetadata ("ArtifactPom", log);

						if (parent_file != null)
							parent_pom = MavenExtensions.ParsePom (parent_file);
					}
				}

				// For each dependency
				foreach (var dependency in pom.Dependencies) {

					// We only care about 'compile' and 'runtime' dependencies
					if (!dependency.IsCompileDependency () && !dependency.IsRuntimeDependency ())
						continue;

					// Apply various fixups to our dependencies
					MavenExtensions.FixDependency (pom, parent_pom, dependency);

					// ..see if it fulfilled
					resolver.IsDependencySatisfied (dependency, log);
				}
			}

			return !log.HasLoggedErrors;
		}
	}
}

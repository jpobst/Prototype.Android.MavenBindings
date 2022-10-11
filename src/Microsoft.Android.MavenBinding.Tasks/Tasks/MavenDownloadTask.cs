using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MavenNet;
using MavenNet.Models;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.LibraryModel;

namespace Prototype.Android.MavenBinding.Tasks
{
	public class MavenDownloadTask : Task
	{
		/// <summary>
		/// The cache directory to use for Maven artifacts.
		/// </summary>
		[Required]
		public string MavenCacheDirectory { get; set; } = null!; // NRT enforced by [Required]

		/// <summary>
		/// The set of input Maven libraries that we need to download.
		/// </summary>
		public ITaskItem []? AndroidMavenLibraries { get; set; }

		/// <summary>
		/// The set of requested Maven libraries that we were able to successfully download.
		/// </summary>
		[Output]
		public ITaskItem []? ResolvedAndroidMavenLibraries { get; set; }

		/// <summary>
		/// Any "parent POMs" we needed to download that will be needed for dependency verification.
		/// </summary>
		[Output]
		public ITaskItem []? ResolvedAndroidMavenParentLibraries { get; set; }

		/// <summary>
		/// This is a hack for unit tests.
		/// </summary>
		public LogWrapper? Logger { get; set; }

		public override bool Execute ()
		{
			return ExecuteAsync ().GetAwaiter ().GetResult ();
		}

		async System.Threading.Tasks.Task<bool> ExecuteAsync ()
		{
			var log = Logger ??= new MSBuildLogWrapper (Log);

			var resolved = new List<ITaskItem> ();

			foreach (var library in AndroidMavenLibraries.OrEmpty ()) {

				// Validate artifact
				var id = library.ItemSpec;
				var version = library.GetRequiredMetadata ("Version", log);

				if (version is null)
					continue;

				var artifact = MavenExtensions.ParseArtifact (id, version, log);

				if (artifact is null)
					continue;

				// Create resolved TaskItem
				var result = new TaskItem (library.ItemSpec);

				// Check for local files
				if (TryGetLocalFiles (library, result, log)) {
					library.CopyMetadataTo (result);
					resolved.Add (result);
					continue;
				}

				// Check for repository files
				if (await TryGetRepositoryFiles (artifact, library, result, log)) {
					library.CopyMetadataTo (result);
					resolved.Add (result);
					continue;
				}
			}

			ResolvedAndroidMavenLibraries = resolved.ToArray ();

			// Check for any needed parent POM files
			var parent_poms = new List<ITaskItem> ();

			foreach (var library in ResolvedAndroidMavenLibraries) {
				if (await TryGetParentPom (library, log) is TaskItem parent_pom)
					parent_poms.Add (parent_pom);
			}

			ResolvedAndroidMavenParentLibraries = parent_poms.ToArray ();

			return !log.HasLoggedErrors;
		}

		bool TryGetLocalFiles (ITaskItem item, TaskItem result, LogWrapper log)
		{
			var type = item.GetMetadataOrDefault ("Repository", "Central");

			if (type.ToLowerInvariant () == "file") {
				var artifact_file = item.GetMetadataOrDefault ("PackageFile", "");
				var pom_file = item.GetMetadataOrDefault ("PomFile", "");

				if (!artifact_file.HasValue () || !pom_file.HasValue ()) {
					log.LogError ("'PackageFile' and 'PomFile' must be specified when using a 'File' repository.");
					return false;
				}

				if (!File.Exists (artifact_file)) {
					log.LogError ("Specified package file '{0}' does not exist.", artifact_file);
					return false;
				}

				if (!File.Exists (pom_file)) {
					log.LogError ("Specified pom file '{0}' does not exist.", pom_file);
					return false;
				}

				result.SetMetadata ("ArtifactFile", artifact_file);
				result.SetMetadata ("ArtifactPom", pom_file);

				return true;
			}

			return false;
		}

		async System.Threading.Tasks.Task<bool> TryGetRepositoryFiles (Artifact artifact, ITaskItem item, TaskItem result, LogWrapper log)
		{
			// Initialize repo
			var repository = GetRepository (item);

			if (repository is null)
				return false;

			artifact.SetRepository (repository);

			// Download artifact
			var artifact_file = await MavenExtensions.DownloadPayload (artifact, MavenCacheDirectory, log);

			if (artifact_file is null)
				return false;

			// Download POM
			var pom_file = await MavenExtensions.DownloadPom (artifact, MavenCacheDirectory, log);

			if (pom_file is null)
				return false;

			result.SetMetadata ("ArtifactFile", artifact_file);
			result.SetMetadata ("ArtifactPom", pom_file);

			return true;
		}

		async System.Threading.Tasks.Task<TaskItem?> TryGetParentPom (ITaskItem item, LogWrapper log)
		{
			var child_pom_file = item.GetRequiredMetadata ("ArtifactPom", log);

			// Shouldn't be possible because we just created this items
			if (child_pom_file is null)
				return null;

			// No parent POM needed
			if (!(MavenExtensions.CheckForNeededParentPom (child_pom_file) is Artifact artifact))
				return null;

			// Initialize repo (parent will be in same repository as child)
			var repository = GetRepository (item);

			if (repository is null)
				return null;

			artifact.SetRepository (repository);

			// Download POM
			var pom_file = await MavenExtensions.DownloadPom (artifact, MavenCacheDirectory, log);

			if (pom_file is null)
				return null;

			var result = new TaskItem ($"{artifact.GroupId}:{artifact.Id}");

			result.SetMetadata ("Version", artifact.Versions.FirstOrDefault ());
			result.SetMetadata ("ArtifactPom", pom_file);

			// Copy repository data
			item.CopyMetadataTo (result);

			return result;
		}

		MavenRepository? GetRepository (ITaskItem item)
		{
			var type = item.GetMetadataOrDefault ("Repository", "Central");

			var repo = type.ToLowerInvariant () switch {
				"central" => MavenRepository.FromMavenCentral (),
				"google" => MavenRepository.FromGoogle (),
				_ => (MavenRepository?) null
			};

			if (repo is null && type.StartsWith ("http", StringComparison.OrdinalIgnoreCase))
				repo = MavenRepository.FromUrl (type);

			if (repo is null)
				Log.LogError ("Unknown Maven repository: '{0}'.", type);

			return repo;
		}
	}
}

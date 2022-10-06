using System;
using System.Collections.Generic;
using System.IO;
using MavenNet;
using MavenNet.Models;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Prototype.Android.MavenBinding.Tasks
{
	public class MavenDownloadTask : Task
	{
		[Required]
		public string MavenCacheDirectory { get; set; } = null!; // NRT enforced by [Required]

		public ITaskItem []? AndroidMavenLibraries { get; set; }

		[Output]
		public ITaskItem []? ResolvedAndroidMavenLibraries { get; set; }

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

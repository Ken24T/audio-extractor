#![forbid(unsafe_code)]

//! Platform-facing helpers for audio-extractor.

use std::fs;
use std::path::PathBuf;

use directories::ProjectDirs;
use extractor_domain::{ApplicationSettings, DomainResult, ExtractionError};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PlatformPaths {
	pub config_dir: PathBuf,
	pub settings_path: PathBuf,
}

pub fn platform_paths() -> DomainResult<PlatformPaths> {
	let project_dirs = ProjectDirs::from("dev", "audio-extractor", "audio-extractor")
		.ok_or_else(|| ExtractionError::dependency("unable to determine config directory"))?;
	let config_dir = project_dirs.config_dir().to_path_buf();

	Ok(PlatformPaths {
		settings_path: config_dir.join("settings.toml"),
		config_dir,
	})
}

pub fn load_settings() -> DomainResult<ApplicationSettings> {
	let paths = platform_paths()?;
	if !paths.settings_path.exists() {
		return Ok(ApplicationSettings::default());
	}

	let content = fs::read_to_string(&paths.settings_path)
		.map_err(|error| ExtractionError::runtime(format!("Failed to read settings: {error}")))?;
	toml::from_str(&content)
		.map_err(|error| ExtractionError::runtime(format!("Failed to parse settings: {error}")))
}

pub fn save_settings(_settings: &ApplicationSettings) -> DomainResult<()> {
	let paths = platform_paths()?;
	fs::create_dir_all(&paths.config_dir)
		.map_err(|error| ExtractionError::runtime(format!("Failed to create config directory: {error}")))?;
	let rendered = toml::to_string_pretty(_settings)
		.map_err(|error| ExtractionError::runtime(format!("Failed to serialise settings: {error}")))?;
	fs::write(&paths.settings_path, rendered)
		.map_err(|error| ExtractionError::runtime(format!("Failed to write settings: {error}")))
}

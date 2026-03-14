#![forbid(unsafe_code)]

//! ffmpeg and ffprobe integration layer for audio-extractor.

use std::env;
use std::ffi::OsString;
use std::path::{Path, PathBuf};
use std::process::Command;

use extractor_domain::{
	build_extraction_plan, default_output_path, render_command, validate_request, DomainResult,
	ExtractionError, ExtractionPlan, ExtractionRequest, ExtractionResult,
};

#[derive(Debug, Default)]
pub struct FfmpegRunner;

#[derive(Debug, Clone, PartialEq)]
pub struct BinaryPaths {
	pub ffmpeg: PathBuf,
	pub ffprobe: Option<PathBuf>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ProbeResult {
	pub duration_seconds: f64,
}

impl FfmpegRunner {
	pub fn new() -> Self {
		Self
	}

	pub fn run(&self, request: &ExtractionRequest) -> DomainResult<ExtractionResult> {
		let validated = validate_request(request)?;
		let binaries = resolve_binary_paths(validated.ffmpeg_path.as_deref())?;

		if let Some(probe) = probe_input_duration(&binaries, &validated.input_path)? {
			enforce_duration_guards(&validated, probe.duration_seconds)?;
		}

		let output_path = if validated.overwrite {
			default_output_path(&validated)
		} else {
			non_clobber_path(default_output_path(&validated))
		};

		let plan = build_extraction_plan(&validated, output_path.clone())?;
		run_plan(&binaries.ffmpeg, &plan)?;

		if plan.autoplay {
			let _ = open_in_default_app(&plan.output_path);
		}

		Ok(ExtractionResult::success(output_path))
	}
}

pub fn resolve_binary_paths(provided_ffmpeg_path: Option<&Path>) -> DomainResult<BinaryPaths> {
	let ffmpeg = match provided_ffmpeg_path {
		Some(path) => {
			if path.exists() {
				path.to_path_buf()
			} else {
				return Err(ExtractionError::dependency(format!(
					"ffmpeg not found: {}",
					path.display()
				)));
			}
		}
		None => find_on_path(binary_candidates("ffmpeg"))
			.ok_or_else(|| ExtractionError::dependency("ffmpeg not in PATH. Install or configure it.".to_string()))?,
	};

	let ffprobe = match provided_ffmpeg_path {
		Some(path) => {
			let sibling = path.parent().map(|dir| dir.join(executable_name("ffprobe")));
			sibling.filter(|candidate| candidate.exists()).or_else(|| find_on_path(binary_candidates("ffprobe")))
		}
		None => find_on_path(binary_candidates("ffprobe")),
	};

	Ok(BinaryPaths { ffmpeg, ffprobe })
}

pub fn probe_input_duration(binaries: &BinaryPaths, input_path: &Path) -> DomainResult<Option<ProbeResult>> {
	let Some(ffprobe) = &binaries.ffprobe else {
		return Ok(None);
	};

	let output = Command::new(ffprobe)
		.arg("-v")
		.arg("error")
		.arg("-show_entries")
		.arg("format=duration")
		.arg("-of")
		.arg("default=noprint_wrappers=1:nokey=1")
		.arg(input_path)
		.output();

	let Ok(output) = output else {
		return Ok(None);
	};

	if !output.status.success() {
		return Ok(None);
	}

	let rendered = String::from_utf8_lossy(&output.stdout);
	let Ok(duration_seconds) = rendered.trim().parse::<f64>() else {
		return Ok(None);
	};

	Ok(Some(ProbeResult { duration_seconds }))
}

pub fn enforce_duration_guards(
	request: &extractor_domain::ValidatedExtractionRequest,
	media_duration_seconds: f64,
) -> DomainResult<()> {
	const EPSILON: f64 = 0.0001;

	if let Some(start_seconds) = request.start_seconds {
		if start_seconds - media_duration_seconds > EPSILON {
			return Err(ExtractionError::validation("Start time exceeds input duration."));
		}
	}

	if let Some(end_seconds) = request.end_seconds {
		if end_seconds - media_duration_seconds > EPSILON {
			return Err(ExtractionError::validation("End time exceeds input duration."));
		}
	}

	if let (Some(start_seconds), Some(duration_seconds)) = (request.start_seconds, request.duration_seconds) {
		if (start_seconds + duration_seconds) - media_duration_seconds > EPSILON {
			return Err(ExtractionError::validation(
				"Start time + duration exceeds input duration.",
			));
		}
	}

	Ok(())
}

pub fn non_clobber_path(path: PathBuf) -> PathBuf {
	if !path.exists() {
		return path;
	}

	let directory = path.parent().unwrap_or_else(|| Path::new(".")).to_path_buf();
	let stem = path.file_stem().and_then(|value| value.to_str()).unwrap_or("output");
	let extension = path.extension().and_then(|value| value.to_str()).unwrap_or_default();

	for index in 1..=9_999 {
		let candidate = if extension.is_empty() {
			directory.join(format!("{stem}_{index:03}"))
		} else {
			directory.join(format!("{stem}_{index:03}.{extension}"))
		};

		if !candidate.exists() {
			return candidate;
		}
	}

	let timestamp = format!(
		"{}",
		std::time::SystemTime::now()
			.duration_since(std::time::UNIX_EPOCH)
			.map(|duration| duration.as_secs())
			.unwrap_or_default()
	);

	if extension.is_empty() {
		directory.join(format!("{stem}_{timestamp}"))
	} else {
		directory.join(format!("{stem}_{timestamp}.{extension}"))
	}
}

pub fn run_plan(ffmpeg: &Path, plan: &ExtractionPlan) -> DomainResult<()> {
	if plan.verbose {
		println!("Running ffmpeg:");
		println!("{}", render_command(&ffmpeg.display().to_string(), &plan.args));
	}

	let status = Command::new(ffmpeg)
		.args(plan.args.iter())
		.status()
		.map_err(|error| ExtractionError::runtime(format!("Failed to start ffmpeg: {error}")))?;

	if status.success() {
		Ok(())
	} else {
		Err(ExtractionError::runtime(format!(
			"ffmpeg failed ({})",
			status.code().unwrap_or(-1)
		)))
	}
}

pub fn open_in_default_app(file_path: &Path) -> DomainResult<()> {
	let status = if cfg!(target_os = "windows") {
		Command::new("cmd")
			.arg("/C")
			.arg("start")
			.arg("")
			.arg(file_path)
			.status()
	} else {
		Command::new("xdg-open").arg(file_path).status()
	};

	match status {
		Ok(status) if status.success() => Ok(()),
		Ok(status) => Err(ExtractionError::runtime(format!(
			"Could not open file in default app ({})",
			status.code().unwrap_or(-1)
		))),
		Err(error) => Err(ExtractionError::runtime(format!(
			"Could not open file in default app: {error}"
		))),
	}
}

fn binary_candidates(base: &str) -> Vec<OsString> {
	if cfg!(target_os = "windows") {
		vec![OsString::from(format!("{base}.exe")), OsString::from(base)]
	} else {
		vec![OsString::from(base)]
	}
}

fn executable_name(base: &str) -> String {
	if cfg!(target_os = "windows") {
		format!("{base}.exe")
	} else {
		base.to_string()
	}
}

fn find_on_path(candidates: Vec<OsString>) -> Option<PathBuf> {
	let path_env = env::var_os("PATH")?;

	for directory in env::split_paths(&path_env) {
		for candidate in &candidates {
			let path = directory.join(candidate);
			if path.exists() {
				return Some(path);
			}
		}
	}

	None
}

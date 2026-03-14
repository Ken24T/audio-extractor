#![forbid(unsafe_code)]

//! Domain layer for audio-extractor.

use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const DEFAULT_TTS_SAMPLE_RATE: u32 = 24_000;
pub const DEFAULT_TTS_HIGHPASS_HZ: u32 = 80;
pub const DEFAULT_TTS_LOWPASS_HZ: u32 = 11_000;
pub const DEFAULT_TARGET_LUFS: i32 = -16;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct TimingOptions {
	pub start: Option<String>,
	pub end: Option<String>,
	pub duration: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct OutputOptions {
	pub output_path: Option<PathBuf>,
	pub sample_rate: Option<u32>,
	pub channels: Option<u8>,
	pub overwrite: bool,
	pub autoplay: bool,
	pub verbose: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct TtsOptions {
	pub sample_rate: u32,
	pub highpass_hz: u32,
	pub lowpass_hz: u32,
	pub target_lufs: i32,
}

impl Default for TtsOptions {
	fn default() -> Self {
		Self {
			sample_rate: DEFAULT_TTS_SAMPLE_RATE,
			highpass_hz: DEFAULT_TTS_HIGHPASS_HZ,
			lowpass_hz: DEFAULT_TTS_LOWPASS_HZ,
			target_lufs: DEFAULT_TARGET_LUFS,
		}
	}
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
pub enum ProcessingProfile {
	#[default]
	TextToSpeech,
	PreserveInput,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ExtractionRequest {
	pub input_path: PathBuf,
	pub timing: TimingOptions,
	pub output: OutputOptions,
	pub ffmpeg_path: Option<PathBuf>,
	pub processing_profile: ProcessingProfile,
	pub tts: TtsOptions,
}

impl ExtractionRequest {
	pub fn new(input_path: impl Into<PathBuf>) -> Self {
		Self {
			input_path: input_path.into(),
			timing: TimingOptions::default(),
			output: OutputOptions::default(),
			ffmpeg_path: None,
			processing_profile: ProcessingProfile::default(),
			tts: TtsOptions::default(),
		}
	}
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Default)]
pub struct ExtractionResult {
	pub output_path: Option<PathBuf>,
	pub warnings: Vec<String>,
}

impl ExtractionResult {
	pub fn success(output_path: impl Into<PathBuf>) -> Self {
		Self {
			output_path: Some(output_path.into()),
			warnings: Vec::new(),
		}
	}
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct WindowPlacement {
	pub left: f64,
	pub top: f64,
	pub width: f64,
	pub height: f64,
	pub maximized: bool,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct ApplicationSettings {
	pub ffmpeg_path: Option<PathBuf>,
	pub window_placement: Option<WindowPlacement>,
}

#[derive(Debug, Error, Clone, PartialEq, Eq)]
pub enum ExtractionError {
	#[error("validation error: {0}")]
	Validation(String),
	#[error("dependency error: {0}")]
	Dependency(String),
	#[error("runtime error: {0}")]
	Runtime(String),
	#[error("{0}")]
	NotImplemented(&'static str),
}

impl ExtractionError {
	pub fn validation(message: impl Into<String>) -> Self {
		Self::Validation(message.into())
	}

	pub fn dependency(message: impl Into<String>) -> Self {
		Self::Dependency(message.into())
	}

	pub fn runtime(message: impl Into<String>) -> Self {
		Self::Runtime(message.into())
	}

	pub fn not_implemented(message: &'static str) -> Self {
		Self::NotImplemented(message)
	}
}

pub type DomainResult<T> = Result<T, ExtractionError>;

#[derive(Debug, Clone, PartialEq)]
pub struct ValidatedExtractionRequest {
	pub input_path: PathBuf,
	pub requested_output_path: Option<PathBuf>,
	pub start: Option<String>,
	pub end: Option<String>,
	pub duration: Option<String>,
	pub start_seconds: Option<f64>,
	pub end_seconds: Option<f64>,
	pub duration_seconds: Option<f64>,
	pub sample_rate: Option<u32>,
	pub channels: Option<u8>,
	pub ffmpeg_path: Option<PathBuf>,
	pub processing_profile: ProcessingProfile,
	pub tts: TtsOptions,
	pub overwrite: bool,
	pub autoplay: bool,
	pub verbose: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ExtractionPlan {
	pub output_path: PathBuf,
	pub args: Vec<String>,
	pub autoplay: bool,
	pub verbose: bool,
}

pub fn validate_request(request: &ExtractionRequest) -> DomainResult<ValidatedExtractionRequest> {
	if request.input_path.as_os_str().is_empty() {
		return Err(ExtractionError::validation("Input file is required."));
	}

	if !request.input_path.exists() {
		return Err(ExtractionError::validation(format!(
			"Input file not found: {}",
			request.input_path.display()
		)));
	}

	if let Some(channels) = request.output.channels {
		if channels != 1 && channels != 2 {
			return Err(ExtractionError::validation("Channels must be 1 or 2."));
		}
	}

	if request.timing.end.is_some() && request.timing.duration.is_some() {
		return Err(ExtractionError::validation(
			"Use either --end OR --duration (not both).",
		));
	}

	if (request.timing.end.is_some() || request.timing.duration.is_some())
		&& request.timing.start.is_none()
	{
		return Err(ExtractionError::validation(
			"Start time required when using --end/--duration.",
		));
	}

	let start_seconds = parse_time_to_seconds(request.timing.start.as_deref())?;
	let end_seconds = parse_time_to_seconds(request.timing.end.as_deref())?;
	let duration_seconds = parse_time_to_seconds(request.timing.duration.as_deref())?;

	if let Some(duration_seconds) = duration_seconds {
		if duration_seconds <= 0.0 {
			return Err(ExtractionError::validation("Duration must be > 0"));
		}
	}

	if let (Some(start_seconds), Some(end_seconds)) = (start_seconds, end_seconds) {
		if end_seconds <= start_seconds {
			return Err(ExtractionError::validation(format!(
				"End time ({}) must be AFTER Start time ({}).",
				request.timing.end.as_deref().unwrap_or_default(),
				request.timing.start.as_deref().unwrap_or_default()
			)));
		}
	}

	Ok(ValidatedExtractionRequest {
		input_path: request.input_path.clone(),
		requested_output_path: request.output.output_path.clone(),
		start: request.timing.start.clone(),
		end: request.timing.end.clone(),
		duration: request.timing.duration.clone(),
		start_seconds,
		end_seconds,
		duration_seconds,
		sample_rate: request.output.sample_rate,
		channels: request.output.channels,
		ffmpeg_path: request.ffmpeg_path.clone(),
		processing_profile: request.processing_profile,
		tts: request.tts.clone(),
		overwrite: request.output.overwrite,
		autoplay: request.output.autoplay,
		verbose: request.output.verbose,
	})
}

pub fn build_extraction_plan(
	request: &ValidatedExtractionRequest,
	output_path: PathBuf,
) -> DomainResult<ExtractionPlan> {
	let mut args = vec![
		"-hide_banner".to_string(),
		"-loglevel".to_string(),
		"warning".to_string(),
	];

	if let Some(start) = &request.start {
		args.push("-ss".to_string());
		args.push(start.clone());
	}

	args.push("-i".to_string());
	args.push(request.input_path.display().to_string());

	if let Some(duration) = &request.duration {
		args.push("-t".to_string());
		args.push(duration.clone());
	} else if let (Some(end_seconds), Some(start_seconds)) = (request.end_seconds, request.start_seconds) {
		let calculated_duration = end_seconds - start_seconds;
		args.push("-t".to_string());
		args.push(format!("{calculated_duration:.3}"));
	}

	args.push("-vn".to_string());
	args.push("-c:a".to_string());
	args.push("pcm_s16le".to_string());

	match request.processing_profile {
		ProcessingProfile::PreserveInput => {
			if let Some(sample_rate) = request.sample_rate {
				args.push("-ar".to_string());
				args.push(sample_rate.to_string());
			}

			if let Some(channels) = request.channels {
				args.push("-ac".to_string());
				args.push(channels.to_string());
			}
		}
		ProcessingProfile::TextToSpeech => {
			let filter = format!(
				"highpass=f={},lowpass=f={},aresample={},loudnorm=I={}:TP=-1.5:LRA=11",
				request.tts.highpass_hz,
				request.tts.lowpass_hz,
				request.tts.sample_rate,
				request.tts.target_lufs,
			);

			args.push("-af".to_string());
			args.push(filter);
			args.push("-ac".to_string());
			args.push("1".to_string());
			args.push("-ar".to_string());
			args.push(request.tts.sample_rate.to_string());
		}
	}

	args.push(if request.overwrite {
		"-y".to_string()
	} else {
		"-n".to_string()
	});
	args.push(output_path.display().to_string());

	Ok(ExtractionPlan {
		output_path,
		args,
		autoplay: request.autoplay,
		verbose: request.verbose,
	})
}

pub fn default_output_path(request: &ValidatedExtractionRequest) -> PathBuf {
	request.requested_output_path.clone().unwrap_or_else(|| {
		build_auto_output_name(
			&request.input_path,
			request.processing_profile,
			request.start.as_deref(),
			request.end.as_deref(),
			request.duration.as_deref(),
		)
	})
}

pub fn render_command(program: &str, args: &[String]) -> String {
	let rendered_args = args.iter().map(|arg| quote_if_needed(arg)).collect::<Vec<_>>();
	format!("\"{}\" {}", program, rendered_args.join(" "))
}

fn quote_if_needed(value: &str) -> String {
	if value.is_empty() || value.contains(' ') || value.contains('"') {
		format!("\"{}\"", value.replace('"', "\\\""))
	} else {
		value.to_string()
	}
}

pub fn parse_time_to_seconds(time_text: Option<&str>) -> DomainResult<Option<f64>> {
	let Some(time_text) = time_text else {
		return Ok(None);
	};

	let trimmed = time_text.trim();
	if trimmed.is_empty() {
		return Ok(None);
	}

	let parts: Vec<_> = trimmed.split(':').collect();
	if parts.len() > 3 {
		return Err(ExtractionError::validation(format!(
			"Invalid time format: {trimmed} (use SS, MM:SS, or HH:MM:SS)",
		)));
	}

	let (hours, minutes, seconds) = match parts.as_slice() {
		[seconds_part] => (0, 0, parse_seconds_part(seconds_part, trimmed)?),
		[minutes_part, seconds_part] => (
			0,
			parse_int_part(minutes_part, trimmed)?,
			parse_seconds_part(seconds_part, trimmed)?,
		),
		[hours_part, minutes_part, seconds_part] => (
			parse_int_part(hours_part, trimmed)?,
			parse_int_part(minutes_part, trimmed)?,
			parse_seconds_part(seconds_part, trimmed)?,
		),
		_ => unreachable!("split always returns at least one segment"),
	};

	Ok(Some((hours as f64 * 3600.0) + (minutes as f64 * 60.0) + seconds))
}

pub fn build_auto_output_name(
	input_path: impl AsRef<Path>,
	processing_profile: ProcessingProfile,
	start: Option<&str>,
	end: Option<&str>,
	duration: Option<&str>,
) -> PathBuf {
	let input_path = input_path.as_ref();
	let base_name = input_path
		.file_stem()
		.and_then(|value| value.to_str())
		.unwrap_or("output");
	let directory = input_path
		.parent()
		.filter(|path| !path.as_os_str().is_empty())
		.unwrap_or_else(|| Path::new("."));
	let mode = match processing_profile {
		ProcessingProfile::TextToSpeech => "_tts",
		ProcessingProfile::PreserveInput => "_out",
	};

	let mut tags = Vec::new();
	if let Some(start) = start.filter(|value| !value.trim().is_empty()) {
		tags.push(format!("s{}", to_file_time_token(start)));
	}
	if let Some(end) = end.filter(|value| !value.trim().is_empty()) {
		tags.push(format!("e{}", to_file_time_token(end)));
	}
	if let Some(duration) = duration.filter(|value| !value.trim().is_empty()) {
		tags.push(format!("d{}", to_file_time_token(duration)));
	}

	let tag_segment = if tags.is_empty() {
		String::new()
	} else {
		format!("_{}", tags.join("_"))
	};

	directory.join(format!("{base_name}{mode}{tag_segment}.wav"))
}

pub fn to_file_time_token(time_text: &str) -> String {
	time_text.trim().replace(':', "-").replace(' ', "")
}

fn parse_int_part(part: &str, original_text: &str) -> DomainResult<i64> {
	part.parse::<i64>()
		.map_err(|_| ExtractionError::validation(format!("Invalid time format: {original_text}")))
}

fn parse_seconds_part(part: &str, original_text: &str) -> DomainResult<f64> {
	if part.starts_with(['+', '-']) || part.is_empty() || part.matches('.').count() > 1 {
		return Err(ExtractionError::validation(format!("Invalid time format: {original_text}")));
	}

	if !part.chars().all(|character| character.is_ascii_digit() || character == '.') {
		return Err(ExtractionError::validation(format!("Invalid time format: {original_text}")));
	}

	part.parse::<f64>()
		.map_err(|_| ExtractionError::validation(format!("Invalid time format: {original_text}")))
}

#[cfg(test)]
mod tests {
	use super::*;

	#[test]
	fn extraction_request_defaults_match_current_tts_profile() {
		let request = ExtractionRequest::new("input.wav");

		assert_eq!(request.processing_profile, ProcessingProfile::TextToSpeech);
		assert_eq!(request.tts.sample_rate, DEFAULT_TTS_SAMPLE_RATE);
		assert_eq!(request.tts.highpass_hz, DEFAULT_TTS_HIGHPASS_HZ);
		assert_eq!(request.tts.lowpass_hz, DEFAULT_TTS_LOWPASS_HZ);
		assert_eq!(request.tts.target_lufs, DEFAULT_TARGET_LUFS);
	}

	#[test]
	fn parse_time_to_seconds_returns_none_for_missing_input() {
		assert_eq!(parse_time_to_seconds(None).unwrap(), None);
		assert_eq!(parse_time_to_seconds(Some("" )).unwrap(), None);
		assert_eq!(parse_time_to_seconds(Some("   ")).unwrap(), None);
	}

	#[test]
	fn parse_time_to_seconds_supports_current_valid_formats() {
		let cases = [
			("10", 10.0),
			("90", 90.0),
			("01:30", 90.0),
			("00:01:30", 90.0),
			("1:23:45", 5_025.0),
			("00:00:03.25", 3.25),
			("1:30.5", 90.5),
			("0:0:10.123", 10.123),
		];

		for (input, expected) in cases {
			let result = parse_time_to_seconds(Some(input)).unwrap().unwrap();
			assert!((result - expected).abs() < 0.000_1, "input={input}, result={result}");
		}
	}

	#[test]
	fn parse_time_to_seconds_rejects_current_invalid_formats() {
		let cases = [
			(
				"1:2:3:4",
				"validation error: Invalid time format: 1:2:3:4 (use SS, MM:SS, or HH:MM:SS)",
			),
			("abc", "validation error: Invalid time format: abc"),
			("1:abc", "validation error: Invalid time format: 1:abc"),
			("1.5:30", "validation error: Invalid time format: 1.5:30"),
		];

		for (input, expected) in cases {
			let error = parse_time_to_seconds(Some(input)).unwrap_err();
			assert_eq!(error.to_string(), expected);
		}
	}

	#[test]
	fn to_file_time_token_formats_time_for_filenames() {
		let cases = [
			("00:01:30", "00-01-30"),
			("1:23:45", "1-23-45"),
			("90", "90"),
			("01:30", "01-30"),
		];

		for (input, expected) in cases {
			assert_eq!(to_file_time_token(input), expected);
		}
	}

	#[test]
	fn build_auto_output_name_generates_tts_name() {
		let result = build_auto_output_name(
			"test.mp4",
			ProcessingProfile::TextToSpeech,
			None,
			None,
			None,
		);

		assert!(result.ends_with("test_tts.wav"));
	}

	#[test]
	fn build_auto_output_name_generates_preserve_input_name() {
		let result = build_auto_output_name(
			"test.mp4",
			ProcessingProfile::PreserveInput,
			None,
			None,
			None,
		);

		assert!(result.ends_with("test_out.wav"));
	}

	#[test]
	fn build_auto_output_name_includes_time_tokens() {
		let result = build_auto_output_name(
			"test.mp4",
			ProcessingProfile::TextToSpeech,
			Some("00:01:00"),
			Some("00:02:00"),
			Some("00:00:30"),
		);
		let rendered = result.display().to_string();

		assert!(rendered.contains("_s00-01-00"));
		assert!(rendered.contains("_e00-02-00"));
		assert!(rendered.contains("_d00-00-30"));
	}

	#[test]
	fn build_auto_output_name_preserves_directory() {
		let result = build_auto_output_name(
			"videos/test.mp4",
			ProcessingProfile::TextToSpeech,
			None,
			None,
			None,
		);

		assert_eq!(result, PathBuf::from("videos").join("test_tts.wav"));
	}

	#[test]
	fn build_auto_output_name_uses_current_directory_when_no_parent_exists() {
		let result = build_auto_output_name(
			"test.mp4",
			ProcessingProfile::TextToSpeech,
			None,
			None,
			None,
		);

		assert_eq!(result, PathBuf::from(".").join("test_tts.wav"));
	}

	#[test]
	fn validate_request_rejects_missing_end_or_duration_without_start() {
		let temp_dir = std::env::temp_dir();
		let input_path = temp_dir.join("audio-extractor-validate-start.wav");
		let _ = std::fs::write(&input_path, []);

		let mut request = ExtractionRequest::new(&input_path);
		request.timing.end = Some("00:00:10".to_string());

		let error = validate_request(&request).unwrap_err();
		assert_eq!(
			error.to_string(),
			"validation error: Start time required when using --end/--duration.",
		);

		let _ = std::fs::remove_file(input_path);
	}

	#[test]
	fn validate_request_accepts_current_defaults() {
		let temp_dir = std::env::temp_dir();
		let input_path = temp_dir.join("audio-extractor-validate-ok.wav");
		let _ = std::fs::write(&input_path, []);

		let request = ExtractionRequest::new(&input_path);
		let validated = validate_request(&request).unwrap();

		assert_eq!(validated.processing_profile, ProcessingProfile::TextToSpeech);
		assert_eq!(validated.input_path, input_path);

		let _ = std::fs::remove_file(validated.input_path);
	}

	#[test]
	fn build_extraction_plan_matches_current_tts_flow() {
		let temp_dir = std::env::temp_dir();
		let input_path = temp_dir.join("audio-extractor-plan.wav");
		let output_path = temp_dir.join("audio-extractor-plan_tts.wav");
		let _ = std::fs::write(&input_path, []);

		let mut request = ExtractionRequest::new(&input_path);
		request.timing.start = Some("00:00:05".to_string());
		request.timing.duration = Some("00:00:03".to_string());

		let validated = validate_request(&request).unwrap();
		let plan = build_extraction_plan(&validated, output_path.clone()).unwrap();

		assert_eq!(plan.args[0], "-hide_banner");
		assert!(plan.args.contains(&"-af".to_string()));
		assert!(plan.args.contains(&output_path.display().to_string()));

		let _ = std::fs::remove_file(input_path);
	}

	#[test]
	fn build_extraction_plan_matches_current_no_tts_flow() {
		let temp_dir = std::env::temp_dir();
		let input_path = temp_dir.join("audio-extractor-plan-no-tts.wav");
		let output_path = temp_dir.join("audio-extractor-plan-no-tts_out.wav");
		let _ = std::fs::write(&input_path, []);

		let mut request = ExtractionRequest::new(&input_path);
		request.processing_profile = ProcessingProfile::PreserveInput;
		request.output.sample_rate = Some(48_000);
		request.output.channels = Some(2);

		let validated = validate_request(&request).unwrap();
		let plan = build_extraction_plan(&validated, output_path.clone()).unwrap();

		assert!(plan.args.contains(&"-ar".to_string()));
		assert!(plan.args.contains(&"48000".to_string()));
		assert!(plan.args.contains(&"-ac".to_string()));
		assert!(plan.args.contains(&"2".to_string()));

		let _ = std::fs::remove_file(input_path);
	}
}

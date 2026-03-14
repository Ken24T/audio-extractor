#![forbid(unsafe_code)]

use std::ffi::OsString;
use std::path::PathBuf;

use clap::{ArgAction, CommandFactory, Parser};
use extractor_domain::{
    ExtractionRequest, OutputOptions, ProcessingProfile, TimingOptions, TtsOptions,
};
use extractor_ffmpeg::FfmpegRunner;

#[derive(Debug, Parser)]
#[command(name = "audio-extractor")]
#[command(about = "Extract audio using ffmpeg")]
struct Cli {
    input_file: PathBuf,
    #[arg(long, short = 'o', visible_alias = "Output")]
    output: Option<PathBuf>,
    #[arg(long, visible_alias = "Start")]
    start: Option<String>,
    #[arg(long, visible_alias = "End")]
    end: Option<String>,
    #[arg(long, visible_alias = "Duration")]
    duration: Option<String>,
    #[arg(long, visible_alias = "SampleRate")]
    sample_rate: Option<u32>,
    #[arg(long, visible_alias = "Channels")]
    channels: Option<u8>,
    #[arg(long, visible_alias = "FfmpegPath")]
    ffmpeg_path: Option<PathBuf>,
    #[arg(long = "no-tts", visible_alias = "NoTTS", action = ArgAction::SetTrue)]
    no_tts: bool,
    #[arg(long, visible_alias = "Force", action = ArgAction::SetTrue)]
    force: bool,
    #[arg(long, visible_alias = "Autoplay", action = ArgAction::SetTrue)]
    autoplay: bool,
    #[arg(long, visible_alias = "Verbose", action = ArgAction::SetTrue)]
    verbose: bool,
    #[arg(long = "tts-sample-rate")]
    tts_sample_rate: Option<u32>,
    #[arg(long = "tts-highpass-hz")]
    tts_highpass_hz: Option<u32>,
    #[arg(long = "tts-lowpass-hz")]
    tts_lowpass_hz: Option<u32>,
    #[arg(long = "target-lufs")]
    target_lufs: Option<i32>,
}

fn main() {
    let raw_args: Vec<OsString> = std::env::args_os().collect();
    if raw_args.len() == 1 {
        let mut command = Cli::command();
        let _ = command.print_help();
        println!();
        std::process::exit(1);
    }

    if raw_args.len() == 2 && is_help_token(&raw_args[1]) {
        let mut command = Cli::command();
        let _ = command.print_help();
        println!();
        return;
    }

    let cli = match Cli::try_parse_from(normalize_args(raw_args)) {
        Ok(cli) => cli,
        Err(error) => {
            let _ = error.print();
            std::process::exit(error.exit_code());
        }
    };

    let runner = FfmpegRunner::new();
    let request = ExtractionRequest {
        input_path: cli.input_file,
        timing: TimingOptions {
            start: cli.start,
            end: cli.end,
            duration: cli.duration,
        },
        output: OutputOptions {
            output_path: cli.output,
            sample_rate: cli.sample_rate,
            channels: cli.channels,
            overwrite: cli.force,
            autoplay: cli.autoplay,
            verbose: cli.verbose,
        },
        ffmpeg_path: cli.ffmpeg_path,
        processing_profile: if cli.no_tts {
            ProcessingProfile::PreserveInput
        } else {
            ProcessingProfile::TextToSpeech
        },
        tts: TtsOptions {
            sample_rate: cli.tts_sample_rate.unwrap_or(extractor_domain::DEFAULT_TTS_SAMPLE_RATE),
            highpass_hz: cli.tts_highpass_hz.unwrap_or(extractor_domain::DEFAULT_TTS_HIGHPASS_HZ),
            lowpass_hz: cli.tts_lowpass_hz.unwrap_or(extractor_domain::DEFAULT_TTS_LOWPASS_HZ),
            target_lufs: cli.target_lufs.unwrap_or(extractor_domain::DEFAULT_TARGET_LUFS),
        },
    };

    match runner.run(&request) {
        Ok(result) => {
            if let Some(output_path) = result.output_path {
                println!("Done -> {}", output_path.display());
            }
        }
        Err(error) => {
            eprintln!("{error}");
            std::process::exit(2);
        }
    }
}

fn is_help_token(token: &OsString) -> bool {
    matches!(token.to_str(), Some("--help" | "-h" | "/?" | "-?"))
}

fn normalize_args(raw_args: Vec<OsString>) -> Vec<OsString> {
    raw_args
        .into_iter()
        .map(|token| match token.to_str() {
            Some("-Output") => OsString::from("--output"),
            Some("-Start") => OsString::from("--start"),
            Some("-End") => OsString::from("--end"),
            Some("-Duration") => OsString::from("--duration"),
            Some("-SampleRate") => OsString::from("--sample-rate"),
            Some("-Channels") => OsString::from("--channels"),
            Some("-FfmpegPath") => OsString::from("--ffmpeg-path"),
            Some("-NoTTS") => OsString::from("--no-tts"),
            Some("-Force") => OsString::from("--force"),
            Some("-Autoplay") => OsString::from("--autoplay"),
            Some("-Verbose") => OsString::from("--verbose"),
            Some("/?") | Some("-?") => OsString::from("--help"),
            _ => token,
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn normalize_args_converts_powershell_aliases() {
        let args = vec![
            OsString::from("audio-extractor"),
            OsString::from("input.mp4"),
            OsString::from("-Output"),
            OsString::from("out.wav"),
            OsString::from("-NoTTS"),
            OsString::from("-Autoplay"),
        ];

        let normalized = normalize_args(args);
        let rendered = normalized
            .into_iter()
            .map(|value| value.to_string_lossy().to_string())
            .collect::<Vec<_>>();

        assert_eq!(
            rendered,
            vec![
                "audio-extractor",
                "input.mp4",
                "--output",
                "out.wav",
                "--no-tts",
                "--autoplay",
            ]
        );
    }
}

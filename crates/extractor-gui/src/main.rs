#![forbid(unsafe_code)]

use std::sync::mpsc::{self, Receiver};
use std::thread;

use eframe::egui;
use extractor_domain::{
    ApplicationSettings, ExtractionRequest, OutputOptions, ProcessingProfile, TimingOptions,
    TtsOptions,
};
use extractor_ffmpeg::FfmpegRunner;
use extractor_platform::{load_settings, save_settings};
use rfd::FileDialog;

#[derive(Debug)]
struct ExtractionApp {
    input_path: String,
    output_path: String,
    start: String,
    end: String,
    duration: String,
    sample_rate: String,
    channels: String,
    no_tts: bool,
    force: bool,
    autoplay: bool,
    verbose: bool,
    tts_sample_rate: String,
    tts_highpass_hz: String,
    tts_lowpass_hz: String,
    target_lufs: String,
    ffmpeg_path: String,
    status: String,
    log_lines: Vec<String>,
    is_running: bool,
    receiver: Option<Receiver<Result<String, String>>>,
}

impl Default for ExtractionApp {
    fn default() -> Self {
        let settings = load_settings().unwrap_or_default();
        Self {
            input_path: String::new(),
            output_path: String::new(),
            start: String::new(),
            end: String::new(),
            duration: String::new(),
            sample_rate: String::new(),
            channels: String::new(),
            no_tts: false,
            force: false,
            autoplay: true,
            verbose: false,
            tts_sample_rate: extractor_domain::DEFAULT_TTS_SAMPLE_RATE.to_string(),
            tts_highpass_hz: extractor_domain::DEFAULT_TTS_HIGHPASS_HZ.to_string(),
            tts_lowpass_hz: extractor_domain::DEFAULT_TTS_LOWPASS_HZ.to_string(),
            target_lufs: extractor_domain::DEFAULT_TARGET_LUFS.to_string(),
            ffmpeg_path: settings
                .ffmpeg_path
                .map(|path| path.display().to_string())
                .unwrap_or_default(),
            status: "Ready".to_string(),
            log_lines: Vec::new(),
            is_running: false,
            receiver: None,
        }
    }
}

impl ExtractionApp {
    fn run_extraction(&mut self) {
        let request = self.build_request();
        let (sender, receiver) = mpsc::channel();
        self.receiver = Some(receiver);
        self.is_running = true;
        self.status = "Running extraction...".to_string();
        self.log_lines.clear();

        thread::spawn(move || {
            let runner = FfmpegRunner::new();
            let outcome = runner.run(&request).map(|result| {
                result
                    .output_path
                    .map(|path| path.display().to_string())
                    .unwrap_or_else(|| "Extraction complete.".to_string())
            });

            let _ = sender.send(outcome.map_err(|error| error.to_string()));
        });
    }

    fn build_request(&self) -> ExtractionRequest {
        ExtractionRequest {
            input_path: self.input_path.trim().into(),
            timing: TimingOptions {
                start: optional_string(&self.start),
                end: optional_string(&self.end),
                duration: optional_string(&self.duration),
            },
            output: OutputOptions {
                output_path: optional_string(&self.output_path).map(Into::into),
                sample_rate: parse_optional_u32(&self.sample_rate),
                channels: parse_optional_u8(&self.channels),
                overwrite: self.force,
                autoplay: self.autoplay,
                verbose: self.verbose,
            },
            ffmpeg_path: optional_string(&self.ffmpeg_path).map(Into::into),
            processing_profile: if self.no_tts {
                ProcessingProfile::PreserveInput
            } else {
                ProcessingProfile::TextToSpeech
            },
            tts: TtsOptions {
                sample_rate: parse_required_u32(&self.tts_sample_rate, extractor_domain::DEFAULT_TTS_SAMPLE_RATE),
                highpass_hz: parse_required_u32(&self.tts_highpass_hz, extractor_domain::DEFAULT_TTS_HIGHPASS_HZ),
                lowpass_hz: parse_required_u32(&self.tts_lowpass_hz, extractor_domain::DEFAULT_TTS_LOWPASS_HZ),
                target_lufs: parse_required_i32(&self.target_lufs, extractor_domain::DEFAULT_TARGET_LUFS),
            },
        }
    }
}

impl eframe::App for ExtractionApp {
    fn save(&mut self, _storage: &mut dyn eframe::Storage) {
        let _ = save_settings(&ApplicationSettings {
            ffmpeg_path: optional_string(&self.ffmpeg_path).map(Into::into),
            window_placement: None,
        });
    }

    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        if let Some(receiver) = &self.receiver {
            if let Ok(message) = receiver.try_recv() {
                self.is_running = false;
                self.receiver = None;
                match message {
                    Ok(output_path) => {
                        self.status = "Extraction complete.".to_string();
                        self.log_lines.push(format!("Done -> {output_path}"));
                    }
                    Err(error) => {
                        self.status = "Extraction failed.".to_string();
                        self.log_lines.push(error);
                    }
                }
            }
        }

        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading("Audio Extractor");
            ui.label(&self.status);
            ui.separator();

            ui.group(|ui| {
                ui.heading("Files");
                ui.horizontal(|ui| {
                    ui.label("Input");
                    ui.text_edit_singleline(&mut self.input_path);
                    if ui.button("Browse").clicked() {
                        if let Some(path) = FileDialog::new().pick_file() {
                            self.input_path = path.display().to_string();
                            if self.output_path.trim().is_empty() {
                                let mut request = ExtractionRequest::new(path);
                                request.processing_profile = if self.no_tts {
                                    ProcessingProfile::PreserveInput
                                } else {
                                    ProcessingProfile::TextToSpeech
                                };
                                if let Ok(validated) = extractor_domain::validate_request(&request) {
                                    self.output_path = extractor_domain::default_output_path(&validated)
                                        .display()
                                        .to_string();
                                }
                            }
                        }
                    }
                });
                ui.horizontal(|ui| {
                    ui.label("Output");
                    ui.text_edit_singleline(&mut self.output_path);
                    if ui.button("Browse").clicked() {
                        if let Some(path) = FileDialog::new().save_file() {
                            self.output_path = path.display().to_string();
                        }
                    }
                });
            });

            ui.add_space(8.0);
            ui.group(|ui| {
                ui.heading("Time");
                field_row(ui, "Start", &mut self.start);
                field_row(ui, "End", &mut self.end);
                field_row(ui, "Duration", &mut self.duration);
            });

            ui.add_space(8.0);
            ui.group(|ui| {
                ui.heading("Processing");
                ui.checkbox(&mut self.no_tts, "No TTS");
                ui.checkbox(&mut self.force, "Overwrite existing");
                ui.checkbox(&mut self.autoplay, "Autoplay");
                ui.checkbox(&mut self.verbose, "Verbose");
                field_row(ui, "Sample Rate", &mut self.sample_rate);
                field_row(ui, "Channels", &mut self.channels);
            });

            ui.add_space(8.0);
            ui.group(|ui| {
                ui.heading("TTS Settings");
                field_row(ui, "TTS Sample Rate", &mut self.tts_sample_rate);
                field_row(ui, "Highpass Hz", &mut self.tts_highpass_hz);
                field_row(ui, "Lowpass Hz", &mut self.tts_lowpass_hz);
                field_row(ui, "Target LUFS", &mut self.target_lufs);
            });

            ui.add_space(8.0);
            ui.group(|ui| {
                ui.heading("Settings");
                field_row(ui, "ffmpeg Path", &mut self.ffmpeg_path);
            });

            ui.add_space(8.0);
            ui.horizontal(|ui| {
                if ui.add_enabled(!self.is_running, egui::Button::new("Run")).clicked() {
                    self.run_extraction();
                }
                if ui.button("Clear Log").clicked() {
                    self.log_lines.clear();
                }
            });

            ui.add_space(8.0);
            ui.group(|ui| {
                ui.heading("Log");
                egui::ScrollArea::vertical().show(ui, |ui| {
                    for line in &self.log_lines {
                        ui.label(line);
                    }
                });
            });
        });
    }
}

fn main() -> Result<(), eframe::Error> {
    let options = eframe::NativeOptions::default();
    eframe::run_native(
        "Audio Extractor",
        options,
        Box::new(|_cc| Ok(Box::<ExtractionApp>::default())),
    )
}

fn field_row(ui: &mut egui::Ui, label: &str, value: &mut String) {
    ui.horizontal(|ui| {
        ui.label(label);
        ui.text_edit_singleline(value);
    });
}

fn optional_string(value: &str) -> Option<String> {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        None
    } else {
        Some(trimmed.to_string())
    }
}

fn parse_optional_u32(value: &str) -> Option<u32> {
    optional_string(value).and_then(|value| value.parse::<u32>().ok())
}

fn parse_optional_u8(value: &str) -> Option<u8> {
    optional_string(value).and_then(|value| value.parse::<u8>().ok())
}

fn parse_required_u32(value: &str, fallback: u32) -> u32 {
    optional_string(value)
        .and_then(|value| value.parse::<u32>().ok())
        .unwrap_or(fallback)
}

fn parse_required_i32(value: &str, fallback: i32) -> i32 {
    optional_string(value)
        .and_then(|value| value.parse::<i32>().ok())
        .unwrap_or(fallback)
}

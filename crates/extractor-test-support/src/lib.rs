#![forbid(unsafe_code)]

//! Shared test fixtures and helpers for audio-extractor.

use extractor_domain::ExtractionRequest;

pub fn sample_request() -> ExtractionRequest {
	ExtractionRequest::new("fixtures/sample-input.wav")
}

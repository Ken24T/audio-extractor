using AudioExtractor;
using Xunit;

namespace AudioExtractor.Tests;

public class TimeParsingTests
{
    [Theory]
    [InlineData("10", 10.0)]
    [InlineData("90", 90.0)]
    [InlineData("01:30", 90.0)]
    [InlineData("00:01:30", 90.0)]
    [InlineData("1:23:45", 5025.0)]
    [InlineData("00:00:03.25", 3.25)]
    [InlineData("1:30.5", 90.5)]
    [InlineData("0:0:10.123", 10.123)]
    public void ConvertToSeconds_ValidFormats_ReturnsCorrectSeconds(string input, double expected)
    {
        var result = Program.ConvertToSeconds(input);
        
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value, precision: 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertToSeconds_NullOrWhitespace_ReturnsNull(string? input)
    {
        var result = Program.ConvertToSeconds(input);
        
        Assert.Null(result);
    }

    [Theory]
    [InlineData("1:2:3:4")]
    [InlineData("abc")]
    [InlineData("1:abc")]
    [InlineData("1.5:30")]
    public void ConvertToSeconds_InvalidFormats_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => Program.ConvertToSeconds(input));
    }

    [Theory]
    [InlineData("00:01:30", "00-01-30")]
    [InlineData("1:23:45", "1-23-45")]
    [InlineData("90", "90")]
    [InlineData("01:30", "01-30")]
    public void ToFileTimeToken_ValidInput_ReturnsFormattedToken(string input, string expected)
    {
        var result = Program.ToFileTimeToken(input);
        
        Assert.Equal(expected, result);
    }
}

public class FileNamingTests
{
    [Fact]
    public void BuildAutoOutputName_NoTtsMode_GeneratesCorrectName()
    {
        var result = Program.BuildAutoOutputName("test.mp4", isNoTts: true, null, null, null);
        
        Assert.EndsWith("_out.wav", result);
        Assert.Contains("test", result);
    }

    [Fact]
    public void BuildAutoOutputName_TtsMode_GeneratesCorrectName()
    {
        var result = Program.BuildAutoOutputName("test.mp4", isNoTts: false, null, null, null);
        
        Assert.EndsWith("_tts.wav", result);
        Assert.Contains("test", result);
    }

    [Fact]
    public void BuildAutoOutputName_WithStartTime_IncludesStartToken()
    {
        var result = Program.BuildAutoOutputName("test.mp4", false, "00:01:00", null, null);
        
        Assert.Contains("_s00-01-00", result);
    }

    [Fact]
    public void BuildAutoOutputName_WithEndTime_IncludesEndToken()
    {
        var result = Program.BuildAutoOutputName("test.mp4", false, "00:01:00", "00:02:00", null);
        
        Assert.Contains("_s00-01-00", result);
        Assert.Contains("_e00-02-00", result);
    }

    [Fact]
    public void BuildAutoOutputName_WithDuration_IncludesDurationToken()
    {
        var result = Program.BuildAutoOutputName("test.mp4", false, "00:01:00", null, "00:00:30");
        
        Assert.Contains("_s00-01-00", result);
        Assert.Contains("_d00-00-30", result);
    }

    [Fact]
    public void BuildAutoOutputName_WithFullPath_PreservesDirectory()
    {
        var result = Program.BuildAutoOutputName(@"C:\Videos\test.mp4", false, null, null, null);
        
        Assert.StartsWith(@"C:\Videos\", result);
    }

    [Fact]
    public void BuildAutoOutputName_NoDirectory_UsesCurrentDirectory()
    {
        var result = Program.BuildAutoOutputName("test.mp4", false, null, null, null);
        
        Assert.Contains("test", result);
        Assert.EndsWith(".wav", result);
    }
}

public class ArgumentParsingTests
{
    [Fact]
    public void ParseArgs_NoArguments_ReturnsHelp()
    {
        var result = Program.ParseArgs(Array.Empty<string>());
        
        Assert.True(result.ShowHelp);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    [InlineData("-?")]
    public void ParseArgs_HelpFlags_ReturnsHelp(string helpFlag)
    {
        var result = Program.ParseArgs(new[] { helpFlag });
        
        Assert.True(result.ShowHelp);
    }

    [Fact]
    public void ParseArgs_InputFileOnly_SetsInputFile()
    {
        var result = Program.ParseArgs(new[] { "test.mp4" });
        
        Assert.Equal("test.mp4", result.InputFile);
        Assert.False(result.ShowHelp);
    }

    [Fact]
    public void ParseArgs_WithStartTime_SetsStartTime()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--start", "00:01:00" });
        
        Assert.Equal("00:01:00", result.Start);
    }

    [Fact]
    public void ParseArgs_WithEndTime_SetsEndTime()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--start", "00:01:00", "--end", "00:02:00" });
        
        Assert.Equal("00:02:00", result.End);
    }

    [Fact]
    public void ParseArgs_WithDuration_SetsDuration()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--start", "00:01:00", "--duration", "00:00:30" });
        
        Assert.Equal("00:00:30", result.Duration);
    }

    [Fact]
    public void ParseArgs_WithOutput_SetsOutput()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--output", "output.wav" });
        
        Assert.Equal("output.wav", result.Output);
    }

    [Fact]
    public void ParseArgs_NoTtsFlag_SetsNoTts()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--no-tts" });
        
        Assert.True(result.NoTts);
    }

    [Fact]
    public void ParseArgs_ForceFlag_SetsForce()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--force" });
        
        Assert.True(result.Force);
    }

    [Fact]
    public void ParseArgs_AutoplayFlag_SetsAutoplay()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--autoplay" });
        
        Assert.True(result.Autoplay);
    }

    [Fact]
    public void ParseArgs_VerboseFlag_SetsVerbose()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--verbose" });
        
        Assert.True(result.Verbose);
    }

    [Fact]
    public void ParseArgs_WithSampleRate_SetsSampleRate()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--sample-rate", "48000" });
        
        Assert.Equal(48000, result.SampleRate);
    }

    [Fact]
    public void ParseArgs_WithChannels_SetsChannels()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--channels", "2" });
        
        Assert.Equal(2, result.Channels);
    }

    [Fact]
    public void ParseArgs_WithTtsSampleRate_SetsTtsSampleRate()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--tts-sample-rate", "22050" });
        
        Assert.Equal(22050, result.TtsSampleRate);
    }

    [Fact]
    public void ParseArgs_WithTtsHighpass_SetsTtsHighpass()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--tts-highpass-hz", "100" });
        
        Assert.Equal(100, result.TtsHighpassHz);
    }

    [Fact]
    public void ParseArgs_WithTtsLowpass_SetsTtsLowpass()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--tts-lowpass-hz", "10000" });
        
        Assert.Equal(10000, result.TtsLowpassHz);
    }

    [Fact]
    public void ParseArgs_WithTargetLufs_SetsTargetLufs()
    {
        var result = Program.ParseArgs(new[] { "test.mp4", "--target-lufs", "-18" });
        
        Assert.Equal(-18, result.TargetLufs);
    }

    [Fact]
    public void ParseArgs_DefaultValues_AreSetCorrectly()
    {
        var result = Program.ParseArgs(new[] { "test.mp4" });
        
        Assert.Equal(24000, result.TtsSampleRate);
        Assert.Equal(80, result.TtsHighpassHz);
        Assert.Equal(11000, result.TtsLowpassHz);
        Assert.Equal(-16, result.TargetLufs);
    }

    [Fact]
    public void ParseArgs_UnknownOption_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => 
            Program.ParseArgs(new[] { "test.mp4", "--unknown-flag" }));
    }

    [Fact]
    public void ParseArgs_MissingValueForOption_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => 
            Program.ParseArgs(new[] { "test.mp4", "--output" }));
    }

    [Fact]
    public void ParseArgs_InvalidInteger_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => 
            Program.ParseArgs(new[] { "test.mp4", "--sample-rate", "abc" }));
    }

    [Fact]
    public void ParseArgs_PowerShellStyleFlags_WorkCorrectly()
    {
        var result = Program.ParseArgs(new[] { 
            "test.mp4", 
            "-Output", "out.wav",
            "-Start", "00:01:00",
            "-NoTTS",
            "-Force",
            "-Autoplay",
            "-Verbose"
        });
        
        Assert.Equal("out.wav", result.Output);
        Assert.Equal("00:01:00", result.Start);
        Assert.True(result.NoTts);
        Assert.True(result.Force);
        Assert.True(result.Autoplay);
        Assert.True(result.Verbose);
    }
}

public class OptionsTests
{
    [Fact]
    public void Options_WithHelp_ReturnsHelpInstance()
    {
        var result = Program.Options.WithHelp();
        
        Assert.True(result.ShowHelp);
    }
}

using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace AudioExtractor.Gui;

public partial class App : Application
{
	// Static fallback when the system accent cannot be detected.
	private static readonly Color FallbackAccent = Color.FromRgb(0x2A, 0x8C, 0x82);

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		try
		{
			ApplyAccentColor();
		}
		catch
		{
			// Accent theming is cosmetic — never crash the app for it.
		}
	}

	private void ApplyAccentColor()
	{
		var accent = GetSystemAccentColor();
		var accentDark = Darken(accent, 0.22);
		var accentTint = ComputeTint(accent, 0.20);

		Resources["AccentColor"] = accent;
		Resources["AccentDarkColor"] = accentDark;
		Resources["AccentBrush"] = new SolidColorBrush(accent);
		Resources["AccentDarkBrush"] = new SolidColorBrush(accentDark);
		Resources["AccentForegroundBrush"] = new SolidColorBrush(ContrastForeground(accent));
		Resources["AccentTintColor"] = accentTint;
	}

	/// <summary>
	/// Reads the Windows accent color from the DWM registry key.
	/// Falls back to SystemParameters.WindowGlassColor, then to the static default.
	/// </summary>
	private static Color GetSystemAccentColor()
	{
		// 1. Registry: HKCU\SOFTWARE\Microsoft\Windows\DWM\AccentColor
		//    Stored as a DWORD in 0xAABBGGRR (BGR) byte order.
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\DWM");
			if (key?.GetValue("AccentColor") is int raw)
			{
				byte a = (byte)((raw >> 24) & 0xFF);
				byte b = (byte)((raw >> 16) & 0xFF);
				byte g = (byte)((raw >> 8) & 0xFF);
				byte r = (byte)(raw & 0xFF);
				if (a == 0) a = 0xFF; // Treat fully transparent as opaque
				return Color.FromArgb(a, r, g, b);
			}
		}
		catch
		{
			// Registry access may fail — continue to next fallback.
		}

		// 2. WPF built-in: DWM glass color
		var glass = SystemParameters.WindowGlassColor;
		if (glass.A > 0 && (glass.R | glass.G | glass.B) != 0)
		{
			return glass;
		}

		// 3. Static fallback
		return FallbackAccent;
	}

	/// <summary>
	/// Returns white or dark text depending on the perceived brightness of the background.
	/// Uses the ITU-R BT.601 luma formula.
	/// </summary>
	private static Color ContrastForeground(Color bg)
	{
		double luma = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
		return luma > 0.5 ? Color.FromRgb(0x1C, 0x1A, 0x18) : Colors.White;
	}

	/// <summary>
	/// Blends the accent color at the given opacity over white to produce a light tint.
	/// </summary>
	private static Color ComputeTint(Color accent, double opacity)
	{
		opacity = Math.Clamp(opacity, 0, 1);
		byte Blend(byte c) => (byte)(255 + (c - 255) * opacity);
		return Color.FromRgb(Blend(accent.R), Blend(accent.G), Blend(accent.B));
	}

	private static Color Darken(Color color, double amount)
	{
		amount = Math.Clamp(amount, 0, 1);
		byte Scale(byte channel) => (byte)Math.Clamp(channel * (1 - amount), 0, 255);
		return Color.FromArgb(color.A, Scale(color.R), Scale(color.G), Scale(color.B));
	}
}

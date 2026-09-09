using System.Globalization;
using System.Runtime.InteropServices;

namespace WorkGuard.Windows.Platform;

// Optional Windows SAPI voice. No download, network, microphone or extra runtime package.
internal sealed class MaintenanceVoice : IDisposable
{
    private object? _voice;
    private bool _attempted;
    public bool Available => _voice is not null;
    public void Speak(string text)
    {
        try
        {
            if (!_attempted)
            {
                _attempted = true;
                var type = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (type is null) return;
                _voice = Activator.CreateInstance(type);
                dynamic voice = _voice!;
                object? tokensObject = null;
                var found = false;
                try
                {
                    tokensObject = voice.GetVoices();
                    dynamic tokens = tokensObject!;
                    for (var i = 0; i < tokens.Count; i++)
                    {
                        object tokenObject = tokens.Item(i);
                        try
                        {
                            dynamic token = tokenObject;
                            string languages = token.GetAttribute("Language");
                            if (languages.Split(';').Any(code => int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) &&
                                CultureInfo.GetCultureInfo(id).TwoLetterISOLanguageName == "zh"))
                            { voice.Voice = token; found = true; break; }
                        }
                        finally { if (Marshal.IsComObject(tokenObject)) Marshal.FinalReleaseComObject(tokenObject); }
                    }
                }
                finally { if (tokensObject is not null && Marshal.IsComObject(tokensObject)) Marshal.FinalReleaseComObject(tokensObject); }
                if (!found) { Dispose(); return; }
                voice.Rate = -1;
                voice.Volume = 80;
            }
            if (_voice is not null) ((dynamic)_voice).Speak(text, 3); // async + purge previous prompt
        }
        catch (Exception error) when (error is COMException or InvalidOperationException or ArgumentException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { Dispose(); }
    }
    public void Stop()
    {
        try { if (_voice is not null) ((dynamic)_voice).Speak("", 3); }
        catch (Exception error) when (error is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
    }
    public void Dispose()
    {
        Stop();
        if (_voice is not null && Marshal.IsComObject(_voice)) Marshal.FinalReleaseComObject(_voice);
        _voice = null;
    }
}

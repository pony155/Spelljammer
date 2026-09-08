using System.Windows.Threading;
using Spelljammer.Interop;
using Spelljammer.Settings;

namespace Spelljammer.Presentation;

/// <summary>
/// Owns the SpriteForge audio instance on the WPF dispatcher thread and applies
/// presentation-only game settings to its buses.
/// </summary>
/// <remarks>
/// Code flow: Validated game volume settings are converted to normalized engine values and sent to the native master, music, and effects audio buses for the service lifetime.
/// </remarks>
internal sealed class SpriteForgeAudioService : IDisposable
{
    private const float SettingsRampSeconds = 0.05f;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer updateTimer;
    private nint audio;
    private bool disposed;

    private SpriteForgeAudioService(nint audio, EngineStatus status)
    {
        dispatcher = Dispatcher.CurrentDispatcher;
        this.audio = audio;
        LastStatus = status;
        updateTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        updateTimer.Tick += UpdateTimer_Tick;
    }

    internal bool IsAvailable => audio != nint.Zero;

    internal EngineStatus LastStatus { get; private set; }

    internal static SpriteForgeAudioService Create(GameSettingsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EngineStatus status;
        nint handle = nint.Zero;
        try
        {
            status = SpriteForgeNative.SpriteForge_GetDefaultAudioConfig(out EngineAudioConfig config);
            if (status == EngineStatus.Success)
            {
                status = SpriteForgeNative.SpriteForge_CreateAudio(in config, out handle);
            }
        }
        catch (EntryPointNotFoundException)
        {
            status = EngineStatus.NotSupported;
        }
        catch (DllNotFoundException)
        {
            status = EngineStatus.BackendUnavailable;
        }

        SpriteForgeAudioService service = new(handle, status);
        if (status != EngineStatus.Success || handle == nint.Zero)
        {
            service.ReleaseAudio();
            return service;
        }

        if (!service.Apply(profile, 0f))
        {
            EngineStatus failure = service.LastStatus;
            service.ReleaseAudio();
            service.LastStatus = failure;
            return service;
        }

        service.updateTimer.Start();
        return service;
    }

    internal bool Apply(GameSettingsProfile profile) => Apply(profile, SettingsRampSeconds);

    private bool Apply(GameSettingsProfile profile, float rampSeconds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        dispatcher.VerifyAccess();
        if (disposed || !profile.IsValid)
        {
            LastStatus = EngineStatus.InvalidArgument;
            return false;
        }

        if (audio == nint.Zero)
        {
            return false;
        }

        if (!SetGain(EngineAudioBus.Master, profile.MasterVolume, rampSeconds) ||
            !SetGain(EngineAudioBus.Music, profile.MusicVolume, rampSeconds) ||
            !SetGain(EngineAudioBus.SoundEffects, profile.EffectsVolume, rampSeconds))
        {
            return false;
        }

        LastStatus = EngineStatus.Success;
        return true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        dispatcher.VerifyAccess();
        disposed = true;
        updateTimer.Stop();
        updateTimer.Tick -= UpdateTimer_Tick;
        ReleaseAudio();
        GC.SuppressFinalize(this);
    }

    private bool SetGain(EngineAudioBus bus, int percentage, float rampSeconds)
    {
        EngineStatus status = SpriteForgeNative.SpriteForge_AudioSetBusGain(
            audio,
            bus,
            percentage / (float)GameSettingsProfile.MaximumVolume,
            rampSeconds);
        LastStatus = status;
        return status == EngineStatus.Success;
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        LastStatus = SpriteForgeNative.SpriteForge_AudioUpdate(audio);
    }

    private void ReleaseAudio()
    {
        if (audio == nint.Zero)
        {
            return;
        }

        SpriteForgeNative.SpriteForge_DestroyAudio(audio);
        audio = nint.Zero;
    }
}

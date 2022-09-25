using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum AudioIndex { SupportRed, SupportGreen, SupportBlue, Fragment, Length, Song, Collect }
public class AudioController : MonoBehaviour {
    class AudioInfo {
        public static float baseVolume = 1.0f;
        public AudioSource source;
        public int count;

        public AudioInfo(AudioSource source) {
            source.volume = baseVolume;
            this.source = source;
            count = 1;
        }
    }

    class PositionalAudio {
        public AudioSource audioSource;
        public List<Data> datas = new List<Data>();
        public float volumeMultiplier;
        public IEnumerator setVolumeCoroutine;

        public PositionalAudio(GameObject holder, AudioMixerGroup audioMixerGroup) {
            audioSource = holder.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = audioMixerGroup;
            audioSource.volume = 0;
        }
    }

    [Header("Sources")]
    public AudioMixer mixer;
    public AudioSource audioSound;
    public GameObject audioPitchedSound;
    public GameObject audioPositionalSound;

    AudioSource[] audioPitchedSources;
    Dictionary<AudioClip, float> clipVolumes;
    Dictionary<AudioClip, AudioInfo> currentClips = new Dictionary<AudioClip, AudioInfo>();
    PositionalAudio[] positionalAudios = new PositionalAudio[7];
    PositionalAudio groundDustAudio;
    const float POSITIONAL_DISTANCE_MAX = 150;

    [Header("Player")]
    public AudioClip PlayerShoot;
    public AudioClip PlayerShake;
    public AudioClip PlayerGrow;
    public AudioClip PlayerShrink;
    public AudioClip PlayerUndo;
    public AudioClip PlayerReset;
    public AudioClip PlayerResetIncrement;
    public AudioClip PlayerEyeGlow;
    public AudioClip PlayerBlocked;
    public AudioClip PlayerTunnel;
    public AudioClip PlayerMove;
    public AudioClip PlayerSingShort;
    public AudioClip PlayerSingMedium;
    public AudioClip PlayerSingLong;
    public AudioClip BubblePop;

    [Header("Blocks")]
    public AudioClip BulletHit;
    public AudioClip CrystalMove;
    public AudioClip CrystalActivate;
    public AudioClip RedShoot;
    public AudioClip BlueBreak;
    public AudioClip GreenMove;
    public AudioClip RockLand;
    public AudioClip RockMove;
    public AudioClip DigBreak;

    [Header("Collectable")]
    public AudioClip CollectFloat;
    public AudioClip CollectLength;
    public AudioClip CollectColor;
    public AudioClip CollectTime;
    public AudioClip CollectFragment;
    public AudioClip CollectFragmentFloat;
    public AudioClip PlaceFragment;
    public AudioClip TeethClack;

    [Header("Puzzle")]
    public AudioClip ButtonOn;
    public AudioClip ButtonOff;
    public AudioClip DoorOpen;
    public AudioClip DoorClose;
    public AudioClip PistonExtend;
    public AudioClip PistonRetract;
    public AudioClip GateOpen;
    public AudioClip GateClose;
    public AudioClip PipeEnter;
    public AudioClip PipeExit;

    [Header("Menu")]
    public AudioClip MenuMove;
    public AudioClip MenuSelect;
    public AudioClip MenuCancel;
    public AudioClip MenuBack;
    public AudioClip MenuOpen;
    public AudioClip MenuClose;

    [Header("UI")]
    public AudioClip LengthMeterAdd;
    public AudioClip CloudBlip;
    public AudioClip CloudPuff;
    public AudioClip ButtonUp;
    public AudioClip ButtonDown;
    public AudioClip ButtonFeedbackUp;
    public AudioClip ButtonFeedbackDown;

    [Header("Misc")]
    public AudioClip GroundDust;
    public AudioClip SupportCrystalBeat;
    public AudioClip PowerDown;

    public static AudioClip playerShoot;
    public static AudioClip playerShake;
    public static AudioClip playerGrow;
    public static AudioClip playerShrink;
    public static AudioClip playerUndo;
    public static AudioClip playerReset;
    public static AudioClip playerResetIncrement;
    public static AudioClip playerEyeGlow;
    public static AudioClip playerBlocked;
    public static AudioClip playerTunnel;
    public static AudioClip playerMove;
    public static AudioClip playerSingShort;
    public static AudioClip playerSingMedium;
    public static AudioClip playerSingLong;
    public static AudioClip bubblePop;
    public static AudioClip bulletHit;
    public static AudioClip crystalMove;
    public static AudioClip crystalActivate;
    public static AudioClip redShoot;
    public static AudioClip blueBreak;
    public static AudioClip greenMove;
    public static AudioClip rockLand;
    public static AudioClip rockMove;
    public static AudioClip digBreak;
    public static AudioClip collectFloat;
    public static AudioClip collectLength;
    public static AudioClip collectColor;
    public static AudioClip collectTime;
    public static AudioClip collectFragment;
    public static AudioClip collectFragmentFloat;
    public static AudioClip placeFragment;
    public static AudioClip teethClack;
    public static AudioClip buttonOn;
    public static AudioClip buttonOff;
    public static AudioClip doorOpen;
    public static AudioClip doorClose;
    public static AudioClip pistonExtend;
    public static AudioClip pistonRetract;
    public static AudioClip gateOpen;
    public static AudioClip gateClose;
    public static AudioClip pipeEnter;
    public static AudioClip pipeExit;
    public static AudioClip menuMove;
    public static AudioClip menuSelect;
    public static AudioClip menuCancel;
    public static AudioClip menuBack;
    public static AudioClip menuOpen;
    public static AudioClip menuClose;
    public static AudioClip lengthMeterAdd;
    public static AudioClip cloudBlip;
    public static AudioClip cloudPuff;
    public static AudioClip buttonUp;
    public static AudioClip buttonDown;
    public static AudioClip buttonFeedbackUp;
    public static AudioClip buttonFeedbackDown;
    public static AudioClip groundDust;
    public static AudioClip supportCrystalBeat;
    public static AudioClip powerDown;

    public void Init() {
        playerShoot          = PlayerShoot;
        playerShake          = PlayerShake;
        playerGrow           = PlayerGrow;
        playerShrink         = PlayerShrink;
        playerUndo           = PlayerUndo;
        playerReset          = PlayerReset;
        playerResetIncrement = PlayerResetIncrement;
        playerEyeGlow        = PlayerEyeGlow;
        playerBlocked        = PlayerBlocked;
        playerTunnel         = PlayerTunnel;
        playerMove           = PlayerMove;
        bubblePop            = BubblePop;
        playerSingShort      = PlayerSingShort;
        playerSingMedium     = PlayerSingMedium;
        playerSingLong       = PlayerSingLong;
        bulletHit            = BulletHit;
        crystalMove          = CrystalMove;
        crystalActivate      = CrystalActivate;
        redShoot             = RedShoot;
        blueBreak            = BlueBreak;
        greenMove            = GreenMove;
        rockLand             = RockLand;
        rockMove             = RockMove;
        digBreak             = DigBreak;
        collectFloat         = CollectFloat;
        collectLength        = CollectLength;
        collectColor         = CollectColor;
        collectTime          = CollectTime;
        collectFragment      = CollectFragment;
        collectFragmentFloat = CollectFragmentFloat;
        placeFragment        = PlaceFragment;
        teethClack           = TeethClack;
        buttonOn             = ButtonOn;
        buttonOff            = ButtonOff;
        doorOpen             = DoorOpen;
        doorClose            = DoorClose;
        pistonExtend         = PistonExtend;
        pistonRetract        = PistonRetract;
        gateOpen             = GateOpen;
        gateClose            = GateClose;
        pipeEnter            = PipeEnter;
        pipeExit             = PipeExit;
        menuMove             = MenuMove;
        menuSelect           = MenuSelect;
        menuCancel           = MenuCancel;
        menuBack             = MenuBack;
        menuOpen             = MenuOpen;
        menuClose            = MenuClose;
        lengthMeterAdd       = LengthMeterAdd;
        cloudBlip            = CloudBlip;
        cloudPuff            = CloudPuff;
        groundDust           = GroundDust;
        buttonUp             = ButtonUp;
        buttonDown           = ButtonDown;
        buttonFeedbackUp     = ButtonFeedbackUp;
        buttonFeedbackDown   = ButtonFeedbackDown;
        supportCrystalBeat   = SupportCrystalBeat;
        powerDown            = PowerDown;

        clipVolumes = new Dictionary<AudioClip, float>() {
            { buttonUp            , 0.50f },
            { buttonDown          , 0.50f },
            { buttonFeedbackUp    , 0.50f },
            { buttonFeedbackDown  , 0.50f },
            { cloudBlip           , 0.50f },
            { playerMove          , 0.75f },
            { playerResetIncrement, 0.75f },
            { teethClack          , 0.50f }
        };

        audioPitchedSources = new AudioSource[20];
        for (int i = 0; i < audioPitchedSources.Length; i++) {
            AudioSource a = audioPitchedSources[i] = audioPitchedSound.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.outputAudioMixerGroup = audioSound.outputAudioMixerGroup;
        }
        
        for (int i = 0; i < positionalAudios.Length; i++) {
            positionalAudios[i] = new PositionalAudio(audioPositionalSound, audioSound.outputAudioMixerGroup);
            AudioSource a = positionalAudios[i].audioSource;

            switch ((AudioIndex)i) {
                case AudioIndex.Fragment: positionalAudios[i].volumeMultiplier = 0.30f; a.clip = collectFragmentFloat; break;
                case AudioIndex.Collect : positionalAudios[i].volumeMultiplier = 0.55f; a.clip = collectFloat;         break;
                case AudioIndex.Song    : positionalAudios[i].volumeMultiplier = 0.55f;                                break;
                default                 : positionalAudios[i].volumeMultiplier = 0.20f; a.clip = supportCrystalBeat;   break;
            }
            if (i >= (int)AudioIndex.Fragment && i != (int)AudioIndex.Song) {
                a.loop = true;
                a.Play();
            }
        }

        groundDustAudio = new PositionalAudio(audioPositionalSound, audioSound.outputAudioMixerGroup);
        groundDustAudio.volumeMultiplier = 0.5f;
        groundDustAudio.audioSource.clip = groundDust;
    }

    public void PlayPitched(AudioClip clip, float pitch, bool nonDuplicate = false, bool loudenDuplicates = false) {
        if (audioPitchedSources == null)
            return;

        currentClips.TryGetValue(clip, out AudioInfo audioInfo);
        if (nonDuplicate) {
            if (audioInfo != null) {
                if (loudenDuplicates)
                    audioInfo.source.volume = Mathf.Clamp(AudioInfo.baseVolume + (++audioInfo.count * 0.1f), 0, 1);
                return;
            }
        }

        clipVolumes.TryGetValue(clip, out float volume);
        if (volume == 0) volume = 1;

        foreach (AudioSource a in audioPitchedSources) {
            if (!a.isPlaying) {
                if (audioInfo == null)
                    currentClips.Add(clip, new AudioInfo(a));

                a.volume = volume;
                a.pitch  = pitch;
                a.PlayOneShot(clip);
                StartCoroutine(EndClip(a));
                break;
            }
        }

        IEnumerator EndClip(AudioSource audioSource) {
            yield return new WaitWhile(() => audioSource.isPlaying);
            currentClips.Remove(clip);
        }
    }

    public void PlayRandom(AudioClip clip, bool nonDuplicate = false, bool loudenDuplicates = false) {
        PlayPitched(clip, GetRandomPitch(1), nonDuplicate, loudenDuplicates);
    }

    public static float GetRandomPitch(float basePitch) {
        return basePitch + Random.Range(-0.3f, 0.3f);
    }

    float GetPositionalVolume(float distance) {
        return Mathf.InverseLerp(POSITIONAL_DISTANCE_MAX, 1, distance);
    }
    void SetPositionalVolume(PositionalAudio positionalAudio, float volume) {
        if (positionalAudio.setVolumeCoroutine != null) StopCoroutine(positionalAudio.setVolumeCoroutine);
            positionalAudio.setVolumeCoroutine  = SetVolume(positionalAudio.audioSource, volume);
        StartCoroutine(positionalAudio.setVolumeCoroutine);
    }
    IEnumerator SetVolume(AudioSource audioSource, float volume) {
        float fromVolume = audioSource.volume;

        float time = 0;
        while (time < 1) {
            audioSource.volume = Mathf.Lerp(fromVolume, volume, GameController.GetCurve(time));
            time += Time.deltaTime * 5;
            yield return null;
        }
    }

    public void InitPositionalAudio(AudioIndex audioIndex, Data data) {
        PositionalAudio pa = positionalAudios[(int)audioIndex];
        pa.datas.Add(data);
    }
    public void UpdatePositionalAudio(Coordinates coordinates) {
        foreach (PositionalAudio pa in positionalAudios) {
            if (pa.datas.Count == 0) {
                SetPositionalVolume(pa, 0);
                continue;
            }

            int count = 0;
            float closest = POSITIONAL_DISTANCE_MAX;
            foreach (Data d in pa.datas) {
                if (d.blockData.destroyed)
                    continue;

                count++;
                float distance = GameController.GetVector(coordinates - d.blockData.coordinates).sqrMagnitude;
                if (distance < closest)
                    closest = distance;
            }
            if (count == 0) {
                SetPositionalVolume(pa, 0);
                continue;
            }

            SetPositionalVolume(pa, GetPositionalVolume(closest) * pa.volumeMultiplier);
        }
    }
    public void ClearPositionalAudio() {
        foreach (PositionalAudio pa in positionalAudios) {
            pa.audioSource.volume = 0;
            pa.datas.Clear();
        }
    }

    public void SetSupportCrystalVolume(int colorIndex, float distance) {
        positionalAudios[colorIndex].volumeMultiplier = Mathf.Lerp(0.15f, 1f, Mathf.InverseLerp(30000, 0, distance));
    }
    public void PlaySupportCrystal(int colorIndex) {
        positionalAudios[colorIndex].audioSource.Play();
    }
    public void PlaySongNote() {
        AudioClip clip = null;
        int randomClip = Random.Range(0, 3);
        switch (randomClip) {
            case 0: clip = playerSingShort;  break;
            case 1: clip = playerSingMedium; break;
            case 2: clip = playerSingLong;   break;
        }
        positionalAudios[(int)AudioIndex.Song].audioSource.pitch = GetRandomPitch(1);
        positionalAudios[(int)AudioIndex.Song].audioSource.PlayOneShot(clip);
    }
    public void PlayGroundDust(Coordinates dustCoordinates, Coordinates toCoordinates) {
        float volume = GetPositionalVolume((GameController.GetVector(toCoordinates) - GameController.GetVector(dustCoordinates)).sqrMagnitude);
        SetPositionalVolume(groundDustAudio, volume * groundDustAudio.volumeMultiplier);

        groundDustAudio.audioSource.pitch = GetRandomPitch(1);
        groundDustAudio.audioSource.Play();
    }
}

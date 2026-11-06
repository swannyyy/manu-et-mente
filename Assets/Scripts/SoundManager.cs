// SPDX-License-Identifier: MPL-2.0
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource musicSource;

    [Header("UI Sound Effects")]
    public AudioClip buttonClickSound;
    public AudioClip buttonHoverSound;

    [Header("Game Sound Effects")]
    public AudioClip pieceMoveSound;
    public AudioClip pieceCaptureSound;
    public AudioClip ringRotateSound;
    public AudioClip invalidMoveSound;

    [Header("Victory Sounds")]
    public AudioClip victorySound;
    public AudioClip defeatSound;

    [Header("Special Effects")]
    public AudioClip pieceSelectSound;
    public AudioClip enPassantSound;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float sfxVolume = 0.7f;
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Setup audio sources if not assigned
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
        }

        // Apply volume settings
        sfxSource.volume = sfxVolume;
        musicSource.volume = musicVolume;
    }

    // Play sound effect
    public void PlaySound(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    // Play sound with custom volume
    public void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    // Specific sound methods for easy calling
    public void PlayButtonClick()
    {
        PlaySound(buttonClickSound);
    }

    public void PlayButtonHover()
    {
        PlaySound(buttonHoverSound, 0.3f); // Quieter hover sound
    }

    public void PlayPieceMove()
    {
        PlaySound(pieceMoveSound);
    }

    public void PlayPieceCapture()
    {
        PlaySound(pieceCaptureSound);
    }

    public void PlayRingRotate()
    {
        PlaySound(ringRotateSound);
    }

    public void PlayInvalidMove()
    {
        PlaySound(invalidMoveSound);
    }

    public void PlayVictory()
    {
        PlaySound(victorySound);
    }

    public void PlayDefeat()
    {
        PlaySound(defeatSound);
    }

    public void PlayPieceSelect()
    {
        PlaySound(pieceSelectSound);
    }

    public void PlayEnPassant()
    {
        PlaySound(enPassantSound);
    }

    // Background music
    public void PlayMusic(AudioClip music)
    {
        if (music != null && musicSource != null)
        {
            musicSource.clip = music;
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    // Volume controls
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }
}
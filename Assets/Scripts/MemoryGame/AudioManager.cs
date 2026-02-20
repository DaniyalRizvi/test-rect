using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SFX")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField] private AudioClip match;
    [SerializeField] private AudioClip gameover;
    [SerializeField] private AudioClip gamewin;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void PlayButtonClick()
    {
        PlayClip(buttonClick);
    }

    public void PlayMatch()
    {
        PlayClip(match);
    }

    public void PlayGameOver()
    {
        PlayClip(gameover);
    }

    public void PlayGameWin()
    {
        PlayClip(gamewin);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }
}

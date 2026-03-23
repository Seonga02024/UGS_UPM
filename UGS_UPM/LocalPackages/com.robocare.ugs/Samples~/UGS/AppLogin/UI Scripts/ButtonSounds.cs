using UnityEngine;

namespace RoboCare.UGS
{
    /// <summary>
    /// This component goes together with a button object and contains
    /// the audio clips to play when the player rolls over and presses it.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ButtonSounds : MonoBehaviour
    {
        public AudioClip pressedSound;
        public AudioClip rolloverSound;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void PlayPressedSound()
        {
            audioSource.clip = pressedSound;
            audioSource.Play();
        }

        public void PlayRolloverSound()
        {
            audioSource.clip = rolloverSound;
            audioSource.Play();
        }
    }
}

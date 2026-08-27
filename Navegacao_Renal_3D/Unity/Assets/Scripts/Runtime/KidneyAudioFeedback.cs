using UnityEngine;

namespace NavegacaoRenal
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class KidneyAudioFeedback : MonoBehaviour
    {
        private AudioSource audioSource;
        private AudioClip wallClip;
        private AudioClip captureClip;
        private AudioClip victoryClip;
        private AudioClip defeatClip;

        public bool UsesProceduralOriginalAudio => true;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            wallClip = CreateTone("WallContact", 185f, 0.12f, 0.42f);
            captureClip = CreateTone("StoneCapture", 520f, 0.16f, 0.32f);
            victoryClip = CreateSweep("Victory", 420f, 760f, 0.42f, 0.28f);
            defeatClip = CreateSweep("Defeat", 260f, 115f, 0.48f, 0.30f);
        }

        public void PlayWallContact() => Play(wallClip);
        public void PlayCapture() => Play(captureClip);
        public void PlayVictory() => Play(victoryClip);
        public void PlayDefeat() => Play(defeatClip);

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float amplitude)
        {
            return CreateSweep(name, frequency, frequency, duration, amplitude);
        }

        private static AudioClip CreateSweep(string name, float startFrequency, float endFrequency, float duration, float amplitude)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float progress = index / (float)Mathf.Max(1, sampleCount - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                phase += 2f * Mathf.PI * frequency / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * progress);
                samples[index] = Mathf.Sin(phase) * envelope * amplitude;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class TransitionSounds
    {
        private const string Folder = "Sounds/Transitions/";

        private readonly AudioSource _source;
        private readonly Dictionary<string, AudioClip?> _clips = new Dictionary<string, AudioClip?>();

        private TransitionSounds(Transform parent)
        {
            var host = new GameObject("Transition sounds");
            host.transform.SetParent(parent, false);
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public static TransitionSounds Create(Transform parent) => new TransitionSounds(parent);

        public float Volume { get; set; } = 0.5f;

        public void Play(string key)
        {
            if (key.Length == 0 || Volume <= 0f)
                return;
            if (!_clips.TryGetValue(key, out var clip))
                _clips[key] = clip = Resources.Load<AudioClip>(Folder + key);
            if (clip != null)
                _source.PlayOneShot(clip, Volume);
        }
    }
}

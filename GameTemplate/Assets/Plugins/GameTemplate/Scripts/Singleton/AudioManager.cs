﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using yaSingleton;

//TODO: add audio source pooling 
[CreateAssetMenu(fileName = "Audio Manager", menuName = "Singletons/AudioManager")]
public class AudioManager : Singleton<AudioManager> {
	public enum AudioChannel : byte { Master, Music, Sound }

	const int lowestDecibles = -80;

	public bool IsEnabled {
		get {
			return isEnabled;
		}
		set {
			isEnabled = value;
			TemplateGameManager.Instance.settingsData.audioSettigns.isEnabled = isEnabled;

			if (value)
				masterMixer.SetFloat(audioGroups[0].volumeExposedStrings, GetAdjustedVolume(TemplateGameManager.Instance.settingsData.audioSettigns.volumes[0]));
			else
				masterMixer.SetFloat(audioGroups[0].volumeExposedStrings, GetAdjustedVolume(0.0f));
		}
	}
	bool isEnabled;

	[Header("Audio mixer refs")]
	public AudioMixer masterMixer;
	public AudioGroupData[] audioGroups;

	[Header("3D sound settings")]
	public bool is3DGame = false;
	public float maxSoundDistance = 20.0f;
	public AnimationCurve volumeRolloff;
	public AnimationCurve spread;
	
	[Header("Music settings")]
	public float crossfadeTime = 2.0f;
	public float muteTime = 0.25f;

	[Header("Misc settings")]
	public float minTimeBetweenSfx = 0.1f;

	Dictionary<AudioClip, AudioSource> musicAudioSources;
	Dictionary<AudioClip, float> audioPlayedTracker;

	AudioClip currMusicClip;
	AudioClip lastMusicClip;
	float lastMusicVolume;

	protected override void Initialize() {
		base.Initialize();

		audioPlayedTracker = new Dictionary<AudioClip, float>();
		musicAudioSources = new Dictionary<AudioClip, AudioSource>();
		lastMusicClip = currMusicClip = null;
		lastMusicVolume = 0.0f;

		StartCoroutine(DelayedSetup());

		IEnumerator DelayedSetup() {
			yield return null;
			yield return null;
			
		}
	}

	public void ApplySettings(AudioSettigns settigns) {
		IsEnabled = settigns.isEnabled;

		for(int i = 0; i < audioGroups.Length; ++i) {
			if (settigns.volumes.Count < i)
				settigns.volumes.Add(1.0f);

			SetVolume((AudioChannel)i, settigns.volumes[i]);
		}
	}

	//--------------------------------------------------------------------------------------
	// General
	/// <param name="volume">[0..1]</param>
	public void SetVolume(AudioChannel channel, float volume) {
		float adjustedVolume = GetAdjustedVolume(volume);

		TemplateGameManager.Instance.settingsData.audioSettigns.volumes[(int)channel] = volume;
		masterMixer.SetFloat(audioGroups[(int)channel].volumeExposedStrings, adjustedVolume);

	}

	public float GetVolume(AudioChannel channel) {
		return TemplateGameManager.Instance.settingsData.audioSettigns.volumes[(int)channel];
	}

	//--------------------------------------------------------------------------------------
	//Music 2D
	public void PlayMusic(AudioClip clip, float volume = 1.0f) {
		if (currMusicClip != clip) {
			AudioSource oldSource = currMusicClip != null ? musicAudioSources[currMusicClip] : null;
			currMusicClip = clip;

			lastMusicClip = currMusicClip;
			lastMusicVolume = volume;

			if (currMusicClip != null) {
				if (musicAudioSources.ContainsKey(currMusicClip) && musicAudioSources[currMusicClip] != null) {
					AudioSource newAS = musicAudioSources[currMusicClip];
					ChangeASVolume(newAS, volume);
				}
				else {
					musicAudioSources[currMusicClip] = PlayLoop(currMusicClip, volume, playDelay: crossfadeTime);
					DontDestroyOnLoad(musicAudioSources[currMusicClip].gameObject);
				}
			}

			ChangeASVolume(oldSource, 0.0f);
		}
	}

	public AudioSource PlayLoop(AudioClip clip, Transform emitter, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Music) {
		AudioSource source = CreatePlaySource(clip, emitter, volume, pitch, playDelay, channel);
		source.loop = true;
		return source;
	}

	public AudioSource PlayLoop(AudioClip clip, Vector3 point, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Music) {
		AudioSource source = CreatePlaySource(clip, point, volume, pitch, playDelay, channel);
		source.loop = true;
		return source;
	}

	public AudioSource PlayLoop(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Music) {
		AudioSource source = CreatePlaySource(clip, Vector3.zero, volume, pitch, playDelay, channel);
		source.loop = true;
		return source;
	}
	   

	//--------------------------------------------------------------------------------------
	//Music 3D
	public AudioSource PlayLoop3D(AudioClip clip, Transform emitter, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Music) {
		AudioSource source = CreatePlaySource3D(clip, emitter, volume, pitch, playDelay, channel);
		source.loop = true;
		return source;
	}

	public AudioSource PlayLoop3D(AudioClip clip, Vector3 point, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Music) {
		AudioSource source = CreatePlaySource3D(clip, point, volume, pitch, playDelay, channel);
		source.loop = true;
		return source;
	}


	//--------------------------------------------------------------------------------------
	//Mute, resume and delete music
	public void MuteMusic() {
		MuteMusic(muteTime);
	}

	public void MuteMusic(float time) {
		if (currMusicClip == null)
			return;

		AudioSource source = musicAudioSources[currMusicClip];
		if (source == null)
			return;

		ChangeASVolume(source, 0.0f, time);

		currMusicClip = null;
	}

	public void ContinueMusicAfterMute() {
		PlayMusic(lastMusicClip, lastMusicVolume);
	}

	public void DeleteAllPlayedMusic(float delay = 0.0f) {
		if (delay == 0) {
			DeleteMusicInner();
		}
		else {
			LeanTween.delayedCall(delay, DeleteMusicInner);
		}

		void DeleteMusicInner() {
			foreach (var musicAudioSource in musicAudioSources)
				Destroy(musicAudioSource.Value.gameObject);
			musicAudioSources.Clear();

			lastMusicClip = currMusicClip = null;
			lastMusicVolume = 0.0f;
		}
	}

	public void MuteMusicAndDelete() {
		MuteMusicAndDelete(muteTime);
	}

	public void MuteMusicAndDelete(float time) {
		MuteMusic(time);
		DeleteAllPlayedMusic(time + 0.1f);
	}


	//--------------------------------------------------------------------------------------
	//2D sound
	public AudioSource Play(AudioClip clip, Transform emitter, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Sound) {
		if ((!IsEnabled && channel != AudioChannel.Music) || (GetSinceLastPlayed(clip) > minTimeBetweenSfx))
			return null;
		AudioSource source = CreatePlaySource(clip, emitter, volume, pitch, playDelay, channel);
		Destroy(source.gameObject, clip.length + 1.0f);
		return source;
	}

	public AudioSource Play(AudioClip clip, Vector3 point, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Sound) {
		if ((!IsEnabled && channel != AudioChannel.Music) || (GetSinceLastPlayed(clip) > minTimeBetweenSfx))
			return null;
		AudioSource source = CreatePlaySource(clip, point, volume, pitch, playDelay, channel);
		Destroy(source.gameObject, clip.length + 1.0f);
		return source;
	}

	public AudioSource Play(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Sound) {
		if ((!IsEnabled && channel != AudioChannel.Music) || (GetSinceLastPlayed(clip) < minTimeBetweenSfx))
			return null;
		AudioSource source = CreatePlaySource(clip, Vector3.zero, volume, pitch, playDelay, channel);
		Destroy(source.gameObject, clip.length + 1.0f);
		return source;
	}

	AudioSource CreatePlaySource(AudioClip clip, Transform emitter, float volume, float pitch, float playDelay, AudioChannel channel) {
		GameObject go = new GameObject("Audio: " + clip.name);
		go.transform.position = emitter.position;
		go.transform.parent = emitter;

		AudioSource source = AddAudioSource(go, clip, volume, pitch, channel);

		PlayDelayed(source, playDelay);
		return source;
	}

	AudioSource CreatePlaySource(AudioClip clip, Vector3 point, float volume, float pitch, float playDelay, AudioChannel channel) {
		GameObject go = new GameObject("Audio: " + clip.name);
		go.transform.position = point;

		AudioSource source = AddAudioSource(go, clip, volume, pitch, channel);

		PlayDelayed(source, playDelay);
		return source;
	}

	AudioSource AddAudioSource(GameObject go, AudioClip clip, float volume, float pitch, AudioChannel channel) {
		AudioSource source = go.AddComponent<AudioSource>();
		source.clip = clip;
		source.volume = volume;
		source.pitch = pitch;
		source.outputAudioMixerGroup = GetAudioMixer(channel);

		SetLastPlayedTime(clip);

		return source;
	}


	//--------------------------------------------------------------------------------------
	//3D sound
	public AudioSource Play3D(AudioClip clip, Transform emitter, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Sound) {
		if ((!IsEnabled && channel != AudioChannel.Music) || (GetSinceLastPlayed(clip) < minTimeBetweenSfx))
			return null;
		AudioSource source = CreatePlaySource3D(clip, emitter, volume, pitch, playDelay, channel);
		Destroy(source.gameObject, clip.length + 1.0f);
		return source;
	}

	public AudioSource Play3D(AudioClip clip, Vector3 point, float volume = 1.0f, float pitch = 1.0f, float playDelay = 0.0f, AudioChannel channel = AudioChannel.Sound) {
		if ((!IsEnabled && channel != AudioChannel.Music) || (GetSinceLastPlayed(clip) < minTimeBetweenSfx))
			return null;
		AudioSource source = CreatePlaySource3D(clip, point, volume, pitch, playDelay, channel);
		Destroy(source.gameObject, clip.length + 1.0f);
		return source;
	}

	AudioSource CreatePlaySource3D(AudioClip clip, Transform emitter, float volume, float pitch, float playDelay, AudioChannel channel) {
		GameObject go = new GameObject("Audio: " + clip.name);
		go.transform.parent = emitter;
		if (is3DGame)
			go.transform.position = emitter.position;
		else
			go.transform.position = new Vector3(emitter.position.x, emitter.position.y, TemplateGameManager.Instance.Camera.transform.position.z);

		AudioSource source = AddAudioSource3D(go, clip, volume, pitch, channel);

		PlayDelayed(source, playDelay);
		return source;
	}

	AudioSource CreatePlaySource3D(AudioClip clip, Vector3 point, float volume, float pitch, float playDelay, AudioChannel channel) {
		GameObject go = new GameObject("Audio: " + clip.name);
		if (is3DGame)
			go.transform.position = point;
		else
			go.transform.position = new Vector3(point.x, point.y, TemplateGameManager.Instance.Camera.transform.position.z);

		AudioSource source = AddAudioSource3D(go, clip, volume, pitch, channel);

		PlayDelayed(source, playDelay);
		return source;
	}

	AudioSource AddAudioSource3D(GameObject go, AudioClip clip, float volume, float pitch, AudioChannel channel) {
		AudioSource source = go.AddComponent<AudioSource>();
		source.clip = clip;
		source.pitch = pitch;
		source.outputAudioMixerGroup = GetAudioMixer(channel);

		source.minDistance = 0.0f;
		source.maxDistance = maxSoundDistance;
		source.spatialBlend = 1.0f;

		source.rolloffMode = AudioRolloffMode.Custom;
		source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloff);
		source.SetCustomCurve(AudioSourceCurveType.Spread, spread);

		SetLastPlayedTime(clip);

		return source;
	}


	//--------------------------------------------------------------------------------------
	//Inner helpers 
	public void ChangeASVolume(AudioSource source, float volume) {
		ChangeASVolume(source, volume, crossfadeTime);
	}

	public void ChangeASVolume(AudioSource source, float volume, float time) {
		if (source != null) {
			LeanTween.cancel(source.gameObject, false);
			if (time == 0) {
				source.volume = volume;
			}
			else {
				LeanTween.value(source.gameObject, source.volume, volume, time)
				.setIgnoreTimeScale(true)
				.setOnUpdate((float v) => {
					source.volume = v;
				});
			}
		}
	}

	float GetAdjustedVolume(float volume) {
		return volume <= 0.0001f ? lowestDecibles : Mathf.Log10(volume) * 20;
	}

	void PlayDelayed(AudioSource source, float delay) {
		if (delay == 0) {
			source.Play();
		}
		else {
			float savedVolume = source.volume;
			source.volume = 0.0f;
			source.Play();
			ChangeASVolume(source, savedVolume, delay);
		}
	}

	AudioMixerGroup GetAudioMixer(AudioChannel channel) {
		return audioGroups[(int)channel].mixer;
	}

	float GetSinceLastPlayed(AudioClip clip) {
		if (audioPlayedTracker.TryGetValue(clip, out var lastPlayedTime))
			return (Time.unscaledTime - lastPlayedTime);
		return float.MaxValue;
	}

	void SetLastPlayedTime(AudioClip clip) {
		if (audioPlayedTracker.TryGetValue(clip, out var lastPlayedTime)) 
			audioPlayedTracker[clip] = Time.unscaledTime;
		else 
			audioPlayedTracker.Add(clip, Time.unscaledTime);
	}

	[Serializable]
	public struct AudioGroupData {
		public AudioMixerGroup mixer;
		public string volumeExposedStrings;
		public string translatedStringForSettings;
	}
}

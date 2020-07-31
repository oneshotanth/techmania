﻿using System;
using System.Collections.Generic;

// Track is the container of all patterns in a musical track. In anticipation of
// format updates, each format version is a derived class of TrackBase.
//
// Because class names are not serialized, we can change class names
// however we want without breaking old files, so the current version
// class will always be called "Track", and deprecated versions will be
// called "TrackVersion1" or such.

[Serializable]
public class TrackBase
{
    public string version;

    private string Serialize()
    {
        return UnityEngine.JsonUtility.ToJson(this, prettyPrint: true);
    }
    private static TrackBase Deserialize(string json)
    {
        string version = UnityEngine.JsonUtility.FromJson<TrackBase>(json).version;
        switch (version)
        {
            case Track.kVersion:
                return UnityEngine.JsonUtility.FromJson<Track>(json);
                // For non-current versions, maybe attempt conversion?
            default:
                throw new Exception($"Unknown version: {version}");
        }
    }

    public TrackBase Clone()
    {
        return Deserialize(Serialize());
    }

    public void SaveToFile(string path)
    {
        System.IO.File.WriteAllText(path, Serialize());
    }

    public static TrackBase LoadFromFile(string path)
    {
        string fileContent = System.IO.File.ReadAllText(path);
        return Deserialize(fileContent);
    }
}

// Heavily inspired by bmson:
// https://bmson-spec.readthedocs.io/en/master/doc/index.html#format-overview
[Serializable]
public class Track : TrackBase
{
    public const string kVersion = "1";
    public Track() { version = kVersion; }
    public Track(string title, string artist)
    {
        version = kVersion;
        trackMetadata = new TrackMetadata();
        trackMetadata.title = title;
        trackMetadata.artist = artist;
        patterns = new List<Pattern>();
    }

    public TrackMetadata trackMetadata;
    public List<Pattern> patterns;
}

[Serializable]
public class TrackMetadata
{
    // Text stuff.

    public string title;
    public string subtitle;
    public string artist;
    public List<string> subArtists;
    public string genre;

    // In track select screen.

    // Filename of eyecatch image.
    public string eyecatchImage;
    // Filename of preview music.
    public string previewTrack;
    // In seconds.
    public double previewStartTime;
    public double previewEndTime;

    // In gameplay.

    // Filename of background image, used in loading screen
    public string backImage;
    // Filename of background animation (BGA)
    // If empty, will show background image
    public string bga;
    // Play BGA from this time.
    public double bgaStartTime;
}

[Serializable]
public class Pattern
{
    public PatternMetadata patternMetadata;
    public List<BpmEvent> bpmEvents;
    public List<SoundChannel> soundChannels;

    public const int pulsesPerBeat = 240;

    // Assumes no note exists at the same location.
    public void AddNote(Note n, string sound)
    {
        if (soundChannels == null)
        {
            soundChannels = new List<SoundChannel>();
        }
        SoundChannel channel = soundChannels.Find(
            (SoundChannel c) => { return c.name == sound; });
        if (channel == null)
        {
            channel = new SoundChannel();
            channel.name = sound;
            channel.notes = new List<Note>();
            soundChannels.Add(channel);
        }
        channel.notes.Add(n);
    }

    public void ModifyNoteKeysound(Note n, string oldSound, string newSound)
    {
        SoundChannel oldChannel = soundChannels.Find(
            (SoundChannel c) => { return c.name == oldSound; });
        if (oldChannel == null)
        {
            throw new Exception(
                $"Sound channel {oldSound} not found in pattern when modifying keysound.");
        }
        SoundChannel newChannel = soundChannels.Find(
            (SoundChannel c) => { return c.name == newSound; });
        if (newChannel == null)
        {
            newChannel = new SoundChannel();
            newChannel.name = newSound;
            newChannel.notes = new List<Note>();
            soundChannels.Add(newChannel);
        }

        oldChannel.notes.Remove(n);
        newChannel.notes.Add(n);
    }

    public void DeleteNote(Note n, string sound)
    {
        SoundChannel channel = soundChannels.Find(
            (SoundChannel c) => { return c.name == sound; });
        if (channel == null)
        {
            throw new Exception(
                $"Sound channel {sound} not found in pattern when deleting.");
        }
        channel.notes.Remove(n);
    }
}

[Serializable]
public enum ControlScheme
{
    Touch = 0,
    Keys = 1,
    KM = 2
}

[Serializable]
public class PatternMetadata
{
    public string patternName;
    public int level;
    public ControlScheme controlScheme;

    // The backing track played in game.
    // This always plays from the beginning.
    // If no keysounds, this should be the entire track.
    public string backingTrack;
    // Beat 0 starts at this time.
    public double firstBeatOffset;

    // These can be changed by events.
    public double initBpm;
    // BPS: beats per scan.
    public int bps;
}

[Serializable]
public class BpmEvent
{
    public int pulse;
    public double bpm;
}

[Serializable]
public class SoundChannel
{
    // Sound file name.
    public string name;
    // Notes using this sound.
    public List<Note> notes;
    public List<DragNote> dragNotes;
}

[Serializable]
public enum NoteType
{
    Basic,
    ChainHead,
    Chain,
    HoldStart,
    HoldEnd,
    Drag,
    RepeatHead,
    RepeatHeadHold,
    Repeat,
    RepeatHoldStart,
    RepeatHoldEnd,
}

[Serializable]
public class Note
{
    public int lane;
    public int pulse;
    public NoteType type;

    public Note Clone()
    {
        return new Note()
        {
            lane = this.lane,
            pulse = this.pulse,
            type = this.type
        };
    }
}

[Serializable]
public class DragNotePath
{
    public int lane;
    public int pulse;
}

[Serializable]
public class DragNote : Note
{
    public List<DragNotePath> path;
}
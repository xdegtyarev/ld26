/*************************************************************
 *       Unity Audio Toolkit (c) by ClockStone 2012          *
 * 
 * Provides useful features for playing audio files in Unity:
 * 
 *  - ease of use: play audio files with a simple static function call, creation 
 *    of required AudioSource objects is handled automatically 
 *  - conveniently define audio assets in categories
 *  - set properties such as the volume for the entire category
 *  - change the volume of all playing audio objects within a category at any time
 *  - define alternative audio clips that get played with a specified 
 *    probability or order
 *  - uses audio object pools for optimized performance
 *  - set audio playing parameters conveniently, such as: 
 *       + random pitch & volume
 *       + minimum time difference between play calls
 *       + delay
 *       + looping
 *  - fade out / in 
 *  - special functions for music including cross-fading 
 *  - music track playlist management with shuffle, loop, etc.
 *  - delegate event call if audio was completely played
 * 
 * 
 * Usage:
 *  - create a unique GameObject named "AudioController" with the 
 *    AudioController script component added
 *  - Create an AudioObject prefab containing the following components: Unity's AudioSource, the AudioObject script, 
 *    and the PoolableObject script (if pooling is wanted). 
 *    Then set your custom AudioSource parameters in this prefab. Next, specify your custom prefab in the AudioController as 
 *    the "audio object".
 *  - create your audio categories in the AudioController using the Inspector, e.g. "Music", "SFX", etc.
 *  - for each audio to be played by a script create an 'audio item' with a unique name. 
 *  - specify any number of audio sub-items (= the AudioClip plus parameters) within an audio item. 
 *  - to play an audio item call the static function 
 *    AudioController.Play( "MyUniqueAudioItemName" )
 *  - Use AudioController.PlayMusic( "MusicAudioItemName" ) to play music. This function assures that only 
 *    one music file is played at a time and handles cross fading automatically according to the configuration
 *    in the AudioController instance
 *  - Note that when you are using pooling and attach an audio object to a parent object then make sure the parent 
 *    object gets deleted using ObjectPoolController.Destroy()
 *   
 ************************************************************/

using UnityEngine;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System;

#pragma warning disable 1591 // undocumented XML code warning


/// <summary>
/// An audio category represents a set of AudioItems. Categories allow to change the volume of all containing audio items.
/// </summary>
[System.Serializable] 
public class AudioCategory
{
    /// <summary>
    /// The name of category ( = <c>categoryID</c> )
    /// </summary>
    public string Name;

    /// <summary>
    /// The volume factor applied to all audio items in the category.
    /// You change the volume by script and the change will be apply to all 
    /// playing audios immediately.
    /// </summary>
    public float Volume
    {
        get { return _volume; }
        set { _volume = value; _ApplyVolumeChange();  }
    }

    public int MaxSimultaniousSoundItems;

    /// <summary>
    /// Allows to define a specific audio object prefab for this category. If none is defined, 
    /// the default prefab as set by <see cref="AudioController.AudioObjectPrefab"/> is taken.
    /// </summary>
    /// <remarks> This way you can e.g. use special effects such as the reverb filter for 
    /// a specific category. Just add the respective filter component to the specified prefab.</remarks>
    public GameObject AudioObjectPrefab;

    /// <summary>
    /// Define your AudioItems using Unity inspector.
    /// </summary>  
    public AudioItem[] AudioItems;

    [SerializeField]
    private float _volume = 1.0f;

    internal void _AnalyseAudioItems( Dictionary<string, AudioItem> audioItemsDict )
    {
        if ( AudioItems == null ) return;

        foreach ( AudioItem ai in AudioItems )
        {
            if ( ai != null )
            {
                ai.category = this;
#if AUDIO_TOOLKIT_DEMO
                int? demoMaxNumAudioItemsConst = 0x12345B;

                int? demoMaxNumAudioItems = (demoMaxNumAudioItemsConst & 0xf);
                demoMaxNumAudioItems++;

                if ( audioItemsDict.Count > demoMaxNumAudioItems )
                {
                    Debug.LogError( "Audio Toolkit: The demo version does not allow more than " + demoMaxNumAudioItems + " audio items." );
                    Debug.LogWarning( "Please buy the full version of Audio Toolkit!" );
                    return;
                }
#endif
                _NormalizeAudioItem( ai.subItems );

                //Debug.Log( string.Format( "SubItem {0}: {1} {2} {3}", fi.Name, ai.FixedOrder, ai.RandomOrderStart, ai._lastChosen ) );
                try
                {
                    audioItemsDict.Add( ai.Name, ai );
                }
                catch ( ArgumentException )
                {
                    Debug.LogWarning( "Multiple audio items with name " + ai.Name );
                }
            }

        }
    }

    private static void _NormalizeAudioItem( AudioSubItem[] audioItems )
    {
        float sum = 0.0f;

        int subItemID = 0;

        foreach ( AudioSubItem i in audioItems )
        {
            if ( _IsValidAudioItem( i ) )
            {
                sum += i.Probability;
            }
            i._subItemID = subItemID;
            subItemID++;
        }

        if ( sum <= 0 )
        {
            return;
        }

        // Compute normalized probabilities

        float summedProb = 0;

        foreach ( AudioSubItem i in audioItems )
        {
            if ( _IsValidAudioItem( i ) )
            {
                summedProb += i.Probability / sum;

                i._SummedProbability = summedProb;
            }
        }
    }
    private static bool _IsValidAudioItem( AudioSubItem item )
    {
        return item.Clip != null;
    }

    private void _ApplyVolumeChange() 
    {
        // TODO: change Volume into a property and call ApplyVolumeChange automatically (requires editor inspector adaption!) 

        AudioObject[] objs = RegisteredComponentController.GetAllOfType<AudioObject>();

        foreach ( AudioObject o in objs )
        {
            if ( o.category == this )
            {
                //if ( o.IsPlaying() )
                {
                    o._ApplyVolume();
                }
            }
        }
    }
}

/// <summary>
/// The AudioItem class represents a uniquely named audio entity that can be played by scripts.
/// </summary>
/// <remarks>
/// AudioItem objects are defined in an AudioCategory using the Unity inspector.
/// </remarks>
[System.Serializable]
public class AudioItem
{
    /// <summary>
    /// The unique name of the audio item ( = audioID )
    /// </summary>
    public string Name;

    /// <summary>
    /// If enabled the audio item will get looped when played.
    /// </summary>
    public bool Loop = false;

    /// <summary>
    /// If disabled, the audio will keep on playing if a new scene is loaded.
    /// </summary>
    public bool DestroyOnLoad = true; 

    /// <summary>
    /// The volume applied to all audio sub-items of this audio item. 
    /// </summary>
    public float Volume = 1;
    
    /// <summary>
    /// If disabled, sub-items are chosen one after the other, starting with the first, 
    /// otherwise chosen according to the sub-items <c>Probability</c> value. 
    /// </summary>
    public bool FixedOrder = false;

    /// <summary>
    /// Only meaningful in combination with <c>FixedOrder = true</c>. If <c>RandomOrderStart</c> is enabled the audio sub-items are are played in order starting with a random sub-item. Otherwise the sub-item with index 0 will be chosen first.
    /// </summary>
    public bool RandomOrderStart = true;

    /// <summary>
    /// Assures that the same audio item will not be played multiple times within this time frame. This is useful if several events triggered at almost the same time want to play the same audio item which can cause unwanted noise artifacts.
    /// </summary>
    public float MinTimeBetweenPlayCalls = 0.1f;

    /// <summary>
    /// Defers the playback of the audio item for <c>Delay</c> seconds.
    /// </summary>
    public float Delay = 0;

    /// <summary>
    /// If set to a valid <c>audioID</c> the specified audio item will be played instead.
    /// </summary>
    public string PlayInstead;
    
    /// <summary>
    /// If set to a valid <c>audioID</c> the specified audio item will be played in addition.
    /// </summary>
    public string PlayAdditional;

    /// <summary>
    /// Define your audio sub-items using the Unity inspector.
    /// </summary>
    public AudioSubItem[] subItems;

    internal int _lastChosen = -1;
    internal float _lastPlayedTime = -1;
    
    /// <summary>
    /// the <c>AudioCategroy</c> the audio item belongs to.
    /// </summary>
    public AudioCategory category
    {
        internal set;
        get;
    }

    void Awake()
    {
        _lastChosen = -1;
    }
}

/// <summary>
/// An AudioSubItem represents a specific Unity audio clip.
/// </summary>
/// <remarks>
/// Add your AudioSubItem to an AudioItem using the Unity inspector.
/// </remarks>
[System.Serializable]
public class AudioSubItem
{
    /// <summary>
    /// Specify the AudioClip using the Unity inspector
    /// </summary>
    public AudioClip Clip;
    
    /// <summary>
    /// The volume applied to the audio sub-item.
    /// </summary>
    public float Volume = 1.0f;

    /// <summary>
    /// If multiple sub-items are defined within an audio item, the specific audio clip is chosen with a probability in proportion to the <c>Probability</c> value.
    /// </summary>
    public float Probability = 1.0f;

    /// <summary>
    /// Alters the pitch in units of half-tones ( thus 12 = twice the speed)
    /// </summary>
    public float PitchShift = 0f;

    /// <summary>
    /// Alters the pan: -1..left,  +1..right
    /// </summary>
    public float Pan2D = 0;

    /// <summary>
    /// Randomly shifts the pitch in units of half-tones ( thus 12 = twice the speed)
    /// </summary>
    public float RandomPitch = 0; 

    /// <summary>
    /// Randomly shifts the volume +/- this value
    /// </summary>
    public float RandomVolume = 0; 

    /// <summary>
    /// Defers the playback of the audio sub-item for <c>Delay</c> seconds.
    /// </summary>
    public float Delay = 0;

    /// <summary>
    /// Overrides the audio clip length (in seconds).
    /// </summary>
    /// <remarks>
    /// Can be used as a workaround for an unknown clip length (e.g. for tracker files)
    /// </remarks>
    public float OverrideClipLength = 0;

    private float _summedProbability = -1.0f; // -1 means not initialized or invalid
    internal int _subItemID = 0;

    internal float _SummedProbability
    {
        get { return _summedProbability; }
        set { _summedProbability = value; }
    }

    /// <summary>
    /// Returns the name of the audio clip for debugging.
    /// </summary>
    /// <returns>
    /// The debug output string.
    /// </returns>
    public override string ToString()
    {
        return Clip.name + "   vol: " + Volume + "   p: " + Probability;
    }

}


/// <summary>
/// The audio managing class used to define and play audio items and categories.
/// </summary>
/// <remarks>
/// Exactly one instance of an AudioController must exist in each scene using the Audio Toolkit. It is good practice
/// to use a persisting AudioController object that survives scene loads by calling Unity's DontDestroyOnLoad() on the 
/// AudioController instance. 
/// </remarks>
/// <example>
/// Once you have defined your audio categories and items in the Unity inspector you can play music and sound effects 
/// very easily:
/// <code>
/// AudioController.Play( "MySoundEffect1" );
/// AudioController.Play( "MySoundEffect2", new Vector3( posX, posY, posZ ) );
/// AudioController.PlayMusic( "MusicTrack1" );
/// AudioController.SetCategoryVolume( "Music", 0.5f );
/// AudioController.PauseMusic();
/// </code>
/// </example>
/// 

#if AUDIO_TOOLKIT_DEMO
[AddComponentMenu( "ClockStone/Audio/AudioController Demo" )]
public class AudioController : MonoBehaviour // can not make DLL with SingletonMonoBehaviour
{
    static public AudioController Instance 
    {
        get {
            return UnitySingleton<AudioController>.GetSingleton( true, null );
        }
    }
#else
[AddComponentMenu( "ClockStone/Audio/AudioController" )]
public class AudioController : SingletonMonoBehaviour<AudioController>
{
#endif

    /// <summary>
    /// For use with the Unity inspector: Disables all audio playback.
    /// </summary>
    public bool DisableAudio
    {
        set
        {
            if ( value == true )
            {
                if ( UnitySingleton<AudioController>.GetSingleton( false, null ) != null ) // check if value changed by inspector
                {
                    StopAll();
                }
            }
            _audioDisabled = value;
        }
        get
        {
            return _audioDisabled;
        }
    }
   
    /// <summary>
    /// The global volume applied to all categories.
    /// You change the volume by script and the change will be apply to all 
    /// playing audios immediately.
    /// </summary>
    public float Volume
    {
        get { return _volume; }
        set { if ( value != _volume ) { _volume = value; _ApplyVolumeChange(); } }
    }

    /// <summary>
    /// You must specify your AudioObject prefab here using the Unity inspector.
    /// <list type="bullet">
    ///     <listheader>
    ///          <description>The prefab must have the following components:</description>
    ///     </listheader>
    ///     <item>
    ///       <term>AudioObject</term>
    ///       <term>AudioSource (Unity built-in)</term>
    ///       <term>PoolableObject</term> <description>only required if pooling is uses</description>
    ///     </item>
    /// </list>
    ///  
    /// </summary>
    public GameObject AudioObjectPrefab;

    /// <summary>
    /// Enables / Disables AudioObject pooling
    /// </summary>
    public bool UsePooledAudioObjects = true;
    
    /// <summary>
    /// If disabled, audios are not played if they have a resulting volume of zero.
    /// </summary>
    public bool PlayWithZeroVolume = false;

    /// <summary>
    /// Gets or sets the musicEnabled.
    /// </summary>
    /// <value>
    ///   <c>true</c> enables music; <c>false</c> disables music
    /// </value>
    public bool musicEnabled
    {
        get { return _musicEnabled; }
        set
        {
            if ( _musicEnabled == value ) return;
            _musicEnabled = value;

            if ( _currentMusic )
            {
                if ( value )
                {
                    if ( _currentMusic.IsPaused() )
                    {
                        _currentMusic.Play();
                    }
                }
                else
                {
                    _currentMusic.Pause();

                }
            }

        }
    }

    /// <summary>
    /// If set to a value > 0 (in seconds) music will automatically be cross-faded with this fading time.
    /// </summary>
    public float musicCrossFadeTime = 0;

    /// <summary>
    /// Specify your audio categories here using the Unity inspector.
    /// </summary>
    public AudioCategory[] AudioCategories;

    /// <summary>
    /// allows to specify a list of audioID that will be played as music one after the other
    /// </summary>
    public string[ ] musicPlaylist;

    /// <summary>
    /// specifies if the music playlist will get looped
    /// </summary>
    public bool loopPlaylist = false;

    /// <summary>
    /// enables / disables shuffling for the music playlist
    /// </summary>
    public bool shufflePlaylist = false;

    /// <summary>
    /// if enabled, the tracks on the playlist will get cross-faded as specified by <see cref="musicCrossFadeTime"/>
    /// </summary>
    public bool crossfadePlaylist = false;

    /// <summary>
    /// Mute time in between two tracks on the playlist.
    /// </summary>
    public float delayBetweenPlaylistTracks = 1;


    // **************************************************************************************************/
    //          public functions
    // **************************************************************************************************/

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> as music.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <param name="volume">The volume [default=1].</param>
    /// <param name="delay">The delay [default=0].</param>
    /// <param name="startTime">The start time [default=0]</param>
    /// <returns>
    /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
    /// </returns>
    /// <remarks>
    /// PlayMusic makes sure that only one music track is played at a time. If music cross fading is enabled in the AudioController
    /// fading is performed automatically.
    /// </remarks>
    static public AudioObject PlayMusic( string audioID, float volume, float delay, float startTime )
    {
        Instance._isPlaylistPlaying = false;
        return Instance._PlayMusic( audioID, volume, delay, startTime );
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> as music.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="PlayMusic( string, float, float, float )"/> with default paramters.
    /// </remarks>
	static public AudioObject PlayMusic( string audioID ) 
    { 
        return AudioController.PlayMusic( audioID, 1, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> as music.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="PlayMusic( string, float, float, float )"/> with default paramters.
    /// </remarks>
	static public AudioObject PlayMusic( string audioID, float volume ) 
    { 
        return AudioController.PlayMusic( audioID, volume, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> as music.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="PlayMusic( string, float, float, float )"/> with default paramters.
    /// </remarks>
	static public AudioObject PlayMusic( string audioID, float volume, float delay ) 
    { 
        return AudioController.PlayMusic( audioID, volume, delay, 0 ); 
    }

    /// <summary>
    /// Stops the currently playing music.
    /// </summary>
    /// <returns>
    /// <c>true</c> if any music was stopped, otherwise <c>false</c>
    /// </returns>
    static public bool StopMusic()
    {
        return Instance._StopMusic();
    }

    /// <summary>
    /// Pauses the currently playing music.
    /// </summary>
    /// <returns>
    /// <c>true</c> if any music was paused, otherwise <c>false</c>
    /// </returns>
    static public bool PauseMusic()
    {
        return Instance._PauseMusic();
    }

    /// <summary>
    /// Uses to test if music is paused
    /// </summary>
    /// <returns>
    /// <c>true</c> if music is paused, otherwise <c>false</c>
    /// </returns>
    static public bool IsMusicPaused()
    {
        if ( Instance._currentMusic != null )
        {
            return Instance._currentMusic.IsPaused();
        }
        return false;
    }

    /// <summary>
    /// Unpauses the current music.
    /// </summary>
    /// <returns>
    /// <c>true</c> if any music was unpaused, otherwise <c>false</c>
    /// </returns>
    static public bool UnpauseMusic()  // un-pauses music
    {
        if ( Instance._currentMusic != null && Instance._currentMusic.IsPaused() )
        {
            Instance._currentMusic.Play();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Enqueues an audio ID to the music playlist queue.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <returns>
    /// The number of music tracks on the playlist.
    /// </returns>
    static public int EnqueueMusic( string audioID )
    {
        return Instance._EnqueueMusic( audioID );
    }

    /// <summary>
    /// Start playing the music playlist.
    /// </summary>
    /// <returns>
    /// The <c>AudioObject</c> of the current music, or <c>null</c> if no music track could be played.
    /// </returns>
    static public AudioObject PlayMusicPlaylist()
    {
        return Instance._PlayMusicPlaylist();
    }

    /// <summary>
    /// Jumps to the next the music track on the playlist.
    /// </summary>
    /// <remarks>
    /// If shuffeling is enabled it will jump to the next randomly chosen track.
    /// </remarks>
    /// <returns>
    /// The <c>AudioObject</c> of the current music, or <c>null</c> if no music track could be played.
    /// </returns>
    static public AudioObject PlayNextMusicOnPlaylist()
    {
        if ( IsPlaylistPlaying() )
        {
            return Instance._PlayNextMusicOnPlaylist( 0 );
        }
        else
            return null;
    }

    /// <summary>
    /// Jumps to the previous music track on the playlist.
    /// </summary>
    /// <remarks>
    /// If shuffeling is enabled it will jump to the previously played track.
    /// </remarks>
    /// <returns>
    /// The <c>AudioObject</c> of the current music, or <c>null</c> if no music track could be played.
    /// </returns>
    static public AudioObject PlayPreviousMusicOnPlaylist()
    {
        if ( IsPlaylistPlaying() )
        {
            return Instance._PlayPreviousMusicOnPlaylist( 0 );
        }
        else
            return null;
    }

    /// <summary>
    /// Determines whether the playlist is playing.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if the current music track is from the playlist; otherwise, <c>false</c>.
    /// </returns>
    static public bool IsPlaylistPlaying()
    {
        return Instance._isPlaylistPlaying;
    }

    /// <summary>
    /// Clears the music playlist.
    /// </summary>
    static public void ClearPlaylist()
    {
        Instance.musicPlaylist = null;
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c>.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <param name="volume">The volume [default=1].</param>
    /// <param name="delay">The delay [default=0].</param>
    /// <param name="startTime">The start time [default=0]</param>
    /// <returns>
    /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
    /// </returns>
    /// <remarks>
    /// The audio clip will be played in front of the current audio listener (which is only relevant for a 3D audio clip)
    /// </remarks>
    static public AudioObject Play( string audioID, float volume, float delay, float startTime )
    {
        AudioListener al = GetCurrentAudioListener();

        if ( al == null )
        {
            Debug.LogWarning( "No AudioListener found in the scene" );
            return null;
        }

        return Play( audioID, al.transform.position + al.transform.forward, null, volume, delay, startTime );
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c>.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, float, float, float )"/> with default paramters.
    /// </remarks>
	static public AudioObject Play( string audioID ) 
    { 
        return AudioController.Play( audioID, 1, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c>.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, float, float, float )"/> with default paramters.
    /// </remarks>
	static public AudioObject Play( string audioID, float volume ) 
    { 
        return AudioController.Play( audioID, volume, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c>.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, float, float, float )"/> with default paramters.
    /// </remarks>
	static public AudioObject Play( string audioID, float volume, float delay ) 
    { 
        return AudioController.Play( audioID, volume, delay, 0 ); 
    }
		
    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <param name="parentObj">The parent transform.</param>
    /// <param name="volume">The volume [default=1].</param>
    /// <param name="delay">The delay [default=0].</param>
    /// <param name="startTime">The start time [default=0]</param>
    /// <returns>
    /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
    /// </returns>
    /// <remarks>
    /// If the audio clip is marked as 3D the audio clip will be played at the position of the parent transform. 
    /// As the audio object will get attached to the transform, it is important to destroy the parent object using the
    /// <see cref="ObjectPoolController.Destroy"/> function, even if the parent object is not poolable itself
    /// </remarks>
    static public AudioObject Play( string audioID, Transform parentObj, float volume, float delay, float startTime )
    {
        return Play( audioID, parentObj.position, parentObj, volume, delay, startTime );
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Transform, float, float, float )"/> with default parameters.
    /// </remarks>
    static public AudioObject Play( string audioID, Transform parentObj ) 
    { 
        return AudioController.Play( audioID, parentObj, 1, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Transform, float, float, float )"/> with default parameters.
    /// </remarks>
    static public AudioObject Play( string audioID, Transform parentObj, float volume ) 
    { 
        return AudioController.Play( audioID, parentObj, volume, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Transform, float, float, float )"/> with default parameters.
    /// </remarks>
    static public AudioObject Play( string audioID, Transform parentObj, float volume, float delay ) 
    { 
        return AudioController.Play( audioID, parentObj, volume, delay, 0 ); 
    }
	

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> at a specified position.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <param name="position">The position in world coordinates.</param>
    /// <param name="volume">The volume [default=1].</param>
    /// <param name="delay">The delay [default=0].</param>
    /// <param name="startTime">The start time [default=0]</param>
    /// <returns>
    /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
    /// </returns>
    /// <remarks>
    /// If the audio clip is marked as 3D the audio clip will be played at the specified world position.
    /// </remarks>
    static public AudioObject Play( string audioID, Vector3 position, float volume, float delay, float startTime )
    {
        return Play( audioID, position, null, volume, delay, startTime );
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> at a specified position.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Vector3, float, float, float )"/> with default parameters.
    /// </remarks>
    static public AudioObject Play( string audioID, Vector3 position ) 
    { 
        return AudioController.Play( audioID, position, 1, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> at a specified position.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Vector3, float, float, float )"/> with default parameters.
    /// </remarks>
	static public AudioObject Play( string audioID, Vector3 position, float volume ) 
    { 
        return AudioController.Play( audioID, position, volume, 0, 0 ); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> at a specified position.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Vector3, float, float, float )"/> with default parameters.
    /// </remarks>
	static public AudioObject Play( string audioID, Vector3 position, float volume, float delay ) 
    { 
        return AudioController.Play( audioID, position, volume, delay, 0 ); 
    }
	
    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform with a world offset.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <param name="worldPosition">The position in world coordinates.</param>
    /// <param name="parentObj">The parent transform.</param>
    /// <param name="volume">The volume [default=1].</param>
    /// <param name="delay">The delay [default=0].</param>
    /// <param name="startTime">The start time [default=0]</param>
    /// <returns>
    /// Returns the reference of the AudioObject that is used to play the audio item, or <c>null</c> if the audioID does not exist.
    /// </returns>
    /// <remarks>
    /// If the audio clip is marked as 3D the audio clip will be played at the position of the parent transform. 
    /// As the audio object will get attached to the transform, it is important to destroy the parent object using the
    /// <see cref="ObjectPoolController.Destroy"/> function, even if the parent object is not poolable itself
    /// </remarks>
    static public AudioObject Play( string audioID, Vector3 worldPosition, Transform parentObj, float volume, float delay, float startTime )
    {
        //Debug.Log( "Play: '" + audioID + "'" );
        return Instance._Play( audioID, volume, worldPosition, parentObj, delay, startTime );
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform with a world offset.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Vector3, Transform, float, float, float )"/> with default parameters.
    /// </remarks>
	static public AudioObject Play( string audioID, Vector3 worldPosition, Transform parentObj ) 
    { 
        return AudioController.Play( audioID, worldPosition, parentObj, 1, 0, 0); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform with a world offset.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Vector3, Transform, float, float, float )"/> with default parameters.
    /// </remarks>
	static public AudioObject Play( string audioID, Vector3 worldPosition, Transform parentObj, float volume ) 
    { 
        return AudioController.Play( audioID, worldPosition, parentObj, volume, 0, 0); 
    }

    /// <summary>
    /// Plays an audio item with the name <c>audioID</c> parented to a specified transform with a world offset.
    /// </summary>
    /// <remarks>
    /// Variant of <see cref="Play( string, Vector3, Transform, float, float, float )"/> with default parameters.
    /// </remarks>
	static public AudioObject Play( string audioID, Vector3 worldPosition, Transform parentObj, float volume, float delay ) 
    { 
        return AudioController.Play( audioID, worldPosition, parentObj, volume, delay, 0); 
    }

    /// <summary>
    /// Stops all playing audio items with name <c>audioID</c> with a fade-out.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <param name="fadeOutTime">The fade out time.</param>
    /// <returns></returns>
    static public bool Stop( string audioID, float fadeOutTime )
    {
        AudioItem sndItem = Instance._GetAudioItem( audioID );

        if ( sndItem == null )
        {
            Debug.LogWarning( "Audio item with name '" + audioID + "' does not exist" );
            return false;
        }

        if ( sndItem.PlayInstead.Length > 0 )
        {
            return Stop( sndItem.PlayInstead, fadeOutTime );
        }

        AudioObject[ ] audioObjs = GetPlayingAudioObjects( audioID );
        
        foreach( AudioObject  audioObj in audioObjs )
        {
            audioObj.Stop( fadeOutTime );
        }
        return audioObjs.Length > 0;
    }

    /// <summary>
    /// Stops all playing audio items with name <c>audioID</c>.
    /// </summary>
	static public bool Stop( string audioID ) 
    { 
        return AudioController.Stop( audioID, 0 ); 
    }

    /// <summary>
    /// Fades out all playing audio items (including the music).
    /// </summary>
    /// <param name="fadeOutTime">The fade out time.</param>
    static public void StopAll( float fadeOutTime )
    {
        Instance._StopMusic();

        AudioObject[] objs = RegisteredComponentController.GetAllOfType<AudioObject>();
        
        foreach ( AudioObject o in objs )
        {
            o.Stop( fadeOutTime );
        }
    }

    /// <summary>
    /// Fades out all playing audio items (including the music).
    /// </summary>
	static public void StopAll() 
    { 
        AudioController.StopAll( 0 ); 
    }

    /// <summary>
    /// Determines whether the specified audio ID is playing.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <returns>
    ///   <c>true</c> if the specified audio ID is playing; otherwise, <c>false</c>.
    /// </returns>
    static public bool IsPlaying( string audioID )
    {
        return GetPlayingAudioObjects( audioID ).Length > 0;
    }

    /// <summary>
    /// Returns an array of all playing audio objects with the specified <c>audioID</c>.
    /// </summary>
    /// <param name="audioID">The audio ID.</param>
    /// <returns>
    /// Array of all playing audio objects with the specified <c>audioID</c>.
    /// </returns>
    static public AudioObject[] GetPlayingAudioObjects( string audioID )
    {
        AudioObject[ ] objs = RegisteredComponentController.GetAllOfType<AudioObject>();
        var matchesList = new List<AudioObject>();

        foreach ( AudioObject o in objs )
        {
            if ( o.audioID == audioID )
            {
                if ( o.IsPlaying() )
                {
                    matchesList.Add( o );
                }
            }
        }
        return matchesList.ToArray();
    }

    static public bool isMaxPlayingAudioObjectInCategoryReached( AudioCategory category)
    { 
        int counter = 0;
        foreach (AudioObject o in RegisteredComponentController.GetAllOfType<AudioObject>())
        {
            if (o.category == category && o.IsPlaying()){
                    counter++;
            }
        }
        return counter >= category.MaxSimultaniousSoundItems;
    }

    /// <summary>
    /// Enables the music.
    /// </summary>
    /// <param name="b">if set to <c>true</c> [b].</param>
    static public void EnableMusic( bool b )
    {
        AudioController.Instance.musicEnabled = b;
    }

    /// <summary>
    /// Determines whether music is enabled.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if music is enabled; otherwise, <c>false</c>.
    /// </returns>
    static public bool IsMusicEnabled()
    {
        return AudioController.Instance.musicEnabled;
    }

    /// <summary>
    /// Gets the currently active Unity audio listener.
    /// </summary>
    /// <returns>
    /// Reference of the currently active AudioListener object.
    /// </returns>
    static public AudioListener GetCurrentAudioListener()
    {
        AudioController MyInstance = Instance;
        if ( MyInstance._currentAudioListener != null && MyInstance._currentAudioListener.gameObject == null ) // TODO: check if this is necessary and if it really works if object was destroyed
        {
            MyInstance._currentAudioListener = null;
        }

        if ( MyInstance._currentAudioListener == null )
        {
            MyInstance._currentAudioListener = (AudioListener) FindObjectOfType( typeof( AudioListener ) );
        }

        return MyInstance._currentAudioListener;
    }



    /// <summary>
    /// Gets the current music.
    /// </summary>
    /// <returns>
    /// Returns a reference to the AudioObject that is currently playing the music.
    /// </returns>
    static public AudioObject GetCurrentMusic()
    {
        return AudioController.Instance._currentMusic;
    }

    /// <summary>
    /// Gets a category.
    /// </summary>
    /// <param name="name">The category's name.</param>
    /// <returns></returns>
    static public AudioCategory GetCategory( string name )
    {
        return AudioController.Instance._GetCategory( name );
    }

    /// <summary>
    /// Changes the category volume. Also effects currently playing audio items.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <param name="volume">The volume.</param>
    static public void SetCategoryVolume( string name, float volume )
    {
        AudioCategory category = GetCategory( name );
        if ( category != null )
        {
            category.Volume = volume;
        }
        else
        {
            Debug.LogWarning( "No audio category with name " + name );
        }
    }

    /// <summary>
    /// Gets the category volume.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <returns></returns>
    static public float GetCategoryVolume( string name )
    {
        AudioCategory category = GetCategory( name );
        if ( category != null )
        {
            return category.Volume;
        }
        else
        {
            Debug.LogWarning( "No audio category with name " + name );
            return 0;
        }
    }

    /// <summary>
    /// Changes the global volume. Effects all currently playing audio items.
    /// </summary>
    /// <param name="volume">The volume.</param>
    static public void SetGlobalVolume(  float volume )
    {
        Instance.Volume = volume;
    }

    /// <summary>
    /// Gets the global volume.
    /// </summary>
    static public float GetGlobalVolume()
    {
        return Instance.Volume;
    }

    // **************************************************************************************************/
    //          private / protected functions and properties
    // **************************************************************************************************/

    protected AudioObject _currentMusic = null;
    protected AudioListener _currentAudioListener = null;

    private bool _musicEnabled = true;
    private bool _categoriesValidated = false;

    [SerializeField]
    private bool _audioDisabled = false;

    Dictionary<string, AudioItem> _audioItems = new Dictionary<string, AudioItem>();

    List<int> _playlistPlayed;
    bool _isPlaylistPlaying = false;

    [SerializeField]
    private float _volume = 1.0f;

    private void _ApplyVolumeChange()
    {
        AudioObject[ ] objs = RegisteredComponentController.GetAllOfType<AudioObject>();

        foreach ( AudioObject o in objs )
        {
            o._ApplyVolume();
        }
    }

    internal AudioItem _GetAudioItem( string audioID )
    {
        AudioItem sndItem;

        _ValidateCategories();

        if ( _audioItems.TryGetValue( audioID, out sndItem ) )
        {
            return sndItem;
        }

        return null;
    }

    protected AudioObject _PlayMusic( string audioID, float volume, float delay, float startTime )
    {
        AudioListener al = GetCurrentAudioListener();
        if ( al == null )
        {
            Debug.LogWarning( "No AudioListener found in the scene" );
            return null;
        }
        return _PlayMusic( audioID, al.transform.position + al.transform.forward, null, volume, delay, startTime );
    }

    protected bool _StopMusic()
    {
        _isPlaylistPlaying = false;

        if ( _currentMusic != null )
        {
            _currentMusic.Stop();
            _currentMusic = null;
            return true;
        }
        return false;
    }

    protected bool _PauseMusic()
    {
        if ( _currentMusic != null )
        {
            _currentMusic.Pause();
            return true;
        }
        return false;
    }

    protected AudioObject _PlayMusic( string audioID, Vector3 position, Transform parentObj, float volume, float delay, float startTime )
    {
        if ( !IsMusicEnabled() ) return null;

        bool doFadeIn;

        if ( _currentMusic != null )
        {
            doFadeIn = true;
            _currentMusic.Stop( musicCrossFadeTime );
        }
        else
            doFadeIn = false;

        //Debug.Log( "PlayMusic " + audioID );

        _currentMusic = _Play( audioID, volume, position, parentObj, delay, startTime );

        if ( _currentMusic && doFadeIn && musicCrossFadeTime > 0 )
        {
            _currentMusic.FadeIn( musicCrossFadeTime );
        }

        return _currentMusic;
    }

    protected int _EnqueueMusic( string audioID )
    {
        int newLength;

        if ( musicPlaylist == null )
        {
            newLength = 1;
        }
        else
            newLength = musicPlaylist.Length + 1;

        string[ ] newPlayList = new string[ newLength ];

        if ( musicPlaylist != null )
        {
            musicPlaylist.CopyTo( newPlayList, 0 );
        }

        newPlayList[ newLength - 1 ] = audioID;
        musicPlaylist = newPlayList;

        return newLength;
    }

    protected AudioObject _PlayMusicPlaylist()
    {
        _ResetLastPlayedList();
        return _PlayNextMusicOnPlaylist( 0 );
    }

    private AudioObject _PlayMusicTrackWithID( int nextTrack, float delay, bool addToPlayedList )
    {
        if ( nextTrack < 0 )
        {
            return null;
        }
        _playlistPlayed.Add( nextTrack );
        _isPlaylistPlaying = true;
        //Debug.Log( "nextTrack: " + nextTrack );
        AudioObject audioObj = _PlayMusic( musicPlaylist[ nextTrack ], 1, delay, 0 );

        if ( audioObj != null )
        {
            audioObj._isCurrentPlaylistTrack = true;
            audioObj.audio.loop = false;
        }
        return audioObj;
    }

    internal AudioObject _PlayNextMusicOnPlaylist( float delay )
    {
        int nextTrack = _GetNextMusicTrack();
        return _PlayMusicTrackWithID( nextTrack, delay, true );
    }

    internal AudioObject _PlayPreviousMusicOnPlaylist( float delay )
    {
        int nextTrack = _GetPreviousMusicTrack();
        return _PlayMusicTrackWithID( nextTrack, delay, false );
    }

    private void _ResetLastPlayedList()
    {
        _playlistPlayed.Clear();
    }

    protected int _GetNextMusicTrack()
    {
        if ( musicPlaylist == null || musicPlaylist.Length == 0 ) return -1;
        if ( musicPlaylist.Length == 1 ) return 0;

        if ( shufflePlaylist )
        {
            return _GetNextMusicTrackShuffled();
        }
        else
        {
            return _GetNextMusicTrackInOrder();

        }
    }

    protected int _GetPreviousMusicTrack()
    {
        if ( musicPlaylist == null || musicPlaylist.Length == 0 ) return -1;
        if ( musicPlaylist.Length == 1 ) return 0;

        if ( shufflePlaylist )
        {
            return _GetPreviousMusicTrackShuffled();
        }
        else
        {
            return _GetPreviousMusicTrackInOrder();

        }
    }

    private int _GetPreviousMusicTrackShuffled()
    {
        if ( _playlistPlayed.Count >= 2 )
        {
            int id = _playlistPlayed[ _playlistPlayed.Count - 2 ];

            _RemoveLastPlayedOnList();
            _RemoveLastPlayedOnList();

            return id;
        }
        else
            return -1;
    }

    private void _RemoveLastPlayedOnList()
    {
        _playlistPlayed.RemoveAt( _playlistPlayed.Count - 1 );
    }

    private int _GetNextMusicTrackShuffled()
    {
        var playedTracksHashed = new HashSet<int>();

        int disallowTracksCount = _playlistPlayed.Count;

        int randomElementCount;

        if ( loopPlaylist )
        {
            randomElementCount = Mathf.Clamp( musicPlaylist.Length / 4, 2, 10 );

            if ( disallowTracksCount > musicPlaylist.Length - randomElementCount )
            {
                disallowTracksCount = musicPlaylist.Length - randomElementCount;

                if ( disallowTracksCount < 1 ) // the same track must never be played twice in a row
                {
                    disallowTracksCount = 1; // musicPlaylist.Length is always >= 2 
                }
            }
        }
        else
        {
            // do not play the same song twice
            if ( disallowTracksCount >= musicPlaylist.Length ) 
            {
                return -1; // stop playing as soon as all tracks have been played 
            }
        }
        
        
        for ( int i = 0; i < disallowTracksCount; i++ )
        {
            playedTracksHashed.Add( _playlistPlayed[ _playlistPlayed.Count - 1 - i ] );
        }

        var possibleTrackIDs = new List<int>();

        for ( int i = 0; i < musicPlaylist.Length; i++ )
        {
            if ( !playedTracksHashed.Contains( i ) )
            {
                possibleTrackIDs.Add( i );
            }
        }

        return possibleTrackIDs[ UnityEngine.Random.Range( 0, possibleTrackIDs.Count ) ];
    }

    private int _GetNextMusicTrackInOrder()
    {
        if ( _playlistPlayed.Count == 0 )
        {
            return 0;
        }
        int next = _playlistPlayed[ _playlistPlayed.Count - 1 ] + 1;

        if ( next >= musicPlaylist.Length ) // reached the end of the playlist
        {
            if ( loopPlaylist )
            {
                next = 0;
            }
            else
                return -1;
        }
        return next;
    }

    private int _GetPreviousMusicTrackInOrder()
    {
        if ( _playlistPlayed.Count < 2 )
        {
            if ( loopPlaylist )
            {
                return musicPlaylist.Length - 1;
            }
            else
                return -1;
        }

        int next = _playlistPlayed[ _playlistPlayed.Count - 1 ] - 1;

        _RemoveLastPlayedOnList();
        _RemoveLastPlayedOnList();

        if ( next < 0 ) // reached the end of the playlist
        {
            if ( loopPlaylist )
            {
                next = musicPlaylist.Length - 1;
            }
            else
                return -1;
        }
        return next;
    }

    protected AudioObject _Play( string audioID, float volume, Vector3 worldPosition, Transform parentObj, float delay, float startTime )
    {
        if ( _audioDisabled ) return null;

        AudioCategory sndSet;

        AudioItem sndItem = _GetAudioItem( audioID );
        AudioObject audioObj = null;

        if ( sndItem == null )
        {
            Debug.LogWarning( "Audio item with name '" + audioID + "' does not exist" );
            return null;
        }
        sndSet = sndItem.category;

        //Debug.Log( "_Play '" + audioID + "'" );
        if(isMaxPlayingAudioObjectInCategoryReached(sndSet))
            return null;

        if ( sndItem.PlayInstead.Length > 0 )
        {
            return _Play( sndItem.PlayInstead, volume, worldPosition, parentObj, delay, startTime );
        }

        if ( sndItem.PlayAdditional.Length > 0 )
        {
            _Play( sndItem.PlayAdditional, volume, worldPosition, parentObj, delay, startTime );
        }

        if ( sndItem._lastPlayedTime > 0 )
        {
            if ( Time.fixedTime < sndItem._lastPlayedTime + sndItem.MinTimeBetweenPlayCalls )
            {
                //audioObj = GetPlayingAudioObject( audioID );
                //if ( audioObj )
                //{
                //    audioObj.Restart();
                //}
                return null;
            }
        }

        sndItem._lastPlayedTime = Time.fixedTime;

        AudioSubItem sndSubItem = _ChooseSubItem( sndItem );

        if ( sndSubItem != null )
        {
            audioObj = _PlayAudioSubItem( sndSet, sndSubItem, sndItem, volume, worldPosition, parentObj, delay, startTime );

            if ( audioObj )
            {
                audioObj.audioID = audioID;
            }
        }

        return audioObj;
    }

    protected AudioCategory _GetCategory( string name )
    {
        foreach ( AudioCategory cat in AudioCategories )
        {
            if ( cat.Name == name )
            {
                return cat;
            }
        }
        return null;
    }
#if AUDIO_TOOLKIT_DEMO
    protected virtual void Awake()
    {
#else
    protected override void Awake()
    {
        base.Awake();
#endif

        if ( AudioObjectPrefab == null )
        {
            Debug.LogError( "No AudioObject prefab specified in AudioController." );
        }
        else
        {
            _ValidateAudioObjectPrefab( AudioObjectPrefab );
        }
        _ValidateCategories();

        _playlistPlayed = new List<int>();
    }

    protected void _ValidateCategories()
    {
        if ( !_categoriesValidated )
        {
            foreach ( AudioCategory category in AudioCategories )
            {
                category._AnalyseAudioItems( _audioItems );
                
                if ( category.AudioObjectPrefab )
                {
                    _ValidateAudioObjectPrefab( category.AudioObjectPrefab );
                }
            }
            _categoriesValidated = true;
        }
    }

    protected void _CopyAudioSource( AudioSource dst, AudioSubItem src, float startTime )
    {
        dst.clip = src.Clip;
        dst.pitch = AudioObject._PitchTransform( src.PitchShift );
        dst.pan = src.Pan2D;
        dst.time = startTime;
    }

    protected static AudioSubItem _ChooseSubItem( AudioItem audioItem )
    {
        if ( audioItem.subItems == null ) return null;
        int arraySize = audioItem.subItems.Length;
        if ( arraySize == 0 ) return null;
        if ( arraySize == 1 )
        {
            return audioItem.subItems[ 0 ];
        }

        int chosen = 0;

        //Debug.Log( string.Format( "_ChooseSubItem {0} {1} {2}", audioItem.FixedOrder, audioItem.RandomOrderStart, audioItem._lastChosen ) );


        if ( audioItem.FixedOrder )
        {
            if ( audioItem.RandomOrderStart && audioItem._lastChosen == -1 )
            {
                chosen = UnityEngine.Random.Range( 0, arraySize );
                //Debug.Log( "randomOrderStart:" + chosen + " arraySize:" + arraySize );
            }
            else
                chosen = ( audioItem._lastChosen + 1 ) % arraySize;

        }
        else
        {
            float rnd = UnityEngine.Random.Range( 0, 1.0f );

            int i;
            for ( i = 0; i < arraySize - 1; i++ )
            {
                if ( audioItem.subItems[ i ]._SummedProbability > rnd )
                {
                    chosen = i;
                    break;
                }
            }
            if ( i == arraySize - 1 )
            {
                chosen = arraySize - 1;
            }
        }
        audioItem._lastChosen = chosen;
        //Debug.Log( "chose:" + chosen );
        return audioItem.subItems[ chosen ];
    }

    protected AudioObject _PlayAudioSubItem( AudioCategory audioCategory, AudioSubItem s, AudioItem audioItem, float volume, Vector3 worldPosition, Transform parentObj, float delay, float startTime )
    {
        if ( s.Clip == null ) return null;

        float volumeWithoutCategory = s.Volume * audioItem.Volume;
        
        if ( s.RandomVolume != 0 )
        {
            volumeWithoutCategory += UnityEngine.Random.Range( -s.RandomVolume, s.RandomVolume );
            volumeWithoutCategory = Mathf.Clamp01( volumeWithoutCategory );
        }

        float volumeWithCategory = volumeWithoutCategory * audioCategory.Volume;

        if ( !PlayWithZeroVolume && ( volumeWithCategory <= 0 || Volume <= 0 ) )
        {
            return null;
        }

        GameObject audioObjInstance;

        //Debug.Log( "PlayAudioItem clip:" + s.Clip.name );

        GameObject audioPrefab;

        if ( audioCategory.AudioObjectPrefab != null )
        {
            audioPrefab = audioCategory.AudioObjectPrefab;
        }
        else
            audioPrefab = AudioObjectPrefab;

        if ( audioItem.DestroyOnLoad )
        {
#if AUDIO_TOOLKIT_DEMO
            audioObjInstance = (GameObject) GameObject.Instantiate( audioPrefab, worldPosition, Quaternion.identity );

#else
            if ( UsePooledAudioObjects )
            {
                audioObjInstance = (GameObject) ObjectPoolController.Instantiate( audioPrefab, worldPosition, Quaternion.identity );
            }
            else
            {
                audioObjInstance = (GameObject) ObjectPoolController.InstantiateWithoutPool( audioPrefab, worldPosition, Quaternion.identity );
            }
#endif
        }
        else
        {   // pooling does not work for DontDestroyOnLoad objects
#if AUDIO_TOOLKIT_DEMO
            audioObjInstance = (GameObject) GameObject.Instantiate( audioPrefab, worldPosition, Quaternion.identity );
#else
            audioObjInstance = (GameObject) ObjectPoolController.InstantiateWithoutPool( audioPrefab, worldPosition, Quaternion.identity );
#endif
            DontDestroyOnLoad( audioObjInstance );
        }


        if ( parentObj )
        {
            audioObjInstance.transform.parent = parentObj;
        }

        AudioObject sndObj = audioObjInstance.gameObject.GetComponent<AudioObject>();

        _CopyAudioSource( sndObj.audio, s, startTime );

        sndObj.audio.loop = audioItem.Loop;
        sndObj._overrideClipLength = s.OverrideClipLength;
        sndObj._volumeExcludingCategory = volumeWithoutCategory;
        sndObj.category = audioCategory;

        sndObj._ApplyVolume();

        if ( s.RandomPitch != 0 )
        {
            sndObj.audio.pitch *= AudioObject._PitchTransform( UnityEngine.Random.Range( -s.RandomPitch, s.RandomPitch ) );
        }

        audioObjInstance.name = "AudioObject:" + sndObj.audio.clip.name;


        sndObj.Play( delay + s.Delay + audioItem.Delay );
        return sndObj;
    }

    internal void _NotifyPlaylistTrackCompleteleyPlayed( AudioObject audioObject )
    {
        audioObject._isCurrentPlaylistTrack = false;
        if ( IsPlaylistPlaying() )
        {
            if ( _currentMusic == audioObject )
            {
                if ( _PlayNextMusicOnPlaylist( delayBetweenPlaylistTracks ) == null )
                {
                    _isPlaylistPlaying = false;
                }
            }
        }
    }

    private void _ValidateAudioObjectPrefab( GameObject audioPrefab )
    {
        if ( UsePooledAudioObjects )
        {
#if AUDIO_TOOLKIT_DEMO
        Debug.LogWarning( "Poolable Audio objects not supported by the Audio Toolkit Demo version" );
#else
            if ( audioPrefab.GetComponent<PoolableObject>() == null )
            {
                Debug.LogWarning( "AudioObject prefab does not have the PoolableObject component. Pooling will not work." );
            }
#endif
        }

        if ( audioPrefab.GetComponent<AudioObject>() == null )
        {
            Debug.LogError( "AudioObject prefab must have the AudioObject script component!" );
        }
    }
}



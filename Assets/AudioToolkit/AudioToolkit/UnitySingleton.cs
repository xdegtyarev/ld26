/*************************************************************
 *       Unity Singleton Class (c) by ClockStone 2011        *
 * 
 * Allows to use a script components like a singleton
 * 
 * Usage:
 * 
 * Derive your script class MyScriptClass from 
 * SingletonMonoBehaviour<MyScriptClass>
 * 
 * Access the script component using the static function
 * MyScriptClass.Instance
 * 
 * use the static function SetSingletonAutoCreate( GameObject )
 * to specify a GameObject - containing the MyScriptClass component -  
 * that should be instantiated in case an instance is requested and 
 * and no objects exists with the MyScriptClass component.
 * 
 * ***********************************************************/

using System;
using UnityEngine;

#pragma warning disable 1591 // undocumented XML code warning

public class UnitySingleton<T>
    where T : UnityEngine.Object
{
    //private static WeakReference _instance = new WeakReference( null );

    static T _instance;

    public static T GetSingleton( bool throwErrorIfNotFound, GameObject autoCreateObject )
    {
        if ( _instance == null ) // Unity operator to check if object was destroyed, 
        {
            T component = GameObject.FindObjectOfType( typeof( T ) ) as T;

            if ( component == null )
            {
                if ( autoCreateObject != null )
                {
                    GameObject go = (GameObject) GameObject.Instantiate( autoCreateObject );
                    go.name = autoCreateObject.name; // removes "(clone)"

                    component = GameObject.FindObjectOfType( typeof( T ) ) as T;

                    if ( component == null )
                    {
                        Debug.LogError( "Auto created object does not have component " + typeof( T ).Name );
                        return null;
                    }
                }
                else
                {
                    if ( throwErrorIfNotFound )
                    {
                        Debug.LogError( "No singleton component " + typeof( T ).Name + " found in the scene." );
                    }
                    return null;
                }
            }

            _instance = component;
        }

        return _instance;
    }

    private UnitySingleton( )
    { }
}


/// <summary>
/// Provides singleton-like access to a unique instance of a MonoBehaviour. <para/>
/// </summary>
/// <example>
/// Derive your own class from SingletonMonoBehaviour. <para/>
/// <code>
/// public class MyScriptClass : SingletonMonoBehaviour&lt;MyScriptClass&gt;
/// {
///     public void MyFunction() { }
/// }
/// </code>
/// <para/>
/// access the instance by writing
/// <code>
/// MyScriptClass.Instance.MyFunction();
/// </code>
/// </example>
/// <typeparam name="T">Your singleton MonoBehaviour</typeparam>
public abstract class SingletonMonoBehaviour<T> : MonoBehaviour
    where T : SingletonMonoBehaviour<T>
{

    private static GameObject _autoCreateObject;
    private static int _GlobalInstanceCount = 0;
    private int _MyInstanceCount = 0;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static T Instance
    { get { return UnitySingleton<T>.GetSingleton( true, _autoCreateObject ); } }

    /// <summary>
    /// Checks if an instance of this MonoBehaviour exists.
    /// </summary>
    public static bool DoesInstanceExist( )
    {
        return UnitySingleton<T>.GetSingleton( false, null ) != null;
    }

    /// <summary>
    /// Activates the singleton instance.
    /// </summary>
    /// <remarks>
    /// Call this function if you set an singleton object inactive before ever accessing the <c>Instance</c>. This is 
    /// required because Unity does not (yet) offer a way to find inactive game objects.
    /// </remarks>
    public static void ActivateSingletonInstance() // 
    {
        UnitySingleton<T>.GetSingleton( true, null );
    }

    /**
     * Sets the game object that will be created automatically if an Instance is requested, but not found. 
     * Either the game object itself or one of its child object must then contain the singleton component
     */

    protected virtual void Awake( ) // should be called in derived class
    {
        //Debug.Log( "Awake: " + this.GetType().Name );

        _GlobalInstanceCount++;
        if ( _GlobalInstanceCount > 1 )
        {
            Debug.LogError( "More than one instance of SingletonMonoBehaviour " + typeof( T ).Name );
        }

        _MyInstanceCount++;
        if ( _MyInstanceCount > 1 )
        {
            // should be unreachable code
            Debug.LogError( "_MyInstanceCount > 1 " );
        }
    }

    /// <summary>
    /// Sets the object to be instantiated automatically if no instance of the singleton is found.
    /// </summary>
    /// <param name="autoCreateObject">The prefab to be instantiated automatically.</param>
    static public void SetSingletonAutoCreate( GameObject autoCreateObject )
    {
        _autoCreateObject = autoCreateObject;
    }

    protected virtual void OnDestroy()  // should be called in derived class
    {
        _IsDestroyed = true;

        if ( _MyInstanceCount > 0 )
        {
            _MyInstanceCount--;

            _GlobalInstanceCount--;
            if ( _GlobalInstanceCount < 0 )
            {
                // should be unreachable code
                Debug.LogError( "_GlobalInstanceCount < 0" );
            }
        }
    }

    private bool _IsDestroyed = false;
    /// <summary>
    /// Checks whether the singleton instance is destroyed.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if the singleton is destroyed; otherwise, <c>false</c>.
    /// </value>
    public bool IsDestroyed
    {
        get { return _IsDestroyed; }
    }
}




using UnityEngine;
using System.Collections;
using System;
public class Inputron : MonoBehaviour {
	public static event Action UpKeyDown;
	public static event Action RightKeyDown;
	public static event Action DownKeyDown;
	public static event Action LeftKeyDown;
	public static event Action SpaceKeyDown;
	public static event Action UpKeyUp;
	public static event Action RightKeyUp;
	public static event Action DownKeyUp;
	public static event Action LeftKeyUp;
	public static event Action SpaceKeyUp;
	// Use this for initialization
	void Start () {
		AudioController.Play("Theme");
	}
	
	// Update is called once per frame
	void Update () {
		#if UNITY_WEBPLAYER || UNITY_EDITOR || UNITY_STANDALONE
		ProcessKeyboardEvents();
		#endif
	}

	void ProcessKeyboardEvents(){
		if(Input.GetKeyDown(KeyCode.UpArrow)){
			if(UpKeyDown!=null)
				UpKeyDown();

		}
		if(Input.GetKeyDown(KeyCode.RightArrow)){
			if(RightKeyDown!=null)
				RightKeyDown();
		}
		if(Input.GetKeyDown(KeyCode.DownArrow)){
			if(DownKeyDown!=null)
				DownKeyDown();
		}
		if(Input.GetKeyDown(KeyCode.LeftArrow)){
			if(LeftKeyDown!=null)
				LeftKeyDown();
		}

		if(Input.GetKeyUp(KeyCode.UpArrow)){
			if(UpKeyUp!=null)
				UpKeyUp();
		}
		if(Input.GetKeyUp(KeyCode.RightArrow)){
			if(RightKeyUp!=null)
				RightKeyUp();
		}
		if(Input.GetKeyUp(KeyCode.DownArrow)){
			if(DownKeyUp!=null)
				DownKeyUp();
		}
		if(Input.GetKeyUp(KeyCode.LeftArrow)){
			if(LeftKeyUp!=null)
				LeftKeyUp();
		}

		if(Input.GetKeyDown(KeyCode.Space)){
			if(SpaceKeyDown!=null)
				SpaceKeyDown();
		}
		if(Input.GetKeyUp(KeyCode.Space)){
			if(SpaceKeyUp!=null)
				SpaceKeyUp();
		}
	}
}


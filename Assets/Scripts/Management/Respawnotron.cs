using UnityEngine;

public class Respawnotron : MonoBehaviour {
	public Transform LastCheckpoint;
	public static Respawnotron instance;
	void Awake(){
		instance = this;
	}

	public static void RegisterCheckPoint(Transform t){
		if(instance.LastCheckpoint != null){
			instance.LastCheckpoint.GetComponent<CheckPoint>().Deactivate();
		}
		instance.LastCheckpoint = t;
	}

	public static void Respawn(GameObject g)
	{
		AudioController.Play("Respawn");
		g.transform.position = instance.LastCheckpoint.position + Vector3.up * 10f;
	}
}

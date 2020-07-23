using UnityEngine;
using System.Collections;

public class Creature : MonoBehaviour {
	public Vector3[] localWaypoints;
	public Vector3[] waypoints;
	public bool done;
	public float maxWaitTime;
	public float moveSpeed;
	public int currentWaypointIndex;
	float TimeToStartNextWaypoint;
	void Start () {
		for(int i = 0; i< localWaypoints.Length; i++)
		{
			waypoints[i] = localWaypoints[i] + transform.position;
		}
		MoveToNextWaypoint();
	}

	void MoveToNextWaypoint(){
		done = false;
		currentWaypointIndex++;
		currentWaypointIndex %= waypoints.Length;
		iTween.MoveTo(gameObject,iTween.Hash("position", waypoints[currentWaypointIndex], "speed", moveSpeed, "oncomplete", "MoveComplete", "oncompletetarget", gameObject, "easetype", iTween.EaseType.linear));
	}

	public void MoveComplete()
	{
		done = true;
		iTween.Stop(gameObject);
		Debug.Log("CompleteMove");
		TimeToStartNextWaypoint = Time.timeSinceLevelLoad + Random.Range(0f,maxWaitTime);
	}

	void Update()
	{
		if(done){
			if(Time.timeSinceLevelLoad>TimeToStartNextWaypoint){
				MoveToNextWaypoint();
			}
		}
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.blue;
		for(int i = 0; i<waypoints.Length; i++)
		{
			Gizmos.DrawLine(waypoints[i], waypoints[(i+1)%waypoints.Length]);
		}
	}
}

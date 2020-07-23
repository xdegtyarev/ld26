using UnityEngine;
using System.Collections;

public class SmoothFollow : MonoBehaviour {
	public Transform target;
	void Update () {
		transform.position = new Vector3(((int)target.position.x - ((int)target.position.x % 320)),((int)target.position.y - ((int)target.position.y % 240)),transform.position.z);
	}
}

using UnityEngine;
using System.Collections;

public class FollowFixed : MonoBehaviour {
	public Transform target;

	void Update () {
	 transform.position = new Vector3((int) target.position.x, (int) target.position.y, transform.position.z);
	}
}

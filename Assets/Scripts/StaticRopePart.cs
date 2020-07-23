using UnityEngine;
using System.Collections;

public class StaticRopePart : MonoBehaviour {

	public StaticRopePart previous;
	public Vector3 start;
	public Vector3 end;

	void OnDrawGizmos() {
		Gizmos.DrawCube(start, new Vector3(0.5f, 0.5f, 0.5f));
		Gizmos.DrawSphere(end, 0.5f);
	}

	public void Setup(Vector3 start, Vector3 end, StaticRopePart previous) {
		this.previous = previous;
		this.start = start;
		this.end = end;

		var direction = end - start;

		transform.position = start;
		transform.eulerAngles =
			new Vector3(0, 0, -Mathf.Sign(direction.x) * Vector3.Angle(Vector3.up, direction));

		var sprite = GetComponent<tk2dTiledSprite>();
		sprite.dimensions = new Vector2(sprite.dimensions.x, direction.magnitude);
	}

	public void Delete() {
		if (previous != null) {
			previous.Delete();
		} else {
			// Delete rope connection point
			Destroy(hingeJoint.connectedBody.gameObject);
		}

		Destroy(gameObject);
	}

}

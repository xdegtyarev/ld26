using UnityEngine;

public class Ropatron : MonoBehaviour {
	public static Ropatron instance;

	public GameObject ropePrefab;

	void Start() {
		instance = this;
	}

	public GameObject CreateRope(Vector3 top, Rigidbody payload) {
		return CreateRope(top, payload, null);
	}

	public GameObject CreateRope(Vector3 start, Rigidbody payload, StaticRopePart previous) {
		var direction = payload.transform.position - start;

		var newRopePart = Instantiate(ropePrefab) as GameObject;
		newRopePart.hingeJoint.anchor = new Vector3(0, 0, 0);
		newRopePart.transform.position = start;
		newRopePart.transform.eulerAngles = new Vector3(0, 0, -Mathf.Sign(direction.x) * Vector3.Angle(Vector3.up, direction));

		var sprite = newRopePart.GetComponent<tk2dTiledSprite>();
		sprite.dimensions = new Vector2(sprite.dimensions.x, direction.magnitude);

		var dynamicRope = newRopePart.GetComponent<DynamicRopePart>();
		dynamicRope.previousStatic = previous;
		dynamicRope.payload = payload;

		return newRopePart;
	}

	public void DeleteRope(GameObject rope) {
		DeleteRope(rope.GetComponent<DynamicRopePart>());
	}

	public void DeleteRope(DynamicRopePart rope) {
		rope.Delete();
	}
}


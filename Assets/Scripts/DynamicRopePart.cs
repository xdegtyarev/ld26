using UnityEngine;
using System;

public class DynamicRopePart : MonoBehaviour {
	public float minThrethold = 0.01f;
	public float maxThrethold = 0.99f;
	public float minPartLength = 4.0f;

	public float connectThreshold = 4.0f;
	public float shrinkThreshold = 3.0f;
	public float newPartCreationDelay = 0.4f;
	public float reconnectionDelay = 0.4f;
	public float collideAcceleration = 1.2f;
	
	public event Action<DynamicRopePart> NewRope;

	private static float lastCreationTime = 0.0f;
	private static float reconnectionTime = 0.0f;

	public StaticRopePart previousStatic;
	public Rigidbody payload;

	private tk2dTiledSprite sprite;
	private BoxCollider boxCollider;

	void Start() {
		sprite = GetComponent<tk2dTiledSprite>();
		boxCollider = collider as BoxCollider;
	}

	Vector3 contactPoint(Collision collision) {
		var contact = Vector3.zero;
		var size = sprite.dimensions;
		var start = transform.TransformPoint(Vector3.zero);
		foreach (var collisionPoint in collision.contacts) {
			var pivot = new Vector3(collisionPoint.point.x + collisionPoint.normal.x * size.x,
			                        collisionPoint.point.y + collisionPoint.normal.y * size.x,
			                        start.z);
			var newPartLenght = (start - pivot).magnitude;
			var threshold =	newPartLenght / size.y;
			if (threshold >= minThrethold &&
				threshold <= maxThrethold &&
				newPartLenght >= minPartLength) {
				contact = pivot;
				break;
			}
		}
		return contact;
	}

	void setupRopeParts(Vector3 start, Vector3 middle) {
		var staticRope = gameObject.AddComponent(typeof(StaticRopePart)) as StaticRopePart;
		staticRope.Setup(start, middle, previousStatic);

		var dynamicRope = Ropatron.instance.CreateRope(middle, payload, staticRope);
		dynamicRope.hingeJoint.connectedBody = staticRope.rigidbody;

		Vector3 originalVelocity = payload.rigidbody.velocity;
		Vector3 originalAngularVelocity = payload.rigidbody.angularVelocity;
		
		payload.hingeJoint.connectedBody = dynamicRope.rigidbody;

		payload.rigidbody.velocity = originalVelocity * collideAcceleration;
		payload.rigidbody.angularVelocity = originalAngularVelocity * collideAcceleration;

		if (NewRope != null)
			NewRope(dynamicRope.GetComponent<DynamicRopePart>());
	}

	void splitRope(Collision collision) {
		Vector3 contact = contactPoint(collision);
		if (contact == Vector3.zero)
			return;

		var start = transform.TransformPoint(Vector3.zero);
		var pivot = new Vector3(contact.x, contact.y, start.z);

		setupRopeParts(start, pivot);

		Destroy(this);
	}

	void OnCollisionEnter(Collision collision) {
		// Avoid creating new rope parts too often
		if (Time.time - lastCreationTime > newPartCreationDelay) {
			splitRope(collision);
			lastCreationTime = Time.time;
		}
    }

	public void Expand(float delta) {
		sprite.dimensions = new Vector2(sprite.dimensions.x, sprite.dimensions.y + delta);

		Vector3 originalVelocity = payload.rigidbody.velocity;
		Vector3 originalAngularVelocity = payload.rigidbody.angularVelocity;

		// Explicitly disconnect payload, move it, and connect again
		// This prevents weird shaking effects and acceleration
		payload.hingeJoint.connectedBody = null;

		payload.transform.position =
			transform.TransformPoint(Vector3.up * boxCollider.size.y);

		payload.hingeJoint.connectedBody = rigidbody;

		payload.rigidbody.velocity = originalVelocity;
		payload.rigidbody.angularVelocity = originalAngularVelocity;
	}

	public void Shrink(float delta) {
		if (previousStatic == null) {
			if (sprite.dimensions.y - delta > shrinkThreshold * 4)		
				Expand(-delta);
		} else {
			if (sprite.dimensions.y - delta < shrinkThreshold)
				connectWithPrevious();
			else			
				Expand(-delta);
		}
	}

	void connectWithPrevious() {
		Debug.Log("Connected");
		var connected =
			Ropatron.instance.CreateRope(previousStatic.start, payload, previousStatic.previous);
		connected.hingeJoint.connectedBody =
			previousStatic.previous != null
				? previousStatic.previous.rigidbody
				: previousStatic.hingeJoint.connectedBody;

		Vector3 originalVelocity = payload.rigidbody.velocity;
		Vector3 originalAngularVelocity = payload.rigidbody.angularVelocity;

		payload.hingeJoint.connectedBody = connected.rigidbody;

		payload.rigidbody.velocity = originalVelocity * collideAcceleration;
		payload.rigidbody.angularVelocity = originalAngularVelocity * collideAcceleration;

		if (NewRope != null)
			NewRope(connected.GetComponent<DynamicRopePart>());

		Destroy(previousStatic.gameObject);
		Destroy(gameObject);
	}

	void tryConnectWithPrevious() {
		if (previousStatic == null)
			return;

		var start = previousStatic.start;
		var middle = previousStatic.end;
		var end = 
			transform.TransformPoint(new Vector3(0, boxCollider.size.y, 0));

		var previous = middle - start;
		var self = end - middle;

		var previousAngle = Vector3.Angle(Vector3.down, previous);
		var selfAngle = Vector3.Angle(Vector3.down, self);

		if (Mathf.Sign(previous.x) != Mathf.Sign(self.x) &&
			selfAngle - previousAngle > connectThreshold &&
			Time.time - reconnectionTime > reconnectionDelay) {
			reconnectionTime = Time.time;
			connectWithPrevious();
		}
	}

	void Update() {
		tryConnectWithPrevious();
	}

	public void Delete() {
		if (previousStatic != null) {
			previousStatic.Delete();
		} else {
			// Delete rope connection point
			Destroy(hingeJoint.connectedBody.gameObject);
		}

		payload.hingeJoint.connectedBody = null;
		Destroy(gameObject);
	}

}

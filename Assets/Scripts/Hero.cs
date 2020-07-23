using UnityEngine;
using System.Collections;

public class Hero : MonoBehaviour {
	float HeightValue;
	bool isTied;
	bool isInAir;
	bool MoveRight;
	bool MoveLeft;
	bool isShrinking;
	bool isExpanding;
	bool collectedRope;
	public tk2dAnimatedSprite sprite;
	public tk2dSprite text;
	public Collider blockCOllider;
	public float moveSpeed = 80.0f;
	public float swingForce = 75.0f;
	public float jumpForce = 120.0f;



	DynamicRopePart rope;

	void OnEnable() {
		Inputron.UpKeyDown += HandleUpKeyDown;
		Inputron.RightKeyDown += HandleRightKeyDown;
		Inputron.LeftKeyDown += HandleLeftKeyDown;
		Inputron.LeftKeyUp += HandleLeftKeyUp;
		Inputron.RightKeyUp += HandleRightKeyUp;
		Inputron.SpaceKeyDown += HandleSpaceKeyDown;
		Inputron.SpaceKeyUp += HandleSpaceKeyUp;
		Inputron.DownKeyDown += HandleDownKeyDown;
		Inputron.UpKeyUp += HandleUpKeyUp;
		Inputron.DownKeyUp += HandleDownKeyUp;
	}

	void HandleDownKeyUp()	{
		if (isTied) {
			isExpanding = false;
		}
	}

	void HandleUpKeyUp() {
		if (isTied) {
			isShrinking = false;
		}
	}

	void HandleSpaceKeyUp() {
	}

	void HandleDownKeyDown() {
		if(isTied) {
			isExpanding = true;
		}
	}

	void TryConnectRope() {
		if(collectedRope){
		RaycastHit info;
		if (Physics.Raycast(transform.position, Vector3.up, out info,1000f)) {
			Debug.Log("rope connection at " + info.point);
			isTied = true;

			Vector3 originalVelocity = rigidbody.velocity;
			Vector3 originalAngularVelocity = rigidbody.angularVelocity;

			gameObject.AddComponent<HingeJoint>();
			hingeJoint.axis = Vector3.forward;

			Vector3 connection = info.point + Vector3.down * 2;
			GameObject r = Ropatron.instance.CreateRope(connection, rigidbody);
			hingeJoint.connectedBody = r.rigidbody;

			GameObject ceilingConnection = new GameObject("rope connection");
			ceilingConnection.transform.position = connection;
			r.hingeJoint.connectedBody = ceilingConnection.AddComponent<Rigidbody>();
			ceilingConnection.rigidbody.isKinematic = true;

			rope = r.GetComponent<DynamicRopePart>();
			rope.NewRope += OnNewRopePart;

			rigidbody.velocity = originalVelocity;
			rigidbody.angularVelocity = originalAngularVelocity;
			AudioController.Play("Rope");
			isInAir = true;
		} else {
			Debug.Log("no hit");
				AudioController.Play("Deny");
		}
		}
		else
		{
			AudioController.Play("Deny");
		}
	}

	void HandleSpaceKeyDown() {
		if (isTied) {
			Destroy(hingeJoint);
			rope.NewRope -= OnNewRopePart;
			Ropatron.instance.DeleteRope(rope);

			rope = null;

			isTied = false;
			isShrinking = false;
			isExpanding = false;
		} else {
			TryConnectRope();
			sprite.Play("Stand");
		}
	}

	void OnNewRopePart(DynamicRopePart part) {
		rope.NewRope -= OnNewRopePart;
		rope = part;
		rope.NewRope += OnNewRopePart;
	}

	void HandleRightKeyUp() {
		sprite.Stop();
		sprite.Play ("Stand");
		MoveRight = false;
	}

	void HandleLeftKeyUp() {
		sprite.Stop();
		sprite.Play ("Stand");
		MoveLeft = false;
	}

	void HandleLeftKeyDown() {
		MoveLeft = true;
		transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x),
		                                   transform.localScale.y,
		                                   transform.localScale.z);
	}

	void HandleRightKeyDown() {
		MoveRight = true;
		transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x),
		                                   transform.localScale.y,
		                                   transform.localScale.z);
	}


	void Update() {
		if ((MoveRight || MoveLeft) && !sprite.IsPlaying("HeroRun") && !isInAir && !isTied){
			sprite.Play("HeroRun");
		}
		if ((MoveRight || MoveLeft) && sprite.IsPlaying("HeroRun") && !isInAir && !isTied){
			if(!AudioController.IsPlaying("Step"))
				AudioController.Play("Step");
		}
		if (!isTied) {
			if (MoveRight) {
				transform.position += Vector3.right * moveSpeed * Time.deltaTime;
			} else if (MoveLeft) {
				transform.position += Vector3.left * moveSpeed * Time.deltaTime;
			}
		}
	}

	void FixedUpdate() {
		if (isShrinking) {
			if(!AudioController.IsPlaying("Climb"))
			AudioController.Play("Climb");
			rope.Shrink(0.5f);
		}

		if (isExpanding && isInAir) {
			if(!AudioController.IsPlaying("Climb"))
				AudioController.Play("Climb");
			rope.Expand(0.5f);
		}

		if (isTied) {
			float ropeLenght = rope.GetComponent<tk2dTiledSprite>().dimensions.y / 80;

			if (MoveRight) {
				if(!AudioController.IsPlaying("Swing"))
					AudioController.Play("Swing");
				rigidbody.AddForce(Vector3.right * swingForce / ropeLenght, ForceMode.Force);
			} else if (MoveLeft) {
				if(!AudioController.IsPlaying("Swing"))
					AudioController.Play("Swing");
				rigidbody.AddForce(Vector3.left * swingForce / ropeLenght, ForceMode.Force);
			}
		}
	}

	void HandleUpKeyDown() {
		if (isTied) {
			isShrinking = true;
		} else if (!isInAir) {
			rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
			sprite.Play("Jump");
			AudioController.Play("Jump");
			isInAir = true;
		}
	}

	void OnCollisionEnter(Collision info) {
		ContactPoint contact = info.contacts[0];
		if (contact.otherCollider.tag == "Floor") {
			if(isInAir){
				isInAir = false;
				sprite.Play ("Stand");
				AudioController.Play("Fall");
			}
		}
	}

	void OnTriggerEnter(Collider c)
	{
		if (c.tag == "CheckPoint") {
			c.gameObject.GetComponent<CheckPoint>().ActivateCheckPoint();
		}
		
		if (c.tag == "DeathCollider") {
			Respawnotron.Respawn(gameObject);
		}

		if(c.tag == "Collectable"){
			AudioController.Play("Pick");
			Destroy(c.gameObject);
			collectedRope = true;
		}

		if(c.tag == "Potato"){
			AudioController.Play("Pick");
			Destroy(c.gameObject);
			text.gameObject.active = true;
			Destroy(blockCOllider);
		}
	}
}

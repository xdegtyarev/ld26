using UnityEngine;
using System.Collections;

public class CheckPoint : MonoBehaviour {
	public tk2dSprite unactivated;
	public tk2dSprite activated;
	public bool isActive = false;
	public void ActivateCheckPoint(){
		if(!isActive)
		{
			AudioController.Play("Checkpoint");
		}
		Respawnotron.RegisterCheckPoint(transform);
		unactivated.gameObject.SetActive(false);
		activated.gameObject.SetActive(true);
		isActive = true;
	}

	public void Deactivate()
	{
		isActive = false;
		unactivated.gameObject.SetActive(true);
		activated.gameObject.SetActive(false);
	}
}

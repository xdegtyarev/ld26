using UnityEngine;
using UnityEditor;
using System.Collections;
[CustomEditor(typeof(Creature))]
public class CreatureEditor : Editor{ 
	Creature trg;
	bool isEditMode;
	void OnEnable()
	{
		trg = target as Creature;
	}
	void OnSceneGUI () {
		if(isEditMode){
			for(int i = 0; i < trg.waypoints.Length; i++){
				trg.localWaypoints[i] = Handles.PositionHandle (trg.localWaypoints[i] + trg.transform.position, Quaternion.identity) - trg.transform.position; 
				trg.waypoints[i] = trg.localWaypoints[i] + trg.transform.position;
			}
		}
	}

	public override void OnInspectorGUI(){
		base.OnInspectorGUI();
		//DrawDefaultInspector();
		isEditMode = GUILayout.Toggle(isEditMode,"Is Edit Mode");
	}
}


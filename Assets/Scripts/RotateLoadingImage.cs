using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class RotateLoadingImage : MonoBehaviour {
	void Update () {
		this.transform.RotateAround(this.transform.position, Vector3.back, 500 * Time.deltaTime);	
	}
}
